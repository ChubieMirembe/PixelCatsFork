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

                // Fix CS8625 at call-site by using nullable parameters in signature (see method below)
                WriteScoreFileAtomic(scoreFilePath, initialScore, gameLocal, state: "Startup", code: null);

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
                    gameLocal.Update(pixels);

                    lastGameOverCode = gameLocal.GetGameOverCode();

                    try
                    {
                        int finalScore = gameLocal.GetScore();
                        WriteScoreFileAtomic(scoreFilePath, finalScore, gameLocal, state: "GameOver", code: lastGameOverCode);
                        lastExportedScore = finalScore;
                        Console.WriteLine($"[ConsoleTest] Final score exported: {finalScore}, Code: {lastGameOverCode} -> {scoreFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ConsoleTest] Failed to write final score to '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
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

        // Fix CS8625: allow null for optional params by making them nullable.
        // Also avoids CS8604 by accepting IGame? and coalescing safely.
        private static void WriteScoreFileAtomic(string path, int score, IGame? game, string? state = null, string? code = null)
        {
            var payload = new
            {
                score,
                state,
                code,
                gameName = game?.ToString() ?? string.Empty,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

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
