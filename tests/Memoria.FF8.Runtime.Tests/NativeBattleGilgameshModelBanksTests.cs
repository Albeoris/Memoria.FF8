using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshModelBanksTests
{
    [Theory]
    [InlineData(0x595D50u, 105)]
    [InlineData(0x5966B0u, 105)]
    [InlineData(0x596710u, 42)]
    [InlineData(0x596800u, 42)]
    [InlineData(0x596970u, 50)]
    [InlineData(0x596A60u, 50)]
    public void Gated_finite_callbacks_keep_every_raw_owner_and_use_signed_post_increment_threshold(uint callback, ushort duration)
    {
        NativeBattleGilgameshModelMemory memory = Gated();
        NativeBattleModulePoolTask task = Task(callback, (ushort)(duration - 1), duration);
        NativeBattleGilgameshModelBankStep result = Run(memory, task);
        Assert.Equal(2, result.Result);
        Assert.Equal(duration, result.UpdatedTask.ReadWord(12));
        Assert.Empty(result.Packets);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory), NativeBattleTestSnapshots.Json(result.ModelMemory));
        NativeBattleGilgameshModelBankStep paused = Run(memory, task, 0x201);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(0, paused.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory), NativeBattleTestSnapshots.Json(paused.ModelMemory));
    }

    [Theory]
    [InlineData(0x595D50u)]
    [InlineData(0x5966B0u)]
    [InlineData(0x596710u)]
    [InlineData(0x596800u)]
    [InlineData(0x596970u)]
    [InlineData(0x596A60u)]
    public void All_finite_counter_WORDs_wrap_and_json_next_callback_keeps_exact_state(uint callback)
    {
        NativeBattleGilgameshModelBankStep first = Run(Gated(), Task(callback, 32767, 105));
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        Assert.Equal(0, first.Result);
        NativeBattleGilgameshModelBankStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGilgameshModelBankStep>(NativeBattleTestSnapshots.Json(first))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Run(first.ModelMemory, first.UpdatedTask)), NativeBattleTestSnapshots.Json(Run(restored.ModelMemory, restored.UpdatedTask)));
    }

    [Fact]
    public void Sword_bank_duration_uses_signed_taskE_and_initializer_reentry_only_tests_signed_counter_zero()
    {
        NativeBattleGilgameshModelBankStep result = Run(Gated(), Task(0x595D50, 32767, 32768));
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)32768, result.UpdatedTask.ReadWord(12));
        Assert.Equal(0, Run(Gated(), Task(0x596710, 65535, 0), 0x201).Result);
        Assert.Equal(0, Run(Gated(), Task(0x596970, 65535, 0), 0x201).Result);
    }

    private static NativeBattleGilgameshModelBankStep Run(NativeBattleGilgameshModelMemory memory, NativeBattleModulePoolTask task, uint flags = 0) => NativeBattleGilgameshModelBanks.Step(memory, NativeBattleEffectArena.Clear(), task, UnusedResources(), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096), new(null, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, flags);
    private static NativeBattleModulePoolTask Task(uint callback, ushort counter, ushort duration) => new NativeBattleModulePoolTask(1, 0, null, callback, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, counter).WriteWord(14, duration);
    private static NativeBattleGilgameshModelResources UnusedResources() => new(null!, null!, null!, Enumerable.Repeat<NativeBattleEffectOwnedModelResource>(null!, 5).ToImmutableArray(), Enumerable.Repeat<NativeBattleEffectOwnedModelResource>(null!, 4).ToImmutableArray());

    private static NativeBattleGilgameshModelMemory Gated()
    {
        NativeBattleGilgameshModelMemory memory = NativeBattleGilgameshModelMemory.FromColdProcess();
        return memory with { Owners = memory.Owners.Select(owner => owner with { State = owner.State with { Model = owner.State.Model.SetItem(0, 12) } }).ToImmutableArray() };
    }
}
