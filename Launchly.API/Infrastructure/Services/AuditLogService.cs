using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Infrastructure.Services;

public class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        AppDbContext db,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // Fire-and-forget — audit failure must never break the main operation.
    // ONLY safe to call when nothing else touches this scoped DbContext
    // afterward in the same request. If the caller does another _db
    // operation right after, use LogAsync(...) with await instead —
    // see CategoryService.UpdateAsync for an example.
    public void Log(
        AuditAction action,
        string? entityType = null,
        Guid? entityId = null,
        string? details = null)
    {
        _ = LogAsync(action, entityType, entityId, details);
    }

    public async Task LogAsync(
        AuditAction action,
        string? entityType,
        Guid? entityId,
        string? details)
    {
        try
        {
            var ip = _httpContextAccessor.HttpContext?
                .Connection.RemoteIpAddress?.ToString();

            var log = new AuditLog
            {
                TenantId = _tenantContext.TenantId,
                UserId = _currentUser.IsAuthenticated ? _currentUser.Id : null,
                UserEmail = _currentUser.IsAuthenticated
                    ? _currentUser.Email
                    : "anonymous",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ip
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Swallow — audit failure must not propagate
            _logger.LogError(ex, "Failed to write audit log for action {Action}", action);
        }
    }
}