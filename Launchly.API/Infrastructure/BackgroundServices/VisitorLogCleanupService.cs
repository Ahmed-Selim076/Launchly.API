using Microsoft.EntityFrameworkCore;
using Launchly.API.Infrastructure.Data;

namespace Launchly.API.Infrastructure.BackgroundServices;

public class VisitorLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitorLogCleanupService> _logger;

    // Run once every 24 hours
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public VisitorLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<VisitorLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VisitorLogCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-90);

            var deleted = await db.VisitorLogs
                .Where(v => v.VisitedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
                _logger.LogInformation(
                    "VisitorLogCleanup: deleted {Count} logs older than 90 days.", deleted);
        }
        catch (Exception ex)
        {
            // Never crash the host — just log and retry next cycle
            _logger.LogError(ex, "VisitorLogCleanup failed.");
        }
    }
}
