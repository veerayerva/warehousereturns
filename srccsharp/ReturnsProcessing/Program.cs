using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WarehouseReturns.ReturnsProcessing.Services;
using WarehouseReturns.ReturnsProcessing.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Add configuration sources
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Register configuration settings
        var configuration = context.Configuration;
        services.Configure<AppSettings>(configuration);
        
        // Configure SharePoint settings with prefix
        services.Configure<SharePointSettings>(options =>
        {
            options.AUTHENTICATION_METHOD = configuration["SHAREPOINT_AUTHENTICATION_METHOD"] ?? "ManagedIdentity";
            options.TENANT_ID = configuration["SHAREPOINT_TENANT_ID"] ?? string.Empty;
            options.CLIENT_ID = configuration["SHAREPOINT_CLIENT_ID"] ?? string.Empty;
            options.CLIENT_SECRET = configuration["SHAREPOINT_CLIENT_SECRET"] ?? string.Empty;
            options.SHAREPOINT_SITE_URL = configuration["SHAREPOINT_SITE_URL"] ?? string.Empty;
            options.SHAREPOINT_LIST_ID = configuration["SHAREPOINT_LIST_ID"] ?? string.Empty;
        });

        services.Configure<DocumentIntelligenceApiSettings>(configuration.GetSection("Values"));
        services.Configure<PieceInfoApiSettings>(configuration.GetSection("Values"));
        services.Configure<ProcessingSettings>(configuration.GetSection("Values"));

        // Register all services
        services.AddScoped<ISharePointService, SharePointService>();
        services.AddScoped<IDocumentIntelligenceApiService, DocumentIntelligenceApiService>();
        services.AddScoped<IPieceInfoApiService, PieceInfoApiService>();
        services.AddScoped<IReturnsProcessingService, ReturnsProcessingService>();
        
        // Configure HTTP clients with proper settings
        services.AddHttpClient<IDocumentIntelligenceApiService, DocumentIntelligenceApiService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<DocumentIntelligenceApiSettings>>().Value;
            client.BaseAddress = new Uri(settings.DOCUMENT_INTELLIGENCE_ENDPOINT);
            client.Timeout = TimeSpan.FromSeconds(settings.DOCUMENT_INTELLIGENCE_TIMEOUT_SECONDS);
        });

        services.AddHttpClient<IPieceInfoApiService, PieceInfoApiService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<PieceInfoApiSettings>>().Value;
            client.BaseAddress = new Uri(settings.PIECE_INFO_API_ENDPOINT);
            client.Timeout = TimeSpan.FromSeconds(settings.PIECE_INFO_API_TIMEOUT_SECONDS);
        });

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

host.Run();