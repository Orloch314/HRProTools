using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed record OperationalAlertView(Guid Id, string Type, string Title, string Message, string Status, DateTimeOffset CreatedAtUtc);

public sealed class OperationalAlertService(
    TenantDbContextFactory tenantFactory,
    VacationWorkflowService workflow)
{
    public async Task<List<OperationalAlertView>> GetHumanResourcesAlertsAsync(CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) return [];
        await using var db = tenantFactory.Create(actor.SchemaName);
        var companies = await HumanResourcesCompanyIdsAsync(db, actor.UserId, ct);
        return await db.OperationalAlerts.AsNoTracking()
            .Where(x => companies.Contains(x.CompanyId) && x.Status == OperationalAlertStatuses.Open)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new OperationalAlertView(x.Id, x.Type, x.Title, x.Message, x.Status, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task MarkAttendedAsync(Guid alertId, CancellationToken ct = default)
    {
        var actor = await workflow.GetWorkflowActorAsync(true, ct);
        if (!actor.IsHumanResources) throw new UnauthorizedAccessException("Solo Recursos Humanos puede atender alertas HR.");
        await using var db = tenantFactory.Create(actor.SchemaName);
        var companies = await HumanResourcesCompanyIdsAsync(db, actor.UserId, ct);
        var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == alertId && companies.Contains(x.CompanyId), ct)
            ?? throw new UnauthorizedAccessException("La alerta esta fuera de su alcance.");
        if (alert.Status == OperationalAlertStatuses.Attended) return;
        alert.Status = OperationalAlertStatuses.Attended;
        alert.AttendedByUserId = actor.UserId;
        alert.AttendedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static async Task<int> GenerateAsync(TenantDbContext db, DateOnly today, CancellationToken ct = default)
    {
        var candidates = new List<OperationalAlert>();
        var employments = await db.Employments.AsNoTracking().Where(x => x.Status == EmploymentStatuses.Active)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.EmployeeNumber,
                Name = x.Person.GivenNames + " " + x.Person.FamilyNames,
                DepartmentId = db.EmploymentAssignments.Where(a => a.EmploymentId == x.Id && a.ValidTo == null).Select(a => (Guid?)a.DepartmentId).SingleOrDefault()
            }).ToListAsync(ct);
        foreach (var employment in employments.Where(x => x.DepartmentId.HasValue))
        {
            var periods = await db.VacationPeriods.AsNoTracking().Where(x => x.EmploymentId == employment.Id).OrderBy(x => x.AccruedOn).ToListAsync(ct);
            if (periods.Count < 2) continue;
            var periodIds = periods.Select(x => x.Id).ToList();
            var movements = await db.VacationMovements.AsNoTracking().Where(x => periodIds.Contains(x.VacationPeriodId)).ToListAsync(ct);
            var pending = periods.Where(p => today > p.UseBy && LegalRemaining(p, movements) > 0).ToList();
            if (pending.Count == 0) continue;
            var nextRisk = periods.FirstOrDefault(p => p.AccruedOn > pending[0].AccruedOn && p.UseBy >= today && p.UseBy <= today.AddDays(60) && LegalRemaining(p, movements) > 0);
            if (nextRisk is null) continue;
            candidates.Add(new OperationalAlert
            {
                AlertKey = $"vacation-second-pending:{employment.Id:N}:{nextRisk.Id:N}",
                Type = OperationalAlertTypes.VacationSecondPendingRisk,
                CompanyId = employment.CompanyId,
                DepartmentId = employment.DepartmentId,
                EmploymentId = employment.Id,
                VacationPeriodId = nextRisk.Id,
                Title = "Riesgo de segundo periodo pendiente",
                Message = $"{employment.EmployeeNumber} · {employment.Name}: el siguiente limite es {nextRisk.UseBy:dd/MM/yyyy}."
            });
        }

        var absences = await db.Absences.AsNoTracking().Include(x => x.Employment).ThenInclude(x => x.Person).Include(x => x.Documents)
            .Where(x => x.Status != AbsenceStatuses.Cancelled).ToListAsync(ct);
        foreach (var absence in absences)
        {
            var employee = $"{absence.Employment.EmployeeNumber} · {absence.Employment.Person.GivenNames} {absence.Employment.Person.FamilyNames}";
            if (absence.Status is AbsenceStatuses.Declared or AbsenceStatuses.PendingReview)
                candidates.Add(ForAbsence(absence, OperationalAlertTypes.AbsencePendingReview, "Ausencia pendiente de revision", $"{employee}: registro del {absence.StartDate:dd/MM/yyyy} pendiente de revision."));
            if (absence.Status == AbsenceStatuses.Unjustified || absence.FinalClassification == AbsenceTypes.Unjustified || absence.DeclaredType == AbsenceTypes.Unjustified)
                candidates.Add(ForAbsence(absence, OperationalAlertTypes.AbsenceUnjustified, "Ausencia sin justificacion", $"{employee}: ausencia sin justificacion registrada."));
            if ((absence.FinalClassification ?? absence.DeclaredType) == AbsenceTypes.Medical && !absence.Documents.Any(x => x.AntivirusStatus == FileScanStatuses.Clean))
                candidates.Add(ForAbsence(absence, OperationalAlertTypes.MedicalDocumentPending, "Documentacion pendiente", $"{employee}: falta documentacion validada para una ausencia medica."));
        }

        var keys = candidates.Select(x => x.AlertKey).ToList();
        var existing = await db.OperationalAlerts.AsNoTracking().Where(x => keys.Contains(x.AlertKey)).Select(x => x.AlertKey).ToListAsync(ct);
        var created = candidates.Where(x => !existing.Contains(x.AlertKey, StringComparer.Ordinal)).ToList();
        db.OperationalAlerts.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created.Count;
    }

    internal static async Task<HashSet<Guid>> HumanResourcesCompanyIdsAsync(TenantDbContext db, string userId, CancellationToken ct)
    {
        var scopes = await db.HumanResourcesScopes.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.CompanyId).ToListAsync(ct);
        return scopes.Any(x => x is null)
            ? (await db.Companies.AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet()
            : scopes.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
    }

    private static decimal LegalRemaining(VacationPeriod period, IReadOnlyCollection<VacationMovement> movements) =>
        period.LegalDays
        - movements.Where(x => x.VacationPeriodId == period.Id && x.Type == VacationMovementTypes.Consume).Sum(x => x.LegalDays)
        + movements.Where(x => x.VacationPeriodId == period.Id && x.Type == VacationMovementTypes.Reversal).Sum(x => x.LegalDays);

    private static OperationalAlert ForAbsence(Absence absence, string type, string title, string message) => new()
    {
        AlertKey = $"{type.ToLowerInvariant()}:{absence.Id:N}",
        Type = type,
        CompanyId = absence.CompanyId,
        DepartmentId = absence.DepartmentId,
        EmploymentId = absence.EmploymentId,
        AbsenceId = absence.Id,
        Title = title,
        Message = message
    };
}

public sealed class OperationalAlertHostedService(
    IServiceScopeFactory scopes,
    TenantDbContextFactory tenantFactory,
    ILogger<OperationalAlertHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var global = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var schemas = await global.Tenants.AsNoTracking().Where(x => x.Status == TenantStatuses.Active).Select(x => x.SchemaName).ToListAsync(stoppingToken);
                foreach (var schema in schemas)
                {
                    await using var db = tenantFactory.Create(schema);
                    await OperationalAlertService.GenerateAsync(db, ParaguayVacationRules.ParaguayToday(), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "No fue posible generar las alertas operativas."); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
