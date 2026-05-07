// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using TollCents.ConsolePlayground;
using TollCents.Core;

ServiceProvider provider = CreateServiceProvider();
await provider.GetRequiredService<ConsoleCommandService>().CommandLoop();

static ServiceProvider CreateServiceProvider()
{
    IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: false)
        .Build();

    var services = new ServiceCollection();
    services.AddSingleton(configuration);
    services.Configure<List<RouteAnalysisEntry>>(configuration.GetSection("RouteAnalysisMatrix:Addresses"));
    services.RegisterGoogleMapsIntegration(configuration.GetSection("Integrations"));
    services.AddSerilog(loggerConfiguration => loggerConfiguration.ReadFrom.Configuration(configuration));

    // Command Orchestration
    services.AddSingleton<ConsoleCommandService>();
    services.AddSingleton<IInputOutputSystem, ConsoleIOSystem>();
    services.TryAddEnumerable(new List<ServiceDescriptor>
    { 
        ServiceDescriptor.Singleton<ICommandExecutor, ExistingRouteFileLoader>(),
        ServiceDescriptor.Singleton<ICommandExecutor, ManualAddressEntryExecutor>(),
        ServiceDescriptor.Singleton<ICommandExecutor, RouteMatrixAnalysisExecutor>()
    });

    return services.BuildServiceProvider();
}

public class RouteAnalysisEntry
{
    public required string Address1 { get; set; }

    public required string Address2 { get; set; }

    public bool Bidirectional { get; set; }
}
