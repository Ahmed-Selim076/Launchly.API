using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Products.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Products;

public class ProductService
{
    private const int FreePlanProductLimit = 10;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;

    public ProductService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog)
    {
        _db = db;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
    }

    // ─── List (paginated) ─────────────────────────────────────────────────────

    public async Task<Result<PagedResult<ProductDto>>> GetAllAsync(ProductsQuery query)
    {
        var q = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(term) ||
                              (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);

        if (query.IsActive.HasValue)
            q = q.Where(p => p.IsActive == query.IsActive.Value);

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => ToDto(p))
            .ToListAsync();

        return Result<PagedResult<ProductDto>>.Success(new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    // ─── Get By Id ────────────────────────────────────────────────────────────

    public async Task<Result<ProductDto>> GetByIdAsync(Guid id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return Result<ProductDto>.NotFound("Product not found.");

        return Result<ProductDto>.Success(ToDto(product));
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<ProductDto>.Failure("Store context is required.");

        // Free plan limit check
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId.Value);

        if (tenant?.PlanType == PlanType.Free)
        {
            var productCount = await _db.Products.CountAsync();
            if (productCount >= FreePlanProductLimit)
                return Result<ProductDto>.Failure(
                    $"Free plan is limited to {FreePlanProductLimit} products. Upgrade to add more.");
        }

        // Category must belong to this tenant (Global Query Filter handles this)
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value);

            if (!categoryExists)
                return Result<ProductDto>.NotFound("Category not found.");
        }

        var slug = await GenerateUniqueSlugAsync(request.Name);

        var product = new Product
        {
            TenantId = _tenantContext.TenantId.Value,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = request.IsActive
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Awaited (not fire-and-forget) — the Category reload right after
        // shares this same scoped DbContext, and running both concurrently
        // throws a DbContext concurrency exception.
        await _auditLog.LogAsync(AuditAction.Created, nameof(Product), product.Id,
            $"Created product \"{product.Name}\" at {product.Price:C}.");

        // Reload with category for the response
        await _db.Entry(product).Reference(p => p.Category).LoadAsync();

        return Result<ProductDto>.Created(ToDto(product));
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<Result<ProductDto>> UpdateAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return Result<ProductDto>.NotFound("Product not found.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value);

            if (!categoryExists)
                return Result<ProductDto>.NotFound("Category not found.");
        }

        // Regenerate slug only if name changed
        if (!product.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            product.Slug = await GenerateUniqueSlugAsync(request.Name, excludeId: id);

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;
        product.ImageUrl = request.ImageUrl?.Trim();
        product.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        // Awaited — same DbContext-concurrency reason as CreateAsync above.
        await _auditLog.LogAsync(AuditAction.Updated, nameof(Product), product.Id,
            $"Updated product \"{product.Name}\".");

        await _db.Entry(product).Reference(p => p.Category).LoadAsync();

        return Result<ProductDto>.Success(ToDto(product));
    }

    // ─── Soft Delete ──────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(Guid id)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return Result<bool>.NotFound("Product not found.");

        product.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Deleted, nameof(Product), product.Id,
            $"Deleted product \"{product.Name}\".");

        return Result<bool>.Success(true);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<string> GenerateUniqueSlugAsync(string name, Guid? excludeId = null)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var counter = 1;

        while (true)
        {
            var q = _db.Products.Where(p => p.Slug == slug);

            if (excludeId.HasValue)
                q = q.Where(p => p.Id != excludeId.Value);

            var exists = await q.AnyAsync();
            if (!exists) break;

            slug = $"{baseSlug}-{counter++}";
        }

        return slug;
    }

    private static string Slugify(string name)
    {
        // Lowercase, replace spaces/special chars with hyphens, collapse duplicates
        var slug = name.Trim().ToLower();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-");
        slug = slug.Trim('-');
        return slug.Length > 160 ? slug[..160] : slug;
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id,
        p.Name,
        p.Slug,
        p.Description,
        p.Price,
        p.Stock,
        p.ImageUrl,
        p.IsActive,
        p.CategoryId,
        p.Category?.Name,
        p.CreatedAt,
        p.UpdatedAt
    );
}