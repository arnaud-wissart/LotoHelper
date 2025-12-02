namespace Loto.Api.Contracts;

public sealed class StrategyBacktestRequest
{
    public PredictionStrategy Strategy { get; init; } = PredictionStrategy.FrequencyGlobal;
    public string? DateFrom { get; init; } // "yyyy-MM-dd"
    public string? DateTo { get; init; }   // "yyyy-MM-dd"
    public int? SampleSize { get; init; }  // optionnel : limiter le nb de tirages analysés
}

public sealed class MatchDistributionDto
{
    public int MatchedMain { get; init; }
    public bool MatchedLucky { get; init; }
    public int Count { get; init; }
}

public sealed class StrategyBacktestResultDto
{
    public PredictionStrategy Strategy { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    public int TotalDrawsAnalyzed { get; init; }
    public double AverageMatchedMain { get; init; }

    public IReadOnlyList<MatchDistributionDto> Distributions { get; init; } = Array.Empty<MatchDistributionDto>();
}
