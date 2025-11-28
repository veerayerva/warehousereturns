using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VendorReturnsService.Configuration;
using VendorReturnsService.Services;
using VendorReturnsService.Utilities;

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
        // Configuration
        var sharePointSettings = new SharePointSettings();
        context.Configuration.Bind("SharePoint", sharePointSettings);
        services.AddSingleton(sharePointSettings);

        var documentIntelligenceSettings = new DocumentIntelligenceSettings();
        context.Configuration.Bind("DocumentIntelligence", documentIntelligenceSettings);
        services.AddSingleton(documentIntelligenceSettings);

        var pieceInfoSettings = new PieceInfoSettings();
        context.Configuration.Bind("PieceInfo", pieceInfoSettings);
        services.AddSingleton(pieceInfoSettings);

        var processingSettings = new ProcessingSettings();
        context.Configuration.Bind("Processing", processingSettings);
        services.AddSingleton(processingSettings);

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // HTTP Clients
        services.AddHttpClient("SharePoint");
        services.AddHttpClient("PieceInfo");
        services.AddHttpClient("DocumentIntelligence", client =>
        {
            client.BaseAddress = new Uri(documentIntelligenceSettings.ApiEndpoint);
            client.Timeout = TimeSpan.FromSeconds(documentIntelligenceSettings.TimeoutSeconds);
            if (!string.IsNullOrEmpty(documentIntelligenceSettings.ApiKey))
            {
                client.DefaultRequestHeaders.Add("x-functions-key", documentIntelligenceSettings.ApiKey);
            }
        });

        // Services
        services.AddScoped<ISharePointService, SharePointService>();
        services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
        services.AddScoped<IPieceInfoService, PieceInfoService>();
        services.AddScoped<IVendorReturnProcessor, VendorReturnProcessor>();
        services.AddScoped<ImageSourceHandler>();
    })
    .Build();

host.Run();

