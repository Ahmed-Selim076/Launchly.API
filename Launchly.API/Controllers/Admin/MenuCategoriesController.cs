using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Restaurant;
using Launchly.API.Application.Restaurant.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu-categories")]
[Authorize(Policy = "TenantAdmin")]
public class MenuCategoriesController : ControllerBase
{
    private readonly RestaurantService _restaurantService;

    public MenuCategoriesController(RestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => ToResponse(await _restaurantService.GetCategoriesAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => ToResponse(await _restaurantService.GetCategoryByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMenuCategoryRequest request)
        => ToResponse(await _restaurantService.CreateCategoryAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMenuCategoryRequest request)
        => ToResponse(await _restaurantService.UpdateCategoryAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
        => ToResponse(await _restaurantService.DeleteCategoryAsync(id));

    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));

        return StatusCode(result.StatusCode, ApiResponse<T>.Fail(
            result.Error ?? "An error occurred.", result.ValidationErrors));
    }
}
