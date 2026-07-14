using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Launchly.API.Infrastructure.Services;

public interface ICloudinaryService
{
    SignedUploadParams GenerateSignedUploadParams(string folder);
    Task DeleteAsync(string publicId);
    string? ExtractPublicId(string? url);
}

public record SignedUploadParams(
    string CloudName,
    string ApiKey,
    string Signature,
    long Timestamp,
    string Folder,
    string UploadPreset
);

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary? _cloudinary;
    private readonly string? _cloudName;
    private readonly string? _apiKey;
    private readonly string? _apiSecret;
    private readonly ILogger<CloudinaryService> _logger;
    private readonly bool _configured;

    private const int SignatureValidSeconds = 600;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
        _logger = logger;

        _cloudName = config["CLOUDINARY_CLOUD_NAME"];
        _apiKey    = config["CLOUDINARY_API_KEY"];
        _apiSecret = config["CLOUDINARY_API_SECRET"];

        // If any credential is missing, run in degraded mode.
        // Upload/delete calls will return an error instead of crashing the app.
        _configured = !string.IsNullOrWhiteSpace(_cloudName)
                   && !string.IsNullOrWhiteSpace(_apiKey)
                   && !string.IsNullOrWhiteSpace(_apiSecret);

        if (_configured)
        {
            var account = new Account(_cloudName, _apiKey, _apiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }
        else
        {
            _logger.LogWarning(
                "Cloudinary credentials are not configured. " +
                "Logo upload will be unavailable until CLOUDINARY_CLOUD_NAME, " +
                "CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET are set.");
        }
    }

    public SignedUploadParams GenerateSignedUploadParams(string folder)
    {
        if (!_configured || _cloudinary is null)
            throw new InvalidOperationException(
                "Cloudinary is not configured. Please set CLOUDINARY_CLOUD_NAME, " +
                "CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var paramsToSign = new SortedDictionary<string, object>
        {
            ["folder"]    = folder,
            ["timestamp"] = timestamp,
        };

        var signature = _cloudinary.Api.SignParameters(paramsToSign);

        return new SignedUploadParams(
            CloudName:    _cloudName!,
            ApiKey:       _apiKey!,
            Signature:    signature,
            Timestamp:    timestamp,
            Folder:       folder,
            UploadPreset: string.Empty
        );
    }

    public async Task DeleteAsync(string publicId)
    {
        if (!_configured || _cloudinary is null || string.IsNullOrWhiteSpace(publicId))
            return;

        try
        {
            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));

            if (result.Result != "ok")
                _logger.LogWarning(
                    "Cloudinary delete returned non-ok result for public_id {PublicId}: {Result}",
                    publicId, result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete Cloudinary asset with public_id {PublicId}.", publicId);
        }
    }

    public string? ExtractPublicId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var uri = new Uri(url);

            if (!uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
                return null;

            var path = uri.AbsolutePath;
            const string marker = "/upload/";
            var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (idx < 0) return null;

            var afterUpload = path[(idx + marker.Length)..];

            if (afterUpload.StartsWith('v') && afterUpload.Contains('/'))
            {
                var slash = afterUpload.IndexOf('/');
                if (int.TryParse(afterUpload[1..slash], out _))
                    afterUpload = afterUpload[(slash + 1)..];
            }

            var dot = afterUpload.LastIndexOf('.');
            return dot > 0 ? afterUpload[..dot] : afterUpload;
        }
        catch
        {
            return null;
        }
    }
}