using Memoria.FF8.TitleScene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class TitleArtworkGraphicsHostTests
{
    [Fact]
    public void Multisampled_artwork_retains_the_backdrop_and_restores_backbuffer_state()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "Set MEMORIA_FF8_GRAPHICS_HOST_TEST=1 to run this graphics host check on a graphical Unix desktop.");
        using ArtworkGame game = new();
        game.Run();
        Assert.True(game.Completed);
    }

    private sealed class ArtworkGame : Game
    {
        private readonly GraphicsDeviceManager graphics;

        internal ArtworkGame()
        {
            graphics = new(this) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 180, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false, PreferMultiSampling = false };
            graphics.PreparingDeviceSettings += (_, settings) => settings.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            Window.Title = "Memoria title artwork validation";
        }

        internal bool Completed { get; private set; }

        protected override void Draw(GameTime gameTime)
        {
            if (Completed)
            {
                return;
            }
            GraphicsDevice.Adapter.QueryRenderTargetFormat(GraphicsProfile.HiDef, SurfaceFormat.Color, DepthFormat.None, 8, out _, out _, out int samples);
            Assert.True(samples > 1, "This graphics check requires a multisampled color target.");
            using TitleArtworkRenderer artwork = new(GraphicsDevice);
            Viewport viewport = new(9, 7, 300, 160);
            Rectangle bounds = new(0, 0, viewport.Width, viewport.Height);
            Color background = new(19, 43, 67, 255);
            Color[] direct = new Color[GraphicsDevice.PresentationParameters.BackBufferWidth * GraphicsDevice.PresentationParameters.BackBufferHeight];
            Color[] multisampled = new Color[direct.Length];
            GraphicsDevice.Clear(background);
            GraphicsDevice.Viewport = viewport;
            artwork.Draw(bounds, 3.25);
            GraphicsDevice.GetBackBufferData(direct);
            GraphicsDevice.Clear(background);
            GraphicsDevice.Viewport = viewport;
            Rectangle scissor = new(11, 12, 270, 140);
            GraphicsDevice.ScissorRectangle = scissor;
            RenderTargetUsage usage = GraphicsDevice.PresentationParameters.RenderTargetUsage;
            BlendState blend = GraphicsDevice.BlendState;
            DepthStencilState depth = GraphicsDevice.DepthStencilState;
            RasterizerState rasterizer = GraphicsDevice.RasterizerState;
            SamplerState sampler = GraphicsDevice.SamplerStates[0];
            artwork.Draw(bounds, 3.25, samples);
            Assert.Equal(viewport, GraphicsDevice.Viewport);
            Assert.Equal(scissor, GraphicsDevice.ScissorRectangle);
            Assert.Equal(usage, GraphicsDevice.PresentationParameters.RenderTargetUsage);
            Assert.Same(blend, GraphicsDevice.BlendState);
            Assert.Same(depth, GraphicsDevice.DepthStencilState);
            Assert.Same(rasterizer, GraphicsDevice.RasterizerState);
            Assert.Same(sampler, GraphicsDevice.SamplerStates[0]);
            Assert.Empty(GraphicsDevice.GetRenderTargets());
            GraphicsDevice.GetBackBufferData(multisampled);
            Assert.Equal(background, multisampled[0]);
            Assert.Equal(background, multisampled[^1]);
            Assert.Equal(background, multisampled[viewport.Y * GraphicsDevice.PresentationParameters.BackBufferWidth + viewport.X]);
            Assert.True(multisampled.Where((pixel, index) => pixel != direct[index]).Any(), "Multisampled triangle edges must differ from the direct single-sample artwork.");
            Assert.True(multisampled.Any(pixel => pixel != background), "The artwork must remain visible over the retained backdrop.");
            Completed = true;
            Exit();
        }
    }
}
