using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class Ff7Game : Game
{
    private readonly Ff7Installation installation;
    private readonly string mediaToolsDirectory;
    private readonly string savePath;
    private readonly Ff7ProjectionDepth projection;
    private readonly Func<int, Ff7MovieSource> resolveMovie;
    private readonly IFf7ActorAppearance? appearance;
    private readonly CombinedSessionSave? initialEnvelope;
    private readonly GraphicsDeviceManager graphics;
    private readonly LaunchOptions? launchOptions;
    private SpriteBatch batch = null!;
    private Ff7Scene scene = null!;
    private int updateFrames;
    private bool frameCaptured;

    public Ff7Game(Ff7Installation installation, string mediaToolsDirectory, string savePath, Ff7ProjectionDepth projection, Func<int, Ff7MovieSource> resolveMovie, CombinedSessionSave? envelope = null, IFf7ActorAppearance? appearance = null, LaunchOptions? launchOptions = null)
    {
        this.installation = installation;
        this.mediaToolsDirectory = mediaToolsDirectory;
        this.savePath = savePath;
        this.projection = projection;
        this.resolveMovie = resolveMovie;
        initialEnvelope = envelope;
        this.launchOptions = launchOptions;
        this.appearance = appearance;
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = launchOptions?.Width ?? 960, PreferredBackBufferHeight = launchOptions?.Height ?? 720, GraphicsProfile = GraphicsProfile.HiDef };
        graphics.SynchronizeWithVerticalRetrace = true;
        Window.Title = "Memoria - Final Fantasy VII";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        scene = new Ff7Scene(GraphicsDevice, installation, mediaToolsDirectory, projection, resolveMovie, initialEnvelope, appearance, launchOptions?.AudioEnabled ?? true);
        scene.SaveRequested += saved => CombinedSessionCodec.Save(savePath, saved);
        scene.ContinueRequested += () =>
        {
            using FileStream stream = File.OpenRead(savePath);
            scene.Continue(CombinedSessionCodec.Read(stream));
        };
        if (launchOptions?.Ff7StartNewGame == true)
        {
            scene.StartNewGame();
        }
        else if (launchOptions?.CrossoverStatePath is not null && initialEnvelope?.Ff7 is not null)
        {
            scene.Continue(initialEnvelope);
        }
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        scene.Update(gameTime, Keyboard.GetState());
        updateFrames++;
        if (launchOptions?.ExitAfterFrames is int frames && updateFrames >= frames || launchOptions?.ExitAfterSeconds is double seconds && gameTime.TotalGameTime.TotalSeconds >= seconds)
        {
            Exit();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        int fitWidth = Math.Min(width, height * 4 / 3);
        int fitHeight = fitWidth * 3 / 4;
        scene.Draw(batch, new Rectangle((width - fitWidth) / 2, (height - fitHeight) / 2, fitWidth, fitHeight));
        if (!frameCaptured && launchOptions?.CaptureFramePath is string path && gameTime.TotalGameTime.TotalSeconds >= launchOptions.CaptureAfterSeconds)
        {
            Color[] colors = new Color[width * height];
            GraphicsDevice.GetBackBufferData(colors);
            using Texture2D capture = new(GraphicsDevice, width, height);
            capture.SetData(colors);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using Stream output = File.Create(path);
            capture.SaveAsPng(output, width, height);
            frameCaptured = true;
            Console.WriteLine($"ff7-captured={path} frame={updateFrames} field={scene.Session?.Field.Name ?? "title"} nativeTick={scene.Session?.Host.Process.Tick ?? 0} error={scene.Error ?? "none"}");
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        scene?.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
