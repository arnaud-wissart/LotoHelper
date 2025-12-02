namespace Loto.Api.Services;

public sealed class PredictionConstraints
{
    public int? MinSum { get; init; }
    public int? MaxSum { get; init; }

    public int? MinEven { get; init; }
    public int? MaxEven { get; init; }

    public int? MinLow { get; init; }
    public int? MaxLow { get; init; }

    public HashSet<int> IncludeNumbers { get; init; } = new();
    public HashSet<int> ExcludeNumbers { get; init; } = new();
}
