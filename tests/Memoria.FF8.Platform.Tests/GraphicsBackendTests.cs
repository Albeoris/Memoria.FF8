using System.Text.Json;

namespace Memoria.FF8.Platform.Tests;

public sealed class GraphicsBackendTests
{
    [Fact]
    public void PrecedenceAndBoundedFallbackAreDeterministic()
    {
        Assert.Equal(GraphicsBackend.Vulkan, GraphicsBackendSelection.Resolve(GraphicsBackend.Vulkan, GraphicsBackend.Direct3D11));
        Assert.Equal(GraphicsBackend.Direct3D11, GraphicsBackendSelection.Resolve(null, GraphicsBackend.Direct3D11));
        Assert.Equal(GraphicsBackend.Auto, GraphicsBackendSelection.Resolve(null, null));
        Assert.Equal([GraphicsBackend.Direct3D11, GraphicsBackend.Direct3D12, GraphicsBackend.Vulkan], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Auto, HostPlatform.Windows));
        Assert.Equal([GraphicsBackend.Vulkan], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Auto, HostPlatform.Linux));
        Assert.Equal([GraphicsBackend.Vulkan], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Auto, HostPlatform.MacOS));
        Assert.Equal([GraphicsBackend.Direct3D11], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Direct3D11, HostPlatform.Windows));
        Assert.Equal([GraphicsBackend.Vulkan], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Vulkan, HostPlatform.Windows));
    }

    [Fact]
    public void RecognizedUnavailableSelectionsFailExplicitlyWithRecovery()
    {
        Assert.Equal(GraphicsBackend.Direct3D12, GraphicsBackendSelection.Parse("Direct3D12"));
        Assert.Equal(GraphicsBackend.Vulkan, GraphicsBackendSelection.Parse("vulkan"));
        Assert.Throws<ArgumentException>(() => GraphicsBackendSelection.Parse("OpenGL"));
        Assert.Throws<ArgumentException>(() => GraphicsBackendSelection.Parse("99"));
        Assert.Equal([GraphicsBackend.Direct3D12], GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Direct3D12, HostPlatform.Windows));
        Assert.Throws<PlatformNotSupportedException>(() => GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Direct3D12, HostPlatform.MacOS));
        Assert.Contains("Try Auto", Assert.Throws<PlatformNotSupportedException>(() => GraphicsBackendSelection.InitializationOrder(GraphicsBackend.Direct3D11, HostPlatform.Linux)).Message);
        Assert.Contains(GraphicsBackend.Direct3D12, GraphicsBackendSelection.Available(HostPlatform.Windows));
        Assert.Equal([GraphicsBackend.Vulkan], GraphicsBackendSelection.Available(HostPlatform.MacOS));
    }

    [Fact]
    public void SharedSettingsPreserveExistingAndUnknownProperties()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            Assert.Equal(GraphicsBackend.Auto, GraphicsBackendPreferences.Read(path));
            File.WriteAllText(path, "{\"WindowWidth\":1280,\"Future\":true,\"Graphics\":{\"MsaaSamples\":4}}");
            Assert.Equal(GraphicsBackend.Auto, GraphicsBackendPreferences.Read(path));
            GraphicsBackendPreferences.Write(path, GraphicsBackend.Vulkan);
            Assert.Equal(GraphicsBackend.Vulkan, GraphicsBackendPreferences.Read(path));
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(1280, saved.RootElement.GetProperty("WindowWidth").GetInt32());
            Assert.True(saved.RootElement.GetProperty("Future").GetBoolean());
            Assert.Equal(4, saved.RootElement.GetProperty("Graphics").GetProperty("MsaaSamples").GetInt32());
            Assert.Equal("Vulkan", saved.RootElement.GetProperty("Graphics").GetProperty("Backend").GetString());
            GraphicsBackendPreferences.Write(path, GraphicsBackend.Direct3D12);
            Assert.Equal(GraphicsBackend.Direct3D12, GraphicsBackendPreferences.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
