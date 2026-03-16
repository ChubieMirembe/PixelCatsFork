# Tutorial: Implement the missing Snake game functions (Beginner)


---

## Step 0: Know what you're building

By the end you will have:

- A snake that moves continuously
- Food that spawns randomly
- Score that increases when eating food
- Snake gets longer after eating
- Wrap-around walls (default), with an option for deadly walls
- Game over when hitting yourself (or walls if deadly)
- A 6-digit game-over code generated on death (for your 7-seg display)
- A red-tinted game-over screen

---

## Step 1: Understand the game variables 

These store the snake body, movement, food, score, speed control, and game state:

```csharp
// Game variables
private Queue<(int x, int y)> snake = new Queue<(int x, int y)>();
private int snakeLength = 5;
private int headX = 10;
private int headY = 5;
private int directionX = 0;
private int directionY = 1;
private int nextDirectionX = 0;
private int nextDirectionY = 1;
private Random rand = new Random();
private (int x, int y) food;
private int score = 0;
private float rainbowShift = 0f;
private bool gameOver = false;
private bool wallsAreDeadly = false;
private int moveCounter = 0;
private string gameOverCode = null;
````

What each key part does:

* `snake`: queue of body segments, oldest at the front (tail)
* `snakeLength`: how long the snake should be
* `headX/headY`: current head location
* `directionX/directionY`: current direction used on the next movement tick
* `nextDirectionX/nextDirectionY`: buffered direction from input (prevents instant reverse issues)
* `food`: where the food is
* `moveCounter`: used to slow down movement (snake only moves every N updates)
* `wallsAreDeadly`: if true, hitting the wall kills you; otherwise you wrap
* `gameOverCode`: a 6-digit code for your 7-seg display once you die

---

## Step 2: Implement `Initialize(IPixel[,] pixels)`

`Initialize` runs when the game starts. It must reset everything to a clean state.


```csharp
public void Initialize(IPixel[,] pixels)
{
    // Reset to original values
    snake.Clear();
    snakeLength = 5;
    headX = 10;
    headY = 5;
    directionX = 0;
    directionY = 1;
    nextDirectionX = 0;
    nextDirectionY = 1;
    food = (rand.Next(20), rand.Next(10));
    score = 0;
    gameOver = false;
    moveCounter = 0;
    gameOverCode = null;  // Reset code

    // Initialize snake body
    for (int i = 0; i < snakeLength; i++)
    {
        snake.Enqueue((headX, headY - i));
    }
}
```

What to check after this step:

* Snake body is created as 5 segments
* Food exists somewhere on the 10x20 board
* Score is reset

---

## Step 3: Implement `Update(IPixel[,] pixels)`

`Update` is called repeatedly by the main loop.
It handles:

* speed timing (move only every N frames)
* applying buffered direction
* moving the head
* wall handling (wrap or deadly)
* self collision
* food collision + growth
* updating body queue
* drawing the frame

Add this method:

```csharp
public void Update(IPixel[,] pixels)
{
    if (gameOver)
    {
        DrawGameOver(pixels);
        return;
    }

    moveCounter++;

    if (moveCounter < GetMoveSpeed())
    {
        DrawGame(pixels);
        return;
    }

    moveCounter = 0;

    directionX = nextDirectionX;
    directionY = nextDirectionY;

    // Move snake
    headX += directionX;
    headY += directionY;

    // Check wall collision
    if (wallsAreDeadly)
    {
        // Walls are deadly - game over
        if (headX < 0 || headX >= 20 || headY < 0 || headY >= 10)
        {
            gameOver = true;
            gameOverCode = CodeGenerator.GenerateSixDigitCode(); 
            DrawGameOver(pixels);
            return;
        }
    }
    else
    {
        // Walls wrap around
        if (headX >= 20) headX = 0;
        if (headX < 0) headX = 19;
        if (headY >= 10) headY = 0;
        if (headY < 0) headY = 9;
    }

    // Check self-collision (classic Snake game over condition)
    if (snake.Contains((headX, headY)))
    {
        gameOver = true;
        gameOverCode = CodeGenerator.GenerateSixDigitCode();  // Generate code
        DrawGameOver(pixels);
        return;
    }

    // Check if snake eats food
    if (headX == food.x && headY == food.y)
    {
        score++;
        snakeLength++;

        // Spawn food in empty location
        do
        {
            food = (rand.Next(20), rand.Next(10));
        } while (snake.Contains(food) || (food.x == headX && food.y == headY));
    }

    // Update snake body
    snake.Enqueue((headX, headY));
    if (snake.Count > snakeLength)
        snake.Dequeue();

    // Draw game
    DrawGame(pixels);
}
```

Important detail:

* The snake doesn't move every update. It waits until `moveCounter` hits the current speed value.
* That's how you control difficulty without a timer.

---

## Step 4: Implement `HandleInput(ConsoleKey key, ref bool stateChanged)`

This method:

* ignores movement if game over
* allows Escape to request leaving the play state
* buffers direction changes (writes into `nextDirectionX/Y`)
* prevents reversing into yourself by checking current direction

Add this exactly:

```csharp
public void HandleInput(ConsoleKey key, ref bool stateChanged)
{
    if (gameOver)
    {
        // If game is over, pressing any key could be used to return to title or restart.
        // We do not change state here; Program handles transitions via stateChanged or IsGameOver check.
        if (key == ConsoleKey.Escape)
        {
            stateChanged = true;
        }
        return;
    }

    // Buffer input to prevent reversing into yourself mid-frame
    switch (key)
    {
        case ConsoleKey.W:
        case ConsoleKey.UpArrow:
            // Can't reverse direction
            if (directionX != 1)
            {
                nextDirectionX = -1;
                nextDirectionY = 0;
            }
            break;
        case ConsoleKey.S:
        case ConsoleKey.DownArrow:
            if (directionX != -1)
            {
                nextDirectionX = 1;
                nextDirectionY = 0;
            }
            break;
        case ConsoleKey.A:
        case ConsoleKey.LeftArrow:
            if (directionY != 1)
            {
                nextDirectionX = 0;
                nextDirectionY = -1;
            }
            break;
        case ConsoleKey.D:
        case ConsoleKey.RightArrow:
            if (directionY != -1)
            {
                nextDirectionX = 0;
                nextDirectionY = 1;
            }
            break;
        case ConsoleKey.Escape:
            // Signal Program that user requested to leave the playing state
            stateChanged = true;
            break;
    }
}
```

What to check:

* You can turn smoothly
* You can't reverse instantly into your own body
* Escape triggers `stateChanged = true`

---

## Step 5: Add the helper methods for score + game over

Your Program might want to check if the game ended and what code was generated.

Add these methods:

```csharp
// Expose game-over status so Program can reliably transition to GameOver and export final score. 
public bool IsGameOver() => gameOver;

