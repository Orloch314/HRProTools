using Microsoft.AspNetCore.Identity;

namespace HRProTools.Web.Data;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? InvitationSentAtUtc { get; set; }
    public DateTimeOffset? InvitationAcceptedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
