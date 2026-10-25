using System.Runtime.InteropServices;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    protected override void Initialize()
    {
        base.Initialize();
        if (options.BattleTestPath is null)
        {
            ShowWindow(Window.Handle, ShowMaximized);
        }
    }

    private const int ShowMaximized = 3;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
}
