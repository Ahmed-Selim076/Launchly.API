using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Launchly.API.Infrastructure.Data.Seeds;

/// <summary>
/// Seeds one fully-populated demo tenant per (store type × template) combo —
/// 9 total — so a visitor can browse every real design with real sample
/// data at a fixed, memorable subdomain, without signing up or creating a
/// store themselves. Purely for showcase/portfolio purposes: each demo
/// tenant's owner account uses a fixed known password (not meant to be
/// secure — these are throwaway demo records, not real merchant accounts).
/// Idempotent: skipped entirely once the first demo subdomain already exists.
/// </summary>
public static class DemoDataSeed
{
    private const string DemoPassword = "Demo12345!";

    public static async Task RunAsync(AppDbContext db)
    {
        if (await db.Tenants.AnyAsync(t => t.Subdomain == "demo-ecommerce-1"))
            return; // already seeded

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword, workFactor: 12);

        await SeedEcommerce(db, passwordHash, 1, "demo-ecommerce-1", "Aura Goods",   "#C1522A", "#F2EDE6");
        await SeedEcommerce(db, passwordHash, 2, "demo-ecommerce-2", "Voltage",      "#6D28D9", "#F3EEFF");
        await SeedEcommerce(db, passwordHash, 3, "demo-ecommerce-3", "The Atelier",  "#1F2937", "#F7F5F0");

        await SeedRestaurant(db, passwordHash, 1, "demo-restaurant-1", "Molina",       "#C1522A", "#F2EDE6");
        await SeedRestaurant(db, passwordHash, 2, "demo-restaurant-2", "Ember & Oak",  "#6D28D9", "#F3EEFF");
        await SeedRestaurant(db, passwordHash, 3, "demo-restaurant-3", "Bistro Noir",  "#1F2937", "#F7F5F0");

        await SeedBooking(db, passwordHash, 1, "demo-booking-1", "Willow Spa",       "#C1522A", "#F2EDE6");
        await SeedBooking(db, passwordHash, 2, "demo-booking-2", "PowerHouse Fit",   "#6D28D9", "#F3EEFF");
        await SeedBooking(db, passwordHash, 3, "demo-booking-3", "Clarity Coaching", "#1F2937", "#F7F5F0");

