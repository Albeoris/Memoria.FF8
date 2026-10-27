using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

public sealed class GraphicsHostReadinessTests
{
    [Fact]
    public void Missing_game_resources_do_not_allow_graphics_fallback()
    {
        Exception failure = new InvalidOperationException("Game resource initialization failed.", new FileNotFoundException("Required game asset is missing."));
        Assert.False(GraphicsHostReadiness.IsInitializationFailure(failure));
        Assert.False(GraphicsHostReadiness.IsInitializationFailure(new DllNotFoundException("SDL3 audio runtime could not be loaded.")));
    }

    [Fact]
    public void Graphics_device_and_native_graphics_runtime_failures_allow_initialization_fallback()
    {
        Assert.True(GraphicsHostReadiness.IsInitializationFailure(new NoSuitableGraphicsDeviceException("No supported graphics device.")));
        Assert.True(GraphicsHostReadiness.IsInitializationFailure(new DllNotFoundException("Unable to load shared library 'mgruntime'.")));
    }

    [Fact]
    public void Failed_shader_initialization_is_classified_with_original_reason()
    {
        InvalidOperationException shaderFailure = new("The backend effect profile is invalid.");
        GraphicsInitializationException failure = Assert.Throws<GraphicsInitializationException>(() => GraphicsHostReadiness.Initialize<object>(() => throw shaderFailure));
        Assert.Same(shaderFailure, failure.InnerException);
        Assert.Contains(shaderFailure.Message, failure.Message);
        Assert.True(GraphicsHostReadiness.IsInitializationFailure(failure));
    }
}
