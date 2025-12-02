namespace Loto.Api.Contracts;

public sealed class PredictedDrawDto
{
    public int[] Numbers { get; init; } = Array.Empty<int>();
    public int LuckyNumber { get; init; }
    public double Score { get; init; }
}
