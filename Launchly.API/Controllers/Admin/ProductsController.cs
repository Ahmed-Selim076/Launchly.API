using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Products;
using Launchly.API.Application.Products.DTOs;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Policy = "TenantAdmin")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ProductsQuery query)
    {
        var result = await _productService.GetAllAsync(query);
        return ToResponse(result);
    }

    // ─── Get By Id ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productService.GetByIdAsync(id);
        return ToResponse(result);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateAsync(request);
        return ToResponse(result);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var result = await _productService.UpdateAsync(id, request);
        return ToResponse(result);
    }

    // ─── Delete (Soft) ────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _productService.DeleteAsync(id);
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
