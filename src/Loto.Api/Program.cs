using System.Globalization;
using Loto.Api.Contracts;
using Loto.Api.Services;
using Loto.Infrastructure;
using Loto.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
builder.Services.AddScoped<IDrawsQueryService, DrawsQueryService>();
builder.Services.AddScoped<IStatsService, StatsService>();

builder.Services.AddOptions<PredictionOptions>()
    .Bind(builder.Configuration.GetSection("Prediction"))
    .Validate(opt => opt.MaxCount > 0, "MaxCount must be positive.")
    .Validate(opt => opt.RecentWindowDays > 0, "RecentWindowDays must be positive.")
    .Validate(opt => opt.MaxIncludeNumbers > 0, "MaxIncludeNumbers must be positive.")
    .Validate(opt => opt.MaxAttemptsMultiplier > 0, "MaxAttemptsMultiplier must be positive.")
    .ValidateOnStart();

builder.Services.AddLotoOpenTelemetry(
    builder.Configuration,
    serviceName: "loto-api",
    includeAspNetCoreInstrumentation: true,
    configureMetrics: metrics => metrics.AddMeter(LotoPredictionService.MeterName));

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .ExcludeFromDescription();

app.MapGet("/api/stats/overview", async (IStatsService statsService, CancellationToken ct) =>
    {
        var overview = await statsService.GetOverviewAsync(ct);
        return Results.Ok(overview);
    })
    .WithName("GetStatsOverview")
    .Produces<StatsOverviewDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/frequencies", async (IStatsService statsService, CancellationToken ct) =>
    {
        var frequencies = await statsService.GetFrequenciesAsync(ct);
        return Results.Ok(frequencies);
    })
    .WithName("GetStatsFrequencies")
    .Produces<StatsFrequenciesDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/patterns", async (
    int? bucketSize,
    IStatsService statsService,
    CancellationToken ct) =>
{
    var patterns = await statsService.GetPatternsAsync(bucketSize ?? 10, ct);
    return Results.Ok(patterns);
})
.WithName("GetStatsPatterns")
.Produces<PatternDistributionDto>(StatusCodes.Status200OK);

app.MapGet("/api/stats/cooccurrence", async (
    int baseNumber,
    int? top,
    IStatsService statsService,
    CancellationToken ct) =>
{
    try
    {
        var stats = await statsService.GetCooccurrenceAsync(baseNumber, top, ct);
        return Results.Ok(stats);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("GetStatsCooccurrence")
.Produces<CooccurrenceStatsDto>(StatusCodes.Status200OK);

app.MapGet("/api/draws", async ([AsParameters] GetDrawsRequest request, IDrawsQueryService drawsQueryService, CancellationToken ct) =>
    {
        try
        {
            var result = await drawsQueryService.GetDrawsAsync(request, ct);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
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
    IOptionsSnapshot<PredictionOptions> predictionOptions,
    CancellationToken ct) =>
{
    var validation = PredictionRequestValidator.Validate(request, predictionOptions.Value);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation.ErrorMessage);
    }

    var result = await predictionService.GeneratePredictionsAsync(
        validation.Count,
        request.Strategy,
        validation.Constraints,
        ct);

    return Results.Ok(result);
})
.WithName("GeneratePredictions")
.Produces<PredictionsResponse>(StatusCodes.Status200OK)
.WithOpenApi();

app.Run();
