namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    protected override void Initialize()
    {
        base.Initialize();
        previousDialogContext = SynchronizationContext.Current;
        ModelBrowserDialogs.Desktop = desktopDialogs;
        ModelBrowserDialogs.Confirmation = ShowGameConfirmationAsync;
    }

    private bool HasPhysicalInputFocus()
    {
        return !focusDisposed && HasWindowInputFocus();
    }

    private void UpdateWindowModeHotkey()
    {
        Microsoft.Xna.Framework.Input.KeyboardState keys = physicalInput.ReadKeyboard();
        if (windowModeHotkey.Read(keys, physicalInput.CanRead))
        {
            var preferences = uiPreferences with { BorderlessWindow = !uiPreferences.BorderlessWindow };
            preferences.Save(UiPreferencesPath);
            uiPreferences = preferences;
            ApplyWindowMode(preferences.BorderlessWindow);
            gameMenus?.InvalidateLiveData();
        }
    }

}
