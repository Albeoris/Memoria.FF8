namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianAnimationFinaleTests
{
    [Theory]
    [InlineData(116, 0x2521738u, 0x6C3940u, 318, false)]
    [InlineData(116, 0x2521738u, 0x6C3940u, 319, true)]
    [InlineData(116, 0x2521738u, 0x6C3940u, 345, true)]
    [InlineData(325, 0x25051A0u, 0x6545F0u, 367, false)]
    [InlineData(325, 0x25051A0u, 0x6545F0u, 368, true)]
    [InlineData(325, 0x25051A0u, 0x6545F0u, 499, true)]
    [InlineData(185, 0x22BC198u, 0x5C4AD0u, 168, false)]
    [InlineData(185, 0x22BC198u, 0x5C77C0u, 0, true)]
    public void Finale_uses_the_original_active_task_boundary(int effectId, uint descriptor, uint callback, int counter, bool expected)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, 0x1000, 0x24, 1);
        (pool, int slot) = pool.Append(callback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, checked((ushort)counter))) });
        NativeBattleOriginalEffectState effect = Effect(effectId) with { Pools = [pool] };
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, NativeBattleEffectMemory.FromColdProcess()));

        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, NativeBattleTaskFixture.EditTask(pool.Slots[slot], taskData => taskData with { Flags = 0 })) });
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(effect with { Pools = [pool] }, NativeBattleEffectMemory.FromColdProcess()));
    }

    [Fact]
    public void Shiva_remembers_the_completed_finale_after_its_worker_is_released()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleShivaProcess shiva = NativeBattleShivaProcess.FromColdProcess() with { Entry = new(0, 3, new(0, 0, 0), matrix, 2) };
        Assert.True(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(185), NativeBattleEffectMemory.FromColdProcess() with { Shiva = shiva }));
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public void Siren_uses_the_current_director_stage_before_contact(int stage, bool expected)
    {
        NativeBattleSiren95State siren = NativeBattleSiren95State.FromColdProcess();
        uint director = siren.DirectorAddress1533010!.Value;
        siren = siren.Write(director + 0x40, BitConverter.GetBytes(checked((ushort)stage)));
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(95), NativeBattleEffectMemory.FromColdProcess() with { Siren95 = siren }));
        Assert.Equal((ushort)0, siren.ReadWord(director + 0x48));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0x10, true)]
    [InlineData(0x30, true)]
    public void Ifrit_reuses_the_original_meteor_release_flag(int flags, bool expected)
    {
        uint header = NativeBattleScriptedEffectMemory.Address + 0x500;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x279744C, BitConverter.GetBytes(header)).Write(header + 2, BitConverter.GetBytes(checked((ushort)flags)));
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(201), NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = core }));
    }

    [Theory]
    [InlineData(116)]
    [InlineData(185)]
    [InlineData(201)]
    [InlineData(95)]
    [InlineData(325)]
    public void Supported_effects_are_explicit(int effectId)
    {
        Assert.True(NativeBattleGuardianAnimationFinale.IsSupported(effectId));
    }

    [Fact]
    public void Uninitialized_tasks_and_unrelated_effects_do_not_report_a_finale()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(116), memory));
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(185), memory));
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(325), memory));
        Assert.False(NativeBattleGuardianAnimationFinale.IsSupported(28));
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(28), memory));
    }

    private static NativeBattleOriginalEffectState Effect(int effectId) => new(checked((ushort)effectId), effectId - 1, new(checked((ushort)effectId), 0, 0), null, null);
}
