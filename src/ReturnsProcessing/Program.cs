using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WarehouseReturns.ReturnsProcessing.Models;
using WarehouseReturns.ReturnsProcessing.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Configure SharePoint settings
        services.Configure<SharePointConfiguration>(
            context.Configuration.GetSection("SharePoint"));
        
        // Register services
        services.AddScoped<ISharePointService, SharePointService>();
        
        // Add HTTP clients
        services.AddHttpClient();
        
        // Add logging
        services.AddLogging();
    })
    .Build();

host.Run();