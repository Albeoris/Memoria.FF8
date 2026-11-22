using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Game : GamePresentationHost
{
    private readonly Ff7Installation installation;
    private readonly string mediaToolsDirectory;
    private readonly string savePath;
    private readonly Ff7ProjectionDepth projection;
    private readonly Func<int, Ff7MovieSource>? resolveMovie;
    private readonly IFf7ActorAppearance? appearance;
    private readonly CombinedSessionSave? initialEnvelope;
    private readonly LaunchOptions? launchOptions;
    private readonly UiPreferences initialPresentationPreferences;
    private SpriteBatch batch = null!;
    private Ff7Scene scene = null!;
    private readonly KeyboardInput keyboard = new();
    private readonly GameplayClock gameplayClock = new();
    private ControllerDevice? controller;
    private readonly HostPhysicalInput physicalInput;
    private int updateFrames;
    private bool frameCaptured;
    private WorldTransferPresentation? worldTransfer;
    private Texture2D? transferPixel;
    private GameResourceHost? originResources;

    public Ff7Game(Ff7Installation installation, string mediaToolsDirectory, string savePath, Ff7ProjectionDepth projection, Func<int, Ff7MovieSource>? resolveMovie, CombinedSessionSave? envelope = null, IFf7ActorAppearance? appearance = null, LaunchOptions? launchOptions = null)
    {
        this.installation = installation;
        this.mediaToolsDirectory = mediaToolsDirectory;
        this.savePath = savePath;
        this.projection = projection;
        this.resolveMovie = resolveMovie;
        initialEnvelope = envelope;
        this.launchOptions = launchOptions;
        physicalInput = new(launchOptions?.AcceptsPhysicalInput != false, () => IsActive, Keyboard.GetState, () => controller?.Read() ?? default, Mouse.GetState);
        this.appearance = appearance;
        initialPresentationPreferences = UiPreferences.Load(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).UiPreferencesPath);
        bool maximize = launchOptions?.IsDiagnosticGraphicalHost != true && launchOptions?.ResolutionSpecified != true && launchOptions?.ControlExclusive != true;
        ConfigurePresentation(initialPresentationPreferences, launchOptions, () => scene?.PresentationPreferences ?? initialPresentationPreferences, "Memoria - Final Fantasy VII", maximize, pace: launchOptions?.ControlStep != true);
        InitializePlatform();
    }

    protected override void LoadGameContent()
    {
        batch = GraphicsHostReadiness.Initialize(() => new SpriteBatch(GraphicsDevice));
        scene = new Ff7Scene(GraphicsDevice, installation, mediaToolsDirectory, projection, resolveMovie, initialEnvelope, appearance, launchOptions?.AudioEnabled ?? true, audioOutput);
        scene.ConfigureEnemyIntegrationMods(launchOptions?.ModsDirectory, launchOptions?.ModLocations);
        scene.ReadStatusStatModifiers = difficulty =>
        {
            originResources ??= new GameResourceHost(launchOptions?.GameDirectory ?? throw new InvalidOperationException("Select the FFVIII installation before entering mixed combat."), modsDirectory: launchOptions?.ModsDirectory, resourceLanguage: launchOptions?.ResourceLanguage, modelSourceDirectory: launchOptions?.ModelSourceDirectory, modLocations: launchOptions?.ModLocations);
            return originResources.ReadStatusStatModifiers(difficulty);
        };
        scene.ConfigureCharacterIdentity(launchOptions?.GameDirectory ?? throw new InvalidOperationException("FFVII character presentation requires its primary game installation."));
        scene.RenderLimit = launchOptions?.WorldRenderLimit ?? 2048;
        Window.TextInput += (_, input) =>
        {
            if (physicalInput.CanRead && saveMenu?.IsOpen != true)
            {
                scene.EnterText(input.Character);
            }
        };
        scene.ConfigureSharedDialogueDefaults(batch, launchOptions?.GameDirectory ?? throw new InvalidOperationException("FFVII shared presentation requires its primary game installation."), launchOptions?.SettingsDirectory);
        keyboard.Profile = ControlsProfile.Load(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).ControlsPreferencesPath);
        controller = new ControllerDevice();
        scene.ConfigureSharedControls(keyboard, gameplayClock, profile => profile.Save(new GlobalSettingsPaths(isolatedDirectory: launchOptions?.SettingsDirectory).ControlsPreferencesPath));
        scene.InterpolatePresentation = launchOptions?.ControlStep != true;
        scene.ConfigureAutoPlayCheckpoint(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath))!, "autoplay", "ff7", "current.ffsave"));
        InitializeUpdateHandoff();
        InitializePlayerSaves();
        scene.WorldSwitchRequested += saved =>
        {
            OpenWorldSwitchMapping(saved);
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
    }

    private void RequestCrossover(CombinedSessionSave saved)
    {
        saved.Validate();
        if (launchOptions is null)
        {
            throw new InvalidOperationException("World transfer requires game launch options.");
        }
        if (worldTransfer is not null)
        {
            throw new InvalidOperationException("A world transfer is already in progress.");
        }
        WorldProcessHandoff transfer = WorldProcessHandoff.Start(launchOptions! with { MediaToolsDirectory = mediaToolsDirectory }, saved, windowMode!.CaptureTransfer());
        worldTransfer = new WorldTransferPresentation(transfer, incoming: false, DisposePlatform, static () =>
        {
        }, static () =>
        {
        }, Exit);
        scene.SetFocusPaused(true);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        physicalInput.LoseFocus();
        keyboard.Read(default, default, false);
        windowModeHotkey.Read(default, false);
        base.OnDeactivated(sender, args);
    }

    protected override void Update(GameTime gameTime)
    {
        BeginPresentationUpdate();
        if (worldTransfer is not null)
        {
            try
            {
                worldTransfer.Update(gameTime.ElapsedGameTime);
            }
            catch (Exception failure) when (!worldTransfer.Committed && failure is IOException or InvalidOperationException or OperationCanceledException)
            {
                worldTransfer.Dispose();
                worldTransfer = null;
                scene.ShowNotice("World transfer failed: " + failure.Message, 8);
                scene.SetFocusPaused(!IsActive);
                ResetElapsedTime();
            }
            presentationPacer.RequestFrameLimit(60);
            return;
        }
        PumpPlatform();
        physicalInput.BeginFrame();
        updateControls?.Poll();
        controls?.Poll();
        UpdateWindowModeHotkey(physicalInput.ReadKeyboard(), physicalInput.CanRead);
        scene.SetFocusPaused(mappingMenu is not null || saveMenu.IsOpen || updateSave.Paused || !IsActive && launchOptions?.ControlDirectory is null);
        if (!updateSave.Paused && launchOptions?.ControlStep != true)
        {
            GamePadState pad = physicalInput.ReadController();
            keyboard.ControllerPresentation = controller!.Presentation;
            keyboard.ControllerGeneration = controller.Generation;
            bool inputActive = physicalInput.CanRead || remainingInputFrames > 0;
            KeyboardState keys = ReadControlKeys();
            MouseState mouse = physicalInput.ReadMouse();
            if (saveMenu.IsOpen)
            {
                HandlePlayerSaveInput(keyboard.Read(keys, pad, inputActive));
            }
            else if (mappingMenu is not null)
            {
                mappingMenu.Update(keyboard.Read(keys, pad, inputActive), mouse, GraphicsDevice.Viewport.Bounds);
            }
            else
            {
                scene.Update(gameTime, keys, pad, mouse, inputActive);
            }
        }
        UiPreferences preferences = scene.PresentationPreferences;
        int frameLimit = scene.GetPresentationFrameLimit(preferences, mappingMenu is not null || saveMenu.IsOpen);
        ApplyPresentationSettings(preferences, frameLimit, scene.InvalidateGraphicsPresentation);
        updateFrames++;
        if (launchOptions?.ExitAfterFrames is int frames && updateFrames >= frames || launchOptions?.ExitAfterSeconds is double seconds && gameTime.TotalGameTime.TotalSeconds >= seconds)
        {
            Exit();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        DrawGameFrame(gameTime);
        if (worldTransfer is not null)
        {
            if (transferPixel is null)
            {
                transferPixel = new Texture2D(GraphicsDevice, 1, 1);
                transferPixel.SetData([Color.White]);
            }
            worldTransfer.Draw(batch, transferPixel, GraphicsDevice.Viewport.Bounds);
        }
    }

    private void DrawGameFrame(GameTime gameTime)
    {
        if (Window.Title != scene.LocationWindowTitle)
        {
            Window.Title = scene.LocationWindowTitle;
        }
        GraphicsDevice.Clear(Color.Black);
        Rectangle bounds = BackBufferBounds;
        scene.Draw(batch, bounds);
        mappingMenu?.Draw(batch, bounds, scene.PresentationPreferences.UiFontFamily);
        DrawPlayerSaveMenu(gameTime, bounds);
        string? capturePath = pendingCapture ?? (!frameCaptured && launchOptions?.CaptureFramePath is string requested && gameTime.TotalGameTime.TotalSeconds >= launchOptions.CaptureAfterSeconds ? requested : null);
        if (capturePath is string path)
        {
            WriteBackBufferPng(path);
            frameCaptured = true;
            pendingCapture = null;
            Console.WriteLine($"ff7-captured={path} frame={updateFrames} field={scene.Session?.Field.Name ?? "title"} nativeTick={scene.Session?.Host.Process.Tick ?? 0} error={scene.Error ?? "none"}");
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        DisposePlatform();
        worldTransfer?.Dispose();
        transferPixel?.Dispose();
        controller?.Dispose();
        mappingMenu?.Dispose();
        scene?.Dispose();
        originResources?.Dispose();
        controlAppearanceAssets?.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
