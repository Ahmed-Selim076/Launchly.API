using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Settings.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Settings;

public class SettingsService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;
    private readonly ICloudinaryService _cloudinary;

    public SettingsService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog,
        ICloudinaryService cloudinary)
    {
        _db = db;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
        _cloudinary = cloudinary;
    }

    // ─── Get ──────────────────────────────────────────────────────────────────

    public async Task<Result<SettingsDto>> GetAsync()
    {
        if (_tenantContext.TenantId is null)
            return Result<SettingsDto>.Failure("Store context is required.");

        var settings = await _db.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId);

        if (settings is null)
            return Result<SettingsDto>.NotFound("Store settings not found.");

        return Result<SettingsDto>.Success(new SettingsDto(
            settings.TenantId,
            settings.StoreName,
            settings.LogoUrl,
            settings.PrimaryColor,
            settings.SecondaryColor,
            settings.HeroText,
            settings.AboutText,
            settings.GoogleAnalyticsId,
            settings.ContactPhone,
            settings.WhatsappNumber,
            settings.ContactEmail,
            settings.ContactAddress,
            settings.FacebookUrl,
            settings.InstagramUrl
        ));
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<Result<SettingsDto>> UpdateAsync(UpdateSettingsRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<SettingsDto>.Failure("Store context is required.");

        var settings = await _db.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId);

        if (settings is null)
            return Result<SettingsDto>.NotFound("Store settings not found.");

        settings.StoreName = request.StoreName.Trim();
        settings.PrimaryColor = request.PrimaryColor.Trim();
        settings.SecondaryColor = request.SecondaryColor.Trim();
        settings.HeroText = request.HeroText?.Trim();
        settings.AboutText = request.AboutText?.Trim();
        settings.GoogleAnalyticsId = request.GoogleAnalyticsId?.Trim();
        settings.ContactPhone = request.ContactPhone?.Trim();
        settings.WhatsappNumber = request.WhatsappNumber?.Trim();
        settings.ContactEmail = request.ContactEmail?.Trim();
        settings.ContactAddress = request.ContactAddress?.Trim();
        settings.FacebookUrl = request.FacebookUrl?.Trim();
        settings.InstagramUrl = request.InstagramUrl?.Trim();

        // Update tenant name to match store name
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId);

        if (tenant is not null)
            tenant.Name = request.StoreName.Trim();

        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Updated, "TenantSettings", settings.Id,
            $"Updated store settings for \"{settings.StoreName}\".");

        return Result<SettingsDto>.Success(new SettingsDto(
            settings.TenantId,
            settings.StoreName,
            settings.LogoUrl,
            settings.PrimaryColor,
            settings.SecondaryColor,
            settings.HeroText,
            settings.AboutText,
            settings.GoogleAnalyticsId,
            settings.ContactPhone,
            settings.WhatsappNumber,
            settings.ContactEmail,
            settings.ContactAddress,
            settings.FacebookUrl,
            settings.InstagramUrl
        ));
    }

    // ─── Update Logo ──────────────────────────────────────────────────────────

    public async Task<Result<SettingsDto>> UpdateLogoAsync(string logoUrl)
    {
        if (_tenantContext.TenantId is null)
            return Result<SettingsDto>.Failure("Store context is required.");

        // Validate the URL is an actual Cloudinary asset — not an arbitrary URL
        // someone injected into the request body.
        var newPublicId = _cloudinary.ExtractPublicId(logoUrl);
        if (newPublicId is null)
            return Result<SettingsDto>.Failure(
                "Invalid logo URL. The URL must be a Cloudinary asset returned by the upload API.");

        var settings = await _db.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId);

        if (settings is null)
            return Result<SettingsDto>.NotFound("Store settings not found.");

        // Delete the old logo from Cloudinary so we don't accumulate orphaned assets.
        // Fire-and-forget pattern: we don't await or care if this fails — CloudinaryService
        // already logs the error internally and swallows it.
        var oldPublicId = _cloudinary.ExtractPublicId(settings.LogoUrl);
        if (oldPublicId is not null && oldPublicId != newPublicId)
            _ = _cloudinary.DeleteAsync(oldPublicId);

        settings.LogoUrl = logoUrl.Trim();
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Updated, "TenantSettings", settings.Id,
            "Updated store logo.");

        return Result<SettingsDto>.Success(new SettingsDto(
            settings.TenantId,
            settings.StoreName,
            settings.LogoUrl,
            settings.PrimaryColor,
            settings.SecondaryColor,
            settings.HeroText,
            settings.AboutText,
            settings.GoogleAnalyticsId,
            settings.ContactPhone,
            settings.WhatsappNumber,
            settings.ContactEmail,
            settings.ContactAddress,
            settings.FacebookUrl,
            settings.InstagramUrl
        ));
    }
}
