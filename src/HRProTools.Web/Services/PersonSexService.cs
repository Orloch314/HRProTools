using System.Security.Claims;
using HRProTools.Web.Data;
using HRProTools.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public sealed class PersonSexService(CurrentTenantService currentTenant, TenantDbContextFactory factory,
    AuthenticationStateProvider authentication, IAuthorizationService authorization)
{
    public async Task UpdateAsync(Guid employmentId, string sex, string reason, CancellationToken ct = default)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        if (!(await authorization.AuthorizeAsync(state.User, AppPolicies.TenantAdministration)).Succeeded)
            throw new UnauthorizedAccessException("Solo la administración autorizada puede modificar Sexo.");
        if (sex is not "M" and not "F" || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new InvalidOperationException("Seleccione M o F e indique un motivo de hasta 500 caracteres.");
        var tenant = await currentTenant.GetRequiredAsync(ct);
        await using var db = factory.Create(tenant.SchemaName);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var personId = await db.Employments.Where(x => x.Id == employmentId).Select(x => x.PersonId).SingleAsync(ct);
        var person = await db.People.FromSqlInterpolated($"SELECT * FROM person WHERE \"Id\" = {personId} FOR UPDATE").SingleAsync(ct);
        if (person.Sex != sex)
        {
            db.PersonSexChanges.Add(new PersonSexChange
            {
                PersonId = person.Id,
                PreviousValue = person.Sex,
                NewValue = sex,
                ActorUserId = state.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Reason = reason.Trim()
            });
            person.Sex = sex;
            person.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
