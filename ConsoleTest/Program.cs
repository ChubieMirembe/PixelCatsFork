#nullable enable

using Microsoft.Extensions.Configuration;
using PixelBoard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text.Json;
using ConsoleTest.Games;

namespace SnakeGame
{
    internal class Program
    {
        public enum State { Title, Playing, GameOver }
        public static State state = State.Title;

        public enum GameChoiceState { Snake, Tetris, Education }
        public static GameChoiceState game = GameChoiceState.Snake;

        // Fix CS8625: don't assign null to non-nullable
        public static IConfiguration? _config;

        private static readonly IPixel[,] pixels = new IPixel[20, 10];

        // Fix CS8618: these are initialized in Main, so make them nullable (or initialize here)
        private static Dictionary<GameChoiceState, IGame>? games;
        private static IGame? currentGame;

        private static void Main(string[] args)
        {
            // Fix filename typo: "appsettings. json" -> "appsettings.json"
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            bool useEmulator = true;
            var useEmulatorValue = _config["UseEmulator"];
            if (!string.IsNullOrEmpty(useEmulatorValue))
            {
                _ = bool.TryParse(useEmulatorValue, out useEmulator);
            }

            IDisplay emulatorDisplay = new ConsoleDisplay();
            // IDisplay hardwareDisplay = new ArduinoDisplay(); // Uncomment when hardware display is available

            // Initialize games
            games = new Dictionary<GameChoiceState, IGame>
            {
                { GameChoiceState.Snake, new Snake() },
                { GameChoiceState.Tetris, new Tetris() },
                { GameChoiceState.Education, new Education() }
            };

            currentGame = games[game];

            // Score export setup
            int lastExportedScore = int.MinValue;
            string sharedDir;
            try
            {
                sharedDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "shared"));
                Directory.CreateDirectory(sharedDir);
            }
            catch
            {
                sharedDir = Path.Combine(AppContext.BaseDirectory, "shared");
                Directory.CreateDirectory(sharedDir);
            }

            string scoreFilePath = Path.Combine(sharedDir, "latest_score.json");
            Console.WriteLine($"[ConsoleTest] Score file: {scoreFilePath}");

            // Ensure the file exists at startup with the current game's score (safe fallback to 0)
            try
            {
                // Fix CS8604: currentGame is nullable, so guard it once and keep a non-null local
                var gameLocal = currentGame ?? throw new InvalidOperationException("Current game was not initialized.");

                int initialScore;
                try { initialScore = gameLocal.GetScore(); } catch { initialScore = 0; }

                // Write initial file (no code - server will generate and store it)
                WriteScoreFileAtomic(scoreFilePath, initialScore, gameLocal, state: "Startup");

                lastExportedScore = initialScore;
                Console.WriteLine($"[ConsoleTest] Initialized score file '{scoreFilePath}' with score {initialScore}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConsoleTest] Failed to initialize score file '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
            }

            State previousState = state;
            state = State.Title;

            // Fix CS8600: declare as nullable
            string? lastGameOverCode = null;

            while (true)
            {
                // Fix CS8600/CS8602 patterns by working with non-null locals inside the loop
                var gameLocal = currentGame ?? throw new InvalidOperationException("Current game was not initialized.");

                if (state == State.Title)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.S:
                                state = State.Playing;
                                gameLocal.Initialize(pixels);
                                lastGameOverCode = null;
                                break;

                            case ConsoleKey.A:
                                game = (GameChoiceState)(((int)game + 2) % 3);
                                currentGame = games![game];
                                break;

                            case ConsoleKey.D:
                                game = (GameChoiceState)(((int)game + 1) % 3);
                                currentGame = games![game];
                                break;
                        }
                    }

