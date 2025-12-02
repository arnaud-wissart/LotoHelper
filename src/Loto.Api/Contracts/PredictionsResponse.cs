namespace Loto.Api.Contracts;

public sealed class PredictionsResponse
{
    public DateTime GeneratedAtUtc { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<PredictedDrawDto> Draws { get; init; } = Array.Empty<PredictedDrawDto>();
}
