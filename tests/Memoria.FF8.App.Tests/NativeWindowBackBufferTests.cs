using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeWindowBackBufferTests
{
    [Fact]
    public void Native_maximize_borderless_and_restore_keep_client_viewport_and_backbuffer_equal()
    {
#if MEMORIA_VULKAN
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The native Windows window adapter requires Windows.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_WINDOW_MODE_TEST") == "1", "Set MEMORIA_FF8_WINDOW_MODE_TEST=1 to run the visible window mode check.");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using Game game = new();
                game.Window.Title = "Memoria native resize test " + Guid.NewGuid().ToString("N");
                game.Window.AllowUserResizing = true;
                using GraphicsDeviceManager graphics = new(game) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 240, GraphicsProfile = GraphicsProfile.HiDef };
                game.RunOneFrame();
                using NativeWindowBackBufferSynchronizer synchronizer = new(game.Window, graphics);
                IGameWindowPlatform platform = WindowModeController.CreatePlatform(game.Window);
                WindowModeController controller = new(platform);
                platform.Maximize();
                AssertDimensions();
                controller.Apply(true);
                AssertDimensions();
                controller.Apply(false);
                AssertDimensions();

                void AssertDimensions()
                {
                    synchronizer.Synchronize();
                    Point actual = NativeWindowsGameWindow.GetClientSize(NativeWindowsGameWindow.ResolveHandle(game.Window.Title));
                    PresentationParameters presentation = game.GraphicsDevice.PresentationParameters;
                    Assert.Equal(actual.X, game.Window.ClientBounds.Width);
                    Assert.Equal(actual.Y, game.Window.ClientBounds.Height);
                    Assert.Equal(actual.X, presentation.BackBufferWidth);
                    Assert.Equal(actual.Y, presentation.BackBufferHeight);
                    Assert.Equal(actual.X, game.GraphicsDevice.Viewport.Width);
                    Assert.Equal(actual.Y, game.GraphicsDevice.Viewport.Height);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Native window resize check timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
#else
        Assert.Skip("This check requires the Native Vulkan host build.");
#endif
    }
}
