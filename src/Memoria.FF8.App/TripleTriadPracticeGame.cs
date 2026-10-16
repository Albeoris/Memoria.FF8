using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class TripleTriadPracticeGame : Game
{
    private readonly TripleTriadController controller;
    private readonly string gameDirectory;
    private readonly Action save;
    private readonly Action load;
    private readonly UiView view;
    private SpriteBatch batch = null!;
    private SystemTextRenderer text = null!;
    private Texture2D pixel = null!;
    private MonoGameUiCanvas canvas = null!;
    private KeyboardState previousKeys;
    private double opponentDelay;
    private string? notice;

    public TripleTriadPracticeGame(TripleTriadController controller, string gameDirectory, Action save, Action load)
    {
        this.controller = controller;
        this.gameDirectory = gameDirectory;
        this.save = save;
        this.load = load;
        view = controller.BuildView();
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 672, SynchronizeWithVerticalRetrace = true };
        Window.Title = "Triple Triad · Practice";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        text = new SystemTextRenderer(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        canvas = new MonoGameUiCanvas(GraphicsDevice, batch, pixel, text, gameDirectory);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        if (IsActive)
        {
            Route(Keys.Up, UiKey.Up);
            Route(Keys.Down, UiKey.Down);
            Route(Keys.Left, UiKey.Left);
            Route(Keys.Right, UiKey.Right);
            Route(Keys.PageUp, UiKey.PageUp);
            Route(Keys.PageDown, UiKey.PageDown);
            Route(Keys.Enter, UiKey.Confirm);
            Route(Keys.Escape, UiKey.Cancel);
            try
            {
                if (Pressed(Keys.F5))
                {
                    save();
                    notice = "Practice match saved.";
                }
                if (Pressed(Keys.F9))
                {
                    load();
                    notice = "Practice match loaded.";
                }
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException)
            {
                notice = exception.Message;
            }
            MouseState mouse = Mouse.GetState();
            view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            view.Pointer(mouse.X, mouse.Y, mouse.LeftButton == ButtonState.Pressed);
            opponentDelay += gameTime.ElapsedGameTime.TotalSeconds;
            if (opponentDelay >= 0.45)
            {
                controller.AdvanceOpponent();
                opponentDelay = 0;
            }
        }
        previousKeys = keys;
        base.Update(gameTime);

        bool Pressed(Keys key) => keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);
        void Route(Keys key, UiKey uiKey)
        {
            if (Pressed(key))
            {
                view.Key(uiKey);
                notice = null;
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        text.BeginFrame();
        batch.Begin(samplerState: SamplerState.LinearClamp);
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        view.Draw(canvas, new UiTheme());
        if (notice is not null)
        {
            canvas.Fill(new UiRect(12, 12, GraphicsDevice.Viewport.Width - 24, 40), new UiColor(20, 28, 42));
            canvas.Text(notice, 16, new UiRect(22, 19, GraphicsDevice.Viewport.Width - 44, 30), new UiColor(255, 224, 140));
        }
        batch.End();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        canvas.Dispose();
        text.Dispose();
        pixel.Dispose();
        batch.Dispose();
        base.UnloadContent();
    }
}
