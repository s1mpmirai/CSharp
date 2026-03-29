using System.Net.Http.Json;
using System.Diagnostics;
using FoodStreetAudioGuide.Models;

namespace FoodStreetAudioGuide
{
    public class StallService
    {
        private readonly HttpClient _httpClient;

        public StallService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<StallItem>> GetNearbyStallsAsync(double lat, double lng)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("nearby", new { lat, lng });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<StallItem>>();
                    return result ?? new List<StallItem>();
                }

                Debug.WriteLine($"--- API Trả về lỗi: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(@"--- LỖI KẾT NỐI (Kiểm tra Docker/Backend): " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"--- LỖI HỆ THỐNG: " + ex.Message);
            }

            return new List<StallItem>();
        }
    }
}
