using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
{
    throw new PlatformNotSupportedException("The Vulkan readback smoke requires Linux x64 and an existing graphical session.");
}

using var game = new ReadbackGame();
game.Run();

sealed class ReadbackGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private int draws;

    public ReadbackGame()
    {
        graphics = new GraphicsDeviceManager(this);
        graphics.PreferredBackBufferWidth = 64;
        graphics.PreferredBackBufferHeight = 64;
        graphics.GraphicsProfile = GraphicsProfile.HiDef;
        IsFixedTimeStep = false;
    }

    protected override void Draw(GameTime gameTime)
    {
        Color expected = draws % 2 == 0 ? Color.Red : Color.CornflowerBlue;
        GraphicsDevice.Clear(expected);
        var first = new Color[64 * 64];
        var second = new Color[64 * 64];
        GraphicsDevice.GetBackBufferData(first);
        GraphicsDevice.GetBackBufferData(second);
        if (first[2048] != expected || second[2048] != expected)
        {
            throw new InvalidOperationException($"Readback mismatch at frame {draws}: {first[2048]}, {second[2048]}, expected {expected}.");
        }
        Console.WriteLine($"readbacks frame={draws} count=2 pixels=matched");
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
