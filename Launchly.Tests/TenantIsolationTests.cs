using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Launchly.API.Application.Products;
using Launchly.API.Application.Products.DTOs;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Services;
using Launchly.Tests.Helpers;

namespace Launchly.Tests;

/// <summary>
/// Verifies that EF Core Global Query Filters correctly isolate tenant data.
///
/// These are the most critical tests in the entire codebase:
/// a failure here means Tenant A can see Tenant B's products, orders, or customers —
/// which is a data-breach, not just a bug.
///
/// Test strategy: seed two tenants with overlapping data, set the tenant context
/// to one of them, and assert the service only returns that tenant's records.
/// </summary>
public class TenantIsolationTests
{
    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private static readonly Guid TenantAId = Guid.NewGuid();
    private static readonly Guid TenantBId = Guid.NewGuid();

    /// <summary>
    /// Seeds two tenants with one product each, both with the same name
    /// so we can confirm it's the TenantId filter doing the work, not a name filter.
    ///
    /// The returned ITenantContext is a substitute whose TenantId can be
    /// changed afterward via SetTenant(...) — the db's Query Filter reads
    /// it fresh on every query, so switching it mid-test re-scopes the
    /// same db to a different tenant.
    /// </summary>
    private static async Task<(
        Launchly.API.Infrastructure.Data.AppDbContext db,
        ITenantContext tenantContext,
        Product productA,
        Product productB)>
        SeedTwoTenantsAsync()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        var db = DbFactory.Create(tenantContext: tenantContext);

        var tenantA = new Tenant { Id = TenantAId, Name = "Shop A", Subdomain = "shop-a", StoreType = StoreType.Ecommerce };
        var tenantB = new Tenant { Id = TenantBId, Name = "Shop B", Subdomain = "shop-b", StoreType = StoreType.Ecommerce };
        db.Tenants.AddRange(tenantA, tenantB);

        var productA = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAId,
            Name = "Widget",
            Slug = "widget-a",
            Price = 10,
            IsActive = true
        };

        var productB = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantBId,
            Name = "Widget",          // same name — isolation must rely on TenantId
            Slug = "widget-b",
            Price = 20,
            IsActive = true
        };

        db.Products.AddRange(productA, productB);
        await db.SaveChangesAsync();

        return (db, tenantContext, productA, productB);
    }

    private static void SetTenant(ITenantContext tenantContext, Guid tenantId) =>
        tenantContext.TenantId.Returns(tenantId);

    private static AuditLogService MakeAuditLog()
    {
        // AuditLogService writes to DB — we give it a separate in-memory DB
        // so audit writes don't pollute the shared seeded DB.
        var auditTenantContext = Substitute.For<ITenantContext>();
        var auditDb = DbFactory.Create(tenantContext: auditTenantContext);
        var currentUser = Substitute.For<ICurrentUser>();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var logger = Substitute.For<ILogger<AuditLogService>>();
        return new AuditLogService(auditDb, currentUser, auditTenantContext, httpContextAccessor, logger);
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentTenantProducts()
    {
        var (db, tenantContext, productA, _) = await SeedTwoTenantsAsync();
        SetTenant(tenantContext, TenantAId);
        var auditLog = MakeAuditLog();
        var svc = new ProductService(db, tenantContext, auditLog);

        var result = await svc.GetAllAsync(new ProductsQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(productA.Id,
            because: "Tenant A's context should only see Tenant A's product");
    }

    [Fact]
    public async Task GetById_CannotAccessOtherTenantProduct()
    {
        var (db, tenantContext, _, productB) = await SeedTwoTenantsAsync();

        // Authenticated as Tenant A, trying to fetch Tenant B's product by ID
        SetTenant(tenantContext, TenantAId);
        var auditLog = MakeAuditLog();
        var svc = new ProductService(db, tenantContext, auditLog);

        var result = await svc.GetByIdAsync(productB.Id);

        result.IsSuccess.Should().BeFalse(
            because: "Tenant A must not be able to retrieve Tenant B's product by ID");
        result.StatusCode.Should().Be(404,
            because: "the product should appear as not found — not a 403 that leaks its existence");
    }

    [Fact]
    public async Task Delete_CannotDeleteOtherTenantProduct()
    {
        var (db, tenantContext, _, productB) = await SeedTwoTenantsAsync();

        SetTenant(tenantContext, TenantAId);
        var auditLog = MakeAuditLog();
        var svc = new ProductService(db, tenantContext, auditLog);

        var result = await svc.DeleteAsync(productB.Id);

        result.IsSuccess.Should().BeFalse(
            because: "Tenant A must not be able to delete Tenant B's product");

        // Verify Tenant B's product is still in the DB (not soft-deleted).
        // Same db, same ITenantContext — just re-pointed at Tenant B, since
        // the Query Filter re-reads tenantContext.TenantId on every query.
        SetTenant(tenantContext, TenantBId);
        var check = await svc.GetByIdAsync(productB.Id);
        check.IsSuccess.Should().BeTrue(
            because: "Tenant B's product should be untouched after Tenant A's delete attempt");
    }

    [Fact]
    public async Task SoftDeleted_Products_AreNotVisible_ToAnyTenant()
    {
        var (db, tenantContext, productA, _) = await SeedTwoTenantsAsync();

        SetTenant(tenantContext, TenantAId);
        var auditLog = MakeAuditLog();
        var svc = new ProductService(db, tenantContext, auditLog);

        // Soft-delete Tenant A's own product
        await svc.DeleteAsync(productA.Id);

        // Now it should not be visible even to Tenant A
        var result = await svc.GetByIdAsync(productA.Id);
        result.IsSuccess.Should().BeFalse(
            because: "soft-deleted products must not be visible through normal queries");
    }

    [Fact]
    public async Task Create_SetsCorrectTenantId_Automatically()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        SetTenant(tenantContext, TenantAId);
        var db = DbFactory.Create(tenantContext: tenantContext);

        var tenant = new Tenant { Id = TenantAId, Name = "Shop A", Subdomain = "shop-a", StoreType = StoreType.Ecommerce };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var auditLog = MakeAuditLog();
        var svc = new ProductService(db, tenantContext, auditLog);

        var request = new CreateProductRequest(
            Name: "New Product",
            Description: null,
            Price: 99.99m,
            Stock: 10,
            ImageUrl: null,
            CategoryId: null,
            IsActive: true
        );

        var result = await svc.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();

        // Verify the raw DB record has the correct TenantId
        var raw = db.Products.IgnoreQueryFilters()
                              .Single(p => p.Id == result.Value!.Id);
        raw.TenantId.Should().Be(TenantAId,
            because: "ProductService must stamp TenantId from context, not trust the request");
    }
}
