using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class GraphicsLaunchOptionsTests
{
    [Fact]
    public void BackendOverrideIsExplicitAndDoesNotReplaceOtherArguments()
    {
        LaunchOptions options = LaunchOptions.Parse(["--graphics-backend", "Vulkan", "--resolution", "1280x720", "--mute-audio"]);
        Assert.Equal(GraphicsBackend.Vulkan, options.RequestedGraphicsBackend);
        Assert.Equal(1280, options.Width);
        Assert.Equal(720, options.Height);
        Assert.True(options.MuteAudio);
        Assert.Null(LaunchOptions.Parse([]).RequestedGraphicsBackend);
    }

    [Fact]
    public void InvalidOrMissingBackendOverrideIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--graphics-backend", "OpenGL"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--graphics-backend"]));
    }
}
