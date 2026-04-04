using Microsoft.Maui.Controls.Shapes;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace FoodStreetAudioGuide;

public sealed class QrScannerModalPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CameraBarcodeReaderView _cameraView;
    private bool _hasHandledResult;

    public QrScannerModalPage(LocalizedText text)
    {
        Title = text.QrTitle;
        BackgroundColor = Colors.Black;

        _cameraView = new CameraBarcodeReaderView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsTorchOn = false,
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                Multiple = false,
                AutoRotate = true
            }
        };
        _cameraView.BarcodesDetected += OnBarcodesDetected;

        var closeButton = new Button
        {
            Text = text.CloseText,
            BackgroundColor = Color.FromArgb("#EF8F2A"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 18,
            HeightRequest = 44
        };
        closeButton.Clicked += async (_, _) => await CloseAsync(null);

        var guideLabel = new Label
        {
            Text = text.QrGuideText,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var focusFrame = new Border
        {
            Stroke = Color.FromArgb("#EF8F2A"),
            StrokeThickness = 3,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 240,
            HeightRequest = 240,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) }
        };
        Grid.SetRow(focusFrame, 1);

        var footerButton = new Grid();
        Grid.SetRow(footerButton, 2);
        footerButton.Children.Add(closeButton);

        var overlay = new Grid
        {
            Padding = new Thickness(20, 24),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        overlay.Children.Add(guideLabel);
        overlay.Children.Add(focusFrame);
        overlay.Children.Add(footerButton);

        Content = new Grid
        {
            Children =
            {
                _cameraView,
                overlay
            }
        };
    }

    public Task<string?> WaitForResultAsync() => _resultTcs.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cameraView.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        _cameraView.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_hasHandledResult)
        {
            return;
        }

        var rawValue = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        _hasHandledResult = true;
        MainThread.BeginInvokeOnMainThread(async () => await CloseAsync(rawValue.Trim()));
    }

    private async Task CloseAsync(string? result)
    {
        _cameraView.IsDetecting = false;
        _cameraView.BarcodesDetected -= OnBarcodesDetected;

        if (!_resultTcs.Task.IsCompleted)
        {
            _resultTcs.TrySetResult(result);
        }

        if (Navigation.ModalStack.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
        }
    }
}
