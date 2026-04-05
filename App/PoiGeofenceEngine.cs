using System.Diagnostics;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide;

internal sealed class PoiGeofenceEngine
{
    private readonly HashSet<int> _insideStalls = new();
    private readonly Dictionary<int, DateTime> _lastTriggeredAtUtc = new();
    private readonly Dictionary<int, int> _consecutiveInsideSamples = new();

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(5);
    public double MaxAcceptedAccuracyMeters { get; set; } = 18;
    public double MinimumEntryMarginMeters { get; set; } = 3;
    public double ExitMarginMeters { get; set; } = 6;
    public int RequiredConsecutiveSamples { get; set; } = 2;

    public StallItem? Evaluate(Location userLocation, IReadOnlyCollection<StallItem> stalls, DateTime utcNow)
    {
        var accuracyMeters = userLocation.Accuracy ?? double.MaxValue;
        if (accuracyMeters > MaxAcceptedAccuracyMeters)
        {
            Debug.WriteLine($"--- POI BO QUA do accuracy cao: {accuracyMeters:0.##}m > {MaxAcceptedAccuracyMeters:0.##}m");
            return null;
        }

        var eligibleStalls = stalls
            .Where(stall => stall.Id > 0 && stall.PoiRadiusMeters > 0 && stall.Lat != 0 && stall.Lng != 0)
            .Select(stall => new
            {
                Stall = stall,
                DistanceMeters = Location.CalculateDistance(
                    userLocation.Latitude,
                    userLocation.Longitude,
                    stall.Lat,
                    stall.Lng,
                    DistanceUnits.Kilometers) * 1000
            })
            .OrderBy(item => item.DistanceMeters)
            .ThenByDescending(item => item.Stall.PoiRadiusMeters)
            .ToList();

        var persistedInsideIds = eligibleStalls
            .Where(item => _insideStalls.Contains(item.Stall.Id) && IsInsideExitBoundary(item.DistanceMeters, item.Stall.PoiRadiusMeters))
            .Select(item => item.Stall.Id)
            .ToHashSet();
        _insideStalls.RemoveWhere(id => !persistedInsideIds.Contains(id));

        var candidateEntries = eligibleStalls
            .Where(item => IsInsideEntryBoundary(item.DistanceMeters, item.Stall.PoiRadiusMeters, accuracyMeters))
            .ToList();

        var candidateIds = candidateEntries.Select(item => item.Stall.Id).ToHashSet();
        foreach (var stallId in _consecutiveInsideSamples.Keys.Except(candidateIds).ToList())
        {
            _consecutiveInsideSamples.Remove(stallId);
        }

        foreach (var item in candidateEntries)
        {
            var stallId = item.Stall.Id;
            _consecutiveInsideSamples[stallId] = _consecutiveInsideSamples.TryGetValue(stallId, out var currentSamples)
                ? currentSamples + 1
                : 1;

            if (_insideStalls.Contains(stallId))
            {
                continue;
            }

            if (_consecutiveInsideSamples[stallId] < RequiredConsecutiveSamples)
            {
                continue;
            }

            if (_lastTriggeredAtUtc.TryGetValue(stallId, out var lastTriggeredUtc)
                && utcNow - lastTriggeredUtc < Cooldown)
            {
                continue;
            }

            _insideStalls.Add(stallId);
            _lastTriggeredAtUtc[stallId] = utcNow;
            Debug.WriteLine($"--- POI TRIGGER stall={item.Stall.Id} distance={item.DistanceMeters:0.##}m radius={item.Stall.PoiRadiusMeters:0.##}m accuracy={accuracyMeters:0.##}m samples={_consecutiveInsideSamples[stallId]}");
            return item.Stall;
        }

        if (stalls.Count > 0)
        {
            var nearest = stalls
                .Where(stall => stall.Id > 0 && stall.PoiRadiusMeters > 0 && stall.Lat != 0 && stall.Lng != 0)
                .Select(stall => new
                {
                    Stall = stall,
                    DistanceMeters = Location.CalculateDistance(
                        userLocation.Latitude,
                        userLocation.Longitude,
                        stall.Lat,
                        stall.Lng,
                        DistanceUnits.Kilometers) * 1000
                })
                .OrderBy(item => item.DistanceMeters)
                .FirstOrDefault();

            if (nearest is not null)
            {
                Debug.WriteLine($"--- POI CHUA VAO VUNG stall={nearest.Stall.Id} distance={nearest.DistanceMeters:0.##}m radius={nearest.Stall.PoiRadiusMeters:0.##}m accuracy={accuracyMeters:0.##}m");
            }
        }

        return null;
    }

    public void Reset()
    {
        _insideStalls.Clear();
        _lastTriggeredAtUtc.Clear();
        _consecutiveInsideSamples.Clear();
    }

    private bool IsInsideEntryBoundary(double distanceMeters, double poiRadiusMeters, double accuracyMeters)
    {
        var safetyMargin = Math.Max(
            MinimumEntryMarginMeters,
            Math.Min(accuracyMeters * 0.45, poiRadiusMeters * 0.35));
        return distanceMeters <= Math.Max(1, poiRadiusMeters - safetyMargin);
    }

    private bool IsInsideExitBoundary(double distanceMeters, double poiRadiusMeters)
    {
        return distanceMeters <= poiRadiusMeters + Math.Max(ExitMarginMeters, MinimumEntryMarginMeters);
    }
}
