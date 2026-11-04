namespace Memoria.FF8.App.Tests;

public sealed class NativeSwapchainRecreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_frames_do_not_recreate_but_resize_and_sample_changes_do(bool verticalSync)
    {
        NativeSwapchainRecreation recreation = new();
        List<bool> applied = [];
        recreation.Synchronize(640, 480, 0, verticalSync, applied.Add);
        Assert.Equal(new[] { !verticalSync, verticalSync }, applied);
        applied.Clear();
        recreation.Synchronize(640, 480, 0, verticalSync, applied.Add);
        Assert.Empty(applied);
        recreation.Synchronize(800, 480, 0, verticalSync, applied.Add);
        recreation.Synchronize(800, 600, 0, verticalSync, applied.Add);
        recreation.Synchronize(800, 600, 4, verticalSync, applied.Add);
        Assert.Equal(Enumerable.Repeat(new[] { !verticalSync, verticalSync }, 3).SelectMany(values => values), applied);
    }

    [Theory]
    [InlineData(0, 480)]
    [InlineData(640, 0)]
    [InlineData(-1, 480)]
    public void Minimized_window_does_not_apply_or_cache_an_invalid_size(int width, int height)
    {
        NativeSwapchainRecreation recreation = new();
        List<bool> applied = [];
        recreation.Synchronize(width, height, 0, true, applied.Add);
        Assert.Empty(applied);
        recreation.Synchronize(640, 480, 0, true, applied.Add);
        Assert.Equal(new[] { false, true }, applied);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void Failed_recreation_restores_preference_and_retries_even_at_previous_size(bool verticalSync, int failingCall)
    {
        NativeSwapchainRecreation recreation = new();
        recreation.Synchronize(640, 480, 0, verticalSync, _ => { });
        bool preference = verticalSync;
        int calls = 0;
        InvalidOperationException failure = new("Swapchain reset failed.");
        Exception actual = Assert.Throws<InvalidOperationException>(() => recreation.Synchronize(800, 600, 4, verticalSync, enabled =>
        {
            preference = enabled;
            calls++;
            if (calls == failingCall)
            {
                throw failure;
            }
        }));
        Assert.Same(failure, actual);
        Assert.Equal(verticalSync, preference);
        Assert.Equal(2, calls);
        List<bool> applied = [];
        recreation.Synchronize(640, 480, 0, verticalSync, applied.Add);
        Assert.Equal(new[] { !verticalSync, verticalSync }, applied);
    }
}
