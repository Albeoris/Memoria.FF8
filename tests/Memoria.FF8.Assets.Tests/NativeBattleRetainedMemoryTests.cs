using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedMemoryTests
{
    [Fact]
    public void AuthenticatedLoaderImageProvesTheRuntimeColdProcessContract()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRetainedMemory actual = assets.ReadColdProcessNativeBattleMemory();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRetainedMemory.FromColdProcess()), JsonSerializer.Serialize(actual));
        Assert.Same(actual, assets.ReadColdProcessNativeBattleMemory());
        Assert.Equal(0, actual.ActionPackets.Headers[31].ReadByte(19));
        Assert.Equal(0, actual.ActionPackets.ReadContact(48).Secondary.Amount6);
    }

    [Fact]
    public void UnauthenticatedImageCannotSupplyColdProcessFacts()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleRetainedMemoryReader.ReadColdProcess(new byte[1024]));
    }
}
