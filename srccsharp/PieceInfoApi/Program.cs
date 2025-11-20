using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
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
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // ===================================================================
        // APPLICATION INSIGHTS AND LOGGING CONFIGURATION
        // ===================================================================
        // Application Insights is automatically configured in Azure Functions
        
        // ===================================================================
        // OPENAPI/SWAGGER CONFIGURATION
        // ===================================================================
        services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
        {
            var options = new OpenApiConfigurationOptions()
            {
                Info = new OpenApiInfo()
                {
                    Version = "1.0.0",
                    Title = "PieceInfo API",
                    Description = "API for aggregating piece information from multiple external sources",
                    Contact = new OpenApiContact()
                    {
                        Name = "Warehouse Returns Team"
                    }
                },
                Servers = DefaultOpenApiConfigurationOptions.GetHostNames(),
                OpenApiVersion = OpenApiVersionType.V3,
                IncludeRequestingHostName = true,
                ForceHttps = false,
                ForceHttp = false
            };
            return options;
        });
        
        // Configure JSON serialization options
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.WriteIndented = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        
        // ===================================================================
        // CONFIGURATION BINDING
        // ===================================================================
        var configuration = context.Configuration;
        services.Configure<PieceInfoApiSettings>(options =>
        {
            options.ExternalApiBaseUrl = configuration["EXTERNAL_API_BASE_URL"] ?? "https://apim-dev.nfm.com";
            options.OcpApimSubscriptionKey = configuration["OCP_APIM_SUBSCRIPTION_KEY"] ?? string.Empty;
            options.ApiTimeoutSeconds = int.Parse(configuration["API_TIMEOUT_SECONDS"] ?? "30");
            options.ApiMaxRetries = int.Parse(configuration["API_MAX_RETRIES"] ?? "3");
            options.MaxBatchSize = int.Parse(configuration["MAX_BATCH_SIZE"] ?? "10");
            options.WarehouseReturnsEnv = configuration["WAREHOUSE_RETURNS_ENV"] ?? "development";
            options.VerifySsl = bool.Parse(configuration["VERIFY_SSL"] ?? "false");
            options.LogLevel = configuration["LOG_LEVEL"] ?? "Information";
        });
        
        // ===================================================================
        // HTTP CLIENT CONFIGURATION
        // ===================================================================
        services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
        {
            var baseUrl = configuration["EXTERNAL_API_BASE_URL"] ?? "https://apim-dev.nfm.com";
            var subscriptionKey = configuration["OCP_APIM_SUBSCRIPTION_KEY"];
            var timeout = TimeSpan.FromSeconds(double.Parse(configuration["API_TIMEOUT_SECONDS"] ?? "30"));
            
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = timeout;
            
            // Standard headers for external API communication
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "PieceInfoApi/1.0");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            
            // Add subscription key for API authentication
            if (!string.IsNullOrEmpty(subscriptionKey))
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() => 
        {
            var handler = new HttpClientHandler();
            
            // Enable automatic decompression for gzip and deflate responses
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VERIFY_SSL")) && 
                bool.Parse(Environment.GetEnvironmentVariable("VERIFY_SSL") ?? "false"))
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
            .Enrich.WithProperty("Environment", configuration["WAREHOUSE_RETURNS_ENV"] ?? "development")
            .Enrich.WithProperty("Application", "PieceInfoApi")
            .Enrich.WithProperty("Version", "1.0.0")
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.ApplicationInsights(
                configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"],
                TelemetryConverter.Traces)
            .CreateLogger();

        // Configure Serilog as the logging provider
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