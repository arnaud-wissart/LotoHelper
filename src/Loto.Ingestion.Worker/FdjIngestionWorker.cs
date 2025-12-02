using System.Diagnostics;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Loto.Ingestion.Worker;

public class FdjIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FdjIngestionWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _minRefreshAge;

    public FdjIngestionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<FdjIngestionWorker> logger,
        IOptions<FdjOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = options.Value.IntervalMinutes;
        _interval = TimeSpan.FromMinutes(intervalMinutes);
        _minRefreshAge = TimeSpan.FromMinutes(options.Value.MinRefreshAgeMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseReadyAsync(stoppingToken);

        // Initial ingestion on startup
        await RunIngestionAsync("initial", stoppingToken);

        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunIngestionAsync("scheduled", stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task RunIngestionAsync(string reason, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("FDJ ingestion ({Reason}) started at {Time}", reason, DateTimeOffset.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IFdjIngestionService>();

            // Ensure freshness based on min age, then run ingestion.
            await ingestionService.EnsureFreshDataAsync(_minRefreshAge, stoppingToken);
            var result = await ingestionService.RunFullOrIncrementalAsync(stoppingToken);
            _logger.LogInformation(
                "FDJ ingestion ({Reason}) finished in {Duration}s: total {Total}, inserted {Inserted}, skipped {Skipped}. Full={Full}, Incremental={Incremental}",
                reason,
                stopwatch.Elapsed.TotalSeconds.ToString("F2"),
                result.TotalRead,
                result.Inserted,
                result.Skipped,
                result.UsedFullHistory,
                result.UsedIncremental);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FDJ ingestion ({Reason}) failed after {Duration}s", reason, stopwatch.Elapsed.TotalSeconds.ToString("F2"));
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task EnsureDatabaseReadyAsync(CancellationToken stoppingToken)
    {
        var retries = 5;
        var delay = TimeSpan.FromSeconds(2);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LotoDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(stoppingToken);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await dbContext.Database.MigrateAsync(stoppingToken);
                    _logger.LogInformation("Database migration/creation ensured.");
                    break;
                }
                catch (Exception ex) when (attempt <= retries)
                {
                    _logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying in {Delay}s", attempt, delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database is ready before ingestion.");
            throw;
        }
    }
}
