using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Launchly.API.Core.Entities;

namespace Launchly.API.Infrastructure.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    // ─── Access Token ─────────────────────────────────────────────────────────

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,        user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email,      user.Email),
            new(JwtRegisteredClaimNames.GivenName,  user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            // Both claim names so ClaimTypes.Role-based auth AND 'role' key both work
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role",          user.Role.ToString()),
        };

        if (user.TenantId.HasValue)
        {
            // snake_case for frontend JWT decode, camelCase for legacy ClaimsPrincipal lookup
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
            claims.Add(new Claim("tenantId",  user.TenantId.Value.ToString()));
        }

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JWT_SECRET"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry      = DateTime.UtcNow.AddMinutes(
                              int.Parse(_config["JWT_EXPIRY_MINUTES"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer:             _config["JWT_ISSUER"],
            audience:           _config["JWT_AUDIENCE"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLower();
    }

    // ─── Email Tokens ─────────────────────────────────────────────────────────

    public string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
