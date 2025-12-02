namespace Loto.Api.Services;

public sealed class PredictionOptions
{
    /// <summary>Default number of grids to generate when the request count is missing or invalid.</summary>
    public int DefaultCount { get; init; } = 10;

    /// <summary>Hard limit to avoid excessive load.</summary>
    public int MaxCount { get; init; } = 100;

    /// <summary>Size of the look-back window (in days) for the FrequencyRecent strategy.</summary>
    public int RecentWindowDays { get; init; } = 730;

    /// <summary>Maximum number of forced numbers the caller can provide.</summary>
    public int MaxIncludeNumbers { get; init; } = 5;

    /// <summary>Multiplier applied to the requested count to cap the number of generation attempts.</summary>
    public int MaxAttemptsMultiplier { get; init; } = 100;
}
