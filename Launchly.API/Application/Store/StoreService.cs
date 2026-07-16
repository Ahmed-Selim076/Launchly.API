using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Auth.DTOs;
using Launchly.API.Application.Restaurant.DTOs;
using Launchly.API.Application.Store.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Store;

public class StoreService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;
    private readonly TokenService _tokenService;

    public StoreService(
        AppDbContext db,
        ITenantContext tenantContext,
        IConfiguration config,
        TokenService tokenService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _config = config;
        _tokenService = tokenService;
    }

    // ─── Public Store Settings ────────────────────────────────────────────────

    public async Task<Result<PublicStoreSettingsDto>> GetStoreSettingsAsync()
    {
        if (_tenantContext.TenantId is null)
            return Result<PublicStoreSettingsDto>.NotFound("Store not found.");

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Settings)
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId);

        if (tenant is null || tenant.Settings is null)
            return Result<PublicStoreSettingsDto>.NotFound("Store not found.");

        return Result<PublicStoreSettingsDto>.Success(new PublicStoreSettingsDto(
            tenant.Settings.StoreName,
            tenant.Settings.LogoUrl,
            tenant.Settings.PrimaryColor,
            tenant.Settings.SecondaryColor,
            tenant.Settings.HeroText,
            tenant.Settings.AboutText,
            tenant.Settings.GoogleAnalyticsId,
            tenant.StoreType.ToString(),
            tenant.TemplateId,
            tenant.Settings.ContactPhone,
            tenant.Settings.WhatsappNumber,
            tenant.Settings.ContactEmail,
            tenant.Settings.ContactAddress,
            tenant.Settings.FacebookUrl,
            tenant.Settings.InstagramUrl
        ));
    }

    // ─── Contact form ─────────────────────────────────────────────────────────

    public async Task<Result<bool>> SendContactMessageAsync(ContactMessageRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<bool>.Failure("Store context is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<bool>.Failure("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<bool>.Failure("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 10)
            return Result<bool>.Failure("Message is too short.");

        _db.ContactMessages.Add(new ContactMessage
        {
            TenantId = _tenantContext.TenantId.Value,
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            Subject = request.Subject?.Trim(),
            Message = request.Message.Trim(),
        });
        await _db.SaveChangesAsync();

        return Result<bool>.Created(true);
    }

    // ─── Public Product Listing ───────────────────────────────────────────────

    public async Task<Result<PublicProductListDto>> GetProductsAsync(
        string? search,
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        int page = 1,
        int pageSize = 20,
        Guid? currentCustomerId = null)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Stock > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.ToLower().Contains(search.ToLower()) ||
                (p.Description != null && p.Description.ToLower().Contains(search.ToLower())));

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.Description, p.Price, p.Stock,
                p.ImageUrl, CategoryName = p.Category != null ? p.Category.Name : null,
                p.OriginalPrice, p.Badge,
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0,
                ReviewCount = p.Reviews.Count,
                IsWishlisted = currentCustomerId.HasValue
                    ? p.WishlistItems.Any(w => w.CustomerId == currentCustomerId.Value)
                    : (bool?)null,
            })
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<PublicProductListDto>.Success(new PublicProductListDto(
            items.Select(p => new PublicProductDto(
                p.Id, p.Name, p.Slug, p.Description, p.Price, p.Stock,
                p.ImageUrl, p.CategoryName, p.OriginalPrice, p.Badge,
                Math.Round(p.AverageRating, 1), p.ReviewCount, p.IsWishlisted
            )).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }

    // ─── Public Product Detail ────────────────────────────────────────────────

    public async Task<Result<PublicProductDto>> GetProductBySlugAsync(string slug, Guid? currentCustomerId = null)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Reviews)
            .Include(p => p.WishlistItems)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

        if (product is null)
            return Result<PublicProductDto>.NotFound("Product not found.");

        var averageRating = product.Reviews.Count > 0
            ? Math.Round(product.Reviews.Average(r => r.Rating), 1)
            : 0;

        var isWishlisted = currentCustomerId.HasValue
            ? product.WishlistItems.Any(w => w.CustomerId == currentCustomerId.Value)
            : (bool?)null;

        return Result<PublicProductDto>.Success(new PublicProductDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            product.Stock,
            product.ImageUrl,
            product.Category?.Name,
            product.OriginalPrice,
            product.Badge,
            averageRating,
            product.Reviews.Count,
            isWishlisted
        ));
    }

    // ─── Wishlist (Customer) ──────────────────────────────────────────────────

    public async Task<Result<List<WishlistItemDto>>> GetWishlistAsync(Guid customerId)
    {
        var items = await _db.WishlistItems
            .AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .Include(w => w.Product)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistItemDto(
                w.ProductId,
                w.Product.Name,
                w.Product.Slug,
                w.Product.Price,
                w.Product.OriginalPrice,
                w.Product.ImageUrl,
                w.Product.Stock,
                w.CreatedAt
            ))
            .ToListAsync();

        return Result<List<WishlistItemDto>>.Success(items);
    }

    public async Task<Result<bool>> AddToWishlistAsync(Guid customerId, Guid productId)
    {
        if (_tenantContext.TenantId is null)
            return Result<bool>.Failure("Store context is required.");

        var productExists = await _db.Products.AnyAsync(p => p.Id == productId && p.IsActive);
        if (!productExists)
            return Result<bool>.NotFound("Product not found.");

        var alreadyExists = await _db.WishlistItems
            .AnyAsync(w => w.CustomerId == customerId && w.ProductId == productId);
        if (alreadyExists)
            return Result<bool>.Success(true); // idempotent — already wishlisted

        _db.WishlistItems.Add(new WishlistItem
        {
            TenantId = _tenantContext.TenantId.Value,
            CustomerId = customerId,
            ProductId = productId,
        });
        await _db.SaveChangesAsync();

        return Result<bool>.Created(true);
    }

    public async Task<Result<bool>> RemoveFromWishlistAsync(Guid customerId, Guid productId)
    {
        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == productId);

        if (item is null)
            return Result<bool>.Success(true); // idempotent — already gone

        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    // ─── Reviews (Public read + Customer write) ───────────────────────────────

    public async Task<Result<ReviewSummaryDto>> GetReviewsAsync(string slug)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Reviews).ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (product is null)
            return Result<ReviewSummaryDto>.NotFound("Product not found.");

        var reviews = product.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(
                r.Id,
                r.Customer.FullName,
                r.Rating,
                r.Comment,
                r.CreatedAt
            ))
            .ToList();

        var average = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

        return Result<ReviewSummaryDto>.Success(new ReviewSummaryDto(average, reviews.Count, reviews));
    }

    public async Task<Result<ReviewDto>> AddReviewAsync(Guid customerId, string slug, CreateReviewRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<ReviewDto>.Failure("Store context is required.");

        if (request.Rating < 1 || request.Rating > 5)
            return Result<ReviewDto>.Failure("Rating must be between 1 and 5.");

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);
        if (product is null)
            return Result<ReviewDto>.NotFound("Product not found.");

        var existing = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ProductId == product.Id && r.CustomerId == customerId);

        if (existing is not null)
        {
            // One review per customer per product — editing an existing one
            // rather than allowing duplicates to pile up in the average.
            existing.Rating = request.Rating;
            existing.Comment = request.Comment;
            await _db.SaveChangesAsync();

            var customer = await _db.Users.FirstAsync(u => u.Id == customerId);
            return Result<ReviewDto>.Success(new ReviewDto(
                existing.Id, customer.FullName,
                existing.Rating, existing.Comment, existing.CreatedAt));
        }

        var review = new Review
        {
            TenantId = _tenantContext.TenantId.Value,
            ProductId = product.Id,
            CustomerId = customerId,
            Rating = request.Rating,
            Comment = request.Comment,
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        var newCustomer = await _db.Users.FirstAsync(u => u.Id == customerId);
        return Result<ReviewDto>.Created(new ReviewDto(
            review.Id, newCustomer.FullName,
            review.Rating, review.Comment, review.CreatedAt));
    }

    // ─── Public Categories (Ecommerce storefront) ─────────────────────────────
    // Backs GET /api/v1/store/categories, used by the product grid's category
    // filter. TenantId scoping + soft-delete exclusion both come from
    // AppDbContext's global query filters on Category (a TenantEntity), same
    // as everywhere else in this service — no explicit TenantId check needed.

    public async Task<Result<List<PublicCategoryDto>>> GetCategoriesAsync()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new PublicCategoryDto(
                c.Id, c.Name, c.SortOrder,
                c.Products.Count(p => p.IsActive && p.Stock > 0)))
            .ToListAsync();

        return Result<List<PublicCategoryDto>>.Success(categories);
    }

    // ─── Public Menu (Restaurant storefront) ──────────────────────────────────
    // Categories with their active items nested — the storefront renders this
    // in one shot rather than fetching MenuCategories and MenuItems separately
    // and joining them client-side (admin endpoints do exactly that separation,
    // since the admin UI edits categories and items independently — see
    // RestaurantService.GetCategoriesAsync/GetMenuItemsAsync). This endpoint
    // is intentionally read-only and Restaurant-storefront-specific.

    public async Task<Result<List<PublicMenuCategoryDto>>> GetPublicMenuAsync()
    {
        var categories = await _db.MenuCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new PublicMenuCategoryDto(
                c.Id,
                c.Name,
                c.SortOrder,
                c.Items
                    .Where(i => i.IsActive && i.DeletedAt == null)
                    .Select(i => new PublicMenuItemDto(
                        i.Id,
                        i.Name,
                        i.Description,
                        i.Price,
                        i.ImageUrl
                    ))
                    .ToList()
            ))
            .ToListAsync();

        return Result<List<PublicMenuCategoryDto>>.Success(categories);
    }

    // ─── Customer Registration ────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> RegisterCustomerAsync(RegisterCustomerRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<AuthResponse>.Failure("Store context is required.");

        var emailTaken = await _db.Users
            .AnyAsync(u => u.Email == request.Email.ToLower().Trim() &&
                           u.TenantId == _tenantContext.TenantId);

        if (emailTaken)
            return Result<AuthResponse>.Failure("An account with this email already exists.");

        var customer = new User
        {
            TenantId = _tenantContext.TenantId,
            Email = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Customer,
            IsEmailVerified = true
        };

        _db.Users.Add(customer);
        await _db.SaveChangesAsync();

        // Issue real tokens immediately — the guest-checkout flow that calls
        // this registers the customer and then places an order in the same
        // step, and PlaceOrder requires an authenticated Customer JWT.
        // Returning just `true` here (as before) left the customer with no
        // token at all, so that immediately-following order call would fail
        // with 401 for every brand-new customer.
        var accessToken = _tokenService.GenerateAccessToken(customer);
        var refreshToken = await SaveRefreshTokenAsync(customer.Id);

        return Result<AuthResponse>.Created(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(customer)
        ));
    }

    private async Task<string> SaveRefreshTokenAsync(Guid userId)
    {
        var raw = _tokenService.GenerateRefreshToken();
        var hash = _tokenService.HashRefreshToken(raw);
        var refreshDays = int.Parse(_config["JWT_REFRESH_DAYS"] ?? "7");

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        });
        await _db.SaveChangesAsync();

        return raw;
    }

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role.ToString(),
        user.TenantId,
        null,
        null
    );

    // ─── Place Order ──────────────────────────────────────────────────────────

    private const int MaxConcurrencyRetries = 3;

    public async Task<Result<PlacedOrderDto>> PlaceOrderAsync(
        PlaceOrderRequest request,
        Guid customerId)
    {
        if (_tenantContext.TenantId is null)
            return Result<PlacedOrderDto>.Failure("Store context is required.");

        // Which table backs this order depends entirely on which id is
        // populated on its lines — validated upstream by
        // PlaceOrderRequestValidator (exactly one of ProductId/MenuItemId
        // per line, and all lines in one request must be the same kind).
        var isRestaurantOrder = request.Items.Any(i => i.MenuItemId.HasValue);

        return isRestaurantOrder
            ? await PlaceMenuItemOrderAsync(request, customerId)
            : await PlaceProductOrderAsync(request, customerId);
    }

    private async Task<Result<PlacedOrderDto>> PlaceProductOrderAsync(
        PlaceOrderRequest request,
        Guid customerId)
    {
        var productIds = request.Items.Select(i => i.ProductId!.Value).Distinct().ToList();

        // Stock is decremented against Product.RowVersion (an optimistic
        // concurrency token mapped to PostgreSQL's xmin — see AppDbContext).
        // Two customers racing to buy the last unit of the same product must
        // not both succeed; SaveChangesAsync throws DbUpdateConcurrencyException
        // for whichever request loses the race, and we retry against a fresh
        // read rather than failing the order outright on a transient clash.
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
                return Result<PlacedOrderDto>.Failure("One or more products are unavailable.");

            // Validate stock for each item
            foreach (var line in request.Items)
            {
                var product = products.First(p => p.Id == line.ProductId);
                if (product.Stock < line.Quantity)
                    return Result<PlacedOrderDto>.Failure(
                        $"Insufficient stock for \"{product.Name}\". Available: {product.Stock}.");
            }

            // Build order
            var orderItems = request.Items.Select(line =>
            {
                var product = products.First(p => p.Id == line.ProductId);
                return new OrderItem
                {
                    ProductId = product.Id,
                    Name = product.Name,           // snapshot
                    UnitPrice = product.Price,     // snapshot
                    Quantity = line.Quantity
                };
            }).ToList();

            var totalAmount = orderItems.Sum(i => i.UnitPrice * i.Quantity);

            var order = new Order
            {
                TenantId = _tenantContext.TenantId!.Value,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                Notes = request.Notes,
                Items = orderItems
            };

            _db.Orders.Add(order);

            // Deduct stock
            foreach (var line in request.Items)
            {
                var product = products.First(p => p.Id == line.ProductId);
                product.Stock -= line.Quantity;
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Detach this attempt's tracked entities and retry from a
                // fresh read — their in-memory state no longer matches the DB.
                DetachEntity(order);
                foreach (var product in products)
                    DetachEntity(product);

                continue;
            }

            return Result<PlacedOrderDto>.Created(new PlacedOrderDto(
                order.Id,
                order.TotalAmount,
                order.Status.ToString(),
                order.CreatedAt
            ));
        }

        return Result<PlacedOrderDto>.Failure(
            "This order couldn't be placed due to high demand on one or more items. Please try again.",
            statusCode: 409);
    }

    private async Task<Result<PlacedOrderDto>> PlaceMenuItemOrderAsync(
        PlaceOrderRequest request,
        Guid customerId)
    {
        // MenuItem has no Stock/RowVersion — restaurants don't track exact
        // unit counts the way a product catalog does (a dish is either on
        // the menu or it isn't), so there's no concurrency race to retry
        // here the way PlaceProductOrderAsync has to for Product.Stock.
        var menuItemIds = request.Items.Select(i => i.MenuItemId!.Value).Distinct().ToList();

        var menuItems = await _db.MenuItems
            .Where(m => menuItemIds.Contains(m.Id) && m.IsActive)
            .ToListAsync();

        if (menuItems.Count != menuItemIds.Count)
            return Result<PlacedOrderDto>.Failure("One or more menu items are unavailable.");

        var orderItems = request.Items.Select(line =>
        {
            var menuItem = menuItems.First(m => m.Id == line.MenuItemId);
            return new OrderItem
            {
                MenuItemId = menuItem.Id,
                Name = menuItem.Name,           // snapshot
                UnitPrice = menuItem.Price,      // snapshot
                Quantity = line.Quantity
            };
        }).ToList();

        var totalAmount = orderItems.Sum(i => i.UnitPrice * i.Quantity);

        var order = new Order
        {
            TenantId = _tenantContext.TenantId!.Value,
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            Notes = request.Notes,
            OrderType = request.OrderType.HasValue ? (OrderType)request.OrderType.Value : null,
            Items = orderItems
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return Result<PlacedOrderDto>.Created(new PlacedOrderDto(
            order.Id,
            order.TotalAmount,
            order.Status.ToString(),
            order.CreatedAt
        ));
    }

    private void DetachEntity<TEntity>(TEntity entity) where TEntity : class
    {
        var entry = _db.Entry(entity);
        if (entry.State != EntityState.Detached)
            entry.State = EntityState.Detached;
    }

    // ─── Customer Order Detail ────────────────────────────────────────────────

    public async Task<Result<List<CustomerOrderDto>>> GetOrdersAsync(Guid customerId)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(order => new CustomerOrderDto(
                order.Id,
                order.Status.ToString(),
                order.TotalAmount,
                order.Notes,
                order.CreatedAt,
                order.Items.Select(i => new CustomerOrderItemDto(
                    i.Name,
                    i.UnitPrice,
                    i.Quantity,
                    i.UnitPrice * i.Quantity
                )).ToList()
            ))
            .ToListAsync();

        return Result<List<CustomerOrderDto>>.Success(orders);
    }

    public async Task<Result<CustomerOrderDto>> GetOrderAsync(Guid orderId, Guid customerId)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order is null)
            return Result<CustomerOrderDto>.NotFound("Order not found.");

        return Result<CustomerOrderDto>.Success(new CustomerOrderDto(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount,
            order.Notes,
            order.CreatedAt,
            order.Items.Select(i => new CustomerOrderItemDto(
                i.Name,
                i.UnitPrice,
                i.Quantity,
                i.UnitPrice * i.Quantity
            )).ToList()
        ));
    }

    // ─── Log Visitor ──────────────────────────────────────────────────────────

    public async Task LogVisitorAsync(string ipAddress)
    {
        if (_tenantContext.TenantId is null) return;

        var salt = _config["VISITOR_IP_SALT"] ?? "default-salt";
        var ipHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{ipAddress}:{salt}")));

        var visit = new VisitorLog
        {
            TenantId = _tenantContext.TenantId.Value,
            IpHash = ipHash,
            VisitedAt = DateTime.UtcNow
        };

        _db.VisitorLogs.Add(visit);
        await _db.SaveChangesAsync();
    }
}
