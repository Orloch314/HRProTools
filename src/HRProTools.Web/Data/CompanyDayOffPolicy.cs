namespace HRProTools.Web.Data;

public sealed class CompanyDayOffPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public decimal Days { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
