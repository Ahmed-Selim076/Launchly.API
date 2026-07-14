using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Launchly.API.Infrastructure.Services;

namespace Launchly.Tests;

public class CloudinaryServiceTests
{
    private static ICloudinaryService MakeService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CLOUDINARY_CLOUD_NAME"] = "testcloud",
                ["CLOUDINARY_API_KEY"]    = "123456789012345",
                ["CLOUDINARY_API_SECRET"] = "test_secret_abcdefghijklmnop",
            })
            .Build();

        var logger = Substitute.For<ILogger<CloudinaryService>>();
        return new CloudinaryService(config, logger);
    }

    // ─── ExtractPublicId ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(
        "https://res.cloudinary.com/mycloud/image/upload/v1234567890/launchly/logos/abc123.jpg",
        "launchly/logos/abc123")]
    [InlineData(
        "https://res.cloudinary.com/mycloud/image/upload/launchly/products/xyz.png",
        "launchly/products/xyz")]
    [InlineData(
        "https://res.cloudinary.com/mycloud/image/upload/v9999/folder/sub/image.webp",
        "folder/sub/image")]
    public void ExtractPublicId_ParsesCloudinaryUrls_Correctly(string url, string expected)
    {
        var svc = MakeService();
        svc.ExtractPublicId(url).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/image.jpg")]
    [InlineData("https://s3.amazonaws.com/bucket/image.jpg")]
    [InlineData("not-a-url")]
    public void ExtractPublicId_ReturnsNull_ForNonCloudinaryUrls(string? url)
    {
        var svc = MakeService();
        svc.ExtractPublicId(url).Should().BeNull();
    }

    // ─── GenerateSignedUploadParams ───────────────────────────────────────────

    [Fact]
    public void GenerateSignedUploadParams_ReturnsAllRequiredFields()
    {
        var svc = MakeService();
        var result = svc.GenerateSignedUploadParams("launchly/tenant-id/logos");

        result.CloudName.Should().Be("testcloud");
        result.ApiKey.Should().NotBeNullOrWhiteSpace();
        result.Signature.Should().NotBeNullOrWhiteSpace(
            because: "frontend needs this to authenticate the upload");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Folder.Should().Be("launchly/tenant-id/logos");
    }

    [Fact]
    public void GenerateSignedUploadParams_Timestamp_IsRecent()
    {
        var svc = MakeService();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = svc.GenerateSignedUploadParams("test/folder");
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        result.Timestamp.Should().BeInRange(before, after,
            because: "a stale timestamp will cause Cloudinary to reject the upload");
    }
}
