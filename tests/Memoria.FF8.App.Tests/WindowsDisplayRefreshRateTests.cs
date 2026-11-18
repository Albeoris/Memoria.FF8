namespace Memoria.FF8.App.Tests;

public sealed class WindowsDisplayRefreshRateTests
{
    [Fact]
    public void Refresh_query_is_cached_until_the_actual_monitor_or_display_mode_changes()
    {
        nint monitor = 1;
        int rate = 60;
        int queries = 0;
        DisplayRefreshRateCache cache = new(() => monitor, _ =>
        {
            queries++;
            return rate;
        });
        Assert.Equal(60, cache.RefreshRate);
        Assert.Equal(60, cache.RefreshRate);
        Assert.Equal(1, queries);
        monitor = 2;
        rate = 144;
        Assert.Equal(144, cache.RefreshRate);
        Assert.Equal(2, queries);
        rate = 120;
        Assert.Equal(144, cache.RefreshRate);
        cache.Invalidate();
        Assert.Equal(120, cache.RefreshRate);
        Assert.Equal(3, queries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void Unknown_or_default_display_frequency_does_not_invent_a_refresh_rate(int? observed)
    {
        nint monitor = 1;
        int? rate = observed;
        DisplayRefreshRateCache cache = new(() => monitor, _ => rate);
        Assert.Null(cache.RefreshRate);
        rate = 60;
        Assert.Null(cache.RefreshRate);
        cache.Invalidate();
        Assert.Equal(60, cache.RefreshRate);
        monitor = 0;
        Assert.Null(cache.RefreshRate);
        monitor = 1;
        rate = 144;
        Assert.Equal(144, cache.RefreshRate);
    }
}
