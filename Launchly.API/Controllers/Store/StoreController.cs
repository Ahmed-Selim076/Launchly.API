using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Store;
using Launchly.API.Application.Store.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Interfaces;

namespace Launchly.API.Controllers.Store;

[ApiController]
[Route("api/v1/store")]
public class StoreController : ControllerBase
{
    private readonly StoreService _storeService;
    private readonly ICurrentUser _currentUser;

    public StoreController(StoreService storeService, ICurrentUser currentUser)
    {
        _storeService = storeService;
        _currentUser = currentUser;
    }

    // ─── Public: Store Settings ───────────────────────────────────────────────
    // Cache for 2 minutes — tenant settings change infrequently (logo, colors, name).
    // MUST vary by both "Host" (production: {subdomain}.launchly.com differs
    // per tenant) AND "X-Tenant-Subdomain" (dev: frontend on :4200 calls the
    // API on :5117, so Host is always "localhost" and the real tenant signal
    // is this custom header — see TenantResolutionMiddleware). Varying by only
    // one of these previously caused every tenant to share the same cache
    // entry for this endpoint.

    [HttpGet("settings")]
    [ResponseCache(Duration = 120, VaryByHeader = "Host,X-Tenant-Subdomain")]
    public async Task<IActionResult> GetStoreSettings()
    {
        var result = await _storeService.GetStoreSettingsAsync();
        return ToResponse(result);
    }

    // ─── Public: Product Listing ──────────────────────────────────────────────
    // Cache for 60 seconds — products change more often than settings.
    // Vary by query string (search, filters, pagination) + Host/X-Tenant-Subdomain
    // so different filter combinations AND different tenants each get their
    // own cache entry (see the note on GetStoreSettings above for why both
    // headers are needed).

    // NOTE: no [ResponseCache] here anymore — IsWishlisted varies per
    // logged-in customer, so a shared cache entry would leak one customer's
    // wishlist state into another's response. Product listing is cheap
    // enough (indexed, paged) to run uncached.
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var customerId = _currentUser.IsAuthenticated ? _currentUser.Id : (Guid?)null;
        var result = await _storeService.GetProductsAsync(
            search, categoryId, minPrice, maxPrice, page, pageSize, customerId);
        return ToResponse(result);
    }

    // ─── Public: Categories ────────────────────────────────────────────────────
    // Cache for 2 minutes — categories change about as infrequently as store
    // settings. Backs the Ecommerce storefront's category filter; was missing
    // entirely before (frontend called this and got a 404).

    [HttpGet("categories")]
    [ResponseCache(Duration = 120, VaryByHeader = "Host,X-Tenant-Subdomain")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _storeService.GetCategoriesAsync();
        return ToResponse(result);
    }

    // ─── Public: Product Detail ───────────────────────────────────────────────
    // Cache for 60 seconds per slug per tenant.

    // No [ResponseCache] — same reasoning as GetProducts above (IsWishlisted
    // is per-customer).
    [HttpGet("products/{slug}")]
    public async Task<IActionResult> GetProduct(string slug)
    {
        var customerId = _currentUser.IsAuthenticated ? _currentUser.Id : (Guid?)null;
        var result = await _storeService.GetProductBySlugAsync(slug, customerId);
        return ToResponse(result);
    }

    [HttpPost("contact")]
    public async Task<IActionResult> SendContactMessage([FromBody] ContactMessageRequest request)
    {
        var result = await _storeService.SendContactMessageAsync(request);
        return ToResponse(result);
    }

    [HttpGet("wishlist")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> GetWishlist()
    {
        var result = await _storeService.GetWishlistAsync(_currentUser.Id);
        return ToResponse(result);
    }

    [HttpPost("wishlist/{productId:guid}")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> AddToWishlist(Guid productId)
    {
        var result = await _storeService.AddToWishlistAsync(_currentUser.Id, productId);
        return ToResponse(result);
    }

    [HttpDelete("wishlist/{productId:guid}")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> RemoveFromWishlist(Guid productId)
    {
        var result = await _storeService.RemoveFromWishlistAsync(_currentUser.Id, productId);
        return ToResponse(result);
    }

    // ─── Reviews: Public read, Customer write ─────────────────────────────────

    [HttpGet("products/{slug}/reviews")]
    [ResponseCache(Duration = 60, VaryByHeader = "Host,X-Tenant-Subdomain")]
    public async Task<IActionResult> GetReviews(string slug)
    {
        var result = await _storeService.GetReviewsAsync(slug);
        return ToResponse(result);
    }

    [HttpPost("products/{slug}/reviews")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> AddReview(string slug, [FromBody] CreateReviewRequest request)
    {
        var result = await _storeService.AddReviewAsync(_currentUser.Id, slug, request);
        return ToResponse(result);
    }

    // ─── Public: Restaurant Menu ──────────────────────────────────────────────
    // Cache for 60 seconds — same cadence as product listing, since menu
    // items change at roughly the same frequency as a product catalog.
    // Restaurant-storefront-only; Ecommerce/Booking tenants simply never
    // call this route.

    [HttpGet("menu")]
    [ResponseCache(Duration = 60, VaryByHeader = "Host,X-Tenant-Subdomain")]
    public async Task<IActionResult> GetMenu()
    {
        var result = await _storeService.GetPublicMenuAsync();
        return ToResponse(result);
    }

    // ─── Public: Customer Register ────────────────────────────────────────────
    // No cache — POST + creates a user.

    [HttpPost("register")]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request)
    {
        var result = await _storeService.RegisterCustomerAsync(request);
        return ToResponse(result);
    }

    // ─── Customer: Place Order ────────────────────────────────────────────────

    [HttpPost("orders")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        var result = await _storeService.PlaceOrderAsync(request, _currentUser.Id);
        return ToResponse(result);
    }

    // ─── Customer: List My Orders ─────────────────────────────────────────────
    // Was entirely missing — the storefront's "My Orders" page had nothing
    // to call and 404'd. No caching: this is per-customer authenticated data.

    [HttpGet("orders")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> GetOrders()
    {
        var result = await _storeService.GetOrdersAsync(_currentUser.Id);
        return ToResponse(result);
    }

    // ─── Customer: Get Order ──────────────────────────────────────────────────

    [HttpGet("orders/{orderId:guid}")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        var result = await _storeService.GetOrderAsync(orderId, _currentUser.Id);
        return ToResponse(result);
    }

    // ─── Public: Log Visit ────────────────────────────────────────────────────
    // No cache — writes to DB.

    [HttpPost("visit")]
    public async Task<IActionResult> LogVisit()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _storeService.LogVisitorAsync(ip);
        return Ok();
    }

    // ─── Response Helper ──────────────────────────────────────────────────────

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.",
            result.ValidationErrors
        ));
    }
}
