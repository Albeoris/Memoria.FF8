using System.Diagnostics;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal abstract class GamePresentationHost : Game
{
    private GraphicsDeviceManager graphics = null!;
    private Func<UiPreferences> readPreferences = null!;
    private GraphicsHostReadiness? readiness;
    private ApplicationWindowIcon? applicationWindowIcon;
    private WindowTransferState? incomingWindow;
    private bool maximizeOnStartup;
    private bool paceFrames;
    private int appliedMsaaSamples;
    private int? appliedSoftwareFrameLimit;
#if MEMORIA_NATIVE
    private NativeWindowBackBufferSynchronizer? nativeBackBuffer;
    private nint nativeInputWindow;
#else
    private WindowsDisplayRefreshRate? displayRefreshRate;
#endif

    protected WindowModeController? windowMode;
    protected readonly WindowModeHotkey windowModeHotkey = new();
    protected readonly PresentationPacer presentationPacer = new();
    protected readonly PcmAudioOutput audioOutput = new();
    protected int? appliedFrameLimit;

    protected void ConfigurePresentation(UiPreferences preferences, LaunchOptions? options, Func<UiPreferences> currentPreferences, string title, bool maximize, WindowTransferState? transfer = null, bool pace = true)
    {
        readPreferences = currentPreferences;
        maximizeOnStartup = maximize;
        incomingWindow = transfer;
        paceFrames = pace;
        appliedMsaaSamples = preferences.Graphics.MsaaSamples;
        graphics = GraphicsDeviceSettings.Create(this, preferences, options, currentPreferences);
        Window.Title = title;
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60);
        MaxElapsedTime = PresentationTiming.MaximumElapsed;
        InactiveSleepTime = TimeSpan.Zero;
        if (transfer is not null)
        {
            windowMode = WindowModeController.PrepareTransferChild(Window, graphics);
        }
    }

    protected override void Initialize()
    {
        base.Initialize();
#if MEMORIA_NATIVE
        nativeInputWindow = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.ResolveHandle(Window.Title) : 0;
        if (windowMode?.SupportsHiddenPresentation != false)
        {
            nativeBackBuffer = new NativeWindowBackBufferSynchronizer(Window, graphics, nativeInputWindow);
        }
#endif
        if (incomingWindow is { } transfer)
        {
            windowMode!.RestoreTransfer(transfer);
            SynchronizeWindowBackBuffer();
            return;
        }

        IGameWindowPlatform platform = WindowModeController.CreatePlatform(Window, graphics);
        if (maximizeOnStartup)
        {
            platform.Maximize();
        }
        windowMode = new WindowModeController(platform);
        ApplyWindowMode(readPreferences().BorderlessWindow);
    }

    protected sealed override void LoadContent()
    {
        applicationWindowIcon ??= ApplicationWindowIcon.Apply(Window);
        readiness = GraphicsHostReadiness.Initialize(() => new GraphicsHostReadiness(GraphicsDevice));
        LoadGameContent();
    }

    protected abstract void LoadGameContent();

    protected void RevealTransferWindow()
    {
        windowMode!.RevealTransferChild();
#if MEMORIA_NATIVE
        nativeBackBuffer ??= new NativeWindowBackBufferSynchronizer(Window, graphics, nativeInputWindow);
#endif
        SynchronizeWindowBackBuffer();
    }

    protected bool HasWindowInputFocus()
    {
        if (!OperatingSystem.IsWindows())
        {
            return IsActive;
        }
#if MEMORIA_NATIVE
        return NativeWindowsGameWindow.HasForeground(nativeInputWindow);
#else
        return NativeWindowsGameWindow.HasForeground(Window.Handle);
#endif
    }

    protected void ApplyWindowMode(bool borderless)
    {
        windowMode?.Apply(borderless);
        SynchronizeWindowBackBuffer();
    }

    private void SynchronizeWindowBackBuffer()
    {
#if MEMORIA_NATIVE
        nativeBackBuffer?.Synchronize();
#endif
    }

    protected void ApplyPresentationSettings(UiPreferences preferences, int frameLimit, Action invalidateSceneTargets)
    {
        ApplyWindowMode(preferences.BorderlessWindow);
        if (appliedFrameLimit != frameLimit)
        {
            // Simulations own their callback clocks. The host always uses a variable display step.
            if (frameLimit != 0)
            {
                TargetElapsedTime = TimeSpan.FromSeconds(1d / frameLimit);
            }
            appliedFrameLimit = frameLimit;
            presentationPacer.Reset();
        }
        bool samplesChanged = appliedMsaaSamples != preferences.Graphics.MsaaSamples;
        if (GraphicsDeviceSettings.Apply(graphics, preferences, ref appliedMsaaSamples))
        {
#if !MEMORIA_NATIVE
            displayRefreshRate?.Invalidate();
#endif
            if (samplesChanged)
            {
                invalidateSceneTargets();
            }
        }
        int softwareFrameLimit = frameLimit;
#if !MEMORIA_NATIVE
        if (preferences.VSync && GraphicsDevice.PresentationParameters.PresentationInterval == PresentInterval.One)
        {
            displayRefreshRate ??= new WindowsDisplayRefreshRate(Window.Handle);
            softwareFrameLimit = PresentationPacingPolicy.SoftwareFrameLimit(frameLimit, true, displayRefreshRate.RefreshRate);
        }
#endif
        appliedSoftwareFrameLimit = softwareFrameLimit;
        presentationPacer.RequestFrameLimit(softwareFrameLimit);
    }

    protected void BeginPresentationUpdate()
    {
        if (paceFrames)
        {
            presentationPacer.BeginUpdate(appliedSoftwareFrameLimit ?? appliedFrameLimit ?? 60, Stopwatch.GetTimestamp());
        }
    }

    protected virtual bool CanPresentFrame => true;

    protected sealed override bool BeginDraw()
    {
        if (!CanPresentFrame || !base.BeginDraw())
        {
            return false;
        }
        if (paceFrames)
        {
            presentationPacer.WaitBeforeDraw(Stopwatch.GetTimestamp());
        }
        return true;
    }

    protected virtual bool ObservePresentTiming => false;

    protected sealed override void EndDraw()
    {
        bool observeTiming = ObservePresentTiming;
        long presentBegin = observeTiming ? Stopwatch.GetTimestamp() : 0;
        try
        {
            base.EndDraw();
        }
        catch (Exception failure) when (readiness is { FirstFramePresented: false } && !GraphicsHostReadiness.IsInitializationFailure(failure))
        {
            throw new GraphicsInitializationException($"The {GraphicsHostReadiness.Backend} graphics host could not present its first frame: {failure.Message}", failure);
        }
        long presentEnd = observeTiming ? Stopwatch.GetTimestamp() : 0;
        presentationPacer.CompletePresentedFrame();
        readiness?.Presented(Window);
        OnFramePresented(presentBegin, presentEnd);
    }

    protected virtual void OnFramePresented(long presentBegin, long presentEnd)
    {
    }

    protected Rectangle BackBufferBounds => new(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);

    protected void WriteBackBufferPng(string path)
    {
        Rectangle bounds = BackBufferBounds;
        Color[] colors = new Color[checked(bounds.Width * bounds.Height)];
        GraphicsDevice.GetBackBufferData(colors);
        using Texture2D capture = new(GraphicsDevice, bounds.Width, bounds.Height);
        capture.SetData(colors);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using Stream output = File.Create(path);
        capture.SaveAsPng(output, bounds.Width, bounds.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
#if MEMORIA_NATIVE
            nativeBackBuffer?.Dispose();
#else
            displayRefreshRate?.Dispose();
#endif
            applicationWindowIcon?.Dispose();
            presentationPacer.Dispose();
        }
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                audioOutput.Dispose();
            }
        }
    }
}
