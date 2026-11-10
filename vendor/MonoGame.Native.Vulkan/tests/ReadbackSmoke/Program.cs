using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

if (!(OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture == Architecture.X64 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())))
{
    throw new PlatformNotSupportedException("The Vulkan readback smoke requires a supported desktop runtime and an existing graphical session.");
}
if (OperatingSystem.IsMacOS())
{
    Environment.SetEnvironmentVariable("SDL_VULKAN_LIBRARY", Path.Combine(AppContext.BaseDirectory, "libMoltenVK.dylib"));
}

using var game = new ReadbackGame(!args.Contains("--no-vsync"));
game.Run();

sealed class ReadbackGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private int draws;

    public ReadbackGame(bool verticalSync)
    {
        graphics = new GraphicsDeviceManager(this);
        graphics.PreferredBackBufferWidth = 64;
        graphics.PreferredBackBufferHeight = 64;
        graphics.SynchronizeWithVerticalRetrace = verticalSync;
        graphics.GraphicsProfile = GraphicsProfile.HiDef;
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Resize(80, 64);
    }

    protected override void Update(GameTime gameTime)
    {
        // Keep VSync and MSAA fixed while growing and shrinking the client.
        if (draws == 4)
        {
            Resize(112, 80);
        }
        else if (draws == 8)
        {
            Resize(80, 56);
        }
        base.Update(gameTime);
    }

    private void Resize(int width, int height)
    {
        graphics.PreferredBackBufferWidth = width;
        graphics.PreferredBackBufferHeight = height;
        graphics.ApplyChanges();
    }

    protected override void Draw(GameTime gameTime)
    {
        Color expected = draws % 2 == 0 ? Color.Red : Color.CornflowerBlue;
        GraphicsDevice.Clear(expected);
        int width = graphics.PreferredBackBufferWidth;
        int height = graphics.PreferredBackBufferHeight;
        var first = new Color[width * height];
        var second = new Color[width * height];
        GraphicsDevice.GetBackBufferData(first);
        GraphicsDevice.GetBackBufferData(second);
        if (first[^1] != expected || second[^1] != expected)
        {
            throw new InvalidOperationException($"Readback mismatch at frame {draws} ({width}x{height}): {first[^1]}, {second[^1]}, expected {expected}.");
        }
        Console.WriteLine($"readbacks frame={draws} size={width}x{height} vsync={graphics.SynchronizeWithVerticalRetrace} count=2 pixels=matched");
        draws++;
        if (draws == 12)
        {
            Exit();
        }
        base.Draw(gameTime);
    }

    protected override void EndDraw()
    {
        base.EndDraw();
        Console.WriteLine($"presented frame={draws - 1}");
    }
}
