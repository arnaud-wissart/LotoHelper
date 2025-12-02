using System.Globalization;
using Loto.Api.Contracts;
using Loto.Api.Services;
using Loto.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var frCulture = new CultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = frCulture;
CultureInfo.DefaultThreadCurrentUICulture = frCulture;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("loto-db")
                       ?? builder.Configuration["ConnectionStrings:loto-db"]
                       ?? throw new InvalidOperationException("La chaine de connexion 'loto-db' n'est pas configuree.");

builder.Services.AddDbContext<LotoDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ILotoPredictionService, LotoPredictionService>();
builder.Services.AddScoped<IStrategyBacktestService, StrategyBacktestService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "Loto.Api", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddEntityFrameworkCoreInstrumentation();
        tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddMeter(LotoPredictionService.MeterName);
        metrics.AddOtlpExporter();
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LotoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    var retries = 5;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt <= retries)
        {
            logger.LogWarning(ex, "Echec de la tentative de migration {Attempt}, nouvel essai dans {Delay}s", attempt, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(frCulture),
    SupportedCultures = new[] { frCulture },
    SupportedUICultures = new[] { frCulture }
});

bool TryParseDateOnlyUtc(string value, out DateTime date) =>
    DateTime.TryParseExact(
        value,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out date);

app.MapGet("/health", () => Results.Ok())
   .WithName("Health");

