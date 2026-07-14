using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Restaurant;
using Launchly.API.Application.Restaurant.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu-items")]
[Authorize(Policy = "TenantAdmin")]
public class MenuItemsController : ControllerBase
{
    private readonly RestaurantService _restaurantService;

    public MenuItemsController(RestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => ToResponse(await _restaurantService.GetItemsAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => ToResponse(await _restaurantService.GetItemByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMenuItemRequest request)
        => ToResponse(await _restaurantService.CreateItemAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMenuItemRequest request)
        => ToResponse(await _restaurantService.UpdateItemAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => ToResponse(await _restaurantService.DeleteItemAsync(id));

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.", result.ValidationErrors));
    }
}
