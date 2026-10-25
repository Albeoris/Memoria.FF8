using System.Windows.Forms;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed class WindowModeController(GameWindow window)
{
    private readonly Form form = (Form)(Control.FromHandle(window.Handle) ?? throw new InvalidOperationException("The game window requires a Windows form."));
    private System.Drawing.Rectangle windowedBounds;
    private FormWindowState windowedState;

    public void Apply(bool borderless)
    {
        if (window.IsBorderless == borderless)
        {
            return;
        }
        if (borderless)
        {
            System.Drawing.Rectangle monitorBounds = Screen.FromHandle(window.Handle).Bounds;
            windowedState = form.WindowState;
            windowedBounds = windowedState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            form.WindowState = FormWindowState.Normal;
            window.IsBorderless = true;
            form.Bounds = monitorBounds;
        }
        else
        {
            window.IsBorderless = false;
            form.Bounds = windowedBounds;
            form.WindowState = windowedState;
        }
    }
}
