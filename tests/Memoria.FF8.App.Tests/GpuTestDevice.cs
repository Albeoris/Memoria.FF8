using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("GPU rendering", DisableParallelization = true)]
public sealed class GpuRenderingCollection;

internal sealed class GpuTestDevice : IDisposable
{
    private readonly nint window;

    public GpuTestDevice()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The GPU rendering checks require Windows and a graphics device.");
        window = CreateWindowExW(0, "STATIC", "Memoria offscreen triangle validation", 0, 0, 0, 64, 64, 0, 0, 0, 0);
        Assert.True(window != 0, $"Cannot create hidden render window: {Marshal.GetLastWin32Error()}.");
        Device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters
        {
            DeviceWindowHandle = window,
            BackBufferWidth = 64,
            BackBufferHeight = 64,
            BackBufferFormat = SurfaceFormat.Color,
            DepthStencilFormat = DepthFormat.Depth24,
            IsFullScreen = false,
            PresentationInterval = PresentInterval.Immediate
        });
    }

    public GraphicsDevice Device { get; }

    public RenderTarget2D CreateTarget(int width, int height)
    {
        return new RenderTarget2D(Device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
    }

    public static Color[] Read(RenderTarget2D target)
    {
        target.GraphicsDevice.SetRenderTarget(null);
        Color[] pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    public void Dispose()
    {
        Device.Dispose();
        DestroyWindow(window);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
}
