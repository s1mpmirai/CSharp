namespace FoodStreetAudioGuide.Models
{
    public record StallItem(
        string DistanceText,
        string Name,
        string Rating,
        string Reviews,
        string Cuisine,
        string ScriptVi = "",
        string ScriptEn = "",
        string ScriptKo = "",
        string ScriptJa = "",
        string ScriptZh = "",
        string ImageUrl = ""
    );
}