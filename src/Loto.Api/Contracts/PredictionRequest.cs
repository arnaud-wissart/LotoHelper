namespace Loto.Api.Contracts;

public sealed class PredictionRequest
{
    public int Count { get; init; } = 10;

    public PredictionStrategy Strategy { get; init; } = PredictionStrategy.FrequencyGlobal;

    public int? MinSum { get; init; }
    public int? MaxSum { get; init; }

    public int? MinEven { get; init; }
    public int? MaxEven { get; init; }

    public int? MinLow { get; init; }
    public int? MaxLow { get; init; }

    public int[]? IncludeNumbers { get; init; }
    public int[]? ExcludeNumbers { get; init; }
}
