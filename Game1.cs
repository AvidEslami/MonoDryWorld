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
        public Vector2 Velocity;
        public Color Color;
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Configuration
        private const int CREATURE_COUNT = 5000;
        private int _screenWidth;
        private int _screenHeight;
        private Random _random = new Random();

        // Game Data
        private int[,] _terrain;
        private List<Creature> _creatures;
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

            GenerateColors();
            GenerateTerrain(3);
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
            _circleTexture = CreateCircleTexture(3);
        }

        protected override void Update(GameTime gameTime)
        {
            // Escape to exit (handy for fullscreen testing)
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Math Simulation Loop
            
            int most_red_creature = -999;
            int most_red_X = 0;
            int most_red_Y = 0;
            for (int i = 0; i < _creatures.Count; i++)
            {
                var creature = _creatures[i];
                int redness = creature.Color.R - creature.Color.G - creature.Color.B;
                if (redness > most_red_creature)
                {
                    most_red_creature = redness;
                    most_red_X = (int)creature.Position.X;
                    most_red_Y = (int)creature.Position.Y;
                }
            }

            for (int i = 0; i < _creatures.Count; i++)
            {
                var creature = _creatures[i];

                int redness = creature.Color.R - creature.Color.G - creature.Color.B;
                // Print out redness values
                // Console.WriteLine($"Creature {i} - Redness: {redness}"); 

                int newX = (int)(creature.Position.X + creature.Velocity.X);
                int newY = (int)(creature.Position.Y + creature.Velocity.Y);

                // Screen bounds collision
                newX = Math.Clamp(newX, 0, _screenWidth - 1);
                newY = Math.Clamp(newY, 0, _screenHeight - 1);

                // Make the velocity just towards the most red creature
                Vector2 directionToMostRed = new Vector2(most_red_X, most_red_Y) - creature.Position;
                if (directionToMostRed != Vector2.Zero)
                {
                    // directionToMostRed.Normalize();
                    creature.Velocity = directionToMostRed; // Speed of 1 pixel per frame
                    // Round to integers -1 0 or 1 for grid movement
                    creature.Velocity = new Vector2((float)Math.Round(creature.Velocity.X), (float)Math.Round(creature.Velocity.Y));
                    if (creature.Velocity.X > 1) creature.Velocity = new Vector2(1, creature.Velocity.Y);
                    if (creature.Velocity.X < -1) creature.Velocity = new Vector2(-1, creature.Velocity.Y);
                    if (creature.Velocity.Y > 1) creature.Velocity = new Vector2(creature.Velocity.X, 1);
                    if (creature.Velocity.Y < -1) creature.Velocity = new Vector2(creature.Velocity.X, -1);
                }

                // O(1) Terrain Collision: Just check the grid array directly!
                if (_terrain[newX, newY] == 1)
                {
                    // Check if it's a horizontal or vertical wall by looking at whether or not a change in X or Y would avoid the collision
                    bool horizontalCollision = _terrain[(int)creature.Position.X, newY] == 1;
                    bool verticalCollision = _terrain[newX, (int)creature.Position.Y] == 1;
                    if (horizontalCollision && verticalCollision)
                    {
                        // Both directions blocked, reverse both velocities
                        creature.Velocity = -creature.Velocity;
                    }
                    else if (horizontalCollision)
                    {
                        // Only horizontal blocked, reverse Y velocity
                        creature.Velocity = new Vector2(creature.Velocity.X, -creature.Velocity.Y);
                    }
                    else if (verticalCollision)
                    {
                        // Only vertical blocked, reverse X velocity
                        creature.Velocity = new Vector2(-creature.Velocity.X, creature.Velocity.Y);
                    }
                    else
                    {
                        // Log something weird
                        Console.WriteLine("Unexpected collision state: no adjacent walls but collision detected!");
                    }
                }
                else
                {
                    creature.Position = new Vector2(newX, newY);
                }

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
                    Velocity = new Vector2(velocities[_random.Next(velocities.Length)], velocities[_random.Next(velocities.Length)]),
                    Color = _fullColorList[_random.Next(_fullColorList.Count)]
                });
            }
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