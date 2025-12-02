using System.Globalization;
using Loto.Api.Contracts;
using Loto.Domain;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Loto.Api.Services;

public interface IStrategyBacktestService
{
    Task<StrategyBacktestResultDto> BacktestAsync(StrategyBacktestRequest request, CancellationToken ct);
}

public sealed class StrategyBacktestService : IStrategyBacktestService
{
    private readonly LotoDbContext _dbContext;
    private readonly ILotoPredictionService _lotoPredictionService;
    private readonly ILogger<StrategyBacktestService> _logger;

    public StrategyBacktestService(
        LotoDbContext dbContext,
        ILotoPredictionService lotoPredictionService,
        ILogger<StrategyBacktestService> logger)
    {
        _dbContext = dbContext;
        _lotoPredictionService = lotoPredictionService;
        _logger = logger;
    }

    public async Task<StrategyBacktestResultDto> BacktestAsync(StrategyBacktestRequest request, CancellationToken ct)
    {
        var from = ParseDate(request.DateFrom, nameof(request.DateFrom));
        var to = ParseDate(request.DateTo, nameof(request.DateTo));

        if (from.HasValue && to.HasValue && from > to)
        {
            throw new ArgumentException("dateFrom ne peut pas être postérieure à dateTo.");
        }

        var allDraws = await _dbContext.Draws
            .AsNoTracking()
            .OrderBy(d => d.DrawDate)
            .ToListAsync(ct);

        var drawsToAnalyze = allDraws.AsEnumerable();

        if (from.HasValue)
        {
            drawsToAnalyze = drawsToAnalyze.Where(d => d.DrawDate >= from.Value);
        }

        if (to.HasValue)
        {
            drawsToAnalyze = drawsToAnalyze.Where(d => d.DrawDate <= to.Value);
        }

        var drawsList = drawsToAnalyze.ToList();

        if (drawsList.Count == 0)
        {
            return new StrategyBacktestResultDto
            {
                Strategy = request.Strategy,
                From = from,
                To = to,
                TotalDrawsAnalyzed = 0,
                AverageMatchedMain = 0,
                Distributions = Array.Empty<MatchDistributionDto>()
            };
        }

        if (request.SampleSize is int sampleSize &&
            sampleSize > 0 &&
            sampleSize < drawsList.Count)
        {
            var sampler = new Random(1337);
            drawsList = drawsList
                .OrderBy(_ => sampler.Next())
                .Take(sampleSize)
                .ToList();
        }

        var distribution = new Dictionary<(int matchedMain, bool matchedLucky), int>();
        var matchedMainSum = 0;

        foreach (var draw in drawsList)
        {
            var seed = ComputeSeed(draw, request.Strategy);
            var rng = new Random(seed);

            var predictionResponse = await _lotoPredictionService.GeneratePredictionsAsync(
                1,
                request.Strategy,
                null,
                ct,
                rng,
                allDraws);

            var predicted = predictionResponse.Draws.FirstOrDefault();
            if (predicted is null)
            {
                _logger.LogWarning("Aucune prédiction générée pour le tirage {DrawId}", draw.Id);
                continue;
            }

            var matchedMain = CountMatchedMain(draw, predicted);
            var matchedLucky = predicted.LuckyNumber == draw.LuckyNumber;

            distribution[(matchedMain, matchedLucky)] = distribution.TryGetValue((matchedMain, matchedLucky), out var current)
                ? current + 1
                : 1;

            matchedMainSum += matchedMain;
        }

        var totalAnalyzed = drawsList.Count;

        var distributions = distribution
            .Select(kvp => new MatchDistributionDto
            {
                MatchedMain = kvp.Key.matchedMain,
                MatchedLucky = kvp.Key.matchedLucky,
                Count = kvp.Value
            })
            .OrderByDescending(d => d.MatchedMain)
            .ThenByDescending(d => d.MatchedLucky)
            .ToList();

        return new StrategyBacktestResultDto
        {
            Strategy = request.Strategy,
            From = from,
            To = to,
            TotalDrawsAnalyzed = totalAnalyzed,
            AverageMatchedMain = totalAnalyzed > 0 ? (double)matchedMainSum / totalAnalyzed : 0,
            Distributions = distributions
        };
    }

    private static DateTime? ParseDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Format de {fieldName} invalide. Utilisez yyyy-MM-dd.");
    }

    private static int ComputeSeed(Draw draw, PredictionStrategy strategy)
    {
        var seed = HashCode.Combine(
            draw.Id,
            draw.DrawDate.Date.GetHashCode(),
            draw.Number1,
            draw.Number2,
            draw.Number3);

        seed = HashCode.Combine(
            seed,
            draw.Number4,
            draw.Number5,
            draw.LuckyNumber,
            (int)strategy);

        return Math.Abs(seed) + 1;
    }

    private static int CountMatchedMain(Draw draw, PredictedDrawDto predicted)
    {
        var predictedSet = new HashSet<int>(predicted.Numbers);

        var matches = 0;
        if (predictedSet.Contains(draw.Number1)) matches++;
        if (predictedSet.Contains(draw.Number2)) matches++;
        if (predictedSet.Contains(draw.Number3)) matches++;
        if (predictedSet.Contains(draw.Number4)) matches++;
        if (predictedSet.Contains(draw.Number5)) matches++;

        return matches;
    }
}
