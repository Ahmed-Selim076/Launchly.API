using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Restaurant;
using Launchly.API.Application.Restaurant.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/food-orders")]
[Authorize(Policy = "TenantAdmin")]
public class FoodOrdersController : ControllerBase
{
    private readonly RestaurantService _restaurantService;

    public FoodOrdersController(RestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] FoodOrderQueryRequest query)
        => ToResponse(await _restaurantService.GetOrdersAsync(query));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => ToResponse(await _restaurantService.GetOrderByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFoodOrderRequest request)
        => ToResponse(await _restaurantService.CreateOrderAsync(request));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFoodOrderStatusRequest request)
        => ToResponse(await _restaurantService.UpdateOrderStatusAsync(id, request));

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.", result.ValidationErrors));
    }
}
