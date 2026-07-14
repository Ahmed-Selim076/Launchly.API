namespace Launchly.API.Application.Tenants.DTOs;

public record TemplateOptionDto(
    int TemplateId,
    string Name,
    string ThumbnailUrl
);
