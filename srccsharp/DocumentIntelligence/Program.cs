using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using WarehouseReturns.DocumentIntelligence.Services;
using WarehouseReturns.DocumentIntelligence.Repositories;
using WarehouseReturns.DocumentIntelligence.Configuration;

namespace WarehouseReturns.DocumentIntelligence;

/// <summary>
/// Azure Functions Host Program for Document Intelligence Application
/// 
/// Configures dependency injection, logging, and services for document analysis
/// using Azure Document Intelligence API with blob storage integration.
/// </summary>
public static class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
                
                if (context.HostingEnvironment.IsDevelopment())
                {
                    config.AddUserSecrets("WarehouseReturns.DocumentIntelligence");
                }
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
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
                            Title = "Document Intelligence API",
                            Description = "API for document analysis using Azure Document Intelligence with blob storage integration",
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
                // APPLICATION INSIGHTS AND LOGGING CONFIGURATION
                // ===================================================================
                services.AddApplicationInsightsTelemetryWorkerService();
                
                // ===================================================================
                // HTTP CLIENT CONFIGURATION
                // ===================================================================
                services.AddHttpClient();
                
                // ===================================================================
                // CONFIGURATION BINDING
                // ===================================================================
                // Configure Document Intelligence Settings
                services.Configure<DocumentIntelligenceSettings>(
                    configuration.GetSection("Values"));
                
                // Configure Blob Storage Settings
                services.Configure<BlobStorageSettings>(
                    configuration.GetSection("Values"));
                
                // Configure Processing Settings
                services.Configure<DocumentProcessingSettings>(
                    configuration.GetSection("Values"));
                
                // ===================================================================
                // SERVICE REGISTRATION
                // ===================================================================
                services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
                services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
                
                // Register Repositories
                services.AddScoped<IBlobStorageRepository, BlobStorageRepository>();
                
                // ===================================================================
                // LOGGING CONFIGURATION
                // ===================================================================
                services.AddLogging(builder =>
                {
                    builder.AddApplicationInsights();
                    builder.AddConsole();
                    
                    var logLevel = configuration.GetValue<string>("Values:LOG_LEVEL");
                    if (Enum.TryParse<LogLevel>(logLevel, out var level))
                    {
                        builder.SetMinimumLevel(level);
                    }
                });
            })
            .Build();

        host.Run();
    }
}