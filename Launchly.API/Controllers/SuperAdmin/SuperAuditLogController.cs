using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.SuperAdmin;
using Launchly.API.Common;

namespace Launchly.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super/audit-log")]
[Authorize(Policy = "SuperAdmin")]
public class SuperAuditLogController : ControllerBase
{
    private readonly SuperAdminService _superAdminService;

    public SuperAuditLogController(SuperAdminService superAdminService)
    {
        _superAdminService = superAdminService;
    }

    // GET /api/v1/super/audit-log
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var result = await _superAdminService.GetAuditLogsAsync(page, pageSize);
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
