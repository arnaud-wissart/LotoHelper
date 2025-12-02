using Loto.Ingestion.Worker;
using Loto.Infrastructure;
using Loto.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddLotoOpenTelemetry(
    builder.Configuration,
    serviceName: "loto-worker",
    configureTracing: tracing => tracing.AddSource(FdjIngestionService.ActivitySourceName),
    configureMetrics: metrics => metrics.AddMeter(FdjIngestionService.MeterName));

builder.Services.AddHostedService<FdjIngestionWorker>();

var host = builder.Build();
host.Run();
