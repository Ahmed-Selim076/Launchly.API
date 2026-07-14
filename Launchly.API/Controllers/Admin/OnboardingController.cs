using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Analytics.Onboarding;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/onboarding")]
[Authorize(Policy = "TenantAdmin")]
public class OnboardingController : ControllerBase
{
    private readonly OnboardingService _onboardingService;

    public OnboardingController(OnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _onboardingService.GetStatusAsync();
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