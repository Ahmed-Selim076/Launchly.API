namespace Launchly.API.Application.Settings.DTOs;

// ─── Requests ─────────────────────────────────────────────────────────────────

public record UpdateSettingsRequest(
    string StoreName,
    string PrimaryColor,
    string SecondaryColor,
    string? HeroText,
    string? AboutText,
    string? GoogleAnalyticsId,
    string? ContactPhone,
    string? WhatsappNumber,
    string? ContactEmail,
    string? ContactAddress,
    string? FacebookUrl,
    string? InstagramUrl
);

// ─── Responses ────────────────────────────────────────────────────────────────

public record SettingsDto(
    Guid TenantId,
    string StoreName,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? HeroText,
    string? AboutText,
    string? GoogleAnalyticsId,
    string? ContactPhone,
    string? WhatsappNumber,
    string? ContactEmail,
    string? ContactAddress,
    string? FacebookUrl,
    string? InstagramUrl
);
