namespace FoodStreetAudioGuide;

internal static class OpenStreetMapHtmlFactory
{
    public static string Create(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        double centerLat,
        double centerLng)
    {
        return MapLibreHtmlFactory.Create(minLat, maxLat, minLng, maxLng, centerLat, centerLng);
    }
}
