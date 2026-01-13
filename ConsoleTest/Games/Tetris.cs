using PixelBoard;
using System;
using System.Drawing;

namespace ConsoleTest.Games
{
    public class Tetris : IGame
    {
        private int score = 0;
        private int[,] board = new int[20, 10]; // 0 = empty, 1+ = filled
        private Tetromino currentPiece;
        private int pieceX;
        private int pieceY;
        private Random rand = new Random();
        private int frameCounter = 0;
        private int dropSpeed = 10; // Frames before piece drops
        private bool gameOver = false;

        // Tetromino shapes (7 classic pieces)
        private static readonly int[][,] SHAPES = new int[][,]
        {
            // I piece
            new int[,] { {1,1,1,1} },
            
            // O piece
            new int[,] { {1,1}, {1,1} },
            
            // T piece
            new int[,] { {0,1,0}, {1,1,1} },
            
            // S piece
            new int[,] { {0,1,1}, {1,1,0} },
            
            // Z piece
            new int[,] { {1,1,0}, {0,1,1} },
            
            // J piece
            new int[,] { {1,0,0}, {1,1,1} },
            
            // L piece
            new int[,] { {0,0,1}, {1,1,1} }
        };

        private static readonly Color[] PIECE_COLORS = new Color[]
        {
            Color. Cyan,     // I
            Color.Yellow,   // O
            Color. Purple,   // T
            Color. Green,    // S
            Color.Red,      // Z
            Color.Blue,     // J
            Color.Orange    // L
        };

        private class Tetromino
        {
            public int[,] Shape;
            public Color Color;
            public int ColorIndex;

            public Tetromino(int shapeIndex)
            {
                Shape = (int[,])SHAPES[shapeIndex].Clone();
                ColorIndex = shapeIndex;
                Color = PIECE_COLORS[shapeIndex];
            }

