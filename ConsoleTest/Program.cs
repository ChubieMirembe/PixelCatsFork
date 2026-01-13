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
    class Program
    {
        public enum State { Title, Playing, GameOver };
        public static State state = State.Title;
        public enum GameChoiceState { Snake, Tetris, Education };
        public static GameChoiceState game = GameChoiceState.Snake;

        public static IConfiguration _config = null;
        private static IPixel[,] pixels = new IPixel[20, 10];
        private static Dictionary<GameChoiceState, IGame> games;
        private static IGame currentGame;

        static void Main(string[] args)
        {
            // Load configuration (safer version)
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings. json", optional: true, reloadOnChange: true)
                .Build();

            bool useEmulator = true;
            var useEmulatorValue = _config["UseEmulator"];
            if (!string.IsNullOrEmpty(useEmulatorValue))
            {
                bool.TryParse(useEmulatorValue, out useEmulator);
            }

            IDisplay emulatorDisplay = new ConsoleDisplay();
            //IDisplay hardwareDisplay = new ArduinoDisplay();                      <-- Uncomment when hardware display is available

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
                // fallback to local folder if something goes wrong
                sharedDir = Path.Combine(AppContext.BaseDirectory, "shared");
                Directory.CreateDirectory(sharedDir);
            }

            string scoreFilePath = Path.Combine(sharedDir, "latest_score.json");
            Console.WriteLine($"[ConsoleTest] Score file: {scoreFilePath}");

            // Ensure the file exists at startup with the current game's score (safe fallback to 0)
            try
            {
                int initialScore = 0;
                try { initialScore = currentGame?.GetScore() ?? 0; } catch { initialScore = 0; }
                WriteScoreFileAtomic(scoreFilePath, initialScore, "Startup");
                lastExportedScore = initialScore;
                Console.WriteLine($"[ConsoleTest] Initialized score file '{scoreFilePath}' with score {initialScore}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConsoleTest] Failed to initialize score file '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
            }

            // Track previous state so we only export on state transition to GameOver
            State previousState = state;
            state = State.Title;

            while (true)
            {
                if (state == State.Title)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.S:
                                state = State.Playing;
                                currentGame.Initialize(pixels);
                                break;
                            case ConsoleKey.A:
                                game = (GameChoiceState)(((int)game + 2) % 3);
                                currentGame = games[game];
                                break;
                            case ConsoleKey.D:
                                game = (GameChoiceState)(((int)game + 1) % 3);
                                currentGame = games[game];
                                break;
                        }
                    }

                    // Draw title for current game
                    currentGame.DrawTitle(pixels);
                }

                if (state == State.Playing)
                {
                    Thread.Sleep(100);

                    // Check for input (only Snake needs it in Playing state)
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        bool stateChanged = false;
                        currentGame.HandleInput(key, ref stateChanged);

                        // If the game signalled a state change (Escape -> true), return to title screen.
                        if (stateChanged)
                        {
                            state = State.Title;
                            // Optionally reset or re-initialize visuals for the title screen next loop
                            // currentGame = games[game]; // keep current selection
                        }
                    }

                    // Update current game
                    currentGame.Update(pixels);

                    // NEW: ask the game if it's over (if it exposes IsGameOver)
                    var mg = currentGame.GetType().GetMethod("IsGameOver");
                    if (mg != null)
                    {
                        try
                        {
                            var result = mg.Invoke(currentGame, null);
                            if (result is bool b && b)
                            {
                                state = State.GameOver;
                            }
                        }
                        catch { /* ignore reflection call errors */ }
                    }

                    // IMPORTANT: Do NOT write score on every point update.
                    // Keep score local in the game and only export when the game transitions to GameOver.
                }

                // Detect state transitions and export final score only when the game ends
                if (previousState != state)
                {
                    Console.WriteLine($"[ConsoleTest] State changed {previousState} -> {state}");
                    if (state == State.GameOver)
                    {
                        try
                        {
                            int finalScore = 0;
                            try { finalScore = currentGame?.GetScore() ?? 0; } catch { finalScore = 0; }
                            WriteScoreFileAtomic(scoreFilePath, finalScore, "GameOver");
                            lastExportedScore = finalScore;
                            Console.WriteLine($"[ConsoleTest] Final score exported {finalScore} -> {scoreFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ConsoleTest] Failed to write final score to '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                    previousState = state;
                }

                // Display score (only for Snake in original)
                if (game == GameChoiceState.Snake)
                {
                    try
                    {
                        emulatorDisplay.DisplayInt(currentGame.GetScore());
                        //hardwareDisplay.DisplayInt(currentGame.GetScore());               <-- Uncomment when hardware display is available
                    }
                    catch { }
                }

                emulatorDisplay.Draw(pixels);
                //hardwareDisplay.Draw(pixels);                                             <-- Uncomment when hardware display is available
            }
        }

        // Writes the score, an ISO-8601 UTC timestamp and optional state atomically.
        static void WriteScoreFileAtomic(string path, int score, string state = null)
        {
            var payload = new
            {
                score = score,
                state = state,
                // DateTimeOffset serializes as an ISO 8601 string by System.Text.Json by default.
                timestamp = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
                dir = AppContext.BaseDirectory;
            Directory.CreateDirectory(dir);

            // Write to a temp file in the same directory then replace target to avoid partial reads.
            var tmp = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tmp, json);

            try
            {
                // Copy over (overwrite) and remove temp. Copy is used for cross-platform safety.
                File.Copy(tmp, path, overwrite: true);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}