app.MapGet("/api/stats/overview", async (LotoDbContext db, CancellationToken ct) =>
{
    var totalDraws = await db.Draws.CountAsync(ct);
    if (totalDraws == 0)
    {
        return Results.Ok(new StatsOverviewDto());
    }

    var firstDrawDate = await db.Draws.MinAsync(d => d.DrawDate, ct);
    var lastDrawDate = await db.Draws.MaxAsync(d => d.DrawDate, ct);

    var dayCounts = await db.Draws
        .GroupBy(d => d.DrawDayName)
        .Select(g => new DayOfWeekCountDto
        {
            DayName = g.Key ?? "INCONNU",
            Count = g.Count()
        })
        .OrderByDescending(x => x.Count)
        .ToListAsync(ct);

    var overview = new StatsOverviewDto
    {
        TotalDraws = totalDraws,
        FirstDrawDate = firstDrawDate,
        LastDrawDate = lastDrawDate,
        DrawsPerDayOfWeek = dayCounts
    };

    return Results.Ok(overview);
})
.WithName("GetStatsOverview")
.Produces<StatsOverviewDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/frequencies", async (LotoDbContext db, CancellationToken ct) =>
{
    var draws = await db.Draws.AsNoTracking().ToListAsync(ct);
    if (draws.Count == 0)
    {
        return Results.Ok(new StatsFrequenciesDto());
    }

    var mainFreq = new int[50];
    var luckyFreq = new int[11];

    foreach (var d in draws)
    {
        mainFreq[d.Number1]++;
        mainFreq[d.Number2]++;
        mainFreq[d.Number3]++;
        mainFreq[d.Number4]++;
        mainFreq[d.Number5]++;
        luckyFreq[d.LuckyNumber]++;
    }

    var totalMain = mainFreq.Sum();
    var totalLucky = luckyFreq.Sum();

    var mainList = Enumerable.Range(1, 49)
        .Select(n => new NumberFrequencyDto
        {
            Number = n,
            Count = mainFreq[n],
            Frequency = totalMain > 0 ? (double)mainFreq[n] / totalMain : 0
        })
        .OrderByDescending(x => x.Count)
        .ToList();

    var luckyList = Enumerable.Range(1, 10)
        .Select(n => new NumberFrequencyDto
        {
            Number = n,
            Count = luckyFreq[n],
            Frequency = totalLucky > 0 ? (double)luckyFreq[n] / totalLucky : 0
        })
        .OrderByDescending(x => x.Count)
        .ToList();

    var dto = new StatsFrequenciesDto
    {
        MainNumbers = mainList,
        LuckyNumbers = luckyList
    };

    return Results.Ok(dto);
})
.WithName("GetStatsFrequencies")
.Produces<StatsFrequenciesDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/patterns", async (
    int? bucketSize,
    LotoDbContext db,
    CancellationToken ct) =>
{
    var draws = await db.Draws.AsNoTracking().ToListAsync(ct);
    if (draws.Count == 0)
    {
        return Results.Ok(new PatternDistributionDto());
    }

    var sums = draws.Select(d => d.Number1 + d.Number2 + d.Number3 + d.Number4 + d.Number5).ToList();
    var minSum = sums.Min();
    var maxSum = sums.Max();

    var size = bucketSize.GetValueOrDefault(10);
    if (size <= 0)
    {
        size = 10;
    }

    var buckets = new List<SumBucketDto>();
    for (int start = (minSum / size) * size; start <= maxSum; start += size)
    {
        int end = start + size - 1;
        var count = sums.Count(s => s >= start && s <= end);
        buckets.Add(new SumBucketDto
        {
            MinInclusive = start,
            MaxInclusive = end,
            Count = count
        });
    }

    var evenCountDist = new Dictionary<int, int>();
    var lowCountDist = new Dictionary<int, int>();

    foreach (var d in draws)
    {
        int[] nums = { d.Number1, d.Number2, d.Number3, d.Number4, d.Number5 };

        var evenCount = nums.Count(n => n % 2 == 0);
        var lowCount = nums.Count(n => n <= 25);

        evenCountDist[evenCount] = evenCountDist.TryGetValue(evenCount, out var evc) ? evc + 1 : 1;
        lowCountDist[lowCount] = lowCountDist.TryGetValue(lowCount, out var lc) ? lc + 1 : 1;
    }

    var dto = new PatternDistributionDto
    {
        SumBuckets = buckets,
        EvenCountDistribution = evenCountDist,
        LowCountDistribution = lowCountDist
    };

    return Results.Ok(dto);
})
.WithName("GetStatsPatterns")
.Produces<PatternDistributionDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/cooccurrence", async (
    int baseNumber,
    int? top,
    LotoDbContext db,
    CancellationToken ct) =>
{
    if (baseNumber < 1 || baseNumber > 49)
    {
        return Results.BadRequest("baseNumber must be between 1 and 49.");
    }

    var draws = await db.Draws.AsNoTracking().ToListAsync(ct);
    var totalDraws = draws.Count;
    if (totalDraws == 0)
    {
        return Results.Ok(new CooccurrenceStatsDto
        {
            BaseNumber = baseNumber
        });
    }

    var drawsWithBase = draws
        .Where(d =>
            d.Number1 == baseNumber ||
            d.Number2 == baseNumber ||
            d.Number3 == baseNumber ||
            d.Number4 == baseNumber ||
            d.Number5 == baseNumber)
        .ToList();

    var drawsContainingBaseCount = drawsWithBase.Count;

    var globalCount = new int[50];
    foreach (var d in draws)
    {
        globalCount[d.Number1]++;
        globalCount[d.Number2]++;
        globalCount[d.Number3]++;
        globalCount[d.Number4]++;
        globalCount[d.Number5]++;
    }

    var coCounts = new int[50];

    foreach (var d in drawsWithBase)
    {
        int[] nums = { d.Number1, d.Number2, d.Number3, d.Number4, d.Number5 };
        foreach (var n in nums)
        {
            if (n == baseNumber) continue;
            coCounts[n]++;
        }
    }

    var coList = Enumerable.Range(1, 49)
        .Where(n => n != baseNumber && coCounts[n] > 0)
        .Select(n => new CooccurringNumberDto
        {
            Number = n,
            CooccurrenceCount = coCounts[n],
            ConditionalProbability = drawsContainingBaseCount > 0
                ? (double)coCounts[n] / drawsContainingBaseCount
                : 0,
            GlobalProbability = totalDraws > 0
                ? (double)globalCount[n] / totalDraws
                : 0
        })
        .OrderByDescending(x => x.CooccurrenceCount)
        .ToList();

    var limit = top.GetValueOrDefault(15);
    if (limit > 0 && coList.Count > limit)
    {
        coList = coList.Take(limit).ToList();
    }

    var result = new CooccurrenceStatsDto
    {
        BaseNumber = baseNumber,
        TotalDraws = totalDraws,
        DrawsContainingBase = drawsContainingBaseCount,
        Cooccurrences = coList
    };

    return Results.Ok(result);
})
.WithName("GetStatsCooccurrence")
.Produces<CooccurrenceStatsDto>(StatusCodes.Status200OK);

