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

app.MapPost("/api/predictions", async (
    PredictionRequest request,
    ILotoPredictionService predictionService,
    CancellationToken ct) =>
{
    var count = request.Count <= 0 ? 1 : Math.Min(request.Count, 100);
    var result = await predictionService.GeneratePredictionsAsync(count, request.Strategy, ct);
    return Results.Ok(result);
})
.WithName("GeneratePredictions")
.Produces<PredictionsResponse>(StatusCodes.Status200OK)
.WithOpenApi();

app.Run();
