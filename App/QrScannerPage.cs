using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace FoodStreetAudioGuide;

public sealed class QrScannerPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CameraBarcodeReaderView _cameraView;
    private bool _hasHandledResult;

    public QrScannerPage()
    {
        Title = "Scan QR";
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
            Text = "Đóng",
            BackgroundColor = Color.FromArgb("#EF8F2A"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 18,
            HeightRequest = 44
        };
        closeButton.Clicked += async (_, _) => await CloseAsync(null);

        var footerButton = new Grid
        {
            Grid.Row = 2,
            Children = { closeButton }
        };

        Content = new Grid
        {
            Children =
            {
                _cameraView,
                new Grid
                {
                    Padding = new Thickness(20, 24),
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    Children =
                    {
                        new Label
                        {
                            Text = "Đưa QR vào khung để mở nội dung ngay",
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 18,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Border
                        {
                            Grid.Row = 1,
                            Stroke = Color.FromArgb("#EF8F2A"),
                            StrokeThickness = 3,
                            BackgroundColor = Colors.Transparent,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            WidthRequest = 240,
                            HeightRequest = 240,
                            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) }
                        },
                        footerButton
                    }
                }
            }
        };
    }

    public Task<string?> WaitForResultAsync()
    {
        return _resultTcs.Task;
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
