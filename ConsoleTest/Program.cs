using Microsoft.Extensions.Configuration;
using PixelBoard;
using SnakeGame.Games;
using System;
using System.Collections.Generic;
using System.Threading;

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
                    }

                    // Update current game
                    currentGame.Update(pixels);

                    // Display score (only for Snake in original)
                    if (game == GameChoiceState.Snake)
                    {
                        emulatorDisplay.DisplayInt(currentGame.GetScore());
                        //hardwareDisplay.DisplayInt(currentGame.GetScore());               <-- Uncomment when hardware display is available
                    }
                }

                emulatorDisplay.Draw(pixels);
                //hardwareDisplay.Draw(pixels);                                             <-- Uncomment when hardware display is available
            }
        }
    }
}