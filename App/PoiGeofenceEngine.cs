using System.Diagnostics;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide;

internal sealed class PoiGeofenceEngine
{
    private readonly HashSet<int> _insideStalls = new();
    private readonly Dictionary<int, DateTime> _lastTriggeredAtUtc = new();

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(5);
    public double MaxAcceptedAccuracyMeters { get; set; } = 18;
    public double MinimumEntryMarginMeters { get; set; } = 3;

    public StallItem? Evaluate(Location userLocation, IReadOnlyCollection<StallItem> stalls, DateTime utcNow)
    {
        var accuracyMeters = userLocation.Accuracy ?? double.MaxValue;
        if (accuracyMeters > MaxAcceptedAccuracyMeters)
        {
            Debug.WriteLine($"--- POI BO QUA do accuracy cao: {accuracyMeters:0.##}m > {MaxAcceptedAccuracyMeters:0.##}m");
            return null;
        }

        var candidates = stalls
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
            .Where(item =>
            {
                var safetyMargin = Math.Max(
                    MinimumEntryMarginMeters,
                    Math.Min(accuracyMeters * 0.45, item.Stall.PoiRadiusMeters * 0.35));
                return item.DistanceMeters <= Math.Max(1, item.Stall.PoiRadiusMeters - safetyMargin);
            })
            .OrderBy(item => item.DistanceMeters)
            .ThenByDescending(item => item.Stall.PoiRadiusMeters)
            .ToList();

        var currentInsideIds = candidates.Select(item => item.Stall.Id).ToHashSet();
        _insideStalls.RemoveWhere(id => !currentInsideIds.Contains(id));

        foreach (var item in candidates)
        {
            var stallId = item.Stall.Id;
            var justEntered = _insideStalls.Add(stallId);
            if (!justEntered)
            {
                continue;
            }

            if (_lastTriggeredAtUtc.TryGetValue(stallId, out var lastTriggeredUtc)
                && utcNow - lastTriggeredUtc < Cooldown)
            {
                continue;
            }

            _lastTriggeredAtUtc[stallId] = utcNow;
            Debug.WriteLine($"--- POI TRIGGER stall={item.Stall.Id} distance={item.DistanceMeters:0.##}m radius={item.Stall.PoiRadiusMeters:0.##}m accuracy={accuracyMeters:0.##}m");
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
    }
}
