namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private WindowModeController? windowMode;
    private readonly WindowModeHotkey windowModeHotkey = new();
#if MEMORIA_NATIVE
    private NativeWindowBackBufferSynchronizer? nativeBackBuffer;
    private nint nativeInputWindow;
#endif

    protected override void Initialize()
    {
        base.Initialize();
        previousDialogContext = SynchronizationContext.Current;
        ModelBrowserDialogs.Desktop = desktopDialogs;
        ModelBrowserDialogs.Confirmation = ShowGameConfirmationAsync;
#if MEMORIA_NATIVE
        nativeInputWindow = OperatingSystem.IsWindows() ? NativeWindowsGameWindow.ResolveHandle(Window.Title) : 0;
        nativeBackBuffer = new NativeWindowBackBufferSynchronizer(Window, graphics, nativeInputWindow);
#endif
        IGameWindowPlatform platform = WindowModeController.CreatePlatform(Window, graphics);
        if (!options.IsDiagnosticGraphicalHost)
        {
            platform.Maximize();
        }
        windowMode = new WindowModeController(platform);
        windowMode.Apply(uiPreferences.BorderlessWindow);
#if MEMORIA_NATIVE
        nativeBackBuffer.Synchronize();
#endif
    }

    private bool HasPhysicalInputFocus()
    {
        if (focusDisposed)
        {
            return false;
        }
        if (OperatingSystem.IsWindows())
        {
#if MEMORIA_NATIVE
            return NativeWindowsGameWindow.HasForeground(nativeInputWindow);
#else
            return NativeWindowsGameWindow.HasForeground(Window?.Handle ?? 0);
#endif
        }
        return IsActive;
    }

    private void UpdateWindowModeHotkey()
    {
        Microsoft.Xna.Framework.Input.KeyboardState keys = physicalInput.ReadKeyboard();
        if (windowModeHotkey.Read(keys, physicalInput.CanRead))
        {
            var preferences = uiPreferences with { BorderlessWindow = !uiPreferences.BorderlessWindow };
            preferences.Save(UiPreferencesPath);
            uiPreferences = preferences;
            windowMode?.Apply(preferences.BorderlessWindow);
#if MEMORIA_NATIVE
            nativeBackBuffer?.Synchronize();
#endif
            gameMenus?.InvalidateLiveData();
        }
    }

}
