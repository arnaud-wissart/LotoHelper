namespace Loto.Api.Contracts;

public sealed class StatsOverviewDto
{
    public int TotalDraws { get; init; }
    public DateTime? FirstDrawDate { get; init; }
    public DateTime? LastDrawDate { get; init; }

    public IReadOnlyList<DayOfWeekCountDto> DrawsPerDayOfWeek { get; init; } = Array.Empty<DayOfWeekCountDto>();
}

public sealed class DayOfWeekCountDto
{
    public string DayName { get; init; } = default!;
    public int Count { get; init; }
}

public sealed class NumberFrequencyDto
{
    public int Number { get; init; }
    public int Count { get; init; }
    public double Frequency { get; init; }
}

public sealed class StatsFrequenciesDto
{
    public IReadOnlyList<NumberFrequencyDto> MainNumbers { get; init; } = Array.Empty<NumberFrequencyDto>();
    public IReadOnlyList<NumberFrequencyDto> LuckyNumbers { get; init; } = Array.Empty<NumberFrequencyDto>();
}
