using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WarehouseReturns.DocumentIntelligence.Configuration;
using WarehouseReturns.DocumentIntelligence.Repositories;
using WarehouseReturns.DocumentIntelligence.Services;

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
        var documentIntelligenceSettings = new DocumentIntelligenceSettings();
        context.Configuration.Bind("DocumentIntelligence", documentIntelligenceSettings);
        services.AddSingleton(documentIntelligenceSettings);

        var blobStorageSettings = new BlobStorageSettings();
        context.Configuration.Bind("BlobStorage", blobStorageSettings);
        services.AddSingleton(blobStorageSettings);

        var documentProcessingSettings = new DocumentProcessingSettings();
        context.Configuration.Bind("Processing", documentProcessingSettings);
        services.AddSingleton(documentProcessingSettings);
        
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // HTTP Client
        services.AddHttpClient();
        
        // Services
        services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        
        // Repositories
        services.AddScoped<IBlobStorageRepository, BlobStorageRepository>();
    })
    .Build();

host.Run();