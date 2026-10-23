namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private void InitializeScreenCursor(string screenId)
    {
        if (IsNamingScreen)
        {
            return;
        }
        _selections[screenId] = 0;
    }
}
