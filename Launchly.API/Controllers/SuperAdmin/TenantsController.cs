using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.SuperAdmin;
using Launchly.API.Application.SuperAdmin.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super/tenants")]
[Authorize(Policy = "SuperAdmin")]
public class TenantsController : ControllerBase
{
    private readonly SuperAdminService _superAdminService;

    public TenantsController(SuperAdminService superAdminService)
    {
        _superAdminService = superAdminService;
    }

    // GET /api/v1/super/tenants
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TenantsQuery query)
    {
        var result = await _superAdminService.GetTenantsAsync(query);
        return ToResponse(result);
    }

    // GET /api/v1/super/tenants/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _superAdminService.GetTenantAsync(id);
        return ToResponse(result);
    }

    // PATCH /api/v1/super/tenants/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusRequest request)
    {
        var result = await _superAdminService.UpdateTenantStatusAsync(id, request);
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