        await db.SaveChangesAsync();
    }

    // ─── Ecommerce ──────────────────────────────────────────────────────────────

    private static async Task SeedEcommerce(
        AppDbContext db, string passwordHash, int templateId,
        string subdomain, string storeName, string primary, string secondary)
    {
        var tenant = NewTenant(subdomain, storeName, StoreType.Ecommerce, templateId);
        db.Tenants.Add(tenant);
        db.TenantSettings.Add(NewSettings(tenant.Id, storeName, primary, secondary,
            heroText: "Everyday pieces, thoughtfully made.",
            aboutText: $"{storeName} started as a small idea and grew into a place for considered, well-made goods."));
        db.Users.Add(NewOwner(tenant.Id, passwordHash, subdomain));

        var apparel = new Category { TenantId = tenant.Id, Name = "Apparel", SortOrder = 1 };
        var homeCat = new Category { TenantId = tenant.Id, Name = "Home",    SortOrder = 2 };
        db.Categories.AddRange(apparel, homeCat);

        var products = new[]
        {
            NewProduct(tenant.Id, apparel.Id, "Everyday Tee",       "everyday-tee",       "Soft cotton tee, made to last.", 28.00m, null,  "Best seller"),
            NewProduct(tenant.Id, apparel.Id, "Relaxed Trousers",   "relaxed-trousers",   "A tailored, easy-fit trouser.",  64.00m, 82.00m, "Sale"),
            NewProduct(tenant.Id, apparel.Id, "Wool Overcoat",      "wool-overcoat",      "A warm layer for cold days.",    148.00m, null, null),
            NewProduct(tenant.Id, homeCat.Id, "Ceramic Mug Set",    "ceramic-mug-set",    "Hand-glazed set of two.",        32.00m, null, "New"),
            NewProduct(tenant.Id, homeCat.Id, "Linen Table Runner", "linen-table-runner", "Woven from European linen.",     38.00m, null, null),
            NewProduct(tenant.Id, homeCat.Id, "Oak Serving Board",  "oak-serving-board",  "Solid oak, food-safe finish.",   54.00m, null, null),
        };
        db.Products.AddRange(products);
    }

    // ─── Restaurant ─────────────────────────────────────────────────────────────

    private static async Task SeedRestaurant(
        AppDbContext db, string passwordHash, int templateId,
        string subdomain, string storeName, string primary, string secondary)
    {
        var tenant = NewTenant(subdomain, storeName, StoreType.Restaurant, templateId);
        db.Tenants.Add(tenant);
        db.TenantSettings.Add(NewSettings(tenant.Id, storeName, primary, secondary,
            heroText: "Seasonal plates, cooked with care.",
            aboutText: $"{storeName} is a neighborhood table serving honest food, made fresh every day."));
        db.Users.Add(NewOwner(tenant.Id, passwordHash, subdomain));

        var starters = new MenuCategory { TenantId = tenant.Id, Name = "Starters", SortOrder = 1 };
        var mains    = new MenuCategory { TenantId = tenant.Id, Name = "Mains",    SortOrder = 2 };
        var desserts = new MenuCategory { TenantId = tenant.Id, Name = "Desserts", SortOrder = 3 };
        db.MenuCategories.AddRange(starters, mains, desserts);

        db.MenuItems.AddRange(
            NewMenuItem(tenant.Id, starters.Id, "Burrata & Heirloom Tomato", "Fresh burrata, basil oil, sea salt.", 14.00m),
            NewMenuItem(tenant.Id, starters.Id, "Charred Octopus",           "Smoked paprika, potato, salsa verde.", 18.00m),
            NewMenuItem(tenant.Id, mains.Id,    "Pan-Seared Salmon",         "Lemon butter, seasonal greens.",       27.00m),
            NewMenuItem(tenant.Id, mains.Id,    "Braised Short Rib",         "Red wine jus, root vegetable mash.",   32.00m),
            NewMenuItem(tenant.Id, mains.Id,    "Wild Mushroom Risotto",     "Parmesan, truffle oil.",               22.00m),
            NewMenuItem(tenant.Id, desserts.Id, "Basque Cheesecake",         "Caramelized top, vanilla bean.",       11.00m)
        );
    }

    // ─── Booking ────────────────────────────────────────────────────────────────

    private static async Task SeedBooking(
        AppDbContext db, string passwordHash, int templateId,
        string subdomain, string storeName, string primary, string secondary)
    {
        var tenant = NewTenant(subdomain, storeName, StoreType.Booking, templateId);
        db.Tenants.Add(tenant);
        db.TenantSettings.Add(NewSettings(tenant.Id, storeName, primary, secondary,
            heroText: "Book your next appointment in under a minute.",
            aboutText: $"{storeName} has been welcoming clients for years, with a focus on real care and real availability."));
        db.Users.Add(NewOwner(tenant.Id, passwordHash, subdomain));

        db.Services.AddRange(
            NewService(tenant.Id, "Consultation",        "A first visit to understand your needs.", 30, 0m),
            NewService(tenant.Id, "Standard Session",     "Our most popular full session.",           60, 45.00m),
            NewService(tenant.Id, "Extended Session",     "A longer, deep-focus session.",             90, 65.00m),
            NewService(tenant.Id, "Follow-up",            "A short check-in session.",                 20, 20.00m)
        );
    }

    // ─── Shared builders ──────────────────────────────────────────────────────

    private static Tenant NewTenant(string subdomain, string name, StoreType storeType, int templateId) => new()
    {
        Name = name,
        Subdomain = subdomain,
        StoreType = storeType,
        TemplateId = templateId,
        PlanType = PlanType.Free,
        IsActive = true,
    };

    private static TenantSettings NewSettings(
        Guid tenantId, string storeName, string primary, string secondary,
        string heroText, string aboutText) => new()
    {
        TenantId = tenantId,
        StoreName = storeName,
        PrimaryColor = primary,
        SecondaryColor = secondary,
        HeroText = heroText,
        AboutText = aboutText,
    };

    private static User NewOwner(Guid tenantId, string passwordHash, string subdomain) => new()
    {
        TenantId = tenantId,
        Email = $"owner@{subdomain}.demo.launchly.local",
        PasswordHash = passwordHash,
        FirstName = "Demo",
        LastName = "Owner",
        Role = UserRole.TenantAdmin,
        IsActive = true,
        IsEmailVerified = true,
    };

    private static Product NewProduct(
        Guid tenantId, Guid categoryId, string name, string slug, string description,
        decimal price, decimal? originalPrice, string? badge) => new()
    {
        TenantId = tenantId,
        CategoryId = categoryId,
        Name = name,
        Slug = slug,
        Description = description,
        Price = price,
        OriginalPrice = originalPrice,
        Badge = badge,
        Stock = 25,
        IsActive = true,
    };

    private static MenuItem NewMenuItem(
        Guid tenantId, Guid categoryId, string name, string description, decimal price) => new()
    {
        TenantId = tenantId,
        CategoryId = categoryId,
        Name = name,
        Description = description,
        Price = price,
        IsActive = true,
    };

    private static Service NewService(
        Guid tenantId, string name, string description, int durationMins, decimal price) => new()
    {
        TenantId = tenantId,
        Name = name,
        Description = description,
        DurationMins = durationMins,
        Price = price,
        IsActive = true,
    };
}
