using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress.Services
{
    public interface IAnalysisEvent : IRequest
    {
        string EventMessage { get; }
    }

    public class TEXpressAnalysisEventStream<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IAnalysisEvent
    {
        private readonly ILogger<TEXpressAnalysisEventStream<TRequest, TResponse>> _logger;
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public TEXpressAnalysisEventStream(ILogger<TEXpressAnalysisEventStream<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var serializedRequest = JsonSerializer.Serialize(request, _serializerOptions);
            _logger.LogDebug("{Data}", serializedRequest);
            return default!;
        }
    }

    public record StepEvent(string EventMessage) : IAnalysisEvent;

    public record StepInitialInfoEvent(string EventMessage, int StepNumber, string? StepInstruction, string? Polyline,
        IEnumerable<CardinalDirection>? CardinalDirections, string? Maneuver
    ) : IAnalysisEvent;

    public record StepTollAccessEvent(string EventMessage, string AccessType, string StepLatLong, string? MatchedSegment,
        string? MatchedAccessPoint, double? MatchedDistance, double? ActualClosestDistance, string? ActualClosestSegment,
        string? ActualClosestAccessPoint, IEnumerable<CardinalDirection>? ActualClosestCardinalDirections) : IAnalysisEvent;
}
