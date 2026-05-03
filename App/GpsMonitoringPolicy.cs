namespace FoodStreetAudioGuide;

internal sealed class GpsMonitoringPolicy
{
    public double MeaningfulMovementMeters { get; set; } = 2.5;
    public double NearbyRefreshMovementMeters { get; set; } = 3.0;

    public GpsMonitoringDecision Evaluate(
        Location? previousLocation,
        Location currentLocation,
        Location? lastNearbyFetchLocation,
        bool canRefreshNearbyStalls)
    {
        var movedDistanceMeters = previousLocation is null
            ? double.MaxValue
            : CalculateDistanceMeters(previousLocation, currentLocation);
        var movedEnoughToUpdate = previousLocation is null || movedDistanceMeters >= MeaningfulMovementMeters;

        var movedSinceNearbyFetchMeters = lastNearbyFetchLocation is null
            ? double.MaxValue
            : CalculateDistanceMeters(lastNearbyFetchLocation, currentLocation);
        var shouldRefreshNearbyStalls = canRefreshNearbyStalls &&
                                        (lastNearbyFetchLocation is null ||
                                         movedSinceNearbyFetchMeters >= NearbyRefreshMovementMeters);

        return new GpsMonitoringDecision(
            movedEnoughToUpdate,
            shouldRefreshNearbyStalls,
            previousLocation is null ? null : movedDistanceMeters,
            lastNearbyFetchLocation is null ? null : movedSinceNearbyFetchMeters);
    }

    private static double CalculateDistanceMeters(Location from, Location to)
    {
        return Location.CalculateDistance(
                   from.Latitude,
                   from.Longitude,
                   to.Latitude,
                   to.Longitude,
                   DistanceUnits.Kilometers) * 1000d;
    }
}

internal readonly record struct GpsMonitoringDecision(
    bool ShouldUpdateUi,
    bool ShouldRefreshNearbyStalls,
    double? MovementMeters,
    double? MovementSinceNearbyFetchMeters);
