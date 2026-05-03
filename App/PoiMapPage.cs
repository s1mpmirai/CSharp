using Microsoft.Maui.Controls.Shapes;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide;

public sealed class PoiMapPage : ContentPage
{
    private readonly IReadOnlyList<StallItem> _stalls;
    private readonly Func<StallItem, Task> _openStallAsync;
    private readonly LocalizedText _text;
    private readonly WebView _mapView;
    private readonly Border _detailCard;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private StallItem? _selectedStall;

    // Hàm khởi tạo `PoiMapPage`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
    public PoiMapPage(
        IReadOnlyList<StallItem> stalls,
        Location? userLocation,
        LocalizedText text,
        Func<StallItem, Task> openStallAsync,
        int? preferredStallId = null)
    {
        _stalls = stalls;
        _text = text;
        _openStallAsync = openStallAsync;

        Title = text.MapTitle;
        BackgroundColor = Color.FromArgb("#F6F6F6");

        var nearestStallId = preferredStallId ?? GetNearestStallId(stalls, userLocation);

        var payload = stalls
            .Where(item => item.Lat != 0 && item.Lng != 0)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                cuisine = item.Cuisine,
                lat = item.Lat,
                lng = item.Lng,
                distanceText = item.DistanceText
            })
            .ToList();

        _mapView = new WebView
        {
            Source = new HtmlWebViewSource
            {
                Html = PoiMapHtmlFactory.Create(
                    payload,
                    userLocation?.Latitude,
                    userLocation?.Longitude,
                    nearestStallId,
                    text.MapOpenDetailText,
                    text.MapUserLocationText,
                    text.MapNearestRoutePrefix,
                    text.MapRouteUnavailableText)
            }
        };
        _mapView.Navigating += OnMapNavigating;

        _titleLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2738")
        };

        _subtitleLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#607086")
        };

        var openButton = new Button
        {
            Text = text.MapOpenDetailText,
            BackgroundColor = Color.FromArgb("#EF8F2A"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 16,
            HeightRequest = 40
        };
        openButton.Clicked += OnOpenStallClicked;

        _detailCard = new Border
        {
            IsVisible = false,
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(14, 12),
            Margin = new Thickness(12),
            VerticalOptions = LayoutOptions.End,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    _titleLabel,
                    _subtitleLabel,
                    openButton
                }
            }
        };

        var closeButton = new Button
        {
            Text = text.CloseText,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#1F2738"),
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 18,
            WidthRequest = 76,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(12, 20, 0, 0)
        };
        closeButton.Clicked += async (_, _) => await Navigation.PopModalAsync();

        Content = new Grid
        {
            Children =
            {
                _mapView,
                closeButton,
                _detailCard
            }
        };
    }

    // Hàm `OnMapNavigating`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
    private void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Url) || !e.Url.StartsWith("foodstreet://stall/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        var stallIdText = e.Url.Split('/').LastOrDefault();
        if (!int.TryParse(stallIdText, out var stallId))
        {
            return;
        }

        _selectedStall = _stalls.FirstOrDefault(item => item.Id == stallId);
        if (_selectedStall is null)
        {
            return;
        }

        _titleLabel.Text = _selectedStall.Name;
        _subtitleLabel.Text = string.Join(" • ", new[] { _selectedStall.Cuisine, _selectedStall.DistanceText }.Where(item => !string.IsNullOrWhiteSpace(item)));
        _detailCard.IsVisible = true;
    }

    // Hàm `OnOpenStallClicked`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
    private async void OnOpenStallClicked(object? sender, EventArgs e)
    {
        if (_selectedStall is null)
        {
            return;
        }

        await Navigation.PopModalAsync();
        await _openStallAsync(_selectedStall);
    }
    // Hàm `GetNearestStallId`: lấy dữ liệu hoặc giá trị cần dùng trong file hiện tại.
    private static int? GetNearestStallId(IReadOnlyList<StallItem> stalls, Location? userLocation)
    {
        var candidates = stalls.Where(item => item.Lat != 0 && item.Lng != 0).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        if (userLocation is not null)
        {
            return candidates
                .OrderBy(item => Location.CalculateDistance(
                    userLocation,
                    new Location(item.Lat, item.Lng),
                    DistanceUnits.Kilometers))
                .Select(item => (int?)item.Id)
                .FirstOrDefault();
        }

        return candidates
            .OrderBy(item => item.Distance > 0 ? item.Distance : double.MaxValue)
            .Select(item => (int?)item.Id)
            .FirstOrDefault();
    }
}
