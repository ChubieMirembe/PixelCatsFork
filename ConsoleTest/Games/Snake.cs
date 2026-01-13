using PixelBoard;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ConsoleTest.Games
{
    public class Snake : IGame
    {
        // Exact variables from original
        private Queue<(int x, int y)> snake = new Queue<(int x, int y)>();
        private int snakeLength = 5;
        private int headX = 10;
        private int headY = 5;
        private int directionX = 1;
        private int directionY = 0;
        private Random rand = new Random();
        private (int x, int y) food;
        private int score = 0;
        private float rainbowShift = 0f;

        public void Initialize(IPixel[,] pixels)
        {
            // Reset to original values
            snake.Clear();
            snakeLength = 5;
            headX = 10;
            headY = 5;
            directionX = 1;
            directionY = 0;
            food = (rand.Next(20), rand.Next(10));
            score = 0;
        }

        public void DrawTitle(IPixel[,] pixels)
        {

            rainbowShift += 0.0001f;
            for (sbyte i = 0; i < 20; i++)
            {
                for (sbyte j = 0; j < 10; j++)
                {
                    float hue = (rainbowShift + (i + j) * 0.05f) % 1f;
                    Color color = ColorFromHSV(hue * 360, 1f, 1f);
                    pixels[i, j] = new Pixel(color.R, color.G, color.B);
                }
            }

            int[,] sShape = new int[,]
            {
                {1, 1, 1, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 1, 1}
            };
            int shapeWidth = sShape.GetLength(1);
            int shapeHeight = sShape.GetLength(0);
            int offsetX = (20 - shapeWidth) / 2;
            int offsetY = (10 - shapeHeight) / 2;

            for (int y = 0; y < shapeHeight; y++)
            {
                for (int x = 0; x < shapeWidth; x++)
                {
                    if (sShape[y, x] == 1)
                    {
                        pixels[offsetX + x, offsetY + y] = new Pixel(0, 0, 0);
                    }
                }
            }
        }

        public void Update(IPixel[,] pixels)
        {

            // Check if snake eats food
            if (headX == food.x && headY == food.y)
            {
                score++;
                snakeLength++;
                food = (rand.Next(20), rand.Next(10));
            }

            // Clear background
            for (sbyte i = 0; i < 20; i++)
            {
                for (sbyte j = 0; j < 10; j++)
                {
                    pixels[i, j] = new Pixel(255, 30, 255);
                }
            }

            // Move snake
            headX += directionX;
            headY += directionY;

            if (headX >= 20) headX = 0;
            if (headX < 0) headX = 19;
            if (headY >= 10) headY = 0;
            if (headY < 0) headY = 9;

            snake.Enqueue((headX, headY));
            if (snake.Count > snakeLength)
                snake.Dequeue();

            foreach (var (x, y) in snake)
            {
                pixels[x, y] = new Pixel(0, 255, 0); // Green snake
            }
            // Draw food
            pixels[food.x, food.y] = new Pixel(255, 0, 0); // Red food
        }

        public void HandleInput(ConsoleKey key, ref bool stateChanged)
        {

            switch (key)
            {
                case ConsoleKey.W:
                    directionX = -1; directionY = 0;
                    break;
                case ConsoleKey.S:
                    directionX = 1; directionY = 0;
                    break;
                case ConsoleKey.A:
                    directionX = 0; directionY = -1;
                    break;
                case ConsoleKey.D:
                    directionX = 0; directionY = 1;
                    break;
            }
        }

        public int GetScore() => score;

        public static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            return hi switch
            {
                0 => Color.FromArgb(255, v, t, p),
                1 => Color.FromArgb(255, q, v, p),
                2 => Color.FromArgb(255, p, v, t),
                3 => Color.FromArgb(255, p, q, v),
                4 => Color.FromArgb(255, t, p, v),
                _ => Color.FromArgb(255, v, p, q),
            };
        }
    }
}