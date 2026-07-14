using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Orders.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Orders;

public class OrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;

    public OrderService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog)
    {
        _db = db;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
    }

    // ─── List (paginated) ─────────────────────────────────────────────────────

    public async Task<Result<PagedResult<OrderDto>>> GetAllAsync(OrdersQuery query)
    {
        var q = _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(o => (int)o.Status == query.Status.Value);

        if (query.CustomerId.HasValue)
            q = q.Where(o => o.CustomerId == query.CustomerId.Value);

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => ToDto(o))
            .ToListAsync();

        return Result<PagedResult<OrderDto>>.Success(new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    // ─── Get By Id ────────────────────────────────────────────────────────────

    public async Task<Result<OrderDto>> GetByIdAsync(Guid id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return Result<OrderDto>.NotFound("Order not found.");

        return Result<OrderDto>.Success(ToDto(order));
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    private const int MaxConcurrencyRetries = 3;

    public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<OrderDto>.Failure("Store context is required.");

        // Validate customer belongs to this tenant
        var customerExists = await _db.Users
            .AnyAsync(u =>
                u.Id == request.CustomerId &&
                u.TenantId == _tenantContext.TenantId);

        if (!customerExists)
            return Result<OrderDto>.NotFound("Customer not found.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

        // Stock is decremented against Product.RowVersion (an optimistic
        // concurrency token mapped to PostgreSQL's xmin — see AppDbContext).
        // Two requests racing for the last unit of stock will both read it,
        // but only the first SaveChangesAsync wins; the second throws
        // DbUpdateConcurrencyException instead of silently overwriting the
        // first write and selling stock that doesn't exist. We retry a
        // bounded number of times against fresh data rather than failing
        // the customer's order outright on what is usually a transient clash.
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
                return Result<OrderDto>.NotFound("One or more products not found or inactive.");

            var orderItems = new List<OrderItem>();
            decimal total = 0;

            foreach (var itemReq in request.Items)
            {
                var product = products.First(p => p.Id == itemReq.ProductId);

                if (product.Stock < itemReq.Quantity)
                    return Result<OrderDto>.Failure(
                        $"Insufficient stock for \"{product.Name}\". Available: {product.Stock}.");

                product.Stock -= itemReq.Quantity;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    UnitPrice = product.Price,
                    Quantity = itemReq.Quantity
                });

                total += product.Price * itemReq.Quantity;
            }

            var order = new Order
            {
                TenantId = _tenantContext.TenantId.Value,
                CustomerId = request.CustomerId,
                OrderType = request.OrderType.HasValue ? (OrderType)request.OrderType.Value : null,
                TotalAmount = total,
                Notes = request.Notes?.Trim(),
                Status = OrderStatus.Pending,
                Items = orderItems
            };

            _db.Orders.Add(order);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Someone else updated one of these products between our read
                // and our write. Detach everything this attempt touched and
                // retry from a fresh read — do not retry the same in-memory
                // product/order instances, their tracked state is now stale.
                DetachEntity(order);
                foreach (var product in products)
                    DetachEntity(product);

                continue;
            }

            // Awaited — the Customer reload right after shares this same scoped
            // DbContext, so fire-and-forget here would race with it.
            await _auditLog.LogAsync(AuditAction.Created, nameof(Order), order.Id,
                $"Created order with {orderItems.Count} item(s), total {total:C}.");

            await _db.Entry(order).Reference(o => o.Customer).LoadAsync();

            return Result<OrderDto>.Created(ToDto(order));
        }

        // All retries exhausted under sustained contention on the same products.
        return Result<OrderDto>.Failure(
            "This order couldn't be placed due to high demand on one or more items. Please try again.",
            statusCode: 409);
    }

    private void DetachEntity<TEntity>(TEntity entity) where TEntity : class
    {
        var entry = _db.Entry(entity);
        if (entry.State != EntityState.Detached)
            entry.State = EntityState.Detached;
    }

    // ─── Update Status ────────────────────────────────────────────────────────

    public async Task<Result<OrderDto>> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request)
    {
        var newStatus = (OrderStatus)request.Status;

        // Same optimistic-concurrency reasoning as CreateAsync: restoring
        // stock on cancellation touches Product.RowVersion, so a concurrent
        // write to the same product (another order being placed or another
        // cancellation) can make this SaveChangesAsync fail. Retry from a
        // fresh read of both the order and the products rather than reusing
        // any tracked instance from a failed attempt.
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var order = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
                return Result<OrderDto>.NotFound("Order not found.");

            // Guard: Delivered orders are immutable once fulfilled — mirrors
            // the same rule BookingService enforces for completed appointments.
            if (order.Status == OrderStatus.Delivered)
                return Result<OrderDto>.Failure("Cannot change status of a delivered order.");

            List<Product> products = [];

            // Restore stock if cancelling
            if (newStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
            {
                var productIds = order.Items
                    .Where(i => i.ProductId.HasValue)
                    .Select(i => i.ProductId!.Value)
                    .ToList();

                products = await _db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product is not null)
                        product.Stock += item.Quantity;
                }
            }

            order.Status = newStatus;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                DetachEntity(order);
                foreach (var product in products)
                    DetachEntity(product);

                continue;
            }

            _auditLog.Log(AuditAction.Updated, nameof(Order), order.Id,
                $"Order status changed to {newStatus}.");

            return Result<OrderDto>.Success(ToDto(order));
        }

        return Result<OrderDto>.Failure(
            "Couldn't update the order status due to a conflicting update. Please try again.",
            statusCode: 409);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerId,
        $"{o.Customer?.FirstName} {o.Customer?.LastName}".Trim(),
        o.Status.ToString(),
        o.OrderType?.ToString(),
        o.TotalAmount,
        o.Notes,
        o.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.Name,
            i.UnitPrice,
            i.Quantity,
            i.LineTotal
        )).ToList(),
        o.CreatedAt,
        o.UpdatedAt
    );
}