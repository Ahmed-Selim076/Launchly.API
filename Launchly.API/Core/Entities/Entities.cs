using Launchly.API.Core.Enums;
using Launchly.API.Core.Entities.Base;

namespace Launchly.API.Core.Entities;

// ─── TENANT ───────────────────────────────────────────────────────────────────

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public StoreType StoreType { get; set; }

    // Which of the 3 layout templates for this StoreType the tenant
    // picked at signup (1, 2, or 3). Scoped within StoreType — there's
    // no cross-StoreType template identity (Ecommerce template 2 and
    // Restaurant template 2 are unrelated designs that happen to share
    // a number). See BACKEND_PLAN.md Section 17.
    public int TemplateId { get; set; } = 1;

    public PlanType PlanType { get; set; } = PlanType.Free;
    public bool IsActive { get; set; } = true;

    // Navigation
    public TenantSettings? Settings { get; set; }
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Service> Services { get; set; } = [];
    public ICollection<MenuItem> MenuItems { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<VisitorLog> VisitorLogs { get; set; } = [];
}

// ─── TENANT SETTINGS ──────────────────────────────────────────────────────────

public class TenantSettings : BaseEntity
{
    public Guid TenantId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#C1522A";
    public string SecondaryColor { get; set; } = "#F2EDE6";
    public string? HeroText { get; set; }
    public string? AboutText { get; set; }
    public string? GoogleAnalyticsId { get; set; }

    // Contact channels — shown on the storefront Contact page and Footer.
    // All optional: the UI falls back to sensible "not provided" states
    // (hiding the card / link) rather than rendering placeholder junk.
    public string? ContactPhone { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactAddress { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}

// ─── CONTACT MESSAGE ────────────────────────────────────────────────────────

// Submissions from the public storefront Contact form. No admin UI to
// browse these yet (flagged separately) — they're persisted so nothing
// submitted today is lost once that UI exists.
public class ContactMessage : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
}

// ─── USER ─────────────────────────────────────────────────────────────────────

public class User : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerifyToken { get; set; }
    public DateTime? EmailVerifyExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderId { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<WishlistItem> WishlistItems { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}

// ─── REFRESH TOKEN ────────────────────────────────────────────────────────────

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public User User { get; set; } = null!;
}

// ─── AUDIT LOG ────────────────────────────────────────────────────────────────

public class AuditLog : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}

// ─── VISITOR LOG ──────────────────────────────────────────────────────────────

public class VisitorLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public string IpHash { get; set; } = string.Empty;
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}

// ─── CATEGORY ─────────────────────────────────────────────────────────────────

public class Category : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public ICollection<Product> Products { get; set; } = [];
}

// ─── PRODUCT ──────────────────────────────────────────────────────────────────

public class Product : AuditableEntity
{
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; } = 0;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Pre-discount reference price. When set and greater than Price, the
    // storefront renders a strikethrough "was" price + a "SALE" badge hint.
    public decimal? OriginalPrice { get; set; }

    // Free-text merchant label shown as a pill on the product card
    // (e.g. "New", "Best seller"). Null = no badge rendered.
    public string? Badge { get; set; }

    // Optimistic concurrency token, mapped to PostgreSQL's xmin system
    // column (see AppDbContext.OnModelCreating — Property(...).IsRowVersion()).
    // xmin is a uint on Npgsql, not a byte[] like SQL Server's rowversion.
    // Two simultaneous orders for the last unit of stock must not both
    // succeed — EF Core includes this in the UPDATE's WHERE clause and
    // throws DbUpdateConcurrencyException if another transaction already
    // changed the row first.
    public uint RowVersion { get; set; }

    // Navigation
    public Category? Category { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public ICollection<WishlistItem> WishlistItems { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}

// ─── WISHLIST ───────────────────────────────────────────────────────────────

// One row per (customer, product). Customers are Users with UserRole.Customer
// (see UserRole in Core/Enums/Enums.cs) — no separate "Customer" entity exists
// in this schema, so we reference User directly, same as Order.CustomerId.
public class WishlistItem : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }

    // Navigation
    public User Customer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

// ─── REVIEW ─────────────────────────────────────────────────────────────────

public class Review : TenantEntity
{
    public Guid ProductId { get; set; }
    public Guid CustomerId { get; set; }
    public int Rating { get; set; }           // 1–5
    public string? Comment { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public User Customer { get; set; } = null!;
}

// ─── ORDER ────────────────────────────────────────────────────────────────────

public class Order : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderType? OrderType { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public User Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
}

// ─── ORDER ITEM ───────────────────────────────────────────────────────────────

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? MenuItemId { get; set; }

    // Snapshots — immutable after creation
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    // Navigation
    public Order Order { get; set; } = null!;
    public Product? Product { get; set; }
    public MenuItem? MenuItem { get; set; }
}

// ─── SERVICE (Booking) ────────────────────────────────────────────────────────

public class Service : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMins { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }

    // Navigation
    public ICollection<Appointment> Appointments { get; set; } = [];
}

// ─── APPOINTMENT (Booking) ────────────────────────────────────────────────────

public class Appointment : TenantEntity
{
    public Guid ServiceId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }

    // Navigation
    public Service Service { get; set; } = null!;
    public User Customer { get; set; } = null!;
}

// ─── MENU CATEGORY (Restaurant) ───────────────────────────────────────────────

public class MenuCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public ICollection<MenuItem> Items { get; set; } = [];
}

// ─── MENU ITEM (Restaurant) ───────────────────────────────────────────────────

public class MenuItem : AuditableEntity
{
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public MenuCategory? Category { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}