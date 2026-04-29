using TollCents.Core.Entities;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress.Services
{
    public interface ITEXpressSegmentSkipAnomolies
    {
        bool ShouldSkipSegmentAnalysis(IEnumerable<NumberedTollRouteStep> routeSteps, int currentStepIndex);
    }

    public class TEXpressSegmentSkipAnomolies : ITEXpressSegmentSkipAnomolies
    {
        private readonly double _tollAccessPointMatchToleranceMiles;
        public TEXpressSegmentSkipAnomolies(IIntegrationsConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration.Integrations?.TEXpress,
                nameof(configuration.Integrations.TEXpress));
            var config = configuration.Integrations.TEXpress;
            _tollAccessPointMatchToleranceMiles = config.TollAccessPointMatchToleranceMiles;
        }

        public bool ShouldSkipSegmentAnalysis(IEnumerable<NumberedTollRouteStep> routeSteps, int currentStepIndex)
        {
            return CheckIfI35toEastBound635RampStep(routeSteps, currentStepIndex);
        }

        // typically identified with polyline
        // iyxgEvhlnQwHMyC@eCMoBYsBi@wBy@oBgAwAcA{AyAkAwAiAgBu@{AgAwCaAmD}BkO_AmH_AmIUMwAyF]aBu@yEMS
        private bool CheckIfI35toEastBound635RampStep(IEnumerable<NumberedTollRouteStep> routeSteps, int currentStepIndex)
        {
            var previousStep = routeSteps.FirstOrDefault(s => s.StepNumber == currentStepIndex - 1);
            if (previousStep == null) return false;

            var currentStep = routeSteps.First(s => s.StepNumber == currentStepIndex).Step;
            var startCoord = new Coordinate { Latitude = 32.9002066, Longitude = -96.8975605 };
            var endCoord = new Coordinate { Latitude = 32.9104301, Longitude = -96.8838014 };
            if (currentStep.StartLocation.ToCoordinate().GetDistanceToInMiles(startCoord) <= _tollAccessPointMatchToleranceMiles &&
                currentStep.EndLocation.ToCoordinate().GetDistanceToInMiles(endCoord) <= _tollAccessPointMatchToleranceMiles)
            {
                return true;
            }

            return false;
        }
    }
}
