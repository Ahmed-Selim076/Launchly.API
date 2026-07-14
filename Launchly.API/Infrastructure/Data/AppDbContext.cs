using Microsoft.EntityFrameworkCore;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Interfaces;

namespace Launchly.API.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // ─── DbSets ───────────────────────────────────────────────────────────────

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<VisitorLog> VisitorLogs => Set<VisitorLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    // ─── Model Configuration ──────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Tenant ────────────────────────────────────────────────────────────
        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Subdomain).IsUnique();
            e.Property(t => t.StoreType).HasConversion<int>();
            e.Property(t => t.PlanType).HasConversion<int>();
        });

        // ── TenantSettings ────────────────────────────────────────────────────
        builder.Entity<TenantSettings>(e =>
        {
            e.HasIndex(s => s.TenantId).IsUnique();
            e.HasOne(s => s.Tenant)
             .WithOne(t => t.Settings)
             .HasForeignKey<TenantSettings>(s => s.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── User ──────────────────────────────────────────────────────────────
        builder.Entity<User>(e =>
        {
            e.HasIndex(u => new { u.Email, u.TenantId }).IsUnique();
            e.Property(u => u.Role).HasConversion<int>();
        });

        // ── Product ───────────────────────────────────────────────────────────
        builder.Entity<Product>(e =>
        {
            e.HasIndex(p => new { p.Slug, p.TenantId }).IsUnique();
            e.Property(p => p.Price).HasColumnType("numeric(10,2)");
            e.Property(p => p.OriginalPrice).HasColumnType("numeric(10,2)");
            // Maps to PostgreSQL's xmin system column rather than a regular
            // column — see Migrations/20260622090000_AddProductRowVersion.cs.
            e.Property(p => p.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion();
            e.HasQueryFilter(p =>
                p.TenantId == _tenantContext.TenantId &&
                p.DeletedAt == null);
        });

        // ── Category ──────────────────────────────────────────────────────────
        builder.Entity<Category>(e =>
        {
            e.HasQueryFilter(c =>
                c.TenantId == _tenantContext.TenantId &&
                c.DeletedAt == null);
        });

        // ── WishlistItem ──────────────────────────────────────────────────────
        builder.Entity<WishlistItem>(e =>
        {
            // A customer can only wishlist a given product once.
            e.HasIndex(w => new { w.CustomerId, w.ProductId }).IsUnique();
            e.HasOne(w => w.Product)
             .WithMany(p => p.WishlistItems)
             .HasForeignKey(w => w.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Customer)
             .WithMany(u => u.WishlistItems)
             .HasForeignKey(w => w.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(w => w.TenantId == _tenantContext.TenantId);
        });

        // ── Review ────────────────────────────────────────────────────────────
        builder.Entity<Review>(e =>
        {
            // One review per customer per product (AddReviewAsync upserts
            // instead of inserting a duplicate — this index is the backstop).
            e.HasIndex(r => new { r.CustomerId, r.ProductId }).IsUnique();
            e.HasOne(r => r.Product)
             .WithMany(p => p.Reviews)
             .HasForeignKey(r => r.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Customer)
             .WithMany(u => u.Reviews)
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
        });

        // ── ContactMessage ───────────────────────────────────────────────────
        builder.Entity<ContactMessage>(e =>
        {
            e.HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
        });

        // ── Order ─────────────────────────────────────────────────────────────
        builder.Entity<Order>(e =>
        {
            e.Property(o => o.TotalAmount).HasColumnType("numeric(10,2)");
            e.Property(o => o.Status).HasConversion<int>();
            e.Property(o => o.OrderType).HasConversion<int>();
            e.HasQueryFilter(o =>
                o.TenantId == _tenantContext.TenantId &&
                o.DeletedAt == null);
        });

        // ── OrderItem ─────────────────────────────────────────────────────────
        builder.Entity<OrderItem>(e =>
        {
            e.Property(oi => oi.UnitPrice).HasColumnType("numeric(10,2)");
            e.Ignore(oi => oi.LineTotal);
            e.HasOne(oi => oi.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(oi => oi.OrderId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(oi => oi.Product)
             .WithMany(p => p.OrderItems)
             .HasForeignKey(oi => oi.ProductId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(oi => oi.MenuItem)
             .WithMany(m => m.OrderItems)
             .HasForeignKey(oi => oi.MenuItemId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Service ───────────────────────────────────────────────────────────
        builder.Entity<Service>(e =>
        {
            e.Property(s => s.Price).HasColumnType("numeric(10,2)");
            e.HasQueryFilter(s =>
                s.TenantId == _tenantContext.TenantId &&
                s.DeletedAt == null);
        });

        // ── Appointment ───────────────────────────────────────────────────────
        builder.Entity<Appointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<int>();
            e.HasIndex(a => new { a.TenantId, a.ServiceId, a.StartTime })
             .IsUnique()
             .HasFilter("\"DeletedAt\" IS NULL AND \"Status\" != 3");
            e.HasQueryFilter(a =>
                a.TenantId == _tenantContext.TenantId &&
                a.DeletedAt == null);
        });

        // ── MenuCategory ──────────────────────────────────────────────────────
        builder.Entity<MenuCategory>(e =>
        {
            e.HasQueryFilter(mc =>
                mc.TenantId == _tenantContext.TenantId &&
                mc.DeletedAt == null);
        });

        // ── MenuItem ──────────────────────────────────────────────────────────
        builder.Entity<MenuItem>(e =>
        {
            e.Property(m => m.Price).HasColumnType("numeric(10,2)");
            e.HasQueryFilter(m =>
                m.TenantId == _tenantContext.TenantId &&
                m.DeletedAt == null);
        });

        // ── VisitorLog ────────────────────────────────────────────────────────
        builder.Entity<VisitorLog>(e =>
        {
            e.HasIndex(v => new { v.TenantId, v.VisitedAt });
        });

        // ── AuditLog ──────────────────────────────────────────────────────────
        builder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action).HasConversion<int>();
        });
    }

    // ─── SaveChanges — auto-update UpdatedAt ──────────────────────────────────

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Core.Entities.Base.AuditableEntity auditable
                && entry.State == EntityState.Modified)
            {
                auditable.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(ct);
    }
}