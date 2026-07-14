namespace Launchly.API.Core.Entities.Base;

public abstract class AuditableEntity : TenantEntity
{
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}