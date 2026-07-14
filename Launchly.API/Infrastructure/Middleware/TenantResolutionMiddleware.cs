using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Infrastructure.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "tenant:subdomain:";

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db,
        TenantContext tenantContext,
        IConfiguration config)
    {
        // 1. Try Host header first (production & dev with proper subdomain routing)
        var subdomain = ExtractSubdomain(context.Request.Host.Host, config["PLATFORM_DOMAIN"]);

        // 2. Fall back to X-Tenant-Subdomain header (dev: frontend on :4200 calls API on :5117)
        if (subdomain is null)
        {
            var headerSubdomain = context.Request.Headers["X-Tenant-Subdomain"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerSubdomain))
                subdomain = headerSubdomain.Trim().ToLower();
        }

        if (subdomain is null)
        {
            // No subdomain — try JWT tenantId claim for authenticated requests
            var tenantClaim = context.User.FindFirstValue("tenantId")
                           ?? context.User.FindFirstValue("tenant_id");

            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var jwtTenantId))
            {
                var isActive = await IsTenantActiveAsync(db, jwtTenantId, context.RequestAborted);

                if (isActive is false)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(
                        new { message = "This store has been suspended." });
                    return;
                }

                if (isActive is true)
                    tenantContext.Set(jwtTenantId, "dev");
            }

            await _next(context);
            return;
        }

        var cached = await GetCachedTenantAsync(db, subdomain, context.RequestAborted);

        if (cached is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                new { message = "Store not found." });
            return;
        }

        if (!cached.Value.isActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { message = "This store has been suspended." });
            return;
        }

        // Every tenant-scoped EF Core query filter reads TenantContext.TenantId,
        // set below from a subdomain that ultimately came from a client-supplied
        // value (Host header, or X-Tenant-Subdomain in dev) — not from anything
        // the server signed. An authenticated staff/customer request carries its
        // real tenant in the JWT's 'tenantId' claim instead, which can't be
        // forged. If the two disagree, someone is pointing their own valid
        // token at a different tenant's subdomain/header to read or write that
        // tenant's data — reject rather than silently scoping to the header.
        var jwtTenantClaim = context.User.FindFirstValue("tenantId")
                          ?? context.User.FindFirstValue("tenant_id");

        if (jwtTenantClaim is not null &&
            Guid.TryParse(jwtTenantClaim, out var jwtTenantIdForCheck) &&
            jwtTenantIdForCheck != cached.Value.id)
        {
            _logger.LogWarning(
                "Tenant mismatch: authenticated user's token is for tenant {JwtTenantId} " +
                "but the request resolved to tenant {ResolvedTenantId} (subdomain {Subdomain}).",
                jwtTenantIdForCheck, cached.Value.id, subdomain);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { message = "You do not have access to this store." });
            return;
        }

        tenantContext.Set(cached.Value.id, subdomain);

        await _next(context);
    }

    private async Task<(Guid id, bool isActive)?> GetCachedTenantAsync(
        AppDbContext db,
        string subdomain,
        CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{subdomain}";

        if (_cache.TryGetValue(cacheKey, out (Guid id, bool isActive) cached))
            return cached;

        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Subdomain == subdomain)
            .Select(t => new { t.Id, t.IsActive })
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
            return null;

        var result = (tenant.Id, tenant.IsActive);
        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private async Task<bool?> IsTenantActiveAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}id:{tenantId}";

        if (_cache.TryGetValue(cacheKey, out bool? cachedActive))
            return cachedActive;

        var isActive = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => (bool?)t.IsActive)
            .FirstOrDefaultAsync(ct);

        _cache.Set(cacheKey, isActive, CacheTtl);
        return isActive;
    }

    private static string? ExtractSubdomain(string host, string? platformDomain)
    {
        var hostWithoutPort = host.Split(':')[0].ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(platformDomain))
        {
            var normalizedPlatformDomain = platformDomain.Split(':')[0].ToLowerInvariant();

            // Exact match to the platform's own domain (e.g. launchly.com, or
            // the Vercel/Railway domain used before a custom domain is set
            // up) — that's the marketing site / API root, not a tenant.
            if (hostWithoutPort == normalizedPlatformDomain)
                return null;

            var suffix = "." + normalizedPlatformDomain;

            // Only a real subdomain OF the platform domain counts as a
            // tenant — e.g. "storename.launchly.com" when
            // PLATFORM_DOMAIN=launchly.com. A host that merely happens to
            // have 3+ dot-separated labels (Railway's own
            // launchly-api076.up.railway.app, Vercel's own *.vercel.app)
            // does NOT match this suffix and is correctly treated as "no
            // tenant" below, instead of misreading its first label as a
            // subdomain and 404ing every request.
            if (hostWithoutPort.EndsWith(suffix, StringComparison.Ordinal))
            {
                var subdomain = hostWithoutPort[..^suffix.Length];

                // 'admin' hosts the SuperAdmin panel, not a tenant store —
                // it's reserved (see AuthValidators.ReservedSubdomains)
                // precisely so no real tenant can ever collide with it.
                return subdomain.Equals("www", StringComparison.OrdinalIgnoreCase) ||
                       subdomain.Equals("admin", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : subdomain;
            }

            // Host isn't the platform domain and isn't a subdomain of it —
            // e.g. someone hitting the Railway API domain directly. Not a
            // tenant request.
            return null;
        }

        // No PLATFORM_DOMAIN configured at all — fall back to the old
        // heuristic (only relevant for quick local testing without the env
        // var set; every real environment should set PLATFORM_DOMAIN).
        var parts = hostWithoutPort.Split('.');

        if (parts.Length >= 3)
        {
            var subdomain = parts[0];
            return subdomain.Equals("www", StringComparison.OrdinalIgnoreCase) ||
                   subdomain.Equals("admin", StringComparison.OrdinalIgnoreCase)
                ? null
                : subdomain;
        }

        if (parts.Length == 2 && parts[1].Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return parts[0];
        }

        return null;
    }
}
