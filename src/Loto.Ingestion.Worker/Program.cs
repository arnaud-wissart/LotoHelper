using Loto.Ingestion.Worker;
using Loto.Infrastructure;
using Loto.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("loto-db")
                       ?? builder.Configuration["ConnectionStrings:loto-db"]
                       ?? throw new InvalidOperationException("Connection string 'loto-db' is not configured.");

builder.Services.AddOptions<FdjOptions>()
    .Bind(builder.Configuration.GetSection("Fdj"))
    .Validate(opt => opt.IntervalMinutes > 0, "Fdj:IntervalMinutes must be greater than zero.")
    .Validate(opt => opt.MinRefreshAgeMinutes > 0, "Fdj:MinRefreshAgeMinutes must be greater than zero.")
    .Validate(opt => opt.UseLocalFile || !string.IsNullOrWhiteSpace(opt.NewLotoArchiveUrl), "Fdj:NewLotoArchiveUrl must be configured when UseLocalFile is false.")
    .Validate(opt => !opt.UseLocalFile || !string.IsNullOrWhiteSpace(opt.LocalArchivePath), "Fdj:LocalArchivePath must be configured when UseLocalFile is true.")
    .ValidateOnStart();
builder.Services.AddHttpClient();

builder.Services.AddDbContextFactory<LotoDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IFdjDataSource>(sp =>
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

builder.Services.AddSingleton<IFdjParser, NewLotoZipCsvParser>();
builder.Services.AddSingleton<IFdjIngestionService, FdjIngestionService>();

builder.Services.AddLotoOpenTelemetry(
    builder.Configuration,
    serviceName: "loto-worker",
    configureTracing: tracing => tracing.AddSource(FdjIngestionService.ActivitySourceName),
    configureMetrics: metrics => metrics.AddMeter(FdjIngestionService.MeterName));

builder.Services.AddHostedService<FdjIngestionWorker>();

var host = builder.Build();
host.Run();
