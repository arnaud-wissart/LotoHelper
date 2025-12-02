using Loto.Api.Contracts;
using Loto.Domain;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Loto.Api.Services;

public interface IStatsService
{
    Task<StatsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
    Task<StatsFrequenciesDto> GetFrequenciesAsync(CancellationToken cancellationToken);
    Task<PatternDistributionDto> GetPatternsAsync(int bucketSize, CancellationToken cancellationToken);
    Task<CooccurrenceStatsDto> GetCooccurrenceAsync(int baseNumber, int? top, CancellationToken cancellationToken);
}

public sealed class StatsService : IStatsService
{
    private const int MainNumberMax = 49;
    private readonly LotoDbContext _dbContext;
    private readonly ILogger<StatsService> _logger;

    public StatsService(LotoDbContext dbContext, ILogger<StatsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<StatsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var totalDraws = await _dbContext.Draws.CountAsync(cancellationToken);
        if (totalDraws == 0)
        {
            _logger.LogInformation("Stats overview requested but no draws are available yet.");
            return new StatsOverviewDto();
        }

        _logger.LogInformation("Stats overview requested. Total draws in store: {Count}", totalDraws);

        var firstDrawDate = await _dbContext.Draws.MinAsync(d => d.DrawDate, cancellationToken);
        var lastDrawDate = await _dbContext.Draws.MaxAsync(d => d.DrawDate, cancellationToken);

        var dayCounts = await _dbContext.Draws
            .AsNoTracking()
            .GroupBy(d => d.DrawDayName)
            .Select(g => new DayOfWeekCountDto
            {
                DayName = g.Key ?? "INCONNU",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        return new StatsOverviewDto
        {
            TotalDraws = totalDraws,
            FirstDrawDate = firstDrawDate,
            LastDrawDate = lastDrawDate,
            DrawsPerDayOfWeek = dayCounts
        };
    }

    public async Task<StatsFrequenciesDto> GetFrequenciesAsync(CancellationToken cancellationToken)
    {
        var draws = await _dbContext.Draws.AsNoTracking().ToListAsync(cancellationToken);
        if (draws.Count == 0)
        {
            _logger.LogInformation("Stats frequencies requested but no draws are available yet.");
            return new StatsFrequenciesDto();
        }

        var mainFreq = new int[MainNumberMax + 1];
        var luckyFreq = new int[11];

        foreach (var d in draws)
        {
            mainFreq[d.Number1]++;
            mainFreq[d.Number2]++;
            mainFreq[d.Number3]++;
            mainFreq[d.Number4]++;
            mainFreq[d.Number5]++;
            luckyFreq[d.LuckyNumber]++;
        }

        var totalMain = mainFreq.Sum();
        var totalLucky = luckyFreq.Sum();

        var mainList = Enumerable.Range(1, MainNumberMax)
            .Select(n => new NumberFrequencyDto
            {
                Number = n,
                Count = mainFreq[n],
                Frequency = totalMain > 0 ? (double)mainFreq[n] / totalMain : 0
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var luckyList = Enumerable.Range(1, 10)
            .Select(n => new NumberFrequencyDto
            {
                Number = n,
                Count = luckyFreq[n],
                Frequency = totalLucky > 0 ? (double)luckyFreq[n] / totalLucky : 0
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new StatsFrequenciesDto
        {
            MainNumbers = mainList,
            LuckyNumbers = luckyList
        };
    }

    public async Task<PatternDistributionDto> GetPatternsAsync(int bucketSize, CancellationToken cancellationToken)
    {
        var draws = await _dbContext.Draws.AsNoTracking().ToListAsync(cancellationToken);
        if (draws.Count == 0)
        {
            _logger.LogInformation("Stats patterns requested but no draws are available yet.");
            return new PatternDistributionDto();
        }

        var sums = draws.Select(d => d.Number1 + d.Number2 + d.Number3 + d.Number4 + d.Number5).ToList();
        var minSum = sums.Min();
        var maxSum = sums.Max();

        var size = bucketSize <= 0 ? 10 : bucketSize;

        var buckets = new List<SumBucketDto>();
        for (var start = (minSum / size) * size; start <= maxSum; start += size)
        {
            var end = start + size - 1;
            var count = sums.Count(s => s >= start && s <= end);
            buckets.Add(new SumBucketDto
            {
                MinInclusive = start,
                MaxInclusive = end,
                Count = count
            });
        }

        var evenCountDist = new Dictionary<int, int>();
        var lowCountDist = new Dictionary<int, int>();

        foreach (var d in draws)
        {
            var numbers = new[] { d.Number1, d.Number2, d.Number3, d.Number4, d.Number5 };

            var evenCount = numbers.Count(n => n % 2 == 0);
            var lowCount = numbers.Count(n => n <= 25);

            evenCountDist[evenCount] = evenCountDist.TryGetValue(evenCount, out var evc) ? evc + 1 : 1;
            lowCountDist[lowCount] = lowCountDist.TryGetValue(lowCount, out var lc) ? lc + 1 : 1;
        }

        return new PatternDistributionDto
        {
            SumBuckets = buckets,
            EvenCountDistribution = evenCountDist,
            LowCountDistribution = lowCountDist
        };
    }

    public async Task<CooccurrenceStatsDto> GetCooccurrenceAsync(int baseNumber, int? top, CancellationToken cancellationToken)
    {
        if (baseNumber is < 1 or > MainNumberMax)
        {
            throw new ArgumentException("baseNumber must be between 1 and 49.");
        }

        var draws = await _dbContext.Draws.AsNoTracking().ToListAsync(cancellationToken);
        var totalDraws = draws.Count;
        if (totalDraws == 0)
        {
            _logger.LogInformation("Cooccurrence stats requested for {BaseNumber} but no draws are available yet.", baseNumber);
            return new CooccurrenceStatsDto
            {
                BaseNumber = baseNumber
            };
        }

        var drawsWithBase = draws
            .Where(d =>
                d.Number1 == baseNumber ||
                d.Number2 == baseNumber ||
                d.Number3 == baseNumber ||
                d.Number4 == baseNumber ||
                d.Number5 == baseNumber)
            .ToList();

        var drawsContainingBaseCount = drawsWithBase.Count;

        var globalCount = new int[MainNumberMax + 1];
        foreach (var d in draws)
        {
            globalCount[d.Number1]++;
            globalCount[d.Number2]++;
            globalCount[d.Number3]++;
            globalCount[d.Number4]++;
            globalCount[d.Number5]++;
        }

        var coCounts = new int[MainNumberMax + 1];

        foreach (var d in drawsWithBase)
        {
            var nums = new[] { d.Number1, d.Number2, d.Number3, d.Number4, d.Number5 };
            foreach (var n in nums)
            {
                if (n == baseNumber) continue;
                coCounts[n]++;
            }
        }

        var coList = Enumerable.Range(1, MainNumberMax)
            .Where(n => n != baseNumber && coCounts[n] > 0)
            .Select(n => new CooccurringNumberDto
            {
                Number = n,
                CooccurrenceCount = coCounts[n],
                ConditionalProbability = drawsContainingBaseCount > 0
                    ? (double)coCounts[n] / drawsContainingBaseCount
                    : 0,
                GlobalProbability = totalDraws > 0
                    ? (double)globalCount[n] / totalDraws
                    : 0
            })
            .OrderByDescending(x => x.CooccurrenceCount)
            .ToList();

        var limit = top.GetValueOrDefault(15);
        if (limit > 0 && coList.Count > limit)
        {
            coList = coList.Take(limit).ToList();
        }

        return new CooccurrenceStatsDto
        {
            BaseNumber = baseNumber,
            TotalDraws = totalDraws,
            DrawsContainingBase = drawsContainingBaseCount,
            Cooccurrences = coList
        };
    }
}
