using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Launchly.API.Common;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Controllers.Admin;

/// <summary>
/// Issues short-lived Cloudinary signed-upload tokens so the Angular frontend
/// can upload images directly to Cloudinary without routing the binary through
/// this API server.
///
/// Flow:
///   1. Frontend calls POST /api/v1/admin/upload/sign?type=product  (or logo)
///   2. API returns { cloudName, apiKey, signature, timestamp, folder }
///   3. Frontend POSTs the file directly to Cloudinary using those params
///   4. Cloudinary returns a secure_url
///   5. Frontend saves that URL via PATCH /api/v1/admin/settings/logo
///      or PUT /api/v1/admin/products/{id}
/// </summary>
[ApiController]
[Route("api/v1/admin/upload")]
[Authorize(Policy = "TenantMember")]
public class UploadController : ControllerBase
{
    private readonly ICloudinaryService _cloudinary;
    private readonly ITenantContext _tenantContext;

    // Allowed upload types → Cloudinary folder mapping.
    // Keeping assets in per-tenant sub-folders makes bulk cleanup trivial
    // and prevents public_id collisions across tenants.
    private static readonly Dictionary<string, string> FolderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["product"] = "products",
        ["logo"]    = "logos",
        ["avatar"]  = "avatars",
        ["service"] = "services",
    };

    // "product" and "logo" are store-management assets — only the tenant
    // admin should be able to write those. "avatar" is a user's own profile
    // photo, so a Customer needs it too (see StorefrontAccountComponent).
    // The controller-level policy was widened to TenantMember to let
    // customers in at all; this set claws back the merchant-only types.
    private static readonly HashSet<string> AdminOnlyTypes =
        new(StringComparer.OrdinalIgnoreCase) { "product", "logo", "service" };

    public UploadController(ICloudinaryService cloudinary, ITenantContext tenantContext)
    {
        _cloudinary = cloudinary;
        _tenantContext = tenantContext;
    }

    // ─── Sign ─────────────────────────────────────────────────────────────────
    // POST /api/v1/admin/upload/sign?type=product
    // POST /api/v1/admin/upload/sign?type=logo
    // POST /api/v1/admin/upload/sign?type=avatar

    [HttpPost("sign")]
    public IActionResult Sign([FromQuery] string type)
    {
        if (!FolderMap.TryGetValue(type, out var subFolder))
            return BadRequest(ApiResponse<object>.Fail(
                $"Unknown upload type \"{type}\". Allowed: {string.Join(", ", FolderMap.Keys)}."));

        if (AdminOnlyTypes.Contains(type) && !User.IsInRole("TenantAdmin"))
            return Forbid();

        if (_tenantContext.TenantId is null)
            return Unauthorized(ApiResponse<object>.Fail("Store context is required."));

        // Scope assets under the tenant's own folder so they're isolated in
        // Cloudinary just as they are in the database.
        var folder = $"launchly/{_tenantContext.TenantId}/{subFolder}";
        var signed = _cloudinary.GenerateSignedUploadParams(folder);

        return Ok(ApiResponse<SignedUploadParams>.Ok(signed));
    }
}
