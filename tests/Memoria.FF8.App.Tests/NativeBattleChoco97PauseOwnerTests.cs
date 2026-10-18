using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleChoco97PauseOwnerTests
{
    [Fact]
    public void Imported_divergent_pause_globals_preserve_the_native_secondary_GF_operand()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCO97_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "A complete before-ChocoFire component is required.");
        BattleSession battle = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(path!));
        BattleSnapshot before = battle.Capture();
        NativeBattleModulePoolTask? task = null;
        for (int callback = 0; callback < 64; callback++)
        {
            task = Program(before);
            if (task is not null && task.ReadByte(0x29) == 1 && task.ReadDword(0x98) is > 1 and < 20)
            {
                break;
            }
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1.0 / 30));
            before = battle.Capture();
        }
        Assert.NotNull(task);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        uint frame = task.ReadDword(0x98);
        Assert.InRange(frame, 2u, 19u);
        BattleSnapshot frozen = Continue(1);
        BattleSnapshot running = Continue(0);
        Assert.Equal(frame, Program(frozen)!.ReadDword(0x98));
        Assert.Equal(frame + 1, Program(running)!.ReadDword(0x98));
        Assert.Equal(7u, frozen.NativeEffectMemory!.Choco97!.FreezePrimary);
        Assert.Equal(7u, running.NativeEffectMemory!.Choco97!.FreezePrimary);
        Assert.Equal(1u, frozen.NativeEffectMemory.Choco97.FreezeSecondary);
        Assert.Equal(0u, running.NativeEffectMemory.Choco97.FreezeSecondary);

        BattleSnapshot Continue(uint secondary)
        {
            NativeBattleEffectMemory memory = before.NativeEffectMemory!;
            BattleSnapshot input = before with { NativeEffectMemory = memory with { Choco97 = memory.Choco97! with { FreezePrimary = 7, FreezeSecondary = secondary } } };
            BattleSnapshot independent = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(input))!;
            BattleSession restored = BattleSession.Restore(independent);
            Assert.Equal(JsonSerializer.Serialize(input), JsonSerializer.Serialize(restored.Capture()));
            restored.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1.0 / 30));
            return restored.Capture();
        }
    }

    private static NativeBattleModulePoolTask? Program(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).SingleOrDefault(task => task.Callback == 0x72A660 && task.Flags != 0);
}
