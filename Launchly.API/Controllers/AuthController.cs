using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Auth;
using Launchly.API.Application.Auth.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Interfaces;

namespace Launchly.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;

    public AuthController(AuthService authService, ITenantContext tenantContext, ICurrentUser currentUser)
    {
        _authService = authService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ─── Register (Tenant Signup) ─────────────────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return ToResponse(result);
    }

    // ─── Login (Tenant Admin) ─────────────────────────────────────────────────

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return ToResponse(result);
    }

    // ─── Login (Customer on Storefront) ──────────────────────────────────────

    [HttpPost("login-customer")]
    public async Task<IActionResult> LoginCustomer([FromBody] LoginRequest request)
    {
        if (_tenantContext.TenantId is null)
            return BadRequest(ApiResponse<string>.Fail("Store not found."));

        var result = await _authService.CustomerLoginAsync(request, _tenantContext.TenantId.Value);
        return ToResponse(result);
    }

    // ─── Google OAuth ─────────────────────────────────────────────────────────

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest request)
    {
        var result = await _authService.GoogleAuthAsync(request, _tenantContext.TenantId);
        return ToResponse(result);
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshAsync(request);
        return ToResponse(result);
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.LogoutAsync(request);
        return ToResponse(result);
    }

    // ─── Verify Email ─────────────────────────────────────────────────────────

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _authService.VerifyEmailAsync(request);
        return ToResponse(result);
    }

    // ─── Forgot Password ──────────────────────────────────────────────────────

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        return ToResponse(result);
    }

    // ─── Reset Password ───────────────────────────────────────────────────────

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return ToResponse(result);
    }

    // ─── Change Password (authenticated — knows current password) ────────────
    // Distinct from Forgot/Reset above, which is the unauthenticated,
    // token-based flow for someone who's locked out. This is for a signed-in
    // user changing their password from Account settings.

    [HttpPost("change-password")]
    [Authorize(Policy = "TenantMember")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _authService.ChangePasswordAsync(_currentUser.Id, request);
        return ToResponse(result);
    }

    // ─── My Profile ────────────────────────────────────────────────────────────
    // Was entirely missing — the JWT carries only firstName/lastName/email/role
    // (set once, at login), so anything that can change afterwards without a
    // fresh login — like the avatar added here — needs its own fetch/update
    // pair rather than being baked into the token.

    [HttpGet("me")]
    [Authorize(Policy = "TenantMember")]
    public async Task<IActionResult> GetMe()
    {
        var result = await _authService.GetMeAsync(_currentUser.Id);
        return ToResponse(result);
    }

    [HttpPatch("avatar")]
    [Authorize(Policy = "TenantMember")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        var result = await _authService.UpdateAvatarAsync(_currentUser.Id, request.AvatarUrl);
        return ToResponse(result);
    }

    // ─── Check Subdomain Availability ────────────────────────────────────────

    [HttpGet("check-subdomain/{subdomain}")]
    public async Task<IActionResult> CheckSubdomain(string subdomain)
    {
        var result = await _authService.CheckSubdomainAsync(subdomain);
        return ToResponse(result);
    }

    // ─── Response Helper ──────────────────────────────────────────────────────

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.",
            result.ValidationErrors
        ));
    }
}