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
        Assert.Equal((byte?)0, actual.EffectMemory!.Parameter80);
        Assert.Equal((byte?)0, actual.EffectMemory.Parameter82);
        Assert.Equal((byte?)0, generic.EffectMemory!.Parameter82);
        Assert.Equal((short?)0, actual.EffectMemory.CuraContextF4);
        Assert.Equal((int?)0, actual.EffectMemory.CuraSceneFade);
        Assert.Equal(NativeBattleCuragaExecutableState.FromColdProcess(), actual.EffectMemory.CuragaExecutable);
        var oldEffect = JsonSerializer.SerializeToNode(actual.EffectMemory)!.AsObject();
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuraContextF4)));
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuraSceneFade)));
        Assert.True(oldEffect.Remove(nameof(NativeBattleEffectMemory.CuragaExecutable)));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuraContextF4);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuraSceneFade);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(oldEffect)!.CuragaExecutable);
        Assert.Null(generic.EffectMemory!.QuezacotlExecutableMemory);
        Assert.Null(generic.EffectMemory.DoubleMorph);
        Assert.Null(generic.EffectMemory.TripleMorph);
        Assert.Null(generic.EffectMemory.Effect57Executable);
        Assert.Null(generic.EffectMemory.RemedyExecutable);
        Assert.Null(generic.EffectMemory.RecoveryExecutables);
        Assert.Equal(new ushort[] { 53, 58, 59 }, actual.EffectMemory.RecoveryExecutables!.Value.Select(bank => bank.Effect));
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(57), actual.EffectMemory.Effect57Executable);
        Assert.Equal(NativeBattleRestorationExecutableState.FromColdProcess(67), actual.EffectMemory.RemedyExecutable);
        NativeBattleQuezacotlExecutableMemory executableMemory = Assert.IsType<NativeBattleQuezacotlExecutableMemory>(actual.EffectMemory!.QuezacotlExecutableMemory);
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x1298C68, 0x109FC).ToArray(), executableMemory.Data.AsSpan().Slice(0, 0x109FC).ToArray());
        Assert.Equal(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12A9664, 0x4D9C).ToArray(), executableMemory.Data.AsSpan().Slice(0x109FC, 0x4D9C).ToArray());
        Assert.Equal(0x1298C68u, NativeBattleQuezacotlWorkspace.Source(actual.EffectMemory).Address);
        Assert.Equal(executableMemory.Data.ToArray(), NativeBattleQuezacotlWorkspace.Source(actual.EffectMemory).Data.ToArray());
        NativeBattleDoubleMorph doubleMorph = Assert.IsType<NativeBattleDoubleMorph>(actual.EffectMemory.DoubleMorph);
        Assert.Equal(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x163FFD0u).Bytes.ToArray(), doubleMorph.Bytes.ToArray());
        NativeBattleTripleMorph tripleMorph = Assert.IsType<NativeBattleTripleMorph>(actual.EffectMemory.TripleMorph);
        Assert.Equal(NativeBattleEffectStaticResourcesReader.ReadMesh(executable, 0x1627970u).Bytes.ToArray(), tripleMorph.Bytes.ToArray());
        Assert.Equal(JsonSerializer.Serialize(generic), JsonSerializer.Serialize(actual with { EffectMemory = actual.EffectMemory with { QuezacotlExecutableMemory = null, DoubleMorph = null, TripleMorph = null, Effect57Executable = null, RemedyExecutable = null, RecoveryExecutables = null } }));
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
