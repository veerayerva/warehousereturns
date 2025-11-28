using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using WarehouseReturns.PieceInfoApi.Configuration;
using WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Azure Functions host application for PieceInfo API
/// 
/// Configures dependency injection, logging, HTTP clients, and external API integration
/// for aggregating warehouse piece information from multiple data sources.
/// </summary>
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });
    })
    .ConfigureOpenApi()
    .ConfigureServices((context, services) =>
    {
        // ===================================================================
        // CONFIGURATION BINDING
        // ===================================================================
        var pieceInfoApiSettings = new PieceInfoApiSettings();
        context.Configuration.Bind("PieceInfoApi", pieceInfoApiSettings);
        services.AddSingleton(pieceInfoApiSettings);
        
        // Configure JSON serialization options
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.WriteIndented = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        
        // ===================================================================
        // HTTP CLIENT CONFIGURATION
        // ===================================================================
        services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
        {
            client.BaseAddress = new Uri(pieceInfoApiSettings.ExternalApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(pieceInfoApiSettings.ApiTimeoutSeconds);
            
            // Standard headers for external API communication
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "PieceInfoApi/1.0");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            
            // Add subscription key for API authentication
            if (!string.IsNullOrEmpty(pieceInfoApiSettings.OcpApimSubscriptionKey))
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", pieceInfoApiSettings.OcpApimSubscriptionKey);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() => 
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            
            if (!pieceInfoApiSettings.VerifySsl)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }
            return handler;
        });
        
        // ===================================================================
        // SERVICE REGISTRATION
        // ===================================================================
        services.AddSingleton<IAggregationService, AggregationService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        
        // ===================================================================
        // SERILOG LOGGING CONFIGURATION
        // ===================================================================
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.WithProperty("Environment", pieceInfoApiSettings.WarehouseReturnsEnv)
            .Enrich.WithProperty("Application", "PieceInfoApi")
            .Enrich.WithProperty("Version", "1.0.0")
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.ApplicationInsights(
                context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"],
                TelemetryConverter.Traces)
            .CreateLogger();

        services.AddSerilog(logger);
        Log.Logger = logger;
        Log.Information("PieceInfo API starting up with Serilog configuration");
    })
    .UseSerilog()
    .Build();

try
{
    Log.Information("Starting PieceInfo API host");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PieceInfo API host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}