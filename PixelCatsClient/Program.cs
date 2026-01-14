using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly string ApiBase = "http://10.31.228.19:3000";

    // Shared timestamp holder to avoid duplicate submissions from watcher+poller
    private sealed class SharedTimestamp
    {
        // unix millis
        private long _value;

        public SharedTimestamp(long initialMillis) => Interlocked.Exchange(ref _value, initialMillis);

        public long Read() => Interlocked.Read(ref _value);

        // Try to claim this timestamp:  set to newMillis only if newMillis > current. 
        // Returns true if claim succeeded (this caller "owns" the timestamp and should submit).
        public bool TryClaim(long newMillis)
        {
            while (true)
            {
                long cur = Interlocked.Read(ref _value);
                if (newMillis <= cur) return false;
                if (Interlocked.CompareExchange(ref _value, newMillis, cur) == cur) return true;
            }
        }
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("PixelCats Leaderboard client (listening mode)");

        string scoreFilePath = ResolveScoreFilePath(args);

        // Ensure the directory exists
        try
        {
            var directory = Path.GetDirectoryName(scoreFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Console.WriteLine($"[PixelCatsClient] Created directory: {directory}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PixelCatsClient] Warning: Could not create directory: {ex.Message}");
        }

        Console.WriteLine($"[PixelCatsClient] Watching score file: {scoreFilePath} (exists: {File.Exists(scoreFilePath)})");

        // Track the last processed timestamp so we only submit new writes.
        long initialMillis = 0;
        if (File.Exists(scoreFilePath))
        {
            // use file last write time as initial baseline
            var fileWrite = File.GetLastWriteTimeUtc(scoreFilePath);
            initialMillis = new DateTimeOffset(fileWrite).ToUnixTimeMilliseconds();
        }
        var sharedTs = new SharedTimestamp(initialMillis);

        // Read optional API key from args[1] or environment
        string apiKey = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("API_KEY");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Stopping listener...");
            e.Cancel = true;
            cts.Cancel();
        };

        // Start a FileSystemWatcher and a fallback poller to detect writes reliably.
        var watcherTask = RunFileWatcherAsync(scoreFilePath, sharedTs, apiKey, cts.Token);
        var pollerTask = RunPollerAsync(scoreFilePath, sharedTs, apiKey, cts.Token);

        Console.WriteLine("Listening for new scores.  Press Ctrl+C to exit.");

        // Wait for both tasks (don't exit on first completion)
        try
        {
            await Task.WhenAll(watcherTask, pollerTask);
        }
        catch (OperationCanceledException)
        {
            // Expected when Ctrl+C is pressed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine("Listener stopped.");
    }

    static string ResolveScoreFilePath(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            return args[0];

        try
        {
            var shared = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "shared", "latest_score.json"));
            return shared;
        }
        catch
        {
            return Path.Combine(". .", "ConsoleTest", "latest_score.json");
        }
    }

    static async Task RunFileWatcherAsync(string filePath, SharedTimestamp sharedTs, string apiKey, CancellationToken ct)
    {
        var file = new FileInfo(filePath);
        var dir = file.DirectoryName ?? AppContext.BaseDirectory;
        var name = file.Name;

        // Ensure directory exists
        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"[Watcher] Waiting for directory to exist: {dir}");
            while (!Directory.Exists(dir) && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
            }
            if (ct.IsCancellationRequested) return;
        }

        FileSystemWatcher watcher = null;
        var eventQueue = new System.Collections.Concurrent.BlockingCollection<FileSystemEventArgs>();

        try
        {
            watcher = new FileSystemWatcher(dir, name)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };

            FileSystemEventHandler onChanged = (s, e) =>
            {
                // Enqueue the event for serialized processing
                eventQueue.Add(e);
            };

            watcher.Changed += onChanged;
            watcher.Created += onChanged;
            watcher.EnableRaisingEvents = true;

            Console.WriteLine($"[Watcher] Monitoring:  {filePath}");

            while (!ct.IsCancellationRequested)
            {
                FileSystemEventArgs evt = null;
                try
                {
                    evt = eventQueue.Take(ct);
                }
                catch (OperationCanceledException) { break; }

                if (evt == null) continue;

                var (score, timestamp, state, code, gameName) = await TryReadScoreWithTimestampAsync(filePath, ct);
                if (!score.HasValue || !timestamp.HasValue)
                    continue;

                // Only act on final/game-over writes.  Ignore startup/live updates. 
                // ConsoleTest now writes "state":"GameOver" (or you can change the string to match your writer).
                if (!string.Equals(state, "GameOver", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Code must be present
                if (string.IsNullOrEmpty(code))
                {
                    Console.WriteLine("Warning: GameOver state detected but no code found in file");
                    continue;
                }

                long tsMs = timestamp.Value.ToUnixTimeMilliseconds();

                // attempt to claim this timestamp; only the winner proceeds to submit
                if (!sharedTs.TryClaim(tsMs))
                    continue;

                Console.WriteLine($"Detected new score write at {timestamp.Value:u}.  Score={score.Value}, Code={code}, Game={gameName ?? "<unknown>"}");
                var ok = await SubmitCodeAsync(code, score.Value, gameName, apiKey);
                Console.WriteLine(ok ? "Code+score submitted!" : "Failed to submit code+score");

                // Optionally print leaderboard after submit
                var scores = await GetTopScoresAsync(limit: 10);
                if (scores != null)
                {
                    Console.WriteLine("Top scores:");
                    foreach (var s in scores)
                        Console.WriteLine($"{s.name} — {s.score} ({s.created_at})");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Watcher] Error: {ex.Message}");
        }
        finally
        {
            eventQueue.CompleteAdding();
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }

    // A lightweight poller fallback in case FileSystemWatcher misses events or platform issues arise. 
    static async Task RunPollerAsync(string filePath, SharedTimestamp sharedTs, string apiKey, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var writeTime = File.GetLastWriteTimeUtc(filePath);
                    var writeMs = new DateTimeOffset(writeTime).ToUnixTimeMilliseconds();

                    // quick check against shared baseline (avoid expensive parsing if not newer)
                    if (writeMs > sharedTs.Read())
                    {
                        var (score, timestamp, state, code, gameName) = await TryReadScoreWithTimestampAsync(filePath, ct);
                        timestamp ??= DateTimeOffset.FromUnixTimeMilliseconds(writeMs);

                        // Only process final/game-over writes
                        if (!string.Equals(state, "GameOver", StringComparison.OrdinalIgnoreCase))
                        {
                            await Task.Delay(1000, ct);
                            continue;
                        }

                        // Code must be present
                        if (string.IsNullOrEmpty(code))
                        {
                            await Task.Delay(1000, ct);
                            continue;
                        }

                        if (score.HasValue && timestamp.HasValue)
                        {
                            long tsMs = timestamp.Value.ToUnixTimeMilliseconds();

                            // attempt to claim this timestamp; only the winner proceeds to submit
                            if (!sharedTs.TryClaim(tsMs))
                                continue;

                            Console.WriteLine($"Poller detected new score write at {timestamp.Value:u}. Score={score.Value}, Code={code}, Game={gameName ?? "<unknown>"}");
                            var ok = await SubmitCodeAsync(code, score.Value, gameName, apiKey);
                            Console.WriteLine(ok ? "Code+score submitted!" : "Failed to submit code+score");

                            var scores = await GetTopScoresAsync(limit: 10);
                            if (scores != null)
                            {
                                Console.WriteLine("Top scores:");
                                foreach (var s in scores)
                                    Console.WriteLine($"{s.name} — {s.score} ({s.created_at})");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // Keep listening even on transient errors
                Console.WriteLine($"Poller error: {ex.Message}");
            }

            await Task.Delay(1000, ct);
        }
    }

    // Attempts to read "score", "code", optional "state", "gameName" and a timestamp property if present.
    // Supports timestamp as ISO string or numeric unix seconds/milliseconds.
    static async Task<(int? score, DateTimeOffset? timestamp, string state, string code, string gameName)> TryReadScoreWithTimestampAsync(string path, CancellationToken ct)
    {
        // Try a few times in case the game is still writing the file.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(path))
                    return (null, null, null, null, null);

                // Open with shared read so we don't lock the writer
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var json = await sr.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(json))
                    return (null, null, null, null, null);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int? score = null;
                DateTimeOffset? timestamp = null;
                string state = null;
                string code = null;
                string gameName = null;

                if (root.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
                {
                    if (scoreProp.TryGetInt32(out int v))
                        score = v;
                }

                if (root.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String)
                {
                    state = stateProp.GetString();
                }

                if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
                {
                    code = codeProp.GetString();
                }

                if (root.TryGetProperty("gameName", out var gnProp) && gnProp.ValueKind == JsonValueKind.String)
                {
                    gameName = gnProp.GetString();

                    // Normalize fully-qualified game names like "ConsoleTest.Games.Snake"
                    // to just "Snake". If gameName already looks simple, this is a no-op.
                    if (!string.IsNullOrWhiteSpace(gameName))
                    {
                        gameName = gameName.Trim();

                        // If it's a dotted name, take the last segment.
                        int lastDot = gameName.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot < gameName.Length - 1)
                        {
                            gameName = gameName.Substring(lastDot + 1);
                        }
                    }
                }

                // look for common timestamp fields
                string[] tsCandidates = { "timestamp", "time", "written_at", "created_at" };
                foreach (var key in tsCandidates)
                {
                    if (!root.TryGetProperty(key, out var p)) continue;

                    if (p.ValueKind == JsonValueKind.String)
                    {
                        if (DateTimeOffset.TryParse(p.GetString(), out var dto))
                        {
                            timestamp = dto.ToUniversalTime();
                            break;
                        }
                    }
                    else if (p.ValueKind == JsonValueKind.Number)
                    {
                        if (p.TryGetInt64(out long n))
                        {
                            // Heuristics: if value looks like milliseconds (> 1e12), else seconds
                            if (n > 1_000_000_000_000L)
                                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(n);
                            else
                                timestamp = DateTimeOffset.FromUnixTimeSeconds(n);
                            break;
                        }
                    }
                }

                // If no timestamp property, fallback to file last write time
                if (!timestamp.HasValue)
                {
                    timestamp = File.GetLastWriteTimeUtc(path);
                }

                return (score, timestamp, state, code, gameName);
            }
            catch (JsonException)
            {
                // file may be partially written; retry
            }
            catch (IOException)
            {
                // file may be locked; retry
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading score file: {ex.Message}");
                return (null, null, null, null, null);
            }

            await Task.Delay(200, ct);
        }

        return (null, null, null, null, null);
    }

    static async Task<bool> SubmitCodeAsync(string code, int score, string gameName, string apiKey = null)
    {
        using var http = new HttpClient { BaseAddress = new Uri(ApiBase) };
        if (!string.IsNullOrEmpty(apiKey))
            http.DefaultRequestHeaders.Add("x-api-key", apiKey);

        // include gameName if present; server should ignore unknown fields if it doesn't need them.
        var payload = new { code, score, gameName };
        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync("/api/codes", payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"POST error: {ex.Message}");
            return false;
        }

        if (!resp.IsSuccessStatusCode)
        {
            var err = await SafeReadAsync(resp);
            Console.WriteLine($"POST /api/codes failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
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
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(ApiBase) };
            var resp = await http.GetAsync($"/api/leaderboard?limit={limit}");
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"GET failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                return null;
            }
            var data = await resp.Content.ReadFromJsonAsync<ScoreRecord[]>();
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GET error: {ex.Message}");
            return null;
        }
    }

    public record ScoreRecord(int id, string name, int score, string created_at);
}