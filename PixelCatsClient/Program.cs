using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.IO;
using System.Text.Json;

class Program
{
    private static readonly string ApiBase = "http://10.31.228.19:3000";

    static async Task Main(string[] args)
    {
        Console.WriteLine("PixelCats Leaderboard client");

        // Accept explicit score file path as first arg, otherwise use default relative path
        string scoreFilePath = args.Length > 0 ? args[0] : Path.Combine("..", "ConsoleTest", "latest_score.json");

        // Read name
        Console.WriteLine("Please enter a name for the leaderboard:");
        var name = Console.ReadLine();

        // Try to read score from the console-test exported file
        int? fileScore = TryReadScoreFromFile(scoreFilePath);
        int score;
        if (fileScore.HasValue)
        {
            score = fileScore.Value;
            Console.WriteLine($"Using score from file '{scoreFilePath}': {score}");
        }
        else
        {
            // Fallback to manual input
            Console.WriteLine("Score file not found or invalid. Please enter a score for the leaderboard:");
            var scoreInput = Console.ReadLine();
            if (!int.TryParse(scoreInput, out score))
            {
                Console.WriteLine("Invalid score input. Please enter a valid integer.");
                return;
            }
        }

        var ok = await SubmitScoreAsync(name, score);
        Console.WriteLine(ok ? "Score submitted!" : "Failed to submit score");

        // Example: fetch top scores
        var scores = await GetTopScoresAsync(limit: 10);
        if (scores != null)
        {
            Console.WriteLine("Top scores:");
            foreach (var s in scores)
                Console.WriteLine($"{s.name} — {s.score} ({s.created_at})");
        }
    }

    static int? TryReadScoreFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
            {
                if (scoreProp.TryGetInt32(out int v))
                    return v;
            }
        }
        catch
        {
            // ignore and return null to allow fallback
        }
        return null;
    }

    static async Task<bool> SubmitScoreAsync(string name, int score, string apiKey = null)
    {
        using var http = new HttpClient { BaseAddress = new Uri(ApiBase) };

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("name", name),
            new KeyValuePair<string, string>("score", score.ToString())
        });

        if (!string.IsNullOrEmpty(apiKey))
            http.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var resp = await http.PostAsync("/api/leaderboard", form);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await SafeReadAsync(resp);
            Console.WriteLine($"POST failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
            return false;
        }

        Console.WriteLine(await resp.Content.ReadAsStringAsync());
        return true;

        static async Task<string> SafeReadAsync(HttpResponseMessage r)
        {
            try { return await r.Content.ReadAsStringAsync(); } catch { return ""; }
        }
    }

    static async Task<ScoreRecord[]> GetTopScoresAsync(int limit = 10)
    {
        using var http = new HttpClient { BaseAddress = new Uri(ApiBase) };

        var url = $"/api/leaderboard?limit={limit}";
        var resp = await http.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"GET failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            return null;
        }

        var data = await resp.Content.ReadFromJsonAsync<ScoreRecord[]>();
        return data;
    }

    public record ScoreRecord(int id, string name, int score, string created_at);
}
