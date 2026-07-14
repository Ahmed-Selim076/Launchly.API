using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Restaurant.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Restaurant;

public class RestaurantService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;

    public RestaurantService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog)
    {
        _db = db;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MENU CATEGORIES
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<Result<List<MenuCategoryDto>>> GetCategoriesAsync()
    {
        var categories = await _db.MenuCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new MenuCategoryDto(
                c.Id,
                c.Name,
                c.SortOrder,
                c.Items.Count(i => i.DeletedAt == null)
            ))
            .ToListAsync();

        return Result<List<MenuCategoryDto>>.Success(categories);
    }

    public async Task<Result<MenuCategoryDto>> GetCategoryByIdAsync(Guid id)
    {
        var category = await _db.MenuCategories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new MenuCategoryDto(
                c.Id,
                c.Name,
                c.SortOrder,
                c.Items.Count(i => i.DeletedAt == null)
            ))
            .FirstOrDefaultAsync();

        return category is null
            ? Result<MenuCategoryDto>.NotFound("Menu category not found.")
            : Result<MenuCategoryDto>.Success(category);
    }

    public async Task<Result<MenuCategoryDto>> CreateCategoryAsync(CreateMenuCategoryRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<MenuCategoryDto>.Failure("Store context is required.");

        var nameTaken = await _db.MenuCategories
            .AnyAsync(c => c.Name.ToLower() == request.Name.Trim().ToLower());

        if (nameTaken)
            return Result<MenuCategoryDto>.Failure("A menu category with this name already exists.");

        var category = new MenuCategory
        {
            TenantId = _tenantContext.TenantId.Value,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder
        };

        _db.MenuCategories.Add(category);
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Created, nameof(MenuCategory), category.Id,
            $"Created menu category \"{category.Name}\".");

        return Result<MenuCategoryDto>.Created(
            new MenuCategoryDto(category.Id, category.Name, category.SortOrder, 0));
    }

    public async Task<Result<MenuCategoryDto>> UpdateCategoryAsync(Guid id, UpdateMenuCategoryRequest request)
    {
        var category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return Result<MenuCategoryDto>.NotFound("Menu category not found.");

        var nameTaken = await _db.MenuCategories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == request.Name.Trim().ToLower());

        if (nameTaken)
            return Result<MenuCategoryDto>.Failure("A menu category with this name already exists.");

        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();

        // Awaited — CountAsync below shares the same scoped DbContext.
        await _auditLog.LogAsync(AuditAction.Updated, nameof(MenuCategory), category.Id,
            $"Updated menu category \"{category.Name}\".");

        var itemCount = await _db.MenuItems.CountAsync(i => i.CategoryId == id);

        return Result<MenuCategoryDto>.Success(
            new MenuCategoryDto(category.Id, category.Name, category.SortOrder, itemCount));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(Guid id)
    {
        var category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
            return Result<bool>.NotFound("Menu category not found.");

        // Unassign items from deleted category (same pattern as ProductService)
        var affectedItems = await _db.MenuItems
            .Where(i => i.CategoryId == id)
            .ToListAsync();

        foreach (var item in affectedItems)
            item.CategoryId = null;

        category.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Deleted, nameof(MenuCategory), category.Id,
            $"Deleted menu category \"{category.Name}\" ({affectedItems.Count} item(s) unassigned).");

        return Result<bool>.Success(true);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MENU ITEMS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<Result<List<MenuItemDto>>> GetItemsAsync(bool activeOnly = false)
    {
        var query = _db.MenuItems.AsNoTracking();

        if (activeOnly)
            query = query.Where(i => i.IsActive);

        var items = await query
            .OrderBy(i => i.Name)
            .Select(i => new MenuItemDto(
                i.Id,
                i.Name,
                i.Description,
                i.Price,
                i.CategoryId,
                i.Category != null ? i.Category.Name : null,
                i.ImageUrl,
                i.IsActive,
                i.CreatedAt
            ))
            .ToListAsync();

        return Result<List<MenuItemDto>>.Success(items);
    }

    public async Task<Result<MenuItemDto>> GetItemByIdAsync(Guid id)
    {
        var item = await _db.MenuItems
            .AsNoTracking()
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item is null)
            return Result<MenuItemDto>.NotFound("Menu item not found.");

        return Result<MenuItemDto>.Success(MapItemToDto(item));
    }

    public async Task<Result<MenuItemDto>> CreateItemAsync(CreateMenuItemRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<MenuItemDto>.Failure("Store context is required.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.MenuCategories
                .AnyAsync(c => c.Id == request.CategoryId.Value);

            if (!categoryExists)
                return Result<MenuItemDto>.NotFound("Menu category not found.");
        }

        var item = new MenuItem
        {
            TenantId = _tenantContext.TenantId.Value,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            ImageUrl = request.ImageUrl,
            IsActive = request.IsActive
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        // Awaited — Category reload below shares the same scoped DbContext.
        await _auditLog.LogAsync(AuditAction.Created, nameof(MenuItem), item.Id,
            $"Created menu item \"{item.Name}\" at {item.Price:C}.");

        await _db.Entry(item).Reference(i => i.Category).LoadAsync();

        return Result<MenuItemDto>.Created(MapItemToDto(item));
    }

    public async Task<Result<MenuItemDto>> UpdateItemAsync(Guid id, UpdateMenuItemRequest request)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == id);

        if (item is null)
            return Result<MenuItemDto>.NotFound("Menu item not found.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.MenuCategories
                .AnyAsync(c => c.Id == request.CategoryId.Value);

            if (!categoryExists)
                return Result<MenuItemDto>.NotFound("Menu category not found.");
        }

        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.Price = request.Price;
        item.CategoryId = request.CategoryId;
        item.ImageUrl = request.ImageUrl;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Awaited — Category reload below shares the same scoped DbContext.
        await _auditLog.LogAsync(AuditAction.Updated, nameof(MenuItem), item.Id,
            $"Updated menu item \"{item.Name}\".");

        await _db.Entry(item).Reference(i => i.Category).LoadAsync();

        return Result<MenuItemDto>.Success(MapItemToDto(item));
    }

    public async Task<Result<bool>> DeleteItemAsync(Guid id)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == id);

        if (item is null)
            return Result<bool>.NotFound("Menu item not found.");

        item.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Deleted, nameof(MenuItem), item.Id,
            $"Deleted menu item \"{item.Name}\".");

        return Result<bool>.Success(true);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FOOD ORDERS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<Result<PagedResult<FoodOrderDto>>> GetOrdersAsync(FoodOrderQueryRequest query)
    {
        var q = _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            // Restaurant orders have at least one item with a MenuItemId
            .Where(o => o.Items.Any(i => i.MenuItemId != null))
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(o => (int)o.Status == query.Status.Value);

        var totalCount = await q.CountAsync();

        var orders = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var dtos = orders.Select(MapOrderToDto).ToList();

        return Result<PagedResult<FoodOrderDto>>.Success(new PagedResult<FoodOrderDto>
        {
            Items = dtos,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    public async Task<Result<FoodOrderDto>> GetOrderByIdAsync(Guid id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Where(o => o.Items.Any(i => i.MenuItemId != null))
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null
            ? Result<FoodOrderDto>.NotFound("Food order not found.")
            : Result<FoodOrderDto>.Success(MapOrderToDto(order));
    }

    public async Task<Result<FoodOrderDto>> CreateOrderAsync(CreateFoodOrderRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<FoodOrderDto>.Failure("Store context is required.");

        // Validate customer belongs to this tenant
        var customerExists = await _db.Users
            .AnyAsync(u => u.Id == request.CustomerId &&
                           u.TenantId == _tenantContext.TenantId &&
                           u.Role == UserRole.Customer);

        if (!customerExists)
            return Result<FoodOrderDto>.NotFound("Customer not found.");

        // Fetch & validate menu items
        var menuItemIds = request.Items.Select(i => i.MenuItemId).ToList();

        var menuItems = await _db.MenuItems
            .Where(i => menuItemIds.Contains(i.Id) && i.IsActive)
            .ToListAsync();

        if (menuItems.Count != menuItemIds.Distinct().Count())
            return Result<FoodOrderDto>.Failure(
                "One or more menu items are unavailable or do not exist.");

        // Build order items with price snapshot
        var orderItems = request.Items.Select(i =>
        {
            var menuItem = menuItems.First(m => m.Id == i.MenuItemId);
            return new OrderItem
            {
                MenuItemId = menuItem.Id,
                Name = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = i.Quantity
            };
        }).ToList();

        var total = orderItems.Sum(i => i.UnitPrice * i.Quantity);

        var order = new Order
        {
            TenantId = _tenantContext.TenantId.Value,
            CustomerId = request.CustomerId,
            Status = OrderStatus.Pending,
            Notes = request.Notes?.Trim(),
            TotalAmount = total,
            Items = orderItems
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Awaited — Customer reload below shares the same scoped DbContext.
        await _auditLog.LogAsync(AuditAction.Created, nameof(Order), order.Id,
            $"Created food order with {orderItems.Count} item(s), total {total:C}.");

        await _db.Entry(order).Reference(o => o.Customer).LoadAsync();

        return Result<FoodOrderDto>.Created(MapOrderToDto(order));
    }

    public async Task<Result<FoodOrderDto>> UpdateOrderStatusAsync(Guid id, UpdateFoodOrderStatusRequest request)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Where(o => o.Items.Any(i => i.MenuItemId != null))
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return Result<FoodOrderDto>.NotFound("Food order not found.");

        var newStatus = (OrderStatus)request.Status;

        if (order.Status == OrderStatus.Cancelled)
            return Result<FoodOrderDto>.Failure("Cannot update a cancelled order.");

        order.Status = newStatus;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Updated, nameof(Order), order.Id,
            $"Food order status changed to {newStatus}.");

        return Result<FoodOrderDto>.Success(MapOrderToDto(order));
    }

    // ─── Private Mappers ──────────────────────────────────────────────────────

    private static MenuItemDto MapItemToDto(MenuItem item) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Price,
        item.CategoryId,
        item.Category?.Name,
        item.ImageUrl,
        item.IsActive,
        item.CreatedAt
    );

    private static FoodOrderDto MapOrderToDto(Order order) => new(
        order.Id,
        $"{order.Customer?.FirstName} {order.Customer?.LastName}".Trim(),
        order.Customer?.Email ?? "",
        order.Status.ToString(),
        order.Notes,
        order.TotalAmount,
        order.Items.Select(i => new FoodOrderItemDto(
            i.Id,
            i.MenuItemId,
            i.Name,
            i.UnitPrice,
            i.Quantity,
            i.LineTotal
        )).ToList(),
        order.CreatedAt
    );
}
