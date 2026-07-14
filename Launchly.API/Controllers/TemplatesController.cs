using Microsoft.AspNetCore.Mvc;
using Launchly.API.Application.Tenants;
using Launchly.API.Common;
using Launchly.API.Core.Enums;

namespace Launchly.API.Controllers;

[ApiController]
[Route("api/v1/templates")]
public class TemplatesController : ControllerBase
{
    private readonly TemplateService _templateService;

    public TemplatesController(TemplateService templateService)
    {
        _templateService = templateService;
    }

    // ─── List Templates for a Store Type ─────────────────────────────────────
    // Public — used at signup (Step 2.5, before an account exists) and
    // reused by Settings if/when template-switching is enabled later.
    //
    // storeType binds as an int, not StoreType directly: ASP.NET Core's
    // default enum model binding accepts any integer for a non-[Flags] enum
    // (e.g. ?storeType=99 binds successfully to an undefined enum value
    // rather than failing model state) — see
    // https://github.com/dotnet/runtime/issues/42093. Validating explicitly
    // here avoids silently treating an out-of-range value as if it parsed.

    [HttpGet]
    public IActionResult GetTemplates([FromQuery] int storeType)
    {
        if (!Enum.IsDefined(typeof(StoreType), storeType))
            return BadRequest(ApiResponse<object>.Fail("Invalid store type."));

        var result = _templateService.GetTemplatesForStoreType((StoreType)storeType);
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
