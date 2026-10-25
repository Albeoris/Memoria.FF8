using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WindowModeControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Borderless_covers_monitor_and_restores_windowed_geometry(bool maximized)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows window geometry requires Windows.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_WINDOW_MODE_TEST") == "1", "Set MEMORIA_FF8_WINDOW_MODE_TEST=1 to run the visible window mode check.");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                using Game game = new();
                using GraphicsDeviceManager graphics = new(game);
                game.RunOneFrame();
                Form form = (Form)Control.FromHandle(game.Window.Handle)!;
                game.Window.AllowUserResizing = true;
                form.Bounds = new System.Drawing.Rectangle(80, 100, 800, 600);
                form.Show();
                if (maximized)
                {
                    form.WindowState = FormWindowState.Maximized;
                }
                System.Drawing.Rectangle bounds = maximized ? form.RestoreBounds : form.Bounds;
                FormWindowState state = form.WindowState;
                System.Drawing.Rectangle monitor = Screen.FromHandle(game.Window.Handle).Bounds;
                WindowModeController controller = new(game.Window);
                controller.Apply(true);
                Assert.True(game.Window.IsBorderless);
                Assert.Equal(FormBorderStyle.None, form.FormBorderStyle);
                Assert.Equal(monitor, form.Bounds);
                Assert.Equal(form.ClientSize.Width, game.GraphicsDevice.PresentationParameters.BackBufferWidth);
                Assert.Equal(form.ClientSize.Height, game.GraphicsDevice.PresentationParameters.BackBufferHeight);
                controller.Apply(true);
                controller.Apply(false);
                Assert.False(game.Window.IsBorderless);
                Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
                Assert.Equal(state, form.WindowState);
                Assert.Equal(bounds, maximized ? form.RestoreBounds : form.Bounds);
                Assert.Equal(form.ClientSize.Width, game.GraphicsDevice.PresentationParameters.BackBufferWidth);
                Assert.Equal(form.ClientSize.Height, game.GraphicsDevice.PresentationParameters.BackBufferHeight);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Window mode check timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
