using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Categories.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Categories;

public class CategoryService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;

    public CategoryService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog)
    {
        _db = db;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    public async Task<Result<List<CategoryDto>>> GetAllAsync()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.SortOrder,
                c.Products.Count(p => p.DeletedAt == null)
            ))
            .ToListAsync();

        return Result<List<CategoryDto>>.Success(categories);
    }

    // ─── Get By Id ────────────────────────────────────────────────────────────

    public async Task<Result<CategoryDto>> GetByIdAsync(Guid id)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.SortOrder,
                c.Products.Count(p => p.DeletedAt == null)
            ))
            .FirstOrDefaultAsync();

        if (category is null)
            return Result<CategoryDto>.NotFound("Category not found.");

        return Result<CategoryDto>.Success(category);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<CategoryDto>.Failure("Store context is required.");

        var nameTaken = await _db.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.Trim().ToLower());

        if (nameTaken)
            return Result<CategoryDto>.Failure("A category with this name already exists.");

        var category = new Category
        {
            TenantId = _tenantContext.TenantId.Value,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Created, nameof(Category), category.Id,
            $"Created category \"{category.Name}\".");

        return Result<CategoryDto>.Created(new CategoryDto(
            category.Id, category.Name, category.SortOrder, 0));
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return Result<CategoryDto>.NotFound("Category not found.");

        var nameTaken = await _db.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == request.Name.Trim().ToLower());

        if (nameTaken)
            return Result<CategoryDto>.Failure("A category with this name already exists.");

        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();

        // Awaited (not fire-and-forget) because AuditLogService shares this
        // same scoped DbContext — running it concurrently with the
        // CountAsync below throws "a second operation was started on this
        // context instance before a previous operation completed".
        await _auditLog.LogAsync(AuditAction.Updated, nameof(Category), category.Id,
            $"Updated category \"{category.Name}\".");

        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id);

        return Result<CategoryDto>.Success(new CategoryDto(
            category.Id, category.Name, category.SortOrder, productCount));
    }

    // ─── Soft Delete ──────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(Guid id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return Result<bool>.NotFound("Category not found.");

        // Mirror the FK's ON DELETE SET NULL intent for soft deletes too —
        // products should not keep pointing at a category that's gone.
        var affectedProducts = await _db.Products
            .Where(p => p.CategoryId == id)
            .ToListAsync();

        foreach (var product in affectedProducts)
            product.CategoryId = null;

        category.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Deleted, nameof(Category), category.Id,
            $"Deleted category \"{category.Name}\" ({affectedProducts.Count} product(s) unassigned).");

        return Result<bool>.Success(true);
    }
}