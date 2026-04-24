using GoogleApi.Entities.Common.Converters.Factories;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.ConsolePlayground
{
    public class ConsoleCommandService(
        ILogger<ConsoleCommandService> _logger,
        IInputOutputSystem _ioSystem,
        IEnumerable<ICommandExecutor> _commandExecutors,
        ITollInformationGateway _tollInfoGateway) : CommandReaderBase(_ioSystem)
    {
        private readonly IEnumerable<string> _mainOptions = new List<string>
        {
            "Load existing route data from file.",
            "Manually enter route addresses.",
            "Coordinate lookup tool."
        };
        public async Task CommandLoop()
        {
            while (true)
            {
                _ioSystem.WriteLine("Please select an option from below:");
                var selectionIndex = GetUserSelectionIndex(_mainOptions);
                var executor = _commandExecutors.FirstOrDefault(c =>
                    c.CommandExecutorDiscriminator == (CommandExecutorDiscriminator)selectionIndex);
                ArgumentNullException.ThrowIfNull(executor, $"No command executor found for selection index {selectionIndex}");
                await executor.ExecuteCommandAsync(); 
            }
        }
    }

    public abstract class CommandReaderBase(IInputOutputSystem _ioSystem)
    {
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
    }

    public class CommandStep
    {
        public required string CommandHeading { get; set; }

        public IEnumerable<string>? StaticCommandOptions { get; set; }

        public Func<IEnumerable<string>>? DynamicCommandOptionsGenerator { get; set; }

        public Dictionary<int, Func<int, Task>>? ExecuteCommandStep { get; set; }

        public CommandStep? NextCommand { get; set; }
    }

    public interface ICommandExecutor
    {
        CommandExecutorDiscriminator CommandExecutorDiscriminator { get; }
        Task ExecuteCommandAsync();
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

        public IEnumerable<CommandStep> GetCommandSteps()
        {
            return new List<CommandStep>
            {
                new CommandStep
                {
                    CommandHeading = "Select a file to load the route data.",
                    DynamicCommandOptionsGenerator = GetFileNames,
                },
                new CommandStep
                {
                    CommandHeading = "What would you like to do with the loaded route data?",
                    StaticCommandOptions = new List<string>
                    {
                        "Print the route data to the console.",
                        "Run TEXpress analysis for the route."
                    }
                }
            };
        }

        private IEnumerable<string> GetFileNames()
        {
            var filePaths = Directory.GetFiles(_directoryPath, "*.json");
            var fileNames = filePaths.Select(Path.GetFileName).ToList();
            if (fileNames is null || !fileNames.Any() || fileNames.All(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("No route files found in the specified directory.");
            }
            _filePaths = filePaths;
            return fileNames;
        }

        public async Task ExecuteCommandAsync()
        {
            var filePaths = Directory.GetFiles(_directoryPath, "*.json");
            var fileNames = filePaths.Select(Path.GetFileName).ToList();
            if (fileNames is null || !fileNames.Any())
            {
                _ioSystem.WriteLine($"No route files found in the specified directory: {_directoryPath}");
                return;
            }
            _ioSystem.WriteLine("Select a file to load the route data.");
            var selectionIndex = GetUserSelectionIndex(fileNames);


            var fileData = await File.ReadAllTextAsync(filePaths[selectionIndex]);
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
                    _ioSystem.WriteLine("Calculating tolls for the route...");
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
        }
    }
