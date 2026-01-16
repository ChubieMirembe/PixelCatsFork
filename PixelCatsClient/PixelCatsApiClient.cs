using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PixelCatsClient
{
    public record ScoreRecord(int id, string name, int score, string created_at);

    public class PixelCatsApiClient
    {
        private readonly HttpClient _http;

        public PixelCatsApiClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<bool> SubmitCodeAsync(string code, int score, string gameName, string apiKey = null)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("code required", nameof(code));

            if (!string.IsNullOrEmpty(apiKey))
            {
                if (_http.DefaultRequestHeaders.Contains("x-api-key"))
                    _http.DefaultRequestHeaders.Remove("x-api-key");
                _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }

            var payload = new { code, score, gameName };
            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsJsonAsync("/api/codes", payload);
            }
            catch
            {
                return false;
            }

            if (!resp.IsSuccessStatusCode)
                return false;

            return true;
        }

        public async Task<ScoreRecord[]> GetTopScoresAsync(int limit = 10)
        {
            var resp = await _http.GetAsync($"/api/leaderboard?limit={limit}");
            if (!resp.IsSuccessStatusCode)
                return null;

            try
            {
                var data = await resp.Content.ReadFromJsonAsync<ScoreRecord[]>();
                return data;
            }
            catch
            {
                return null;
            }
        }
    }
}