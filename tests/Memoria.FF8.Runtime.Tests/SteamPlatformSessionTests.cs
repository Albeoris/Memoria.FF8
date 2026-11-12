using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SteamPlatformSessionTests
{
    [Theory]
    [InlineData("3837340")]
    [InlineData("39140")]
    public void Ff7PumpsOnlyCallbacksAndDisposesOnce(string product)
    {
        FakeApi api = new(uint.Parse(product));
        SteamPlatformSession session = new(api, product);
        Assert.True(session.Ready);
        Assert.Equal(product, api.SelectedProduct);
        session.Pump();
        Assert.Equal(1, api.PumpCount);
        session.Dispose();
        session.Dispose();
        session.Pump();
        Assert.False(session.Ready);
        Assert.Equal(1, api.PumpCount);
        Assert.Equal(1, api.DisposeCount);
    }

    [Fact]
    public void MismatchedProductShutsDownBeforePumping()
    {
        FakeApi api = new(39150);
        using SteamPlatformSession session = new(api, "3837340");
        Assert.False(session.Ready);
        session.Pump();
        Assert.Equal(0, api.PumpCount);
        Assert.Equal(1, api.DisposeCount);
    }

    [Fact]
    public void FailedInitializationDisposesTheNativeOwner()
    {
        FakeApi api = new(3837340) { InitializeSucceeds = false };
        using SteamPlatformSession session = new(api, "3837340");
        Assert.False(session.Ready);
        Assert.Equal(1, api.DisposeCount);
    }

    private sealed class FakeApi(uint product) : ISteamAchievementApi
    {
        public uint AppId => product;
        public ulong UserId => throw new InvalidOperationException("Platform sessions do not read achievement owners.");
        public bool LoggedOn => throw new InvalidOperationException("Platform sessions do not read achievement owners.");
        public bool InitializeSucceeds { get; set; } = true;
        public string? SelectedProduct { get; private set; }
        public int PumpCount { get; private set; }
        public int DisposeCount { get; private set; }

        public bool Initialize(string selectedProduct, out string? diagnostic)
        {
            SelectedProduct = selectedProduct;
            diagnostic = null;
            return InitializeSucceeds;
        }

        public IReadOnlyList<SteamAchievementCallback> Pump()
        {
            PumpCount++;
            return [];
        }

        public bool GetAchievement(string key, out bool unlocked) => throw new InvalidOperationException("Unverified achievement read.");
        public bool GetStat(string key, out int value) => throw new InvalidOperationException("Unverified stat read.");
        public bool SetAchievement(string key) => throw new InvalidOperationException("Unverified achievement write.");
        public bool SetStat(string key, int value) => throw new InvalidOperationException("Unverified stat write.");
        public bool StoreStats() => throw new InvalidOperationException("Unverified stat store.");

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
