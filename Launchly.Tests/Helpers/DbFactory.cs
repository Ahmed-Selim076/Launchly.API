using Microsoft.EntityFrameworkCore;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using NSubstitute;

namespace Launchly.Tests.Helpers;

/// <summary>
/// Creates a fresh in-memory AppDbContext for each test.
/// Each call gets an isolated database — no state leaks between tests.
///
/// AppDbContext's Global Query Filters read ITenantContext.TenantId at
/// query-execution time, not at model-build time. Passing in a
/// substitute (rather than a fixed value) lets a test change which
/// tenant is "current" after the context already exists, by calling
/// `tenantContext.TenantId.Returns(newId)` again on the same instance.
/// </summary>
public static class DbFactory
{
    public static AppDbContext Create(string? dbName = null, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        // Default: no tenant set — callers that need isolation should pass
        // their own ITenantContext (real or substitute).
        tenantContext ??= Substitute.For<ITenantContext>();

        return new AppDbContext(options, tenantContext);
    }
}
