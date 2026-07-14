using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Customers;
using Launchly.API.Common;

namespace Launchly.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Policy = "TenantAdmin")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _customerService.GetCustomersAsync(page, pageSize);
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
