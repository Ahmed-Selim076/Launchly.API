using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.SuperAdmin;
using Launchly.API.Common;

namespace Launchly.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super/analytics")]
[Authorize(Policy = "SuperAdmin")]
public class SuperAnalyticsController : ControllerBase
{
    private readonly SuperAdminService _superAdminService;

    public SuperAnalyticsController(SuperAdminService superAdminService)
    {
        _superAdminService = superAdminService;
    }

    // GET /api/v1/super/analytics
    [HttpGet]
    public async Task<IActionResult> GetPlatformStats()
    {
        var result = await _superAdminService.GetPlatformStatsAsync();
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
