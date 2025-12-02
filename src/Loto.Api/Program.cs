using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var connectionString = builder.Configuration.GetConnectionString("loto-db")
                       ?? builder.Configuration["ConnectionStrings:loto-db"]
                       ?? throw new InvalidOperationException("Connection string 'loto-db' is not configured.");

builder.Services.AddDbContext<LotoDbContext>(options =>
    options.UseNpgsql(connectionString));

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
            logger.LogWarning(ex, "Database migration attempt {Attempt} failed, retrying in {Delay}s", attempt, delay.TotalSeconds);
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

app.MapGet("/health", () => Results.Ok())
   .WithName("Health");

app.MapGet("/api/draws", async (LotoDbContext db, CancellationToken ct) =>
        await db.Draws
            .OrderByDescending(d => d.DrawDate)
            .Take(100)
            .ToListAsync(ct))
    .WithName("GetDraws")
    .WithOpenApi();

app.Run();
