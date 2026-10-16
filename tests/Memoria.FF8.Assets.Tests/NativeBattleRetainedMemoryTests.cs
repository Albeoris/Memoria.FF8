using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRetainedMemoryTests
{
    [Fact]
    public void AuthenticatedLoaderImageProvesTheRuntimeColdProcessContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRetainedMemory actual = assets.ReadColdProcessNativeBattleMemory();
        NativeBattleRetainedMemory generic = NativeBattleRetainedMemory.FromColdProcess();
        Assert.Null(generic.EffectMemory!.QuezacotlExecutableMemory);
        NativeBattleQuezacotlExecutableMemory executableMemory = Assert.IsType<NativeBattleQuezacotlExecutableMemory>(actual.EffectMemory!.QuezacotlExecutableMemory);
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x1298C68, 0x109FC).ToArray(), executableMemory.Data.AsSpan().Slice(0, 0x109FC).ToArray());
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12A9664, 0x4D9C).ToArray(), executableMemory.Data.AsSpan().Slice(0x109FC, 0x4D9C).ToArray());
        Assert.Equal(0x1298C68u, NativeBattleQuezacotlWorkspace.Source(actual.EffectMemory).Address);
        Assert.Equal(executableMemory.Data.ToArray(), NativeBattleQuezacotlWorkspace.Source(actual.EffectMemory).Data.ToArray());
        Assert.Equal(JsonSerializer.Serialize(generic), JsonSerializer.Serialize(actual with { EffectMemory = actual.EffectMemory with { QuezacotlExecutableMemory = null } }));
        Assert.Same(actual, assets.ReadColdProcessNativeBattleMemory());
        Assert.Equal(0, actual.ActionPackets.Headers[31].ReadByte(19));
        Assert.Equal(0, actual.ActionPackets.ReadContact(48).Secondary.Amount6);
        Assert.Equal((byte)0, actual.ActionScratch.LastPublishedTargetCountE05);
        Assert.Equal((byte)0, actual.ActionScratch.FinisherActionIndexE2E);
        Assert.Equal(new byte?[] { 0, 0, 0, 0, 0, 0, 0 }, actual.ElementAttack);
        Assert.All(actual.ActionMemory.Rows, row => Assert.All(row, value => Assert.Equal((byte)0, value)));
    }

    [Fact]
    public void UnauthenticatedImageCannotSupplyColdProcessFacts()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleRetainedMemoryReader.ReadColdProcess(new byte[1024]));
    }
}