app.MapGet("/api/draws", async ([AsParameters] GetDrawsRequest request, LotoDbContext db, CancellationToken ct) =>
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        DateTime? dateFrom = null;
        DateTime? dateTo = null;

        if (!string.IsNullOrWhiteSpace(request.DateFrom))
        {
            if (!TryParseDateOnlyUtc(request.DateFrom, out var parsedFrom))
            {
                return Results.BadRequest("Format de dateFrom invalide. Utilisez yyyy-MM-dd.");
            }

            dateFrom = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(request.DateTo))
        {
            if (!TryParseDateOnlyUtc(request.DateTo, out var parsedTo))
            {
                return Results.BadRequest("Format de dateTo invalide. Utilisez yyyy-MM-dd.");
            }

            dateTo = parsedTo;
        }

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            return Results.BadRequest("dateFrom ne peut pas etre posterieure a dateTo.");
        }

        var query = db.Draws.AsNoTracking();

        if (dateFrom.HasValue)
        {
            query = query.Where(d => d.DrawDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(d => d.DrawDate <= dateTo.Value);
        }

        query = query.OrderByDescending(d => d.DrawDate);

        var totalCount = await query.CountAsync(ct);

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
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PagedResult<DrawDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Results.Ok(result);
    })
    .WithName("GetDraws")
    .WithOpenApi();

app.MapPost("/api/analysis/strategy-backtest", async (
    StrategyBacktestRequest request,
    IStrategyBacktestService backtestService,
    CancellationToken ct) =>
{
    try
    {
        var result = await backtestService.BacktestAsync(request, ct);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("BacktestStrategy")
.Produces<StrategyBacktestResultDto>(StatusCodes.Status200OK);

app.MapPost("/api/predictions", async (
    PredictionRequest request,
    ILotoPredictionService predictionService,
    CancellationToken ct) =>
{
    var count = request.Count <= 0 ? 1 : Math.Min(request.Count, 100);
    var includeSet = new HashSet<int>();
    if (request.IncludeNumbers is { Length: > 0 })
    {
        if (request.IncludeNumbers.Length > 5)
        {
            return Results.BadRequest("includeNumbers ne peut pas contenir plus de 5 valeurs.");
        }

        foreach (var n in request.IncludeNumbers)
        {
            if (n < 1 || n > 49)
            {
                return Results.BadRequest("includeNumbers doit contenir des valeurs entre 1 et 49.");
            }

            includeSet.Add(n);
        }
    }

    var excludeSet = new HashSet<int>();
    if (request.ExcludeNumbers is { Length: > 0 })
    {
        foreach (var n in request.ExcludeNumbers)
        {
            if (n < 1 || n > 49)
            {
                return Results.BadRequest("excludeNumbers doit contenir des valeurs entre 1 et 49.");
            }

            excludeSet.Add(n);
        }
    }

    PredictionConstraints? constraints = null;
    var hasConstraints =
        request.MinSum.HasValue || request.MaxSum.HasValue ||
        request.MinEven.HasValue || request.MaxEven.HasValue ||
        request.MinLow.HasValue || request.MaxLow.HasValue ||
        includeSet.Count > 0 || excludeSet.Count > 0;

    if (hasConstraints)
    {
        constraints = new PredictionConstraints
        {
            MinSum = request.MinSum,
            MaxSum = request.MaxSum,
            MinEven = request.MinEven,
            MaxEven = request.MaxEven,
            MinLow = request.MinLow,
            MaxLow = request.MaxLow,
            IncludeNumbers = includeSet,
            ExcludeNumbers = excludeSet
        };
    }

    var result = await predictionService.GeneratePredictionsAsync(count, request.Strategy, constraints, ct);
    return Results.Ok(result);
})
.WithName("GeneratePredictions")
.Produces<PredictionsResponse>(StatusCodes.Status200OK)
.WithOpenApi();

app.Run();
