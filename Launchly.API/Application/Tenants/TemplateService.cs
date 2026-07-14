using Launchly.API.Application.Tenants.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;

namespace Launchly.API.Application.Tenants;

/// <summary>
/// Returns the available layout templates for a given StoreType.
///
/// Templates are a fixed, hardcoded set (3 per StoreType) rather than a
/// database table — see BACKEND_PLAN.md Section 17.2 for why: with no
/// tenant-facing template management and a fixed count, a DB table would
/// be normalization with no behavior behind it. Revisit only if templates
/// become data-driven (e.g. a SuperAdmin UI for adding a 4th template).
///
/// Thumbnail URLs point at static frontend assets (previews of the
/// template itself), not Cloudinary — they ship with the frontend build,
/// not tenant-uploaded content.
/// </summary>
public class TemplateService
{
    private static readonly Dictionary<StoreType, TemplateOptionDto[]> TemplatesByStoreType = new()
    {
        [StoreType.Ecommerce] = new[]
        {
            new TemplateOptionDto(1, "Template 1", "/assets/templates/ecommerce/minimal.png"),
            new TemplateOptionDto(2, "Template 2", "/assets/templates/ecommerce/bold.png"),
            new TemplateOptionDto(3, "Template 3", "/assets/templates/ecommerce/editorial.png"),
        },
        [StoreType.Booking] = new[]
        {
            new TemplateOptionDto(1, "Template 1", "/assets/templates/booking/minimal.png"),
            new TemplateOptionDto(2, "Template 2", "/assets/templates/booking/bold.png"),
            new TemplateOptionDto(3, "Template 3", "/assets/templates/booking/editorial.png"),
        },
        [StoreType.Restaurant] = new[]
        {
            new TemplateOptionDto(1, "Template 1", "/assets/templates/restaurant/minimal.png"),
            new TemplateOptionDto(2, "Template 2", "/assets/templates/restaurant/bold.png"),
            new TemplateOptionDto(3, "Template 3", "/assets/templates/restaurant/editorial.png"),
        },
    };

    public Result<IReadOnlyList<TemplateOptionDto>> GetTemplatesForStoreType(StoreType storeType)
    {
        if (!TemplatesByStoreType.TryGetValue(storeType, out var templates))
            return Result<IReadOnlyList<TemplateOptionDto>>.Failure("Unknown store type.");

        return Result<IReadOnlyList<TemplateOptionDto>>.Success(templates);
    }

    /// <summary>
    /// Used by registration validation — every StoreType currently offers
    /// exactly 3 templates (ids 1-3), but this stays storeType-aware rather
    /// than a flat "1 to 3" constant so a future StoreType with a different
    /// count doesn't silently validate against the wrong range.
    /// </summary>
    public bool IsValidTemplateId(StoreType storeType, int templateId) =>
        TemplatesByStoreType.TryGetValue(storeType, out var templates) &&
        templates.Any(t => t.TemplateId == templateId);
}
