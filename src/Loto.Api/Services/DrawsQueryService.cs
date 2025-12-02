using System.Globalization;
using Loto.Api.Contracts;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Loto.Api.Services;

public interface IDrawsQueryService
{
    Task<PagedResult<DrawDto>> GetDrawsAsync(GetDrawsRequest request, CancellationToken cancellationToken);
}

public sealed class DrawsQueryService : IDrawsQueryService
{
    private readonly LotoDbContext _dbContext;
    private readonly ILogger<DrawsQueryService> _logger;

    public DrawsQueryService(LotoDbContext dbContext, ILogger<DrawsQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResult<DrawDto>> GetDrawsAsync(GetDrawsRequest request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        var (dateFrom, dateTo, error) = ParseDateRange(request.DateFrom, request.DateTo);
        if (error is not null)
        {
            _logger.LogWarning("Invalid draws request rejected: {Reason}", error);
            throw new ArgumentException(error);
        }

        var query = _dbContext.Draws.AsNoTracking();

        if (dateFrom.HasValue)
        {
            query = query.Where(d => d.DrawDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(d => d.DrawDate <= dateTo.Value);
        }

        query = query.OrderByDescending(d => d.DrawDate);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DrawDto
            {
                Id = d.Id,
                OfficialDrawId = d.OfficialDrawId ?? string.Empty,
                DrawDate = d.DrawDate,
                DrawDayName = d.DrawDayName,
                Number1 = d.Number1,
                Number2 = d.Number2,
                Number3 = d.Number3,
                Number4 = d.Number4,
                Number5 = d.Number5,
                LuckyNumber = d.LuckyNumber
            })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<DrawDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private static (DateTime? From, DateTime? To, string? Error) ParseDateRange(string? rawFrom, string? rawTo)
    {
        DateTime? from = null;
        DateTime? to = null;

        if (!string.IsNullOrWhiteSpace(rawFrom))
        {
            if (!TryParseDateOnlyUtc(rawFrom, out var parsedFrom))
            {
                return (null, null, "dateFrom must use yyyy-MM-dd format.");
            }

            from = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(rawTo))
        {
            if (!TryParseDateOnlyUtc(rawTo, out var parsedTo))
            {
                return (null, null, "dateTo must use yyyy-MM-dd format.");
            }

            to = parsedTo;
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            return (null, null, "dateFrom cannot be after dateTo.");
        }

        return (from, to, null);
    }

    private static bool TryParseDateOnlyUtc(string value, out DateTime date) =>
        DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out date);
}