                    gameLocal.DrawTitle(pixels);
                }

                if (state == State.Playing)
                {
                    Thread.Sleep(100);

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        bool stateChanged = false;
                        gameLocal.HandleInput(key, ref stateChanged);

                        if (stateChanged)
                        {
                            state = State.Title;
                        }
                    }

                    gameLocal.Update(pixels);

                    if (gameLocal.IsGameOver())
                    {
                        state = State.GameOver;
                    }
                }

                if (state == State.GameOver)
                {
                    // keep updating/drawing while in GameOver visual state
                    gameLocal.Update(pixels);

                    // Write final score first to avoid race with server-side generation.
                    // Do NOT attempt to rely on an in-game code being already present.
                    int finalScore = gameLocal.GetScore();
                    try
                    {
                        // Write score file without code (server will generate code after receiving score)
                        WriteScoreFileAtomic(scoreFilePath, finalScore, gameLocal, state: "GameOver");
                        lastExportedScore = finalScore;
                        Console.WriteLine($"[ConsoleTest] Final score exported: {finalScore} -> {scoreFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ConsoleTest] Failed to write final score to '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
                    }

                    // Now request the server to store the score and return the six-digit code.
                    try
                    {
                        // Block synchronously up to CodeGenerator's timeout so the code is available for display immediately.
                        // This ensures the file has been written before we call the server.
                        var code = ConsoleTest.CodeGenerator.GenerateSixDigitCodeAsync(finalScore, gameLocal.GameId ?? string.Empty, allowFallback: true, timeoutSeconds: 3)
                            .GetAwaiter().GetResult();

                        if (!string.IsNullOrEmpty(code))
                        {
                            lastGameOverCode = code;
                            // Give the game the retrieved code so it can display it.
                            gameLocal.SetGameOverCode(lastGameOverCode);
                            Console.WriteLine($"[ConsoleTest] Server generated code: {lastGameOverCode}");

                            // Also write the score file again including the code so external consumers can pick it up.
                            try
                            {
                                WriteScoreFileAtomic(scoreFilePath, finalScore, gameLocal, state: "GameOver", code: lastGameOverCode);
                                Console.WriteLine($"[ConsoleTest] Updated score file with code: {lastGameOverCode}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ConsoleTest] Failed to write score file with code to '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ConsoleTest] Server did not return a code; using local fallback (if any).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ConsoleTest] Failed to obtain server-generated code: {ex.GetType().Name}: {ex.Message}");
                    }

                    Thread.Sleep(1000);

                    Console.WriteLine($"[ConsoleTest] Returning to title screen with code {lastGameOverCode}");
                    state = State.Title;
                }

                if (previousState != state)
                {
                    Console.WriteLine($"[ConsoleTest] State changed {previousState} -> {state}");
                    previousState = state;
                }

                if (!string.IsNullOrEmpty(lastGameOverCode) && state == State.Title)
                {
                    if (int.TryParse(lastGameOverCode, out int codeInt))
                    {
                        emulatorDisplay.DisplayInt(codeInt);
                        // hardwareDisplay.DisplayInt(codeInt);
                    }
                }
                else
                {
                    emulatorDisplay.DisplayInt(gameLocal.GetScore());
                    // hardwareDisplay.DisplayInt(gameLocal.GetScore());
                }

                emulatorDisplay.Draw(pixels);
                // hardwareDisplay.Draw(pixels);
            }
        }

        // Write score file WITHOUT the code — server stores the code when it receives the score.
        // If 'code' is provided, include it in the JSON (written atomically).
        private static void WriteScoreFileAtomic(string path, int score, IGame? game, string? state = null, string? code = null)
        {
            // Use a dictionary so we only include the 'code' property when it is non-null.
            var payload = new Dictionary<string, object?>
            {
                ["score"] = score,
                ["state"] = state,
                ["gameName"] = game?.ToString() ?? string.Empty,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            };

            if (!string.IsNullOrEmpty(code))
            {
                payload["code"] = code;
            }

            var json = JsonSerializer.Serialize(payload);

            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
                dir = AppContext.BaseDirectory;

            Directory.CreateDirectory(dir);

            var tmp = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tmp, json);

            try
            {
                File.Copy(tmp, path, overwrite: true);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
