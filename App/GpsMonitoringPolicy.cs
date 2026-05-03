namespace FoodStreetAudioGuide;

internal sealed class GpsMonitoringPolicy
{
    // Ngưỡng dịch chuyển tối thiểu để app coi vị trí mới là đủ khác và cần cập nhật UI.
    // Tăng giá trị này sẽ bớt rung UI nhưng phản hồi vị trí chậm hơn.
    public double MeaningfulMovementMeters { get; set; } = 2.5;
    // Ngưỡng dịch chuyển tối thiểu để app gọi lại API nearby stalls.
    // Tăng giá trị này sẽ giảm số lần refresh danh sách gần bạn nhưng dữ liệu mới đến chậm hơn.
    public double NearbyRefreshMovementMeters { get; set; } = 3.0;

    // Hàm `Evaluate`: xử lý logic liên quan trong file hiện tại.
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

    // Hàm `CalculateDistanceMeters`: tính toán giá trị cần dùng trong file hiện tại.
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

// Hàm `GpsMonitoringDecision`: xử lý logic liên quan trong file hiện tại.
internal readonly record struct GpsMonitoringDecision(
    // Có nên cập nhật UI theo vị trí mới hay không.
    bool ShouldUpdateUi,
    // Có nên gọi lại backend để lấy nearby stalls hay không.
    bool ShouldRefreshNearbyStalls,
    // Khoảng cách di chuyển kể từ lần cập nhật vị trí trước.
    double? MovementMeters,
    // Khoảng cách di chuyển kể từ lần fetch nearby gần nhất.
    double? MovementSinceNearbyFetchMeters);
