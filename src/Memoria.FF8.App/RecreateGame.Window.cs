using System.Runtime.InteropServices;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private WindowModeController? windowMode;
    private readonly WindowModeHotkey windowModeHotkey = new();

    protected override void Initialize()
    {
        base.Initialize();
        if (options.BattleTestPath is null)
        {
            ShowWindow(Window.Handle, ShowMaximized);
        }
        windowMode = new WindowModeController(Window);
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

    private const int ShowMaximized = 3;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
}
