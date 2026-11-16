using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeDesktopGameWindowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Vulkan_window_can_start_and_present_with_or_without_a_transfer(bool transfer)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This desktop check runs on Linux.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_WINDOW_MODE_TEST") == "1", "Set MEMORIA_FF8_WINDOW_MODE_TEST=1 to run the visible window mode check.");
        using WindowGame game = new(transfer);
        game.Run();
        Assert.Equal(2, game.PresentedFrames);
    }

    private sealed class WindowGame : Game
    {
        private readonly bool transfer;
        private readonly GraphicsDeviceManager graphics;
        private readonly NativeDesktopGameWindow window;
        private readonly bool deferPresentationUntilReveal;
        private bool sawHiddenUpdate;

        public WindowGame(bool transfer)
        {
            this.transfer = transfer;
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 640;
            graphics.PreferredBackBufferHeight = 480;
            InactiveSleepTime = TimeSpan.Zero;
            IsFixedTimeStep = false;
            Window.AllowUserResizing = true;
            window = Assert.IsType<NativeDesktopGameWindow>(WindowModeController.CreatePlatform(Window, graphics));
            if (transfer)
            {
                deferPresentationUntilReveal = !window.SupportsHiddenPresentation;
                window.PrepareTransferChild();
                Assert.True(window.IsHidden);
            }
        }

        public int PresentedFrames { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();
            if (transfer)
            {
                WindowTransferState state = window.CaptureTransfer();
                window.RestoreTransfer(state);
                Assert.True(window.IsHidden);
            }
            else
            {
                window.Maximize();
            }
        }

        protected override void Update(GameTime gameTime)
        {
            if (deferPresentationUntilReveal && window.IsHidden)
            {
                if (sawHiddenUpdate)
                {
                    window.RevealTransferChild();
                }
                sawHiddenUpdate = true;
            }
        }

        protected override bool BeginDraw()
        {
            return (!deferPresentationUntilReveal || !window.IsHidden) && base.BeginDraw();
        }

        protected override void Draw(GameTime gameTime)
        {
            Assert.Equal(transfer && !deferPresentationUntilReveal && PresentedFrames == 0, window.IsHidden);
            WindowTransferState state = window.CaptureTransfer();
            Assert.True(state.ClientWidth > 0 && state.ClientHeight > 0);
            Assert.True(state.BackBufferWidth > 0 && state.BackBufferHeight > 0);
            GraphicsDevice.Clear(Color.CornflowerBlue);
        }

        protected override void EndDraw()
        {
            base.EndDraw();
            PresentedFrames++;
            if (PresentedFrames == 1 && transfer && !deferPresentationUntilReveal)
            {
                window.RevealTransferChild();
            }
            if (PresentedFrames == 2)
            {
                Exit();
            }
        }
    }
}
