using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("GPU rendering", DisableParallelization = true)]
public sealed class GpuRenderingCollection;

internal sealed class GpuTestDevice : IDisposable
{
    private readonly nint window;

    public GpuTestDevice(int multiSampleCount = 0)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The GPU rendering checks require Windows and a graphics device.");
        RequirePresentationOutput();
        GraphicsAdapter adapter = GraphicsAdapter.DefaultAdapter;
        window = CreateWindowExW(0, "STATIC", "Memoria offscreen triangle validation", 0, 0, 0, 64, 64, 0, 0, 0, 0);
        Assert.True(window != 0, $"Cannot create hidden render window: {Marshal.GetLastWin32Error()}.");
        try
        {
            Device = new GraphicsDevice(adapter, GraphicsProfile.HiDef, new PresentationParameters
            {
                DeviceWindowHandle = window,
                BackBufferWidth = 64,
                BackBufferHeight = 64,
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.Depth24,
                MultiSampleCount = multiSampleCount,
                IsFullScreen = false,
                PresentationInterval = PresentInterval.Immediate
            });
        }
        catch
        {
            DestroyWindow(window);
            throw;
        }
    }

    private static void RequirePresentationOutput()
    {
        using SharpDX.DXGI.Factory1 factory = new();
        int outputs = 0;
        for (int index = 0; index < factory.GetAdapterCount(); index++)
        {
            using SharpDX.DXGI.Adapter1 adapter = factory.GetAdapter1(index);
            outputs += adapter.GetOutputCount();
        }
        Assert.SkipUnless(outputs != 0, "MonoGame WindowsDX exposes adapters only for DXGI presentation outputs; none are accessible in this process. GPU rendering was not verified.");
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
        try
        {
            Device.Dispose();
        }
        finally
        {
            DestroyWindow(window);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
}
