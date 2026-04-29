// See https://aka.ms/new-console-template for more information
using GoogleApi.Entities.Common.Converters.Factories;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;
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
//await provider.GetRequiredService<ConsoleCommandService>().CommandLoop();

var gateway = provider.GetRequiredService<ITollInformationGateway>();
var texPressCalculator = provider.GetRequiredService<ITEXpressTollPriceCalculator>();

/***************************************
 * REAL REQUEST USING REAL GOOGLE MAPS *
 ****************************************
 */
//const string endAddress = "13312 Meandering Way, Dallas TX";
//const string startAddress = "220 E Las Colinas Blvd, Irving TX";
//var something = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
//{
//    StartAddress = startAddress,
//    EndAddress = endAddress,
//    IncludeTollPass = true,
//});

/*******************
 * TEST FILE ROUTE *
 *******************
 */
//var config = provider.GetRequiredService<IConfiguration>();
//var testDataFilePath = config.GetValue<string>("TestDataFilePath")
//    ?? throw new Exception("TestDataFilePath not found in configuration.");
//var testData = await File.ReadAllTextAsync(testDataFilePath);

//var obj = JsonSerializer.Deserialize<RoutesDirectionsResponse>(testData, new JsonSerializerOptions()
//{
//    PropertyNameCaseInsensitive = true,
//    // Custom converter from the GoogleApi NuGet package, necessary for deserialization.
//    Converters = { new JsonStringEnumConverterFactory() }
//});
//var routeLeg = obj!.Routes!.First().Legs!.First();
//var something = await texPressCalculator.GetTEXpressTollPrice(routeLeg.Steps!, hasTollTag: true);



/************************
 * MULTI ROUTE ANALYSIS *
 ************************
 */
var entries = provider.GetService<IOptions<List<RouteAnalysisEntry>>>();
if (entries is not null && entries.Value.Any())
{
    foreach (var entry in entries.Value)
    {
        Log.Debug("\n\n\n\n********************************************************");
        Log.Debug("********************************************************");
        Log.Debug("********************************************************");
        Log.Debug("********************************************************");
        Log.Debug($"Analyzing route from {entry.Address1} to {entry.Address2}");
        var response = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
        {
            StartAddress = entry.Address1,
            EndAddress = entry.Address2,
            IncludeTollPass = true,
        });
        if (entry.Bidirectional)
        {
            Log.Debug("\n\n\n\n********************************************************");
            Log.Debug("********************************************************");
            Log.Debug("********************************************************");
            Log.Debug("********************************************************");
            Console.WriteLine($"Analyzing route from {entry.Address2} to {entry.Address1}");
            var response2 = await gateway.GetRouteTollInformationTXAsync(new ByAddressRequest
            {
                StartAddress = entry.Address2,
                EndAddress = entry.Address1,
                IncludeTollPass = true,
            });
        }
    }
}


//await texPressCalculator.PrintPoints(new TollCents.Core.Entities.Coordinate
//{
//    Latitude = 32.921148699999996,
//    Longitude = -96.8486624,
//}, "Dallas North Tollway to I-35.", true);





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
    services.AddSingleton(configuration);
    services.Configure<List<RouteAnalysisEntry>>(configuration.GetSection("RouteAnalysis:Addresses"));
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

public class RouteAnalysisEntry
{
    public required string Address1 { get; set; }

    public required string Address2 { get; set; }

    public bool Bidirectional { get; set; }
}
