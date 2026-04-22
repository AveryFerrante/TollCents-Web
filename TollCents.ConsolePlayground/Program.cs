// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;
using TollCents.Core;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;

ServiceProvider provider = CreateServiceProvider();

var gateway = provider.GetRequiredService<ITollInformationGateway>();

//var response1 = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
//{
//    StartAddress = "Dominion at Mercer Crossing, 11771 Mira Lago Blvd, Dallas, TX 75234",
//    EndAddress = "13312 Meandering Way, Dallas TX",
//    IncludeTollPass = true
//});
//Console.WriteLine(JsonSerializer.Serialize(response1) + "\n\n\n");
var response2 = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
{
    StartAddress = "220 E Las Colinas Blvd",
    EndAddress = "13312 Meandering Way, Dallas TX",
    IncludeTollPass = true,
    //ViaWaypoints = new List<Coordinate>
    //{
    //    new Coordinate { Latitude = 32.90759310917281, Longitude = -96.90065177604905 },
    //    new Coordinate { Latitude = 32.92003620597855, Longitude = -96.85075543321743 }
    //}
});
Console.WriteLine(JsonSerializer.Serialize(response2));



static ServiceProvider CreateServiceProvider()
{
    IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddJsonFile($"appsettings.json", optional: false)
        .Build();

    var services = new ServiceCollection();
    services.RegisterGoogleMapsIntegration(configuration);
    services.AddSerilog(loggerConfiguration => loggerConfiguration.ReadFrom.Configuration(configuration));

    return services.BuildServiceProvider();
}