public string GetGameOverCode() => gameOverCode;  // Return the generated code

public int GetScore() => score;
```

---

## Step 6: Implement the speed function `GetMoveSpeed()`

This returns how many `Update` calls to wait before moving once.
Lower number = faster snake.

Add this exactly:

```csharp
private int GetMoveSpeed()
{
    if (score < 5) return 3;       // Medium start - more engaging
    if (score < 10) return 2;      // Fast
    if (score < 20) return 1;      // Very fast - challenging
    return 1;                       // Keep at very fast
}
```

---

## Step 7: Implement `DrawGame(IPixel[,] pixels)`

This draws:

* background
* snake body
* snake head
* food

Add this exactly:

```csharp
private void DrawGame(IPixel[,] pixels)
{
    // Clear background
    for (sbyte i = 0; i < 20; i++)
    {
        for (sbyte j = 0; j < 10; j++)
        {
            pixels[i, j] = new Pixel(20, 20, 40);
        }
    }

    // Draw snake
    foreach (var (x, y) in snake.Take(snake.Count - 1))
    {
        pixels[x, y] = new Pixel(0, 180, 0);
    }

    // Draw snake head
    if (snake.Count > 0)
    {
        var head = snake.Last();
        pixels[head.x, head.y] = new Pixel(0, 255, 0);
    }

    // Draw food
    pixels[food.x, food.y] = new Pixel(255, 0, 0);
}
```

---

## Step 8: Implement `DrawGameOver(IPixel[,] pixels)`

The game over effect:

1. Draw last frame of the game
2. Overlay a red tint across the whole board

Add this exactly:

```csharp
private void DrawGameOver(IPixel[,] pixels)
{
    // Draw the final game state 
    DrawGame(pixels);

    // Flash red overlay
    for (int i = 0; i < 20; i++)
    {
        for (int j = 0; j < 10; j++)
        {
            // Red tint overlay
            pixels[i, j] = new Pixel(150, 0, 0);
        }
    }
}
```

---


