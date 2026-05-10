using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonoDryWorld
{
    // 1. We use a struct instead of a dictionary for maximum CPU cache efficiency
    public struct Creature
    {
        public Vector2 Position;
        public int Speed;
        public Vector2 Velocity;
        public Color Color;
    }

    public struct Food
    {
        public Vector2 Position;
        public Color Color;
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Configuration
        private const int CREATURE_COUNT = 5;
        private const int STARTING_SPEED = 25;
        private int _screenWidth;
        private int _screenHeight;
        private Random _random = new Random();

        // Game Data
        private int[,] _terrain;
        private List<Creature> _creatures;
        private List<Food> _food;
        private List<Color> _fullColorList;

        // Visual Assets
        private Texture2D _pixelTexture;
        private Texture2D _circleTexture;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set up Fullscreen / Resolution
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.IsFullScreen = true;

            _graphics.HardwareModeSwitch = false; // Avoid slow mode switch on some platforms (e.g. Windows)
            
            // Lock to 60 FPS (MonoGame defaults to this, but we force it here)
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1d / 60d);
        }

        protected override void Initialize()
        {
            _screenWidth = _graphics.PreferredBackBufferWidth;
            _screenHeight = _graphics.PreferredBackBufferHeight;
            _food = new List<Food>();

            GenerateColors();
            GenerateTerrain(0);
            InitializeCreatures();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Create a 1x1 white pixel for the walls
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            // Dynamically generate a circle texture (radius 8)
            _circleTexture = CreateCircleTexture(5);
        }

        protected override void Update(GameTime gameTime)
        {
            // Escape to exit (handy for fullscreen testing)
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();


            int elapsed_ticks = (int)gameTime.TotalGameTime.Ticks;
            if (elapsed_ticks % 120 == 0) // 120 ticks = 2 seconds at 60 FPS
            {
                SpawnFood();                
            }

            // Math Simulation Loop
            for (int i = 0; i < _creatures.Count; i++)
            {
                var creature = _creatures[i];

                int redness = creature.Color.R - creature.Color.G - creature.Color.B;
                // Print out redness values
                // Console.WriteLine($"Creature {i} - Redness: {redness}"); 

                // Find the closest food item
                Food? closest_food = null;
                float closest_distance = float.MaxValue;
                foreach (var food in _food)
                {
                    float distance = Vector2.Distance(creature.Position, food.Position);
                    if (distance < closest_distance)
                    {
                        closest_distance = distance;
                        closest_food = food;
                    }
                }

                // Move towards the closest food item
                if (closest_food != null)
                {
                    Vector2 direction = closest_food.Value.Position - creature.Position;
                    if (direction.Length() > 3) // If we're more than 3 pixels away, move towards the food
                    {
                        direction.Normalize();
                        float speed_multiplier = creature.Speed / 100f;
                        creature.Velocity = direction * speed_multiplier;
                    }
                    else
                    {
                        // We are eating the food
                        // Delete the food item from the list
                        _food.Remove(closest_food.Value);
                        // Increase creature speed by 10, up to a maximum of 255 (to fit in the red channel)
                        creature.Speed = Math.Min(creature.Speed + 10, 255);
                        // Update creature color to reflect new speed (redder is faster)
                        creature.Color = new Color(creature.Speed, 0, 0);
                    }
                }
                else {
                    creature.Velocity = Vector2.Zero; // No food, no movement
                }

                float newX = (creature.Position.X + creature.Velocity.X);
                float newY = (creature.Position.Y + creature.Velocity.Y);

                // Screen bounds collision
                newX = Math.Clamp(newX, 0, _screenWidth - 1);
                newY = Math.Clamp(newY, 0, _screenHeight - 1);

                creature.Position = new Vector2(newX, newY);

                // Reassign the modified struct back to the list
                _creatures[i] = creature;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // Begin rendering sequence
            _spriteBatch.Begin();

            // 1. Draw Terrain
            for (int x = 0; x < _screenWidth; x++)
            {
                for (int y = 0; y < _screenHeight; y++)
                {
                    if (_terrain[x, y] == 1)
                    {
                        _spriteBatch.Draw(_pixelTexture, new Vector2(x, y), Color.DarkGray);
                    }
                }
            }

            // 2. Draw Creatures
            // We shift the draw position by half the texture size to center the circle on the coordinate
            Vector2 originOffset = new Vector2(_circleTexture.Width / 2f, _circleTexture.Height / 2f);
            
            foreach (var creature in _creatures)
            {
                _spriteBatch.Draw(_circleTexture, creature.Position - originOffset, creature.Color);
            }

            // 3. Draw Food
            foreach (var food in _food)            {
                _spriteBatch.Draw(_circleTexture, food.Position - originOffset, food.Color);
            }

            _spriteBatch.End();

            base.Draw(gameTime);

        }

        #region Helper Methods

        private void GenerateColors()
        {
            _fullColorList = new List<Color>();
            for (int r = 0; r <= 255; r += 8)
                for (int g = 0; g <= 255; g += 8)
                    for (int b = 0; b <= 255; b += 8)
                        _fullColorList.Add(new Color(r, g, b));
        }

        private void GenerateTerrain(int wallCount)
        {
            _terrain = new int[_screenWidth, _screenHeight];

            // Horizontal walls
            for (int i = 0; i < wallCount; i++)
            {
                int ranY = _random.Next(0, _screenHeight);
                for (int x = 0; x < _screenWidth; x++) _terrain[x, ranY] = 1;
            }

            // Vertical walls
            for (int i = 0; i < wallCount; i++)
            {
                int ranX = _random.Next(0, _screenWidth);
                for (int y = 0; y < _screenHeight; y++) _terrain[ranX, y] = 1;
            }

            // Perimeter walls
            for (int x = 0; x < _screenWidth; x++)
            {
                _terrain[x, 0] = 1;
                _terrain[x, _screenHeight - 1] = 1;
            }
            for (int y = 0; y < _screenHeight; y++)
            {
                _terrain[0, y] = 1;
                _terrain[_screenWidth - 1, y] = 1;
            }
        }

        private void InitializeCreatures()
        {
            _creatures = new List<Creature>();
            int[] velocities = { -1, 1 };

            for (int i = 0; i < CREATURE_COUNT; i++)
            {
                _creatures.Add(new Creature
                {
                    Position = new Vector2(_random.Next(0, _screenWidth), _random.Next(0, _screenHeight)),
                    Speed = STARTING_SPEED,
                    Velocity = new Vector2(0, 0),
                    // Color is R: Speed, G: 0, B: 0 to visualize speediness (redder is faster)
                    Color = new Color(STARTING_SPEED, 0, 0) // Start all creatures as red (fast)
                });
            }
        }

        private void SpawnFood()
        {
            // Randomly spawn a food item on the map in a random place in the form of a green circle (similar to creatures but green and static)
            _food.Add(new Food
            {
                Position = new Vector2(_random.Next(0, _screenWidth), _random.Next(0, _screenHeight)),
                Color = Color.Green
            });
        }

        private Texture2D CreateCircleTexture(int radius)
        {
            int diameter = radius * 2;
            Texture2D texture = new Texture2D(GraphicsDevice, diameter, diameter);
            Color[] colorData = new Color[diameter * diameter];

            float radiusSquared = radius * radius;

            for (int x = 0; x < diameter; x++)
            {
                for (int y = 0; y < diameter; y++)
                {
                    int index = x * diameter + y;
                    Vector2 pos = new Vector2(x - radius, y - radius);
                    if (pos.LengthSquared() <= radiusSquared)
                        colorData[index] = Color.White; // White allows us to tint it later
                    else
                        colorData[index] = Color.Transparent;
                }
            }
            texture.SetData(colorData);
            return texture;
        }

        #endregion
    }
}