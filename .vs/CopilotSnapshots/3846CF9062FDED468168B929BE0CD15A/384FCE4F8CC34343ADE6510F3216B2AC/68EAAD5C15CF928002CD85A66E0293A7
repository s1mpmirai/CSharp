using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace FoodStreetAudioGuide
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            var pin = new Pin
            {
                Label = "Gian hàng Bánh Mì",
                Location = new Location(10.7684, 106.6803),
                Address = "Đang phát đoạn ghi âm..."
            };

            map.Pins.Add(pin);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RequestLocationPermissionAsync();
        }

        private static async Task RequestLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }
    }
}
