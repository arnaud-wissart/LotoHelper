namespace Loto.Api.Contracts;

public sealed class SumBucketDto
{
    public int MinInclusive { get; init; }
    public int MaxInclusive { get; init; }
    public int Count { get; init; }
}

public sealed class PatternDistributionDto
{
    public IReadOnlyList<SumBucketDto> SumBuckets { get; init; } = Array.Empty<SumBucketDto>();
    public IReadOnlyDictionary<int, int> EvenCountDistribution { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> LowCountDistribution { get; init; } = new Dictionary<int, int>();
}

public sealed class CooccurringNumberDto
{
    public int Number { get; init; }
    public int CooccurrenceCount { get; init; }
    public double ConditionalProbability { get; init; }
    public double GlobalProbability { get; init; }
}

public sealed class CooccurrenceStatsDto
{
    public int BaseNumber { get; init; }
    public int TotalDraws { get; init; }
    public int DrawsContainingBase { get; init; }
    public IReadOnlyList<CooccurringNumberDto> Cooccurrences { get; init; } = Array.Empty<CooccurringNumberDto>();
}