            public void Rotate()
            {
                int rows = Shape.GetLength(0);
                int cols = Shape.GetLength(1);
                int[,] rotated = new int[cols, rows];

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        rotated[c, rows - 1 - r] = Shape[r, c];
                    }
                }
                Shape = rotated;
            }
        }

        public void Initialize(IPixel[,] pixels)
        {
            score = 0;
            board = new int[20, 10];
            gameOver = false;
            SpawnNewPiece();
        }

        public void DrawTitle(IPixel[,] pixels)
        {
            // Purple background with "T" shape
            for (sbyte i = 0; i < 20; i++)
            {
                for (sbyte j = 0; j < 10; j++)
                {
                    pixels[i, j] = new Pixel(100, 75, 200);
                }
            }

            int[,] tShape = new int[,]
            {
                {1, 0, 0, 0, 0},
                {1, 0, 0, 0, 0},
                {1, 1, 1, 1, 1},
                {1, 0, 0, 0, 0},
                {1, 0, 0, 0, 0}
            };

            int shapeWidth = tShape.GetLength(1);
            int shapeHeight = tShape.GetLength(0);
            int offsetX = (20 - shapeWidth) / 2;
            int offsetY = (10 - shapeHeight) / 2;

            for (int y = 0; y < shapeHeight; y++)
            {
                for (int x = 0; x < shapeWidth; x++)
                {
                    if (tShape[y, x] == 1)
                    {
                        pixels[offsetX + x, offsetY + y] = new Pixel(0, 0, 0);
                    }
                }
            }
        }

        public void Update(IPixel[,] pixels)
        {
            if (gameOver)
            {
                DrawGameOver(pixels);
                return;
            }

            frameCounter++;

            // Auto-drop piece
            if (frameCounter >= dropSpeed)
            {
                frameCounter = 0;
                if (!MovePiece(0, 1))
                {
                    // Piece can't move down, lock it
                    LockPiece();
                    ClearLines();
                    SpawnNewPiece();

                    // Check game over
                    if (!IsValidPosition(pieceX, pieceY))
                    {
                        gameOver = true;
                    }
                }
            }

            DrawBoard(pixels);
        }

        public void HandleInput(ConsoleKey key, ref bool stateChanged)
        {
            if (gameOver) return;

            switch (key)
            {
                case ConsoleKey.A:  // Move left
                case ConsoleKey.LeftArrow:
                    MovePiece(-1, 0);
                    break;
                case ConsoleKey.D: // Move right
                case ConsoleKey.RightArrow:
                    MovePiece(1, 0);
                    break;
                case ConsoleKey.S: // Move down faster
                case ConsoleKey.DownArrow:
                    if (!MovePiece(0, 1))
                    {
                        LockPiece();
                        ClearLines();
                        SpawnNewPiece();
                        if (!IsValidPosition(pieceX, pieceY))
                        {
                            gameOver = true;
                        }
                    }
                    break;
                case ConsoleKey.W: // Rotate
                case ConsoleKey.UpArrow:
                case ConsoleKey.Spacebar:
                    RotatePiece();
                    break;
            }
        }

        public int GetScore() => score;

        private void SpawnNewPiece()
        {
            currentPiece = new Tetromino(rand.Next(SHAPES.Length));
            pieceX = 5 - currentPiece.Shape.GetLength(1) / 2;
            pieceY = 0;
        }

        private bool MovePiece(int dx, int dy)
        {
            int newX = pieceX + dx;
            int newY = pieceY + dy;

            if (IsValidPosition(newX, newY))
            {
                pieceX = newX;
                pieceY = newY;
                return true;
            }
            return false;
        }

        private void RotatePiece()
        {
            currentPiece.Rotate();

            // If rotation causes collision, try to adjust position
            if (!IsValidPosition(pieceX, pieceY))
            {
                // Try shifting left or right
                if (IsValidPosition(pieceX - 1, pieceY))
                {
                    pieceX--;
                }
                else if (IsValidPosition(pieceX + 1, pieceY))
                {
                    pieceX++;
                }
                else
                {
                    // Rotation not possible, rotate back
                    for (int i = 0; i < 3; i++)
                        currentPiece.Rotate();
                }
            }
        }

        private bool IsValidPosition(int x, int y)
        {
            int rows = currentPiece.Shape.GetLength(0);
            int cols = currentPiece.Shape.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (currentPiece.Shape[r, c] == 1)
                    {
                        int boardX = x + c;
                        int boardY = y + r;

                        // Check boundaries
                        if (boardX < 0 || boardX >= 10 || boardY >= 20)
                            return false;

                        // Check collision with existing blocks (allow spawning above board)
                        if (boardY >= 0 && board[boardY, boardX] != 0)
                            return false;
                    }
                }
            }
            return true;
        }

        private void LockPiece()
        {
            int rows = currentPiece.Shape.GetLength(0);
            int cols = currentPiece.Shape.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (currentPiece.Shape[r, c] == 1)
                    {
                        int boardX = pieceX + c;
                        int boardY = pieceY + r;

                        if (boardY >= 0 && boardY < 20 && boardX >= 0 && boardX < 10)
                        {
                            board[boardY, boardX] = currentPiece.ColorIndex + 1;
                        }
                    }
                }
            }
        }

        private void ClearLines()
        {
            int linesCleared = 0;

            for (int row = 19; row >= 0; row--)
            {
                bool fullLine = true;
                for (int col = 0; col < 10; col++)
                {
                    if (board[row, col] == 0)
                    {
                        fullLine = false;
                        break;
                    }
                }

                if (fullLine)
                {
                    linesCleared++;
                    // Move all rows above down
                    for (int r = row; r > 0; r--)
                    {
                        for (int c = 0; c < 10; c++)
                        {
                            board[r, c] = board[r - 1, c];
                        }
                    }
                    // Clear top row
                    for (int c = 0; c < 10; c++)
                    {
                        board[0, c] = 0;
                    }
                    row++; // Check this row again
                }
            }

            // Score:  100 per line, bonus for multiple lines
            if (linesCleared > 0)
            {
                score += linesCleared * 100 * linesCleared;
            }
        }

        private void DrawBoard(IPixel[,] pixels)
        {
            // Draw background and locked pieces
            for (int row = 0; row < 20; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    if (board[row, col] == 0)
                    {
                        // Empty cell - dark background
                        pixels[row, col] = new Pixel(10, 10, 20);
                    }
                    else
                    {
                        // Locked piece
                        Color color = PIECE_COLORS[board[row, col] - 1];
                        pixels[row, col] = new Pixel(color.R, color.G, color.B);
                    }
                }
            }

            // Draw current falling piece
            if (currentPiece != null)
            {
                int rows = currentPiece.Shape.GetLength(0);
                int cols = currentPiece.Shape.GetLength(1);

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (currentPiece.Shape[r, c] == 1)
                        {
                            int boardX = pieceX + c;
                            int boardY = pieceY + r;

                            if (boardY >= 0 && boardY < 20 && boardX >= 0 && boardX < 10)
                            {
                                Color color = currentPiece.Color;
                                pixels[boardY, boardX] = new Pixel(color.R, color.G, color.B);
                            }
                        }
                    }
                }
            }
        }

        private void DrawGameOver(IPixel[,] pixels)
        {
            // Flash red
            for (int row = 0; row < 20; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    pixels[row, col] = new Pixel(50, 0, 0);
                }
            }
        }
    }
}