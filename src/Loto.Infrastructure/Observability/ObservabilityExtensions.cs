using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Loto.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    private const string OtelEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static IServiceCollection AddLotoOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCoreInstrumentation = false,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var resourceBuilder = CreateResourceBuilder(configuration, serviceName);

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (includeAspNetCoreInstrumentation)
                {
                    builder.AddAspNetCoreInstrumentation();
                }

                configureTracing?.Invoke(builder);

                AddOtlpExporter(configuration, builder);
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                if (includeAspNetCoreInstrumentation)
                {
                    builder.AddAspNetCoreInstrumentation();
                }

                configureMetrics?.Invoke(builder);

                AddOtlpExporter(configuration, builder);
            });

        return services;
    }

    private static ResourceBuilder CreateResourceBuilder(IConfiguration configuration, string serviceName)
    {
        var environmentName = configuration["DOTNET_ENVIRONMENT"]
                              ?? configuration["ASPNETCORE_ENVIRONMENT"];
        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

        var builder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment", environmentName!)
            });
        }

        return builder;
    }

    private static void AddOtlpExporter(IConfiguration configuration, TracerProviderBuilder builder)
    {
        var endpoint = configuration[OtelEndpointKey];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            return;
        }

        if (IsDevelopment(configuration))
        {
            builder.AddConsoleExporter();
        }
    }

    private static void AddOtlpExporter(IConfiguration configuration, MeterProviderBuilder builder)
    {
        var endpoint = configuration[OtelEndpointKey];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            return;
        }

        if (IsDevelopment(configuration))
        {
            builder.AddConsoleExporter();
        }
    }

    private static bool IsDevelopment(IConfiguration configuration)
    {
        var environmentName = configuration["DOTNET_ENVIRONMENT"]
                              ?? configuration["ASPNETCORE_ENVIRONMENT"];
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
