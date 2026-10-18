using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogle96MemoryTests
{
    [Fact]
    public void GenuineColdOwnersAndWarmedMutableSourcesSurviveModuleSwitchesAndJson()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleMoogle96State cold = Assert.IsType<NativeBattleMoogle96State>(memory.Moogle96);
        Assert.True(cold.ProgramSourceKnownCold && cold.IoScriptKnownCold);
        Assert.Null(cold.ProgramSource);
        Assert.Null(cold.Io);
        Assert.All(cold.Nodes.Banks, bank => Assert.Equal(0u, bank.InitializationFlag));
        NativeBattleGfAnimationSource program = new(0x169A1BC, Enumerable.Repeat((byte)0xA5, 0x9DF4).ToImmutableArray());
        NativeBattleMoogle96IoState io = NativeBattleMoogle96Io.Initialize7322A0(new byte[0x60].ToImmutableArray(), 0x20DFAB8 + 0x13760).State;
        NativeBattleMoogle96State warm = cold with { ProgramSource = program, ProgramSourceKnownCold = false, Io = io, IoScriptKnownCold = false };
        memory = memory with { Moogle96 = warm };
        foreach (int selectedIndex in new[] { 95, 141, 95 })
        {
            memory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory.BeginLoad(selectedIndex)))!;
            Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(memory.Moogle96));
        }
    }

    [Fact]
    public void MissingImportedOwnerStaysMissingAfterJsonAndModuleLoad()
    {
        NativeBattleEffectMemory imported = NativeBattleEffectMemory.FromColdProcess() with { Moogle96 = null };
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(imported))!;
        Assert.Null(restored.BeginLoad(95).Moogle96);
    }
}
