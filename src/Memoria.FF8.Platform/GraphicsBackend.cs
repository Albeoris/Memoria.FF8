using System.Text.Json.Serialization;

namespace Memoria.FF8.Platform;

[JsonConverter(typeof(JsonStringEnumConverter<GraphicsBackend>))]
public enum GraphicsBackend
{
    Auto,
    Direct3D11,
    Direct3D12,
    Vulkan
}

public static class GraphicsBackendSelection
{
    public static GraphicsBackend Parse(string value)
    {
        if (!Enum.TryParse(value, true, out GraphicsBackend backend) || !Enum.IsDefined(backend))
        {
            throw new ArgumentException("Graphics backend must be Auto, Direct3D11, Direct3D12 or Vulkan.", nameof(value));
        }
        return backend;
    }

    public static GraphicsBackend Resolve(GraphicsBackend? commandLine, GraphicsBackend? saved) => commandLine ?? saved ?? GraphicsBackend.Auto;

    public static IReadOnlyList<GraphicsBackend> Available(HostPlatform platform) => platform == HostPlatform.Windows ? [GraphicsBackend.Auto, GraphicsBackend.Direct3D11, GraphicsBackend.Direct3D12, GraphicsBackend.Vulkan] : [GraphicsBackend.Vulkan];

    public static string? UnavailableReason(GraphicsBackend backend, HostPlatform platform)
    {
        if (!Enum.IsDefined(backend))
        {
            return "Unknown graphics backend. Try Auto.";
        }
        if (backend is GraphicsBackend.Direct3D11 or GraphicsBackend.Direct3D12 && platform != HostPlatform.Windows)
        {
            return $"{backend} requires Windows. Try Auto to use Vulkan on this platform.";
        }
        return null;
    }

    public static IReadOnlyList<GraphicsBackend> InitializationOrder(GraphicsBackend requested, HostPlatform platform)
    {
        if (UnavailableReason(requested, platform) is string reason)
        {
            throw new PlatformNotSupportedException(reason);
        }
        return requested == GraphicsBackend.Auto ? platform == HostPlatform.Windows ? [GraphicsBackend.Direct3D11, GraphicsBackend.Direct3D12, GraphicsBackend.Vulkan] : [GraphicsBackend.Vulkan] : [requested];
    }
}
