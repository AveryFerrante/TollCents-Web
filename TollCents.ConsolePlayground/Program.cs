// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TollCents.Core;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;

ServiceProvider provider = CreateServiceProvider();

var gateway = provider.GetRequiredService<ITollInformationGateway>();
var ai = provider.GetRequiredService<Kernel>();

var response = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
{
    StartAddress = "",
    EndAddress = "",
    IncludeTollPass = true,
    ViaWaypoints = new List<Coordinate>
    {
        // new Coordinate { Latitude = 32.8374489, Longitude = -97.0624648 },
        // new Coordinate { Latitude = 32.8733542, Longitude = -96.8978273 },
        new Coordinate { Latitude = 32.9213473, Longitude = -96.8476687 },
    }
});

//Console.WriteLine(JsonSerializer.Serialize(response));



static ServiceProvider CreateServiceProvider()
{
    IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.Development.json", optional: false)
        .Build();

    var services = new ServiceCollection();
    services.RegisterGoogleMapsIntegration(configuration);
    services.AddKernel().AddOpenAIChatClient(
        modelId: "qwen/qwen3.5-9b",
        apiKey: "lm-studio", // placeholder value, not actually used by LM Studio
        endpoint: new Uri("http://localhost:1234/v1")
    );

    var provider = services.BuildServiceProvider();
    return provider;
}
