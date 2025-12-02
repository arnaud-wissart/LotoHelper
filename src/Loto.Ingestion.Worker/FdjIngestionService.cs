using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Loto.Domain;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Loto.Ingestion.Worker;

public interface IFdjIngestionService
{
    Task<FdjIngestionResult> RunFullOrIncrementalAsync(CancellationToken cancellationToken);
    Task<FdjIngestionResult?> EnsureFreshDataAsync(TimeSpan minAge, CancellationToken cancellationToken);
}

public class FdjIngestionService : IFdjIngestionService
{
    public const string ActivitySourceName = "Loto.Ingestion";
    public const string MeterName = "Loto.Ingestion";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> DrawsIngestedCounter = Meter.CreateCounter<long>("draws_ingested_total");
    private static readonly Counter<long> IngestionFailuresCounter = Meter.CreateCounter<long>("ingestion_failures_total");
    private static readonly Histogram<double> IngestionDurationSeconds = Meter.CreateHistogram<double>("ingestion_duration_seconds");

    private readonly IDbContextFactory<LotoDbContext> _dbContextFactory;
    private readonly IFdjDataSource _dataSource;
    private readonly IFdjParser _parser;
    private readonly ILogger<FdjIngestionService> _logger;
    private DateTimeOffset? _lastSuccessUtc;
    private readonly object _lock = new();

    public FdjIngestionService(
        IDbContextFactory<LotoDbContext> dbContextFactory,
        IFdjDataSource dataSource,
        IFdjParser parser,
        ILogger<FdjIngestionService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _dataSource = dataSource;
        _parser = parser;
        _logger = logger;
    }

    public async Task<FdjIngestionResult> RunFullOrIncrementalAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = ActivitySource.StartActivity("FdjIngestion", ActivityKind.Internal);

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var hasExistingDraws = await dbContext.Draws.AnyAsync(cancellationToken);

            var parsedDraws = new List<Draw>();

            await using var archiveStream = await _dataSource.GetNewLotoArchiveAsync(cancellationToken);
            await foreach (var draw in _parser.ParseNewLotoArchiveAsync(archiveStream, cancellationToken))
            {
                parsedDraws.Add(draw);
            }

            var parsedCount = parsedDraws.Count;

            var existingSet = new HashSet<string>(
                await dbContext.Draws
                    .AsNoTracking()
                    .Select(d => BuildKey(d.OfficialDrawId, d.DrawDate, d.Number1, d.Number2, d.Number3, d.Number4, d.Number5, d.LuckyNumber))
                    .ToListAsync(cancellationToken),
                StringComparer.Ordinal);

            var toInsert = parsedDraws
                .Where(draw => existingSet.Add(BuildKey(draw.OfficialDrawId, draw.DrawDate, draw.Number1, draw.Number2, draw.Number3, draw.Number4, draw.Number5, draw.LuckyNumber)))
                .ToList();

            if (toInsert.Count > 0)
            {
                await dbContext.Draws.AddRangeAsync(toInsert, cancellationToken);
            }

            var inserted = await dbContext.SaveChangesAsync(cancellationToken);
            var skipped = parsedCount - inserted;

            var usedFull = !hasExistingDraws;
            var usedIncremental = hasExistingDraws;

            DrawsIngestedCounter.Add(inserted);
            IngestionDurationSeconds.Record(stopwatch.Elapsed.TotalSeconds);

            activity?.SetTag("ingestion.total", parsedCount);
            activity?.SetTag("ingestion.inserted", inserted);
            activity?.SetTag("ingestion.skipped", skipped);
            activity?.SetTag("ingestion.used_full_history", usedFull);
            activity?.SetTag("ingestion.used_incremental", usedIncremental);

            _logger.LogInformation("FDJ ingestion completed: total {Total}, inserted {Inserted}, skipped {Skipped}. Full={Full}, Incremental={Incremental}",
                parsedCount, inserted, skipped, usedFull, usedIncremental);

            lock (_lock)
            {
                _lastSuccessUtc = DateTimeOffset.UtcNow;
            }

            return new FdjIngestionResult(parsedCount, inserted, skipped, usedFull, usedIncremental);
        }
        catch (Exception ex)
        {
            IngestionFailuresCounter.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError(ex, "FDJ ingestion failed");
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<FdjIngestionResult?> EnsureFreshDataAsync(TimeSpan minAge, CancellationToken cancellationToken)
    {
        DateTimeOffset? last;
        lock (_lock)
        {
            last = _lastSuccessUtc;
        }

        if (last is not null && (DateTimeOffset.UtcNow - last.Value) <= minAge)
        {
            _logger.LogInformation("Skipping ingestion because last success {LastSuccess} is within min age {MinAge}.", last, minAge);
            return null;
        }

        _logger.LogInformation("EnsureFreshData triggered. Last success: {LastSuccess}", last);
        return await RunFullOrIncrementalAsync(cancellationToken);
    }

    private static string BuildKey(string? officialDrawId, DateTime drawDate, int n1, int n2, int n3, int n4, int n5, int luckyNumber) =>
        !string.IsNullOrWhiteSpace(officialDrawId)
            ? FormattableString.Invariant($"official:{officialDrawId}")
            : FormattableString.Invariant($"{drawDate:O}|{n1}|{n2}|{n3}|{n4}|{n5}|{luckyNumber}");
}
