namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private void ApplyCursorPreference(string screenId)
    {
        if (screenId == "main")
        {
            _selections[screenId] = 0;
            return;
        }
        if (IsNamingScreen || !IsDemo && _readPreferences is null)
        {
            return;
        }
        bool memory = IsDemo ? Data.Settings.GetValueOrDefault("Cursor") == "Memory" : _readPreferences?.Invoke().CursorMemory == true;
        if (!memory)
        {
            _selections[screenId] = 0;
        }
    }
}
