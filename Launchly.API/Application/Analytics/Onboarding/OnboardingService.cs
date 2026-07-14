using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Analytics.Onboarding.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Application.Analytics.Onboarding;

public class OnboardingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public OnboardingService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Result<OnboardingStatusDto>> GetStatusAsync()
    {
        if (_tenantContext.TenantId is null)
            return Result<OnboardingStatusDto>.Failure("Store context is required.");

        var tenantId = _tenantContext.TenantId.Value;

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant is null)
            return Result<OnboardingStatusDto>.Failure("Store context is required.");

        var settings = await _db.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        var hasLogo = !string.IsNullOrWhiteSpace(settings?.LogoUrl);

        // "Catalog created" and "first real activity" mean a different
        // entity per StoreType — there's no single "Products"/"Orders"
        // table that's meaningful for all three. Each branch below uses
        // each StoreType's actual storefront tables (see AppDbContext):
        // Ecommerce → Categories/Products/Orders, Booking → Services/
        // Appointments, Restaurant → MenuItems + Orders containing at
        // least one MenuItemId line (restaurant "orders" are rows in the
        // same shared Orders table, distinguished by their items — see
        // RestaurantService.GetOrdersAsync — not a separate FoodOrder
        // entity).
        bool hasCatalogItem;
        bool hasFirstActivity;
        string catalogStepKey, catalogStepLabel;
        string activityStepKey, activityStepLabel;

        switch (tenant.StoreType)
        {
            case StoreType.Booking:
                hasCatalogItem  = await _db.Services.AnyAsync();
                hasFirstActivity = await _db.Appointments.AnyAsync();
                catalogStepKey   = "service_created";
                catalogStepLabel = "Add your first service";
                activityStepKey   = "first_appointment";
                activityStepLabel = "Receive your first booking";
                break;

            case StoreType.Restaurant:
                hasCatalogItem  = await _db.MenuItems.AnyAsync();
                hasFirstActivity = await _db.Orders
                    .AnyAsync(o => o.Items.Any(i => i.MenuItemId != null));
                catalogStepKey   = "menu_item_created";
                catalogStepLabel = "Add your first menu item";
                activityStepKey   = "first_order";
                activityStepLabel = "Receive your first order";
                break;

            case StoreType.Ecommerce:
            default:
                hasCatalogItem  = await _db.Products.AnyAsync();
                hasFirstActivity = await _db.Orders
                    .AnyAsync(o => o.Items.Any(i => i.ProductId != null));
                catalogStepKey   = "product_created";
                catalogStepLabel = "Add your first product";
                activityStepKey   = "first_order";
                activityStepLabel = "Receive your first order";
                break;
        }

        var emailVerified = await _db.Users
            .AnyAsync(u => u.TenantId == tenantId &&
                           u.Role == UserRole.TenantAdmin &&
                           u.IsEmailVerified);

        var steps = new List<OnboardingStepDto>
        {
            new("email_verified", "Verify your email address", emailVerified),
            new("logo_uploaded",  "Upload your store logo",     hasLogo),
            new(catalogStepKey,   catalogStepLabel,              hasCatalogItem),
            new(activityStepKey,  activityStepLabel,             hasFirstActivity)
        };

        var completedCount = steps.Count(s => s.IsComplete);

        return Result<OnboardingStatusDto>.Success(new OnboardingStatusDto(
            steps,
            completedCount,
            steps.Count,
            completedCount == steps.Count
        ));
    }
}