namespace Loto.Api.Contracts;

public sealed class GetDrawsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? DateFrom { get; init; }
    public string? DateTo { get; init; }
}
