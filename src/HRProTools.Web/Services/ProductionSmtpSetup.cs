using HRProTools.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HRProTools.Web.Services;

public static class ProductionSmtpSetup
{
    public const string TestAction = "DEPLOYMENT_SMTP_TEST";
    public const string TestSubject = "initial-info-hrprotools";
    public static async Task ConfigureAndTestAsync(IServiceProvider services, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Se requiere la nueva credencial SMTP.");
        var db = services.GetRequiredService<ApplicationDbContext>();
        var smtp = services.GetRequiredService<SmtpSettingsService>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == "orloch314@gmail.com" && x.IsActive)
            ?? throw new InvalidOperationException("Complete primero el bootstrap del administrador inicial.");
        var attemptId = await ReserveSingleTestAsync(db, user.Id);
        await smtp.SaveAsync(new SmtpSettingsModel { Host = "smtps.aruba.it", Port = 587, SecurityMode = "StartTls", Username = "info@hrprotools.com", Password = password, FromAddress = "info@hrprotools.com", FromName = "HR ProTools" }, user.Id);
        var attempt = await db.OperationLogs.SingleAsync(x => x.Id == attemptId);
        try
        {
            await smtp.SendWithReceiptAsync("orloch314@gmail.com", "Prueba autorizada de correo — HR ProTools", "<p>Mensaje único de verificación del entorno de pruebas HR ProTools.</p><p>Remitente: info@hrprotools.com. No contiene contraseñas, tokens ni datos de funcionarios.</p>");
            attempt.Detail = "ACCEPTED_BY_SMTP: inbox receipt and DKIM not confirmed";
            await db.SaveChangesAsync();
            Console.WriteLine("SMTP aceptó el único mensaje de prueba a orloch314@gmail.com. Recepción en bandeja y DKIM pendientes de confirmación.");
        }
        catch
        {
            attempt.Detail = "FAILED_OR_UNCERTAIN: no automatic resend; inspect before any new authorization";
            await db.SaveChangesAsync(); throw;
        }
    }

    public static async Task<long> ReserveSingleTestAsync(ApplicationDbContext db, string userId)
    {
        // Persist an attempt BEFORE contacting SMTP: ambiguous network failures never trigger an automatic resend.
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE platform.operation_log IN SHARE ROW EXCLUSIVE MODE");
            if (await db.OperationLogs.AnyAsync(x => x.Action == TestAction && x.Subject == TestSubject))
                throw new InvalidOperationException("La prueba inicial ya fue intentada. Revise su resultado antes de autorizar otro envío.");
            db.OperationLogs.Add(new OperationLog { ActorUserId = userId, Action = TestAction, Subject = TestSubject, Detail = "ATTEMPTED: single authorized message to orloch314@gmail.com" });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
        }
        return await db.OperationLogs.Where(x => x.Action == TestAction && x.Subject == TestSubject).Select(x => x.Id).SingleAsync();
    }
}
