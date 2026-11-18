namespace HRProTools.Web.Data;

public static class TenantStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Error = "Error";
}

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string Status { get; set; } = TenantStatuses.Pending;
    public string? ProvisioningError { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SmtpConfiguration
{
    public int Id { get; set; } = 1;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string SecurityMode { get; set; } = "StartTls";
    public string Username { get; set; } = string.Empty;
    public string ProtectedPassword { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "HR ProTools";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class OperationLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ActorUserId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
