using System.Diagnostics;
using System.Diagnostics.Metrics;
using Loto.Api.Contracts;
using Loto.Domain;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Loto.Api.Services;

public sealed class LotoPredictionService : ILotoPredictionService
{
    private const int MainNumbersMax = 49;
    private const int LuckyNumbersMax = 10;
    private const double ColdEpsilon = 1e-3;

    public const string MeterName = "Loto.Api.Predictions";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> PredictionRequestsCounter = Meter.CreateCounter<long>("loto_predictions_requests_total");
    private static readonly Histogram<double> PredictionDurationHistogram = Meter.CreateHistogram<double>("loto_predictions_duration_ms");

    private readonly LotoDbContext _dbContext;
    private readonly ILogger<LotoPredictionService> _logger;
    private readonly IOptionsMonitor<PredictionOptions> _options;
    private readonly Random _random = Random.Shared;

    public LotoPredictionService(
        LotoDbContext dbContext,
        ILogger<LotoPredictionService> logger,
        IOptionsMonitor<PredictionOptions> options)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options;
    }

    public async Task<PredictionsResponse> GeneratePredictionsAsync(
        int count,
        PredictionStrategy strategy,
        PredictionConstraints? constraints,
        CancellationToken cancellationToken,
        Random? random = null,
        IReadOnlyList<Draw>? preloadedDraws = null)
    {
        var options = _options.CurrentValue;
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var randomToUse = random ?? _random;
        try
        {
            var draws = preloadedDraws ?? await _dbContext.Draws.AsNoTracking().ToListAsync(cancellationToken);
            var recentWindow = TimeSpan.FromDays(options.RecentWindowDays);

            var predictions = strategy switch
            {
                PredictionStrategy.Uniform => GeneratePredictionsWithWeights(
                    BuildUniformWeights(MainNumbersMax),
                    BuildUniformWeights(LuckyNumbersMax),
                    count,
                    randomToUse,
                    constraints,
                    options),

                PredictionStrategy.FrequencyGlobal => GenerateFrequencyBasedPredictions(draws, count, randomToUse, constraints, options),

                PredictionStrategy.FrequencyRecent => GenerateFrequencyBasedPredictions(
                    draws.Where(d => d.DrawDate >= DateTime.UtcNow.Add(-recentWindow)),
                    count,
                    randomToUse,
                    constraints,
                    options,
                    draws),

                PredictionStrategy.Cold => GenerateColdPredictions(draws, count, randomToUse, constraints, options),

                PredictionStrategy.Cooccurrence => GenerateCooccurrencePredictions(draws, count, randomToUse, constraints, options),

                _ => GeneratePredictionsWithWeights(
                    BuildUniformWeights(MainNumbersMax),
                    BuildUniformWeights(LuckyNumbersMax),
                    count,
                    randomToUse,
                    constraints,
                    options)
            };

            success = true;

            return new PredictionsResponse
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Count = predictions.Count,
                Draws = predictions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while generating predictions with strategy {Strategy}", strategy);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            TrackMetric(strategy, success, stopwatch.Elapsed);
        }
    }

    private void TrackMetric(PredictionStrategy strategy, bool success, TimeSpan duration)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("strategy", GetStrategyTag(strategy)),
            new KeyValuePair<string, object?>("success", success ? "true" : "false")
        };

        PredictionRequestsCounter.Add(1, tags);
        PredictionDurationHistogram.Record(duration.TotalMilliseconds, tags);
    }

    private static string GetStrategyTag(PredictionStrategy strategy) => strategy switch
    {
        PredictionStrategy.Uniform => "uniform",
        PredictionStrategy.FrequencyGlobal => "frequency-global",
        PredictionStrategy.FrequencyRecent => "frequency-recent",
        PredictionStrategy.Cold => "cold",
        PredictionStrategy.Cooccurrence => "cooccurrence",
        _ => "unknown"
    };

    private List<PredictedDrawDto> GenerateFrequencyBasedPredictions(
        IEnumerable<Draw> draws,
        int count,
        Random random,
        PredictionConstraints? constraints,
        PredictionOptions options,
        IEnumerable<Draw>? fallbackDraws = null)
    {
        var selectedDraws = draws.ToList();
        if (!selectedDraws.Any() && fallbackDraws is not null)
        {
            selectedDraws = fallbackDraws.ToList();
        }

        var (mainFreq, luckyFreq, totalMain, totalLucky) = BuildFrequencies(selectedDraws);

        var mainWeights = BuildWeights(mainFreq, totalMain, MainNumbersMax);
        var luckyWeights = BuildWeights(luckyFreq, totalLucky, LuckyNumbersMax);

        return GeneratePredictionsWithWeights(mainWeights, luckyWeights, count, random, constraints, options);
    }

    private List<PredictedDrawDto> GenerateColdPredictions(
        IEnumerable<Draw> draws,
        int count,
        Random random,
        PredictionConstraints? constraints,
        PredictionOptions options)
    {
        var drawsList = draws.ToList();
        var (mainFreq, luckyFreq, _, _) = BuildFrequencies(drawsList);

        var mainWeights = BuildColdWeights(mainFreq, MainNumbersMax);
        var luckyWeights = BuildColdWeights(luckyFreq, LuckyNumbersMax);

        return GeneratePredictionsWithWeights(mainWeights, luckyWeights, count, random, constraints, options);
    }

    private List<PredictedDrawDto> GenerateCooccurrencePredictions(
        IEnumerable<Draw> draws,
        int count,
        Random random,
        PredictionConstraints? constraints,
        PredictionOptions options)
    {
        var drawsList = draws.ToList();
        var (mainFreq, luckyFreq, totalMain, totalLucky) = BuildFrequencies(drawsList);

        var globalMainWeights = BuildWeights(mainFreq, totalMain, MainNumbersMax);
        var globalLuckyWeights = BuildWeights(luckyFreq, totalLucky, LuckyNumbersMax);
        var cooccurrenceMatrix = BuildCooccurrenceMatrix(drawsList);

        var results = new List<PredictedDrawDto>();
        var seen = new HashSet<string>();
        var maxAttempts = Math.Max(count * options.MaxAttemptsMultiplier, count);
        var attempts = 0;

        var maxMainWeight = globalMainWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxLuckyWeight = globalLuckyWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxPossibleScore = (maxMainWeight * 5) + maxLuckyWeight;

        while (results.Count < count && attempts < maxAttempts)
        {
            attempts++;

            var numbers = DrawCooccurringNumbers(cooccurrenceMatrix, globalMainWeights, 5, random);
            Array.Sort(numbers);

            var lucky = DrawWeightedNumber(globalLuckyWeights, random);

            if (!SatisfiesConstraints(numbers, constraints))
            {
                continue;
            }

            var key = $"{string.Join('-', numbers)}+{lucky}";
            if (!seen.Add(key))
            {
                continue;
            }

            var rawScore = numbers.Sum(n => globalMainWeights[n]) + globalLuckyWeights[lucky];
            var score = maxPossibleScore > 0 ? rawScore / maxPossibleScore : 0d;

            results.Add(new PredictedDrawDto
            {
                Numbers = numbers,
                LuckyNumber = lucky,
                Score = Math.Round(score, 4)
            });
        }

        if (results.Count < count)
        {
            _logger.LogWarning("Unable to generate {Requested} distinct draws (co-occurrence) with constraints, {Generated} generated after {Attempts} attempts.", count, results.Count, attempts);
        }

        return results;
    }

    private int[] DrawCooccurringNumbers(int[,] cooccurrenceMatrix, double[] fallbackWeights, int numbersToPick, Random random)
    {
        var selected = new List<int>();
        var selectedSet = new HashSet<int>();

        var first = DrawWeightedNumber(fallbackWeights, random);
        selected.Add(first);
        selectedSet.Add(first);

        while (selected.Count < numbersToPick)
        {
            var next = DrawCooccurringNumber(selectedSet, cooccurrenceMatrix, fallbackWeights, random);
            selected.Add(next);
            selectedSet.Add(next);
        }

        return selected.ToArray();
    }

    private int DrawCooccurringNumber(HashSet<int> selected, int[,] cooccurrenceMatrix, double[] fallbackWeights, Random random)
    {
        var candidates = new List<(int number, double weight)>();

        for (var number = 1; number <= MainNumbersMax; number++)
        {
            if (selected.Contains(number))
            {
                continue;
            }

            double weight = 0;
            foreach (var s in selected)
            {
                weight += cooccurrenceMatrix[s, number];
            }

            candidates.Add((number, weight));
        }

        var chosen = DrawFromCandidates(candidates, random);
        if (chosen.HasValue)
        {
            return chosen.Value;
        }

        var fallbackCandidates = candidates
            .Select(c => (c.number, Math.Max(fallbackWeights[c.number], 0d)))
            .ToList();

        chosen = DrawFromCandidates(fallbackCandidates, random);
        if (chosen.HasValue)
        {
            return chosen.Value;
        }

        return fallbackCandidates.Count > 0
            ? fallbackCandidates[random.Next(fallbackCandidates.Count)].number
            : random.Next(1, MainNumbersMax + 1);
    }

    private int? DrawFromCandidates(List<(int number, double weight)> candidates, Random random)
    {
        var totalWeight = candidates.Sum(c => c.weight);
        if (totalWeight <= 0)
        {
            return null;
        }

        var draw = random.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var candidate in candidates)
        {
            cumulative += candidate.weight;
            if (draw <= cumulative)
            {
                return candidate.number;
            }
        }

        return candidates.Last().number;
    }

    private List<PredictedDrawDto> GeneratePredictionsWithWeights(
        double[] mainWeights,
        double[] luckyWeights,
        int count,
        Random random,
        PredictionConstraints? constraints,
        PredictionOptions options)
    {
        var results = new List<PredictedDrawDto>();
        var seen = new HashSet<string>();
        var maxAttempts = Math.Max(count * options.MaxAttemptsMultiplier, count);
        var attempts = 0;

        var maxMainWeight = mainWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxLuckyWeight = luckyWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxPossibleScore = (maxMainWeight * 5) + maxLuckyWeight;

        while (results.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var numbers = DrawWeightedNumbersWithoutReplacement(mainWeights, 5, random);
            Array.Sort(numbers);
            var lucky = DrawWeightedNumber(luckyWeights, random);

            if (!SatisfiesConstraints(numbers, constraints))
            {
                continue;
            }

            var key = $"{string.Join('-', numbers)}+{lucky}";
            if (!seen.Add(key))
            {
                continue;
            }

            var rawScore = numbers.Sum(n => mainWeights[n]) + luckyWeights[lucky];
            var score = maxPossibleScore > 0 ? rawScore / maxPossibleScore : 0d;

            results.Add(new PredictedDrawDto
            {
                Numbers = numbers,
                LuckyNumber = lucky,
                Score = Math.Round(score, 4)
            });
        }

        if (results.Count < count)
        {
            _logger.LogWarning("Unable to generate {Requested} distinct draws with constraints, {Generated} generated after {Attempts} attempts.", count, results.Count, attempts);
        }

        return results;
    }

    private static (int[] mainFrequencies, int[] luckyFrequencies, int totalMain, int totalLucky) BuildFrequencies(IEnumerable<Draw> draws)
    {
        var mainFrequencies = new int[MainNumbersMax + 1];
        var luckyFrequencies = new int[LuckyNumbersMax + 1];

        foreach (var draw in draws)
        {
            mainFrequencies[draw.Number1]++;
            mainFrequencies[draw.Number2]++;
            mainFrequencies[draw.Number3]++;
            mainFrequencies[draw.Number4]++;
            mainFrequencies[draw.Number5]++;
            luckyFrequencies[draw.LuckyNumber]++;
        }

        var totalMain = mainFrequencies.Sum();
        var totalLucky = luckyFrequencies.Sum();

        return (mainFrequencies, luckyFrequencies, totalMain, totalLucky);
    }

    private static double[] BuildWeights(int[] frequencies, int total, int maxNumber)
    {
        if (total == 0)
        {
            return BuildUniformWeights(maxNumber);
        }

        var weights = new double[maxNumber + 1];
        for (var i = 1; i <= maxNumber; i++)
        {
            weights[i] = (double)frequencies[i] / total;
        }

        return weights;
    }

    private static double[] BuildColdWeights(int[] frequencies, int maxNumber)
    {
        var weights = new double[maxNumber + 1];
        for (var i = 1; i <= maxNumber; i++)
        {
            weights[i] = 1d / (frequencies[i] + ColdEpsilon);
        }

        var total = weights.Skip(1).Sum();
        if (total <= 0)
        {
            return BuildUniformWeights(maxNumber);
        }

        for (var i = 1; i <= maxNumber; i++)
        {
            weights[i] /= total;
        }

        return weights;
    }

    private static double[] BuildUniformWeights(int maxNumber)
    {
        var uniform = 1d / maxNumber;
        var weights = new double[maxNumber + 1];
        for (var i = 1; i <= maxNumber; i++)
        {
            weights[i] = uniform;
        }

        return weights;
    }

    private static bool SatisfiesConstraints(int[] numbers, PredictionConstraints? constraints)
    {
        if (constraints is null)
        {
            return true;
        }

        var sum = numbers.Sum();
        var even = numbers.Count(n => n % 2 == 0);
        var low = numbers.Count(n => n <= 25);

        if (constraints.MinSum.HasValue && sum < constraints.MinSum.Value) return false;
        if (constraints.MaxSum.HasValue && sum > constraints.MaxSum.Value) return false;

        if (constraints.MinEven.HasValue && even < constraints.MinEven.Value) return false;
        if (constraints.MaxEven.HasValue && even > constraints.MaxEven.Value) return false;

        if (constraints.MinLow.HasValue && low < constraints.MinLow.Value) return false;
        if (constraints.MaxLow.HasValue && low > constraints.MaxLow.Value) return false;

        if (constraints.IncludeNumbers.Count > 0 &&
            !constraints.IncludeNumbers.All(n => numbers.Contains(n)))
        {
            return false;
        }

        if (constraints.ExcludeNumbers.Count > 0 &&
            numbers.Any(n => constraints.ExcludeNumbers.Contains(n)))
        {
            return false;
        }

        return true;
    }

    private static int[,] BuildCooccurrenceMatrix(IEnumerable<Draw> draws)
    {
        var matrix = new int[MainNumbersMax + 1, MainNumbersMax + 1];

        foreach (var draw in draws)
        {
            var numbers = new[] { draw.Number1, draw.Number2, draw.Number3, draw.Number4, draw.Number5 };

            for (var i = 0; i < numbers.Length; i++)
            {
                for (var j = i + 1; j < numbers.Length; j++)
                {
                    var a = numbers[i];
                    var b = numbers[j];

                    matrix[a, b]++;
                    matrix[b, a]++;
                }
            }
        }

        return matrix;
    }

    private int[] DrawWeightedNumbersWithoutReplacement(double[] weights, int count, Random random)
    {
        var candidates = new List<(int number, double weight)>();
        for (var i = 1; i < weights.Length; i++)
        {
            candidates.Add((i, weights[i]));
        }

        var result = new int[count];

        for (var pickIndex = 0; pickIndex < count; pickIndex++)
        {
            var totalWeight = candidates.Sum(c => c.weight);
            if (totalWeight <= 0)
            {
                var uniformWeight = 1d / candidates.Count;
                candidates = candidates.Select(c => (c.number, uniformWeight)).ToList();
                totalWeight = 1d;
            }

            var draw = random.NextDouble() * totalWeight;
            double cumulative = 0;
            var selectedIndex = 0;

            for (var i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].weight;
                if (draw <= cumulative)
                {
                    selectedIndex = i;
                    break;
                }
            }

            result[pickIndex] = candidates[selectedIndex].number;
            candidates.RemoveAt(selectedIndex);
        }

        return result;
    }

    private int DrawWeightedNumber(double[] weights, Random random)
    {
        var totalWeight = weights.Skip(1).Sum();
        if (totalWeight <= 0)
        {
            return random.Next(1, weights.Length);
        }

        var draw = random.NextDouble() * totalWeight;
        double cumulative = 0;
        for (var i = 1; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (draw <= cumulative)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }
}
