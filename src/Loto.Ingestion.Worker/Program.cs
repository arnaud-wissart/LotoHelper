using Loto.Ingestion.Worker;
using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("loto-db")
                       ?? builder.Configuration["ConnectionStrings:loto-db"]
                       ?? throw new InvalidOperationException("Connection string 'loto-db' is not configured.");

builder.Services.Configure<FdjOptions>(builder.Configuration.GetSection("Fdj"));
builder.Services.AddHttpClient();

builder.Services.AddDbContextFactory<LotoDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IFdjDataSource>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FdjOptions>>();
    if (options.Value.UseLocalFile)
    {
        return new FileNewLotoDataSource(
            options,
            sp.GetRequiredService<ILogger<FileNewLotoDataSource>>());
    }

    return new HttpNewLotoDataSource(
        sp.GetRequiredService<IHttpClientFactory>(),
        options,
        sp.GetRequiredService<ILogger<HttpNewLotoDataSource>>());
});

builder.Services.AddScoped<IFdjParser, NewLotoZipCsvParser>();
builder.Services.AddScoped<IFdjIngestionService, FdjIngestionService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "Loto.Ingestion.Worker", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing.AddSource(FdjIngestionService.ActivitySourceName);
        tracing.AddHttpClientInstrumentation();
        tracing.AddEntityFrameworkCoreInstrumentation();
        tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(FdjIngestionService.MeterName);
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddOtlpExporter();
    });

builder.Services.AddHostedService<FdjIngestionWorker>();

var host = builder.Build();
host.Run();
