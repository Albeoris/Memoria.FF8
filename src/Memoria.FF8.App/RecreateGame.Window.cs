namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private WindowModeController? windowMode;
    private readonly WindowModeHotkey windowModeHotkey = new();
#if MEMORIA_NATIVE
    private NativeWindowBackBufferSynchronizer? nativeBackBuffer;
#endif

    protected override void Initialize()
    {
        base.Initialize();
        previousDialogContext = SynchronizationContext.Current;
        ModelBrowserDialogs.Desktop = desktopDialogs;
        ModelBrowserDialogs.Confirmation = ShowGameConfirmationAsync;
#if MEMORIA_NATIVE
        nativeBackBuffer = new NativeWindowBackBufferSynchronizer(Window, graphics);
#endif
        IGameWindowPlatform platform = WindowModeController.CreatePlatform(Window, graphics);
        if (options.BattleTestPath is null)
        {
            platform.Maximize();
        }
        windowMode = new WindowModeController(platform);
        windowMode.Apply(uiPreferences.BorderlessWindow);
#if MEMORIA_NATIVE
        nativeBackBuffer.Synchronize();
#endif
    }

    private void UpdateWindowModeHotkey()
    {
        Microsoft.Xna.Framework.Input.KeyboardState keys = options.BattleTestPath is null ? Microsoft.Xna.Framework.Input.Keyboard.GetState() : default;
        if (windowModeHotkey.Read(keys, IsActive))
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
