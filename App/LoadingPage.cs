namespace FoodStreetAudioGuide
{
    public class LoadingPage : ContentPage
    {
        private readonly ProgressBar _loadingProgressBar;
        private readonly Label _progressTextLabel;
        private readonly Label _statusLabel;
        private readonly StallService _stallService;
        private readonly AudioCacheService _audioCacheService;
        private readonly string[] _statusMessages =
        {
            "Connecting to local vendors...",
            "Loading nearby food stalls...",
            "Preparing audio guides..."
        };

        private bool _isLoaded;
        private bool _hasNavigated;
        private CancellationTokenSource? _loadingCts;

        public LoadingPage(StallService stallService, AudioCacheService audioCacheService)
        {
            _stallService = stallService;
            _audioCacheService = audioCacheService;
            NavigationPage.SetHasNavigationBar(this, false);
            BackgroundColor = Color.FromArgb("#0B0B0F");

            _progressTextLabel = new Label
            {
                Text = "0%",
                TextColor = Color.FromArgb("#F09A31"),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.End
            };

            _loadingProgressBar = new ProgressBar
            {
                Progress = 0,
                ProgressColor = Color.FromArgb("#F09A31"),
                BackgroundColor = Color.FromArgb("#595959"),
                HeightRequest = 8
            };

            _statusLabel = new Label
            {
                Text = _statusMessages[0],
                TextColor = Color.FromArgb("#ACB2BC"),
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Center
            };

            Content = new Grid
            {
                Children =
                {
                    new VerticalStackLayout
                    {
                        Padding = 24,
                        Spacing = 18,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Border
                            {
                                WidthRequest = 100,
                                HeightRequest = 100,
                                HorizontalOptions = LayoutOptions.Center,
                                BackgroundColor = Color.FromArgb("#F09A31"),
                                Stroke = Color.FromArgb("#6C5CE7"),
                                StrokeThickness = 3,
                                Content = new Label
                                {
                                    Text = "SF",
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center,
                                    FontSize = 34,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.White
                                }
                            },
                            new Label
                            {
                                Text = "StreetFeast",
                                HorizontalOptions = LayoutOptions.Center,
                                FontSize = 48,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Colors.White
                            },
                            new Label
                            {
                                Text = "AUDIO GUIDE TO STREET FOOD",
                                HorizontalOptions = LayoutOptions.Center,
                                FontSize = 20,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#F09A31")
                            },
                            new Border
                            {
                                Padding = 16,
                                BackgroundColor = Color.FromArgb("#332F2F2F"),
                                Stroke = Color.FromArgb("#606060"),
                                StrokeThickness = 1,
                                Content = new VerticalStackLayout
                                {
                                    Spacing = 10,
                                    Children =
                                    {
                                        new Label
                                        {
                                            Text = "PREPARING YOUR TOUR",
                                            FontSize = 12,
                                            FontAttributes = FontAttributes.Bold,
                                            TextColor = Color.FromArgb("#AAB1BB")
                                        },
                                        new Grid
                                        {
                                            ColumnDefinitions =
                                            {
                                                new ColumnDefinition { Width = GridLength.Star },
                                                new ColumnDefinition { Width = GridLength.Auto }
                                            },
                                            Children =
                                            {
                                                new Label
                                                {
                                                    Text = "Loading delicious experiences...",
                                                    TextColor = Colors.White,
                                                    FontSize = 18
                                                },
                                                _progressTextLabel
                                            }
                                        },
                                        _loadingProgressBar
                                    }
                                }
                            },
                            _statusLabel
                        }
                    }
                }
            };

            Grid.SetColumn(_progressTextLabel, 1);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_hasNavigated)
            {
                return;
            }

            try
            {
                if (!_isLoaded)
                {
                    _isLoaded = true;
                    _loadingCts = new CancellationTokenSource();
                    await RunLoadingAnimationAsync(_loadingCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"--- LOI LOADING PAGE: {ex.Message}");
            }

            if (_loadingCts?.IsCancellationRequested == true || _hasNavigated)
            {
                return;
            }

            await NavigateToLanguageSelectionAsync();
        }

        protected override void OnDisappearing()
        {
            _loadingCts?.Cancel();
            _loadingCts?.Dispose();
            _loadingCts = null;
            base.OnDisappearing();
        }

        private async Task RunLoadingAnimationAsync(CancellationToken cancellationToken)
        {
            for (var percent = 0; percent <= 100; percent += 5)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _loadingProgressBar.Progress = percent / 100.0;
                _progressTextLabel.Text = $"{percent}%";
                _statusLabel.Text = _statusMessages[(percent / 35) % _statusMessages.Length];
                await Task.Delay(90, cancellationToken);
            }
        }

        private async Task NavigateToLanguageSelectionAsync()
        {
            if (_hasNavigated)
            {
                return;
            }

            _hasNavigated = true;

            try
            {
                var targetPage = new LanguageSelectionPage(_stallService, _audioCacheService);

                if (Navigation is not null)
                {
                    await Navigation.PushAsync(targetPage, false);
                    if (Navigation.NavigationStack.Contains(this))
                    {
                        Navigation.RemovePage(this);
                    }

                    return;
                }

                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is not null)
                {
                    window.Page = new NavigationPage(targetPage);
                    return;
                }
            }
            catch (Exception ex)
            {
                _hasNavigated = false;
                System.Diagnostics.Debug.WriteLine($"--- LOI DIEU HUONG STARTUP: {ex.Message}");
            }
        }
    }
}
