namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private WindowModeController? windowMode;
    private readonly WindowModeHotkey windowModeHotkey = new();

    protected override void Initialize()
    {
        base.Initialize();
        previousDialogContext = SynchronizationContext.Current;
        ModelBrowserDialogs.Desktop = desktopDialogs;
        ModelBrowserDialogs.Confirmation = ShowGameConfirmationAsync;
        IGameWindowPlatform platform = WindowModeController.CreatePlatform(Window);
        if (options.BattleTestPath is null)
        {
            platform.Maximize();
        }
        windowMode = new WindowModeController(platform);
        windowMode.Apply(uiPreferences.BorderlessWindow);
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
            gameMenus?.InvalidateLiveData();
        }
    }

}
