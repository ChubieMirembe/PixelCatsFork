using PixelBoard;
using System;

namespace ConsoleTest.Games
{
    public class Education : IGame
    {
        private int score = 0;

        public void Initialize(IPixel[,] pixels)
        {
            score = 0;
        }

        public void DrawTitle(IPixel[,] pixels)
        {
            for (sbyte i = 0; i < 20; i++)
            {
                for (sbyte j = 0; j < 10; j++)
                {
                    pixels[i, j] = new Pixel(200, 50, 150);
                }
            }
            int[,] sShape = new int[,]
           {
                {1, 1, 1, 1, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 0, 1},
                {1, 0, 1, 0, 1}
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
                        pixels[offsetX + x, offsetY + y] = new Pixel(0, 0, 0); // Black pixel
                    }
                }
            }
        }

        public void Update(IPixel[,] pixels)
        {
            // No playing state code for Education in original
        }

        public void HandleInput(ConsoleKey key, ref bool stateChanged)
        {
            // Allow escape to return to title
            if (key == ConsoleKey.Escape)
            {
                stateChanged = true;
            }
        }

        public bool IsGameOver() => false;  // Education mode never ends

        public string GetGameOverCode() => null;  // No code for Education mode

        public int GetScore() => score;
    }
}