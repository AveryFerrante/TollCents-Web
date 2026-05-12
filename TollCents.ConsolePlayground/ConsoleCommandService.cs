using GoogleApi.Entities.Common.Converters.Factories;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.ConsolePlayground
{
    public class ConsoleCommandService : CommandReaderBase
    {
        private readonly ILogger<ConsoleCommandService> _logger;
        private readonly IEnumerable<ICommandExecutor> _commandExecutors;
        private readonly ITollInformationGateway _tollInfoGateway;

        public ConsoleCommandService(
            ILogger<ConsoleCommandService> logger,
            IInputOutputSystem ioSystem,
            IEnumerable<ICommandExecutor> commandExecutors,
            ITollInformationGateway tollInfoGateway) : base(ioSystem)
        {
            _logger = logger;
            _commandExecutors = commandExecutors;
            _tollInfoGateway = tollInfoGateway;
        }

        private readonly IEnumerable<string> _mainOptions = new List<string>
        {
            "Load existing route data from file.",
            "Manually enter route addresses.",
            "Run Route Matrix",
            "Exit",
        };


        public async Task CommandLoop()
        {
            while (true)
            {
                _ioSystem.ClearScreen();
                _ioSystem.WriteLine("Please select an option from below:");
                var selectionIndex = GetUserSelectionIndex(_mainOptions);

                if (selectionIndex == _mainOptions.Count() - 1) // Exit option
                {
                    _ioSystem.WriteLine("Exiting the application. Goodbye!");
                    break;
                }

                var executor = _commandExecutors.FirstOrDefault(c =>
                    c.CommandExecutorDiscriminator == (CommandExecutorDiscriminator)selectionIndex);
                ArgumentNullException.ThrowIfNull(executor, $"No command executor found for selection index {selectionIndex}");
                await executor.ExecuteCommandAsync();
                _ioSystem.WriteLine("Press any key to return to the main menu...");
                var _ = _ioSystem.GetUserInput();
            }
        }
    }

    public abstract class CommandReaderBase
    {
        protected readonly IInputOutputSystem _ioSystem;

        protected CommandReaderBase(IInputOutputSystem ioSystem)
        {
            _ioSystem = ioSystem;
        }

        protected int GetUserSelectionIndex(IEnumerable<string> options)
        {
            for (int i = 0; i < options.Count(); i++)
            {
                _ioSystem.WriteLine($"{i + 1}. {options.ElementAt(i)}");
            }
            while (true)
            {
                _ioSystem.Write("Enter the number of your selection: ");
                var input = Console.ReadLine();
                if (int.TryParse(input, out int selection) && selection >= 1 && selection <= options.Count())
                {
                    return selection - 1; // Return zero-based index
                }
                else
                {
                    _ioSystem.WriteLine("Invalid selection. Please try again.");
                }
            }
        }
    }

    public enum CommandExecutorDiscriminator
    {
        // Right now, needs to match index of the main options list in ConsoleCommandService.
        ExistingFileLoader = 0,
        ManualAddressEntry = 1,
        RouteMatrixAnalysis = 2
    }

    public interface ICommandExecutor
    {
        CommandExecutorDiscriminator CommandExecutorDiscriminator { get; }
        Task ExecuteCommandAsync();
    }

    public class RouteMatrixAnalysisExecutor(IInputOutputSystem _ioSystem, ITollInformationGateway _tollInfoGateway,
        IOptions<List<RouteAnalysisEntry>> matrixEntries)
        : CommandReaderBase(_ioSystem), ICommandExecutor
    {
        CommandExecutorDiscriminator ICommandExecutor.CommandExecutorDiscriminator =>
            CommandExecutorDiscriminator.RouteMatrixAnalysis;
        public async Task ExecuteCommandAsync()
        {
            matrixEntries.Value.ForEach(entry =>
            {
                _ioSystem.WriteLine($"Start Address: {entry.Address1}, End Address: {entry.Address2}. " +
                    $"Birdirectional {entry.Bidirectional}");
            });
            _ioSystem.WriteLine("Press any key to begin");
            var _ = _ioSystem.GetUserInput();

            foreach (var entry in matrixEntries.Value)
            {
                _ioSystem.WriteLine("\n\n\n");
                _ioSystem.WriteLine("********************************************************");
                _ioSystem.WriteLine("********************************************************");
                _ioSystem.WriteLine("********************************************************");
                _ioSystem.WriteLine($"Analyzing route from {entry.Address1} to {entry.Address2}");
                var response = await _tollInfoGateway.GetRouteTollInformationTXAsync(new ByAddressRequest
                {
                    StartAddress = entry.Address1,
                    EndAddress = entry.Address2,
                    IncludeTollPass = true,
                });
                if (entry.Bidirectional)
                {
                    _ioSystem.WriteLine("********************************************************");
                    _ioSystem.WriteLine("********************************************************");
                    _ioSystem.WriteLine("********************************************************");
                    _ioSystem.WriteLine($"Analyzing route from {entry.Address2} to {entry.Address1}");
                    var response2 = await _tollInfoGateway.GetRouteTollInformationTXAsync(new ByAddressRequest
                    {
                        StartAddress = entry.Address2,
                        EndAddress = entry.Address1,
                        IncludeTollPass = true,
                    });
                }
            }
        }
    }

    public class ManualAddressEntryExecutor(IInputOutputSystem _ioSystem, ITollInformationGateway _tollInfoGateway)
        : CommandReaderBase(_ioSystem), ICommandExecutor
    {
        CommandExecutorDiscriminator ICommandExecutor.CommandExecutorDiscriminator =>
            CommandExecutorDiscriminator.ManualAddressEntry;
        public async Task ExecuteCommandAsync()
        {
            _ioSystem.WriteLine("Please enter the start address:");
            var startAddress = _ioSystem.GetUserInput();
            _ioSystem.WriteLine("Please enter the end address:");
            var endAddress = _ioSystem.GetUserInput();
            if (string.IsNullOrWhiteSpace(startAddress) || string.IsNullOrWhiteSpace(endAddress))
            {
                _ioSystem.WriteLine("Start and end addresses cannot be empty. Please try again.");
                return;
            }
            _ioSystem.WriteLine("Calculating toll information...");
            var response = await _tollInfoGateway.GetRouteTollInformationTXAsync(new ByAddressRequest
            {
                StartAddress = startAddress,
                EndAddress = endAddress,
                IncludeTollPass = true
            });
            _ioSystem.WriteLine("Toll Information Response:");
            _ioSystem.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public class ExistingRouteFileLoader(IInputOutputSystem _ioSystem, ITEXpressTollPriceCalculator _texpressCalculator)
        : CommandReaderBase(_ioSystem), ICommandExecutor
    {
        // TODO: Make configurable
        private const string _directoryPath = @"C:\Users\avery\Repositories\TollCents-Web\TollCents.ConsolePlayground\RouteFiles";

        private IEnumerable<string>? _filePaths { get; set; }

        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            // Custom converter from the GoogleApi NuGet package, necessary for deserialization.
            Converters = { new JsonStringEnumConverterFactory() }
        };

        CommandExecutorDiscriminator ICommandExecutor.CommandExecutorDiscriminator =>
            CommandExecutorDiscriminator.ExistingFileLoader;

        public async Task ExecuteCommandAsync()
        {
            string[] filePaths = Directory.GetFiles(_directoryPath, "*.json");
            IEnumerable<string> fileNames = filePaths?
                .Select(Path.GetFileName)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(x => x!) ?? [];

            if (fileNames is null || !fileNames.Any())
            {
                _ioSystem.WriteLine($"No route files found in the specified directory: {_directoryPath}");
                return;
            }
            _ioSystem.WriteLine("Select a file to load the route data.");
            var selectionIndex = GetUserSelectionIndex(fileNames);


            var fileData = await File.ReadAllTextAsync(filePaths![selectionIndex]);
            RoutesDirectionsResponse directionsData = JsonSerializer.Deserialize<RoutesDirectionsResponse>(fileData, _jsonSerializerOptions)
                ?? throw new InvalidOperationException("Failed to deserialize the route data from the selected file.");


            _ioSystem.WriteLine("What would you like to do with the loaded route data?");
            IEnumerable<string> options = new List<string>
            {
                "Print the route data to the console.",
                "Run TEXpress analysis for the route."
            };
            selectionIndex = GetUserSelectionIndex(options);

            switch (selectionIndex)
            {
                case 0:
                    _ioSystem.WriteLine("Route Data:");
                    _ioSystem.WriteLine(JsonSerializer.Serialize(directionsData, new JsonSerializerOptions { WriteIndented = true }));
                    break;
                case 1:
                    _ioSystem.ClearScreen();
                    _ioSystem.WriteLine("Calculating TEXpress tolls for the route...");
                    var routeSteps = directionsData.Routes.First().Legs.First().Steps;
                    await _texpressCalculator.GetTEXpressTollPrice(routeSteps, hasTollTag: true);
                    break;
                default:
                    _ioSystem.WriteLine("Invalid selection.");
                    break;
            }
        }
    }

    // I/O System
    public interface IInputOutputSystem
    {
        void Write(string message);

        void WriteLine(string message);

        string? GetUserInput();
        void ClearScreen();
    }

    public class ConsoleIOSystem : IInputOutputSystem
    {
        public string? GetUserInput()
        {
            return Console.ReadLine();
        }

        public void Write(string message)
        {
            Console.Write(message);
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void ClearScreen()
        {
            Console.Clear();
        }
    }
}
