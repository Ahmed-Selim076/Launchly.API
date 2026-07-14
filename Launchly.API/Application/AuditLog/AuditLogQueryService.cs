using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.AuditLog.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Application.AuditLog;

public class AuditLogQueryService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AuditLogQueryService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Result<AuditLogListDto>> GetLogsAsync(int page = 1, int pageSize = 30)
    {
        if (_tenantContext.TenantId is null)
            return Result<AuditLogListDto>.Failure("Store context is required.");

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.TenantId == _tenantContext.TenantId)
            .OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserEmail,
                a.Action.ToString(),
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<AuditLogListDto>.Success(new AuditLogListDto(
            items,
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }
}
