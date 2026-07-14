using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Analytics;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/analytics")]
[Authorize(Policy = "TenantAdmin")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;

    public AnalyticsController(AnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    // GET /api/v1/admin/analytics/sales?period=30d
    [HttpGet("sales")]
    public async Task<IActionResult> GetSales([FromQuery] string period = "30d")
    {
        var result = await _analyticsService.GetSalesAsync(period);
        return ToResponse(result);
    }

    // GET /api/v1/admin/analytics/visitors
    [HttpGet("visitors")]
    public async Task<IActionResult> GetVisitors()
    {
        var result = await _analyticsService.GetVisitorsAsync();
        return ToResponse(result);
    }

    // GET /api/v1/admin/analytics/top-products?period=30d
    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] string period = "30d")
    {
        var result = await _analyticsService.GetTopProductsAsync(period);
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
