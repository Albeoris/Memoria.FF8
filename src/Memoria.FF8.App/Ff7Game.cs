using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Game : Game
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
    private readonly UiPreferences initialPresentationPreferences;
    private int appliedMsaaSamples;
    private WindowModeController? windowMode;
#if MEMORIA_NATIVE
    private NativeWindowBackBufferSynchronizer? nativeBackBuffer;
#endif
    private SpriteBatch batch = null!;
    private Ff7Scene scene = null!;
    private readonly KeyboardInput keyboard = new();
    private readonly GameplayClock gameplayClock = new();
    private ControllerDevice? controller;
    private readonly PresentationPacer presentationPacer = new();
    private int updateFrames;
    private bool frameCaptured;
    private readonly Func<CombinedSessionSave, PreparedCrossover>? prepareCrossover;
    public PreparedCrossover? RequestedCrossover { get; private set; }

    public Ff7Game(Ff7Installation installation, string mediaToolsDirectory, string savePath, Ff7ProjectionDepth projection, Func<int, Ff7MovieSource> resolveMovie, CombinedSessionSave? envelope = null, IFf7ActorAppearance? appearance = null, LaunchOptions? launchOptions = null, Func<CombinedSessionSave, PreparedCrossover>? prepareCrossover = null)
    {
        this.installation = installation;
        this.mediaToolsDirectory = mediaToolsDirectory;
        this.savePath = savePath;
        this.projection = projection;
        this.resolveMovie = resolveMovie;
        initialEnvelope = envelope;
        this.launchOptions = launchOptions;
        this.appearance = appearance;
        this.prepareCrossover = prepareCrossover;
        initialPresentationPreferences = UiPreferences.Load(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).UiPreferencesPath);
        appliedMsaaSamples = initialPresentationPreferences.Graphics.MsaaSamples;
        graphics = GraphicsDeviceSettings.Create(this, initialPresentationPreferences, launchOptions, () => scene?.PresentationPreferences ?? initialPresentationPreferences);
        Window.Title = "Memoria - Final Fantasy VII";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
    }

    protected override void Initialize()
    {
        base.Initialize();
#if MEMORIA_NATIVE
        nativeBackBuffer = new NativeWindowBackBufferSynchronizer(Window, graphics);
#endif
        IGameWindowPlatform platform = WindowModeController.CreatePlatform(Window, graphics);
        if (launchOptions?.IsDiagnosticGraphicalHost != true && launchOptions?.ResolutionSpecified != true && launchOptions?.ControlExclusive != true)
        {
            platform.Maximize();
        }
        windowMode = new WindowModeController(platform);
        windowMode.Apply(scene.PresentationPreferences.BorderlessWindow);
#if MEMORIA_NATIVE
        nativeBackBuffer.Synchronize();
#endif
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        scene = new Ff7Scene(GraphicsDevice, installation, mediaToolsDirectory, projection, resolveMovie, initialEnvelope, appearance, launchOptions?.AudioEnabled ?? true);
        Window.TextInput += (_, input) =>
        {
            if (IsActive && launchOptions?.AcceptsPhysicalInput != false)
            {
                scene.EnterText(input.Character);
            }
        };
        scene.ConfigureSharedDialogueDefaults(batch, launchOptions?.GameDirectory ?? throw new InvalidOperationException("FFVII shared presentation requires its primary game installation."), launchOptions?.SettingsDirectory);
        keyboard.Profile = ControlsProfile.Load(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).ControlsPreferencesPath);
        controller = new ControllerDevice();
        scene.ConfigureSharedControls(keyboard, gameplayClock);
        scene.InterpolatePresentation = launchOptions?.ControlStep != true;
        scene.ConfigureAutoPlayCheckpoint(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath))!, "autoplay", "ff7", "current.ffsave"));
        InitializeUpdateHandoff();
        scene.SaveRequested += saved => CombinedSessionCodec.Save(savePath, saved);
        scene.ContinueRequested += () =>
        {
            try
            {
                using FileStream stream = File.OpenRead(savePath);
                CombinedSessionSave saved = CombinedSessionCodec.Read(stream);
                if (saved.Ff8Payload is { Length: > 0 })
                {
                    RequestCrossover(saved);
                }
                else
                {
                    RestoreStandalone(saved);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or InvalidOperationException)
            {
                scene.ShowNotice(exception.Message);
            }
        };
        scene.WorldSwitchRequested += saved =>
        {
            RequestCrossover(saved with { ActiveGame = SessionGame.Ff8 });
        };
        if (launchOptions?.Ff7StartNewGame == true)
        {
            scene.StartNewGame();
        }
        else if (launchOptions?.CrossoverStatePath is not null && initialEnvelope?.Ff7 is not null)
        {
            scene.Continue(initialEnvelope);
        }
        if (launchOptions?.ControlDirectory is string controlDirectory)
        {
            controls = new ControlQueue(controlDirectory, DispatchControl);
        }
        base.LoadContent();
    }

    private void RequestCrossover(CombinedSessionSave saved)
    {
        PreparedCrossover prepared = (prepareCrossover ?? throw new InvalidOperationException("Combined continuation preparation is unavailable."))(saved);
        RequestedCrossover = prepared;
        Exit();
    }

    protected override void Update(GameTime gameTime)
    {
        updateControls?.Poll();
        controls?.Poll();
        scene.SetFocusPaused(updateSave.Paused || !IsActive && launchOptions?.ControlDirectory is null);
        if (!updateSave.Paused && launchOptions?.ControlStep != true)
        {
            GamePadState pad = launchOptions?.AcceptsPhysicalInput == false ? default : controller!.Read();
            keyboard.ControllerPresentation = controller!.Presentation;
            keyboard.ControllerGeneration = controller.Generation;
            scene.Update(gameTime, ReadControlKeys(), pad);
        }
        UiPreferences preferences = scene.PresentationPreferences;
        windowMode?.Apply(preferences.BorderlessWindow);
#if MEMORIA_NATIVE
        nativeBackBuffer?.Synchronize();
#endif
        bool samplesChanged = appliedMsaaSamples != preferences.Graphics.MsaaSamples;
        if (GraphicsDeviceSettings.Apply(graphics, preferences, ref appliedMsaaSamples) && samplesChanged)
        {
            scene.InvalidateGraphicsPresentation();
        }
        if (launchOptions?.ControlStep != true)
        {
            presentationPacer.WaitForNextFrame(scene.PresentationFrameLimit);
        }
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
        scene.Draw(batch, new Rectangle(0, 0, width, height));
        string? capturePath = pendingCapture ?? (!frameCaptured && launchOptions?.CaptureFramePath is string requested && gameTime.TotalGameTime.TotalSeconds >= launchOptions.CaptureAfterSeconds ? requested : null);
        if (capturePath is string path)
        {
            Color[] colors = new Color[width * height];
            GraphicsDevice.GetBackBufferData(colors);
            using Texture2D capture = new(GraphicsDevice, width, height);
            capture.SetData(colors);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using Stream output = File.Create(path);
            capture.SaveAsPng(output, width, height);
            frameCaptured = true;
            pendingCapture = null;
            Console.WriteLine($"ff7-captured={path} frame={updateFrames} field={scene.Session?.Field.Name ?? "title"} nativeTick={scene.Session?.Host.Process.Tick ?? 0} error={scene.Error ?? "none"}");
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        presentationPacer.Dispose();
#if MEMORIA_NATIVE
        nativeBackBuffer?.Dispose();
#endif
        controller?.Dispose();
        scene?.Dispose();
        controlAppearanceAssets?.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
