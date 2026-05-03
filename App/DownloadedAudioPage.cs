using Microsoft.Maui.Controls.Shapes;

namespace FoodStreetAudioGuide
{
    public class DownloadedAudioPage : ContentPage
    {
        private const string SelectedLanguagePreferenceKey = "SelectedLanguage";

        private readonly AudioCacheService _audioCacheService;
        private readonly CollectionView _collectionView;
        private readonly LocalizedText _text;

        // Hàm khởi tạo `DownloadedAudioPage`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
        public DownloadedAudioPage(AudioCacheService audioCacheService)
        {
            _audioCacheService = audioCacheService;
            _text = AppText.Get(Preferences.Get(SelectedLanguagePreferenceKey, AppText.English));
            Title = _text.DownloadedAudioTitle;
            BackgroundColor = Color.FromArgb("#F6F6F6");

            _collectionView = new CollectionView
            {
                SelectionMode = SelectionMode.None,
                EmptyView = new Label
                {
                    Text = _text.DownloadedAudioEmpty,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Color.FromArgb("#607086")
                },
                ItemTemplate = new DataTemplate(() =>
                {
                    var title = new Label
                    {
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 16,
                        TextColor = Color.FromArgb("#1F2738")
                    };
                    title.SetBinding(Label.TextProperty, nameof(DownloadedAudioItem.FilePath), converter: new FileNameConverter());

                    var subtitle = new Label
                    {
                        FontSize = 13,
                        TextColor = Color.FromArgb("#607086")
                    };
                    subtitle.SetBinding(Label.TextProperty, new Binding(path: ".", converter: new DownloadedAudioSubtitleConverter(_text)));

                    var deleteButton = new Button
                    {
                        Text = _text.DeleteText,
                        BackgroundColor = Color.FromArgb("#F6EFE7"),
                        TextColor = Color.FromArgb("#C8741F"),
                        CornerRadius = 14,
                        HeightRequest = 36,
                        WidthRequest = 72
                    };
                    deleteButton.Clicked += OnDeleteClicked;
                    deleteButton.SetBinding(Button.BindingContextProperty, ".");

                    var textStack = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { title, subtitle }
                    };

                    var grid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };
                    grid.Children.Add(textStack);
                    Grid.SetColumn(textStack, 0);
                    grid.Children.Add(deleteButton);
                    Grid.SetColumn(deleteButton, 1);

                    return new Border
                    {
                        Margin = new Thickness(0, 0, 0, 12),
                        Padding = new Thickness(14),
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#E8E1DB"),
                        StrokeThickness = 1,
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
                        Content = grid
                    };
                })
            };

            Content = new Grid
            {
                Padding = new Thickness(16, 18),
                Children = { _collectionView }
            };
        }

        // Hàm `OnAppearing`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _collectionView.ItemsSource = _audioCacheService.GetDownloadedAudioItems();
        }

        // Hàm `OnDeleteClicked`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        private void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.BindingContext is not DownloadedAudioItem item)
            {
                return;
            }

            _audioCacheService.DeleteCachedAudio(item.FilePath);
            _collectionView.ItemsSource = _audioCacheService.GetDownloadedAudioItems();
        }

        private sealed class FileNameConverter : IValueConverter
        {
            // Hàm `Convert`: xử lý logic liên quan trong file hiện tại.
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                var path = value?.ToString() ?? string.Empty;
                return System.IO.Path.GetFileNameWithoutExtension(path);
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class DownloadedAudioSubtitleConverter : IValueConverter
        {
            private readonly LocalizedText _text;

            // Hàm khởi tạo `DownloadedAudioSubtitleConverter`: thiết lập trạng thái ban đầu cho đối tượng trong file hiện tại.
            public DownloadedAudioSubtitleConverter(LocalizedText text)
            {
                _text = text;
            }

            // Hàm `Convert`: xử lý logic liên quan trong file hiện tại.
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is not DownloadedAudioItem item)
                {
                    return string.Empty;
                }

                return $"{_text.LanguageLabel}: {item.LanguageCode} • {_text.DownloadedAtLabel}: {item.DownloadedAt:dd/MM HH:mm}";
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
        }
    }
}
