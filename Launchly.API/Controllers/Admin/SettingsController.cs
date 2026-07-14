using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Settings;
using Launchly.API.Application.Settings.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Policy = "TenantAdmin")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;

    public SettingsController(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    // ─── Get Settings ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _settingsService.GetAsync();
        return ToResponse(result);
    }

    // ─── Update Settings ──────────────────────────────────────────────────────

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var result = await _settingsService.UpdateAsync(request);
        return ToResponse(result);
    }

    // ─── Update Logo ──────────────────────────────────────────────────────────

    [HttpPatch("logo")]
    public async Task<IActionResult> UpdateLogo([FromBody] UpdateLogoRequest request)
    {
        var result = await _settingsService.UpdateLogoAsync(request.LogoUrl);
        return ToResponse(result);
    }

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

public record UpdateLogoRequest(string LogoUrl);
