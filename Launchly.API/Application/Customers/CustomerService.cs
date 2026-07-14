using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Customers.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Application.Customers;

public class CustomerService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomerService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomerListDto>> GetCustomersAsync(int page = 1, int pageSize = 20)
    {
        if (_tenantContext.TenantId is null)
            return Result<CustomerListDto>.Failure("Store context is required.");

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == _tenantContext.TenantId &&
                        u.Role == UserRole.Customer)
            .OrderByDescending(u => u.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new CustomerDto(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.IsActive,
                u.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<CustomerListDto>.Success(new CustomerListDto(
            items,
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }
}
