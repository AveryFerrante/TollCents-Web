// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using TollCents.ConsolePlayground;
using TollCents.Core;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.TEXpress;


/* Nice to have:
 * Load test file routes (maybe all in a directory and can list them and allow selection).
 * Request by manually typing addresses. List of common ones for easy picking.
 * "Boosted" log levels? Enabling debug I guess
 * The "point" mapper tool
 */
ServiceProvider provider = CreateServiceProvider();
await provider.GetRequiredService<ConsoleCommandService>().CommandLoop();

//var gateway = provider.GetRequiredService<ITollInformationGateway>();

//const string startAddress = "109 E Woodbury Drive, Garland TX";
//const string endAddress = "220 E Las Colinas Blvd, Irving TX";
//var testData = await File.ReadAllTextAsync("C:\\Users\\avery\\Repositories\\TollCents-Web\\Data\\alex-to-las-colinas.json");
//var texPressCalculator = provider.GetRequiredService<ITEXpressTollPriceCalculator>();

//var obj = JsonSerializer.Deserialize<RoutesDirectionsResponse>(testData, new JsonSerializerOptions()
//{
//    PropertyNameCaseInsensitive = true,
//    // Custom converter from the GoogleApi NuGet package, necessary for deserialization.
//    Converters = { new JsonStringEnumConverterFactory() }
//});
//var routeLeg = obj!.Routes!.First().Legs!.First();
//var something = await texPressCalculator.GetTEXpressTollPrice(routeLeg.Steps!, hasTollTag: true);
//await texPressCalculator.PrintPoints(new TollCents.Core.Entities.Coordinate
//{
//    Latitude = 32.921148699999996,
//    Longitude = -96.8486624,
//}, "Dallas North Tollway to I-35.", true);

//var something = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
//{
//    StartAddress = startAddress,
//    EndAddress = endAddress,
//    IncludeTollPass = true,
//});



//if (something.SkipWaypoints.Any())
//{
//    Console.WriteLine("\n\n\nSkip waypoint(s) detected. Analyzing route with skip waypoints");
//    var response2 = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
//    {
//        StartAddress = startAddress,
//        EndAddress = endAddress,
//        IncludeTollPass = true,
//        ViaWaypoints = something.SkipWaypoints

//    });
//}

//something.ForEach(s => Console.WriteLine(string.Join("\n\n\n", s)));

//var response2 = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
//{
//    StartAddress = "220 E Las Colinas Blvd",
//    EndAddress = "13312 Meandering Way, Dallas TX",
//    IncludeTollPass = true,
//    //ViaWaypoints = new List<Coordinate>
//    //{
//    //    new Coordinate { Latitude = 32.90759310917281, Longitude = -96.90065177604905 },
//    //    new Coordinate { Latitude = 32.92003620597855, Longitude = -96.85075543321743 }
//    //}
//});
//Console.WriteLine(JsonSerializer.Serialize(response2));



static ServiceProvider CreateServiceProvider()
{
    IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: false)
        .Build();

    var services = new ServiceCollection();
    services.RegisterGoogleMapsIntegration(configuration);
    services.AddSerilog(loggerConfiguration => loggerConfiguration.ReadFrom.Configuration(configuration));

    // Command Orchestration
    services.AddSingleton<ConsoleCommandService>();
    services.AddSingleton<IInputOutputSystem, ConsoleIOSystem>();
    services.TryAddEnumerable(new List<ServiceDescriptor>
    { 
        ServiceDescriptor.Singleton<ICommandExecutor, ExistingRouteFileLoader>()
    });

    return services.BuildServiceProvider();
}
