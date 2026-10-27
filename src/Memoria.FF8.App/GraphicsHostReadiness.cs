using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Reports the initialized host only after content has loaded and a frame has been presented.</summary>
internal sealed class GraphicsHostReadiness
{
#if MEMORIA_VULKAN
    public const string Backend = "Vulkan";
#else
    public const string Backend = "Direct3D11";
#endif
    private readonly GraphicsHostCapabilities capabilities;
    private readonly GraphicsDevice device;
    private bool reported;

    internal static T Initialize<T>(Func<T> initialize)
    {
        try
        {
            return initialize();
        }
        catch (Exception exception) when (exception is not GraphicsInitializationException)
        {
            throw new GraphicsInitializationException($"The {Backend} graphics host could not initialize: {exception.Message}", exception);
        }
    }

    internal static bool IsInitializationFailure(Exception exception)
    {
        return exception is GraphicsInitializationException or NoSuitableGraphicsDeviceException
            || exception is DllNotFoundException && exception.Message.Contains("mgruntime", StringComparison.OrdinalIgnoreCase)
            || exception.InnerException is not null && IsInitializationFailure(exception.InnerException);
    }

    internal GraphicsHostReadiness(GraphicsDevice device)
    {
        this.device = device;
        if (device.GraphicsProfile != GraphicsProfile.HiDef)
        {
            throw new NotSupportedException($"The {Backend} host requires the HiDef graphics profile; actual profile is {device.GraphicsProfile}.");
        }
        device.Adapter.QueryRenderTargetFormat(device.GraphicsProfile, SurfaceFormat.Color, DepthFormat.Depth24, device.PresentationParameters.MultiSampleCount, out SurfaceFormat color, out DepthFormat depth, out int samples);
        if (color != SurfaceFormat.Color || depth != DepthFormat.Depth24)
        {
            throw new NotSupportedException($"The {Backend} device cannot provide the required Color/Depth24 render target (actual {color}/{depth}).");
        }
        // Creation validates the target actually required by every hardware scene renderer.
        using RenderTarget2D target = new(device, 16, 16, false, color, depth, samples, RenderTargetUsage.PreserveContents);
        using RenderTarget2D titleTarget = new(device, 16, 16, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        if (titleTarget.Format != SurfaceFormat.HalfVector4)
        {
            throw new NotSupportedException($"The {Backend} device cannot provide the required HalfVector4 title render target (actual {titleTarget.Format}).");
        }
        string identity = string.IsNullOrWhiteSpace(device.Adapter.Description) ? device.Adapter.DeviceName : device.Adapter.Description;
        capabilities = new(identity, device.Adapter.VendorId, device.Adapter.DeviceId, device.GraphicsProfile.ToString(), target.Format.ToString(), target.DepthStencilFormat.ToString(), titleTarget.Format.ToString(), device.PresentationParameters.MultiSampleCount, target.MultiSampleCount);
        Console.WriteLine($"graphics-backend={Backend} device={capabilities.Device} profile={capabilities.Profile} target={capabilities.ColorFormat}/{capabilities.DepthFormat} samples={capabilities.SceneSamples}");
    }

    internal void Presented(GameWindow? window = null)
    {
        if (reported)
        {
            return;
        }
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_READY_FILE");
        if (!string.IsNullOrWhiteSpace(path))
        {
            string destination = Path.GetFullPath(path);
            string temporary = destination + $".{Environment.ProcessId}.tmp";
            try
            {
                PresentationParameters presentation = device.PresentationParameters;
                Viewport viewport = device.Viewport;
                GraphicsHostCapabilities actualCapabilities = capabilities with { BackBufferSamples = presentation.MultiSampleCount, BackBufferWidth = presentation.BackBufferWidth, BackBufferHeight = presentation.BackBufferHeight, ViewportWidth = viewport.Width, ViewportHeight = viewport.Height, ClientWidth = window?.ClientBounds.Width, ClientHeight = window?.ClientBounds.Height };
                string json = JsonSerializer.Serialize(new { schemaVersion = 1, backend = Backend, processId = Environment.ProcessId, capabilities = actualCapabilities }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                File.WriteAllText(temporary, json);
                File.Move(temporary, destination);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        reported = true;
    }

    private sealed record GraphicsHostCapabilities(string Device, int VendorId, int DeviceId, string Profile, string ColorFormat, string DepthFormat, string TitleColorFormat, int BackBufferSamples, int SceneSamples, int BackBufferWidth = 0, int BackBufferHeight = 0, int ViewportWidth = 0, int ViewportHeight = 0, int? ClientWidth = null, int? ClientHeight = null);
}

internal sealed class GraphicsInitializationException(string message, Exception innerException) : Exception(message, innerException);
