using Microsoft.Extensions.Configuration;
using PixelBoard;
using ConsoleTest.Games;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text.Json;

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
                File.WriteAllText(scoreFilePath, JsonSerializer.Serialize(new { score = initialScore }));
                lastExportedScore = initialScore;
                Console.WriteLine($"[ConsoleTest] Initialized score file '{scoreFilePath}' with score {initialScore}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConsoleTest] Failed to initialize score file '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
            }
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

                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        bool stateChanged = false;
                        currentGame.HandleInput(key, ref stateChanged);
                    }

                    // Update current game
                    currentGame.Update(pixels);

                    try
                    {
                        int currentScore = currentGame.GetScore();
                        if (currentScore != lastExportedScore)
                        {
                            // Write new score and log success/failure
                            try
                            {
                                File.WriteAllText(scoreFilePath, JsonSerializer.Serialize(new { score = currentScore }));
                                lastExportedScore = currentScore;
                                Console.WriteLine($"[ConsoleTest] Exported score {currentScore} -> {scoreFilePath}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ConsoleTest] Failed to write score to '{scoreFilePath}': {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                    catch
                    {
                        // ignore game GetScore errors
                    }

                    // Display score 
                    emulatorDisplay.DisplayInt(currentGame.GetScore());
                    //hardwareDisplay.DisplayInt(currentGame.GetScore());                    <-- Uncomment when hardware display is available
                    
                }

                emulatorDisplay.Draw(pixels);
                //hardwareDisplay.Draw(pixels);                                             <-- Uncomment when hardware display is available
            }
        }
    }
}