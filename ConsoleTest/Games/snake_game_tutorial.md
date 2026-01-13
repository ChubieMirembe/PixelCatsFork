
# Snake.cs – Step-by-Step Build Guide (with explanations)


Create file:
- `Games/Snake.cs`'
- Namespace: `CosnsoleTest.Games`
- Grid size: **20 x 10** (x = 0–19, y = 0–9)

---

## Step 1 — Create the file and basic class structure

### What you’re doing
You’re creating the single class that contains all snake game logic.  
It must live in the correct folder/namespace so the rest of the project can find it.

### Action
Create a file at:

```

Games/Snake.cs

````

Add imports:

```csharp
using PixelBoard;
using System;
using System.Collections.Generic;
using System.Drawing;
````

### Why these imports are needed

* `PixelBoard` gives you `IPixel` and `Pixel`, which are used to draw colors on the display grid.
* `System` gives you basic types like `ConsoleKey`.
* `System.Collections.Generic` gives you `Queue<T>`, which is how we store the snake body.
* `System.Drawing` gives you `Color`, used only for the rainbow title screen.

Now add the class shell:

```csharp
namespace SnakeGame.Games
{
    public class Snake : IGame
    {
        // Next steps will add fields and methods here.
    }
}
```

### What this establishes

* `Snake : IGame` means this class *promises* to implement all methods in the `IGame` interface.
* Your engine can now treat `Snake` as “just another game” and call `Initialize`, `Update`, etc.

---

## Step 2 — Add the game state variables (fields)

### What you’re doing

Snake needs to remember:

* where its body is
* where its head is
* which direction it is moving
* where food is
* current score
* title screen animation state

### Action

Inside the class, add:

```csharp
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
```

### Explanation of each field

* `snake` is a **queue of coordinates**. Each item is one segment `(x, y)`.

  * We use a queue so we can easily remove the oldest segment (tail) as the snake moves.
* `snakeLength` controls how long the snake should be. Eating food increases this.
* `headX/headY` is the current head position.
* `directionX/directionY` is the movement delta per tick.

  * Example: `directionX = 1, directionY = 0` means move right each update.
* `rand` is used to generate random food positions.
* `food` stores the current food coordinate.
* `score` increments each time the snake eats food.
* `rainbowShift` changes slightly each title frame to animate the rainbow.

---

## Step 3 — Implement `Initialize()`

### What you’re doing

When the game starts, you want a clean reset:

* empty body
* default length
* start position
* start direction
* new food position
* score reset

### Action

Add this method:

```csharp
public void Initialize(IPixel[,] pixels)
{
    snake.Clear();
    snakeLength = 5;

    headX = 10;
    headY = 5;

    directionX = 1;
    directionY = 0;

    food = (rand.Next(20), rand.Next(10));
    score = 0;
}
```

### Why the food uses `Next(20)` and `Next(10)`

* Your grid is **20 wide** and **10 high**.
* `rand.Next(20)` produces 0–19
* `rand.Next(10)` produces 0–9

This guarantees food spawns inside the board.

---

## Step 4 — Implement `DrawTitle()` (title screen)

### What you’re doing

Before the game starts, you show a title screen:

1. Fill the whole board with an animated rainbow.
2. Draw a big black “S” in the center.

### Action

Add this method:

```csharp
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

    int[,] sShape =
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
                pixels[offsetX + x, offsetY + y] = new Pixel(0, 0, 0);
        }
    }
}
```

### Explanation: how the rainbow works

* The board is filled every frame.
* Each cell gets a hue based on `(i + j)` plus a slowly increasing `rainbowShift`.
* `% 1f` keeps hue in the range 0–1.
* `ColorFromHSV()` converts hue into an RGB color.

### Explanation: how the “S” is centered

* `sShape` is a 5x5 grid of 1s and 0s.
* `offsetX/offsetY` shifts that shape into the middle of the 20x10 board.
* For every `1` in the shape, we draw a black pixel.

---

## Step 5 — Implement `Update()` (one game tick)

### What you’re doing

Every tick you:

1. Check if the snake has eaten food (grow + score).
2. Clear background to a base color.
3. Move the head.
4. Wrap around edges.
5. Add the new head position to the snake body.
6. Trim the tail if the snake is too long.
7. Draw the snake.
8. Draw the food.

### Action

Add this method:

```csharp
public void Update(IPixel[,] pixels)
{
    if (headX == food.x && headY == food.y)
    {
        score++;
        snakeLength++;
        food = (rand.Next(20), rand.Next(10));
    }

    for (sbyte i = 0; i < 20; i++)
        for (sbyte j = 0; j < 10; j++)
            pixels[i, j] = new Pixel(255, 30, 255);

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
        pixels[x, y] = new Pixel(0, 255, 0);

    pixels[food.x, food.y] = new Pixel(255, 0, 0);
}
```

### Explanation: why a queue makes snake movement easy

The queue represents the body segments in order:

* Each tick, you add the head to the end: `Enqueue()`
* If the body is longer than allowed, remove the oldest: `Dequeue()`

This creates the illusion of movement without manually shifting arrays.

### Explanation: wrap-around edges

Instead of dying when you hit a wall:

* going off the right wraps to the left
* going off the bottom wraps to the top

That’s what the boundary checks do.

---

## Step 6 — Implement `HandleInput()` (direction changes)

### What you’re doing

This method updates the movement direction based on keys.
Your engine calls this when keys are pressed.

### Action

Add this method:

```csharp
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
```

### Important note about this mapping

This mapping is **exactly** what you posted, but it’s not the “standard feel” for many players:

* In most grid games, **W** means “up” (y--)
* Here, **W** means “left” (x--)

If you want classic WASD later, swap to:

```csharp
case ConsoleKey.W: directionX = 0;  directionY = -1; break;
case ConsoleKey.S: directionX = 0;  directionY = 1;  break;
case ConsoleKey.A: directionX = -1; directionY = 0;  break;
case ConsoleKey.D: directionX = 1;  directionY = 0;  break;
```

---

## Step 7 — Implement `GetScore()`

### What you’re doing

Your engine needs a way to show the score.
This method returns the current score value.

### Action

Add:

```csharp
public int GetScore() => score;
```

---

## Step 8 — Add `ColorFromHSV()` (used by the title screen)

### What you’re doing

The rainbow title uses hue values (0–360 degrees).
This helper converts HSV into RGB.

### Action

Add:

```csharp
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
```

### Why this exists

PixelBoard pixels want RGB values.
HSV is an easy way to create smooth color cycling for a rainbow.

---

## Step 9 — Full finished `Snake.cs` (reference)

Use this if someone prefers to paste the complete file after following the steps.

```csharp
using PixelBoard;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace SnakeGame.Games
{
    public class Snake : IGame
    {
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

            int[,] sShape =
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
                        pixels[offsetX + x, offsetY + y] = new Pixel(0, 0, 0);
                }
            }
        }

        public void Update(IPixel[,] pixels)
        {
            if (headX == food.x && headY == food.y)
            {
                score++;
                snakeLength++;
                food = (rand.Next(20), rand.Next(10));
            }

            for (sbyte i = 0; i < 20; i++)
                for (sbyte j = 0; j < 10; j++)
                    pixels[i, j] = new Pixel(255, 30, 255);

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
                pixels[x, y] = new Pixel(0, 255, 0);

            pixels[food.x, food.y] = new Pixel(255, 0, 0);
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
```

