using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Auth.DTOs;
using Launchly.API.Application.Tenants;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Auth;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly TemplateService _templateService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        TokenService tokenService,
        IEmailService emailService,
        TemplateService templateService,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
        _templateService = templateService;
        _config = config;
        _logger = logger;
    }

    // ─── Register ─────────────────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        // Check subdomain availability
        var subdomainTaken = await _db.Tenants
            .AnyAsync(t => t.Subdomain == request.Subdomain.ToLower());

        if (subdomainTaken)
            return Result<AuthResponse>.Failure("This subdomain is already taken.");

        // Check email availability
        var emailTaken = await _db.Users
            .AnyAsync(u => u.Email == request.Email && u.TenantId == null);

        if (emailTaken)
            return Result<AuthResponse>.Failure("An account with this email already exists.");

        // Validate the chosen template against the chosen store type. This
        // mirrors AuthValidators' StoreType validity check — both exist
        // because FluentValidation runs before this method, but a
        // cross-field rule (is TemplateId valid *for this* StoreType) isn't
        // expressible as a single-property rule, so it lives here instead.
        var storeType = (StoreType)request.StoreType;
        if (!_templateService.IsValidTemplateId(storeType, request.TemplateId))
            return Result<AuthResponse>.Failure(
                $"Template {request.TemplateId} is not available for this store type.");

        // Create tenant
        var tenant = new Tenant
        {
            Name = request.StoreName,
            Subdomain = request.Subdomain.ToLower().Trim(),
            StoreType = storeType,
            TemplateId = request.TemplateId,
            PlanType = PlanType.Free,
            IsActive = true
        };

        _db.Tenants.Add(tenant);

        // Create tenant settings
        var (defaultPrimary, defaultSecondary) = request.TemplateId switch
        {
            // Template 1 — Minimal: warm terracotta + cream (existing default)
            1 => ("#C1522A", "#F2EDE6"),
            // Template 2 — Bold: deep violet + soft lilac
            2 => ("#6D28D9", "#F3EEFF"),
            // Template 3 — Editorial: ink navy + warm ivory
            3 => ("#1F2937", "#F7F5F0"),
            _ => ("#C1522A", "#F2EDE6"),
        };

        var settings = new TenantSettings
        {
            TenantId = tenant.Id,
            StoreName = request.StoreName,
            PrimaryColor = defaultPrimary,
            SecondaryColor = defaultSecondary,
        };

        _db.TenantSettings.Add(settings);

        // Create tenant admin user
        var user = new User
        {
            TenantId = tenant.Id,
            Email = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.TenantAdmin,
            IsEmailVerified = false,
            EmailVerifyToken = _tokenService.GenerateSecureToken(),
            EmailVerifyExpiry = DateTime.UtcNow.AddHours(24)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Send verification email — non-blocking, never fails registration
        await _emailService.SendVerificationEmailAsync(
            user.Email, user.FirstName, user.EmailVerifyToken!);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await SaveRefreshTokenAsync(user.Id);

        return Result<AuthResponse>.Created(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(user, tenant.Subdomain)
        ));
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLower().Trim() &&
                u.Role != UserRole.Customer);

        if (user is null || user.PasswordHash is null)
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Your account has been suspended.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await SaveRefreshTokenAsync(user.Id);

        // SuperAdmin has no Tenant (user.Tenant is null), so subdomain comes
        // back null for them — the frontend keeps them on the current host
        // and routes to /super instead of trying a tenant redirect.
        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(user, user.Tenant?.Subdomain)
        ));
    }

    // ─── Customer Login ───────────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> CustomerLoginAsync(
        LoginRequest request,
        Guid tenantId)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLower().Trim() &&
                u.TenantId == tenantId &&
                u.Role == UserRole.Customer);

        if (user is null || user.PasswordHash is null)
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Your account has been suspended.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await SaveRefreshTokenAsync(user.Id);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(user)
        ));
    }

    // ─── Google OAuth ─────────────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> GoogleAuthAsync(GoogleAuthRequest request, Guid? tenantId)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config["GOOGLE_CLIENT_ID"]!]
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, validationSettings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed.");
            return Result<AuthResponse>.Unauthorized("Invalid Google token.");
        }

        User? user;

        if (tenantId is null)
        {
            // Platform root — this is a staff login (TenantAdmin/SuperAdmin),
            // not a per-tenant customer one. Staff users always have a real,
            // non-null TenantId, so they can never match a `TenantId == null`
            // filter — scope this lookup by role instead, the same way
            // LoginAsync does for the password flow.
            user = await _db.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u =>
                    u.Role != UserRole.Customer &&
                    u.ExternalProvider == "google" &&
                    u.ExternalProviderId == payload.Subject);

            if (user is null)
            {
                user = await _db.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Role != UserRole.Customer && u.Email == payload.Email.ToLower());

                if (user is not null)
                {
                    // Existing email/password staff account — link Google to it
                    user.ExternalProvider = "google";
                    user.ExternalProviderId = payload.Subject;
                    user.IsEmailVerified = true;
                    await _db.SaveChangesAsync();
                }
            }

            // No account creation here — Google can't supply a store name,
            // subdomain, or store type, so a brand-new business still has to
            // go through the normal multi-step signup form.
            if (user is null)
                return Result<AuthResponse>.Failure(
                    "No Launchly account found for this Google account. Sign up first.");
        }
        else
        {
            // Tenant subdomain — customer login/signup, scoped to this tenant.
            // Look up by ExternalProviderId first, then by email (account linking)
            user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.ExternalProvider == "google" &&
                    u.ExternalProviderId == payload.Subject &&
                    u.TenantId == tenantId);

            if (user is null)
            {
                user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == payload.Email.ToLower() && u.TenantId == tenantId);

                if (user is not null)
                {
                    // Existing email/password account — link Google to it
                    user.ExternalProvider = "google";
                    user.ExternalProviderId = payload.Subject;
                    user.IsEmailVerified = true;
                    await _db.SaveChangesAsync();
                }
            }

            if (user is null)
            {
                user = new User
                {
                    TenantId = tenantId,
                    Email = payload.Email.ToLower(),
                    FirstName = payload.GivenName ?? "",
                    LastName = payload.FamilyName ?? "",
                    Role = UserRole.Customer,
                    IsEmailVerified = true,
                    ExternalProvider = "google",
                    ExternalProviderId = payload.Subject
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }
        }

        if (!user.IsActive)
            return Result<AuthResponse>.Forbidden("Your account has been suspended.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await SaveRefreshTokenAsync(user.Id);

        // Only staff (platform-root) logins need the subdomain back — that's
        // what the frontend uses to redirect to the right tenant host. On a
        // tenant subdomain, user.Tenant isn't loaded and isn't needed there.
        var tenantSubdomain = tenantId is null ? user.Tenant?.Subdomain : null;

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(user, tenantSubdomain)
        ));
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is null || !stored.IsActive)
            return Result<AuthResponse>.Unauthorized("Invalid or expired refresh token.");

        // Revoke old token
        stored.RevokedAt = DateTime.UtcNow;

        // Issue new tokens
        var accessToken = _tokenService.GenerateAccessToken(stored.User);
        var refreshToken = await SaveRefreshTokenAsync(stored.User.Id);

        await _db.SaveChangesAsync();

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            MapToUserDto(stored.User)
        ));
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    public async Task<Result<bool>> LogoutAsync(RefreshTokenRequest request)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (stored is null)
            return Result<bool>.Success(true);

        stored.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    // ─── Verify Email ─────────────────────────────────────────────────────────

    public async Task<Result<bool>> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.EmailVerifyToken == request.Token);

        if (user is null)
            return Result<bool>.Failure("Invalid verification token.");

        if (user.EmailVerifyExpiry < DateTime.UtcNow)
            return Result<bool>.Failure("Verification token has expired.");

        user.IsEmailVerified = true;
        user.EmailVerifyToken = null;
        user.EmailVerifyExpiry = null;

        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    // ─── Forgot Password ──────────────────────────────────────────────────────

    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower().Trim());

        // Always return success — never reveal if email exists
        if (user is null)
            return Result<bool>.Success(true);

        user.PasswordResetToken = _tokenService.GenerateSecureToken();
        user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(30);

        await _db.SaveChangesAsync();

        await _emailService.SendPasswordResetEmailAsync(
            user.Email, user.FirstName, user.PasswordResetToken);

        return Result<bool>.Success(true);
    }

    // ─── Reset Password ───────────────────────────────────────────────────────

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

        if (user is null)
            return Result<bool>.Failure("Invalid reset token.");

        if (user.PasswordResetExpiry < DateTime.UtcNow)
            return Result<bool>.Failure("Reset token has expired. Please request a new one.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;

        // Revoke all refresh tokens for security
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
            token.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    // ─── Change Password (authenticated user, knows current password) ────────
    // Distinct from ResetPasswordAsync above, which is the "forgot password"
    // flow (unauthenticated, token-based). This one is for a logged-in user
    // who wants to change their password from Account settings.

    public async Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.PasswordHash is null)
            return Result<bool>.Failure("Account not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return Result<bool>.Failure("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    // ─── Profile: Avatar + Me ─────────────────────────────────────────────────

    public async Task<Result<UserDto>> UpdateAvatarAsync(Guid userId, string? avatarUrl)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<UserDto>.Failure("Account not found.");

        user.AvatarUrl = avatarUrl;
        await _db.SaveChangesAsync();

        return Result<UserDto>.Success(MapToUserDto(user));
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<UserDto>.Failure("Account not found.");

        return Result<UserDto>.Success(MapToUserDto(user));
    }

    // ─── Check Subdomain ──────────────────────────────────────────────────────

    public async Task<Result<bool>> CheckSubdomainAsync(string subdomain)
    {
        var taken = await _db.Tenants
            .AnyAsync(t => t.Subdomain == subdomain.ToLower().Trim());

        return taken
            ? Result<bool>.Failure("This subdomain is already taken.")
            : Result<bool>.Success(true);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<string> SaveRefreshTokenAsync(Guid userId)
    {
        var raw = _tokenService.GenerateRefreshToken();
        var hash = _tokenService.HashRefreshToken(raw);

        var refreshDays = int.Parse(_config["JWT_REFRESH_DAYS"] ?? "7");

        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        return raw;
    }

    private static UserDto MapToUserDto(User user, string? tenantSubdomain = null) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role.ToString(),
        user.TenantId,
        user.AvatarUrl,
        tenantSubdomain
    );
}