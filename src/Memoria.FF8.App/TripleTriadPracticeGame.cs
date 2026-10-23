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
    private readonly UiNavigationRepeat horizontalRepeat = new();
    private readonly UiNavigationRepeat verticalRepeat = new();
    private FieldAudioPresentation? audio;
    private TripleTriadAudioPresentation? cardAudio;
    private double opponentDelay;
    private string? notice;

    public TripleTriadPracticeGame(TripleTriadController controller, string gameDirectory, Action save, Action load)
    {
        this.controller = controller;
        this.gameDirectory = gameDirectory;
        this.save = save;
        this.load = load;
        controller.ReplayDeclined += Exit;
        controller.EnablePresentation();
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
        canvas.PrepareTripleTriad(controller);
        audio = new FieldAudioPresentation(Window.Handle, gameDirectory, "");
        cardAudio = new TripleTriadAudioPresentation(audio, controller);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        if (IsActive)
        {
            controller.AdvancePresentation(gameTime.ElapsedGameTime);
            int vertical = verticalRepeat.Update(Direction(Keys.Up, Keys.Down), PressedDirection(Keys.Up, Keys.Down), gameTime.ElapsedGameTime);
            int horizontal = horizontalRepeat.Update(Direction(Keys.Left, Keys.Right), PressedDirection(Keys.Left, Keys.Right), gameTime.ElapsedGameTime);
            if (vertical != 0)
            {
                view.Key(vertical < 0 ? UiKey.Up : UiKey.Down);
                notice = null;
            }
            if (horizontal != 0)
            {
                view.Key(horizontal < 0 ? UiKey.Left : UiKey.Right);
                notice = null;
            }
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
                    opponentDelay = 0;
                    cardAudio?.Dispose();
                    cardAudio = new TripleTriadAudioPresentation(audio!, controller);
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
            if (!controller.IsAnimating && controller.Match is { CurrentPlayer: 1, Outcome: Runtime.TripleTriadOutcome.InProgress } && !controller.AwaitingRules && controller.TradingHand is null)
            {
                opponentDelay += gameTime.ElapsedGameTime.TotalSeconds;
                if (opponentDelay >= 0.45)
                {
                    controller.AdvanceOpponent();
                    opponentDelay = 0;
                }
            }
            else
            {
                opponentDelay = 0;
            }
        }
        audio?.Update();
        if (!IsActive)
        {
            view.Pointer(0, 0, false);
            horizontalRepeat.Update(0, 0, TimeSpan.Zero);
            verticalRepeat.Update(0, 0, TimeSpan.Zero);
            opponentDelay = 0;
        }
        previousKeys = keys;
        base.Update(gameTime);

        bool Pressed(Keys key) => keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);
        int Direction(Keys negative, Keys positive) => (keys.IsKeyDown(positive) ? 1 : 0) - (keys.IsKeyDown(negative) ? 1 : 0);
        int PressedDirection(Keys negative, Keys positive) => (Pressed(positive) ? 1 : 0) - (Pressed(negative) ? 1 : 0);
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
        cardAudio?.Dispose();
        audio?.Dispose();
        canvas.Dispose();
        text.Dispose();
        pixel.Dispose();
        batch.Dispose();
        base.UnloadContent();
    }
}
