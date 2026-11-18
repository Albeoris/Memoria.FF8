using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeGpuTextureReuseHostTests
{
    [Fact]
    public void Native_game_host_preserves_texture_pixels_ownership_invalidation_and_combined_budgets()
    {
#if MEMORIA_NATIVE
        Assert.SkipUnless(OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "Set MEMORIA_FF8_GRAPHICS_HOST_TEST=1 to run this graphics host check on a graphical Unix desktop.");
        using TextureReuseGame game = new();
        game.Run();
        Assert.True(game.Completed);
#else
        Assert.Skip("This check requires the Native graphics host build.");
#endif
    }

    private sealed class TextureReuseGame : Game
    {
        private readonly GraphicsDeviceManager graphics;

        internal TextureReuseGame()
        {
            graphics = new(this) { PreferredBackBufferWidth = 128, PreferredBackBufferHeight = 64, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            Window.Title = "Memoria native texture reuse validation";
        }

        internal bool Completed { get; private set; }

        protected override void Draw(GameTime gameTime)
        {
            if (Completed)
            {
                return;
            }
            GpuTriangleTextureCacheTests.AssertDeadCpuImageStorage(GraphicsDevice);
            GpuTriangleTextureCacheTests.AssertSameSizedStorageReuse(GraphicsDevice);
            GpuTriangleTextureCacheTests.AssertDimensionMismatch(GraphicsDevice);
            GpuTriangleTextureCacheTests.AssertOrphanFirstEviction(GraphicsDevice, 2, 1024);
            GpuTriangleTextureCacheTests.AssertOrphanFirstEviction(GraphicsDevice, 8, 12);
            Completed = true;
            Exit();
        }
    }
}
