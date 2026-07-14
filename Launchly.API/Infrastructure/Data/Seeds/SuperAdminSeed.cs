using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Launchly.API.Infrastructure.Data.Seeds;

public static class SuperAdminSeed
{
    public static async Task RunAsync(AppDbContext db, IConfiguration config)
    {
        var email = config["SUPER_ADMIN_EMAIL"]
            ?? throw new InvalidOperationException(
                "SUPER_ADMIN_EMAIL is not configured.");

        var password = config["SUPER_ADMIN_PASSWORD"]
            ?? throw new InvalidOperationException(
                "SUPER_ADMIN_PASSWORD is not configured.");

        var exists = await db.Users
            .AnyAsync(u => u.Email == email && u.Role == UserRole.SuperAdmin);

        if (exists) return;

        var superAdmin = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            FirstName = "Super",
            LastName = "Admin",
            Role = UserRole.SuperAdmin,
            IsEmailVerified = true,
            TenantId = null
        };

        db.Users.Add(superAdmin);
        await db.SaveChangesAsync();
    }
}