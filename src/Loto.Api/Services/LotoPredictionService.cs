using Loto.Api.Contracts;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Loto.Api.Services;

public sealed class LotoPredictionService : ILotoPredictionService
{
    private readonly LotoDbContext _dbContext;
    private readonly ILogger<LotoPredictionService> _logger;
    private readonly Random _random = Random.Shared;

    public LotoPredictionService(LotoDbContext dbContext, ILogger<LotoPredictionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PredictionsResponse> GeneratePredictionsAsync(int count, CancellationToken cancellationToken)
    {
        var draws = await _dbContext.Draws.AsNoTracking().ToListAsync(cancellationToken);

        var mainFrequencies = new int[50];
        var luckyFrequencies = new int[11];

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

        var mainWeights = BuildWeights(mainFrequencies, totalMain, 49);
        var luckyWeights = BuildWeights(luckyFrequencies, totalLucky, 10);

        var maxMainWeight = mainWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxLuckyWeight = luckyWeights.Skip(1).DefaultIfEmpty(0).Max();
        var maxPossibleScore = (maxMainWeight * 5) + maxLuckyWeight;

        var results = new List<PredictedDrawDto>();
        var seen = new HashSet<string>();
        var maxAttempts = count * 20;
        var attempts = 0;

        // Simulation basée sur les fréquences historiques, ne garantit évidemment aucun gain réel.
        while (results.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var numbers = DrawWeightedNumbersWithoutReplacement(mainWeights, 5);
            Array.Sort(numbers);
            var lucky = DrawWeightedNumber(luckyWeights);

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
            _logger.LogWarning("Impossible de générer {Requested} tirages distincts, {Generated} générés après {Attempts} tentatives.", count, results.Count, attempts);
        }

        return new PredictionsResponse
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Count = results.Count,
            Draws = results
        };
    }

    private static double[] BuildWeights(int[] frequencies, int total, int maxNumber)
    {
        var weights = new double[maxNumber + 1];
        if (total == 0)
        {
            var uniform = 1d / maxNumber;
            for (var i = 1; i <= maxNumber; i++)
            {
                weights[i] = uniform;
            }
            return weights;
        }

        for (var i = 1; i <= maxNumber; i++)
        {
            weights[i] = (double)frequencies[i] / total;
        }

        return weights;
    }

    private int[] DrawWeightedNumbersWithoutReplacement(double[] weights, int count)
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

            var draw = _random.NextDouble() * totalWeight;
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

    private int DrawWeightedNumber(double[] weights)
    {
        var totalWeight = weights.Skip(1).Sum();
        if (totalWeight <= 0)
        {
            return _random.Next(1, weights.Length);
        }

        var draw = _random.NextDouble() * totalWeight;
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
