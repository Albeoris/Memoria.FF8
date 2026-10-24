using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianFinalePhaseTests
{
    [Theory]
    [InlineData(278, 0x2508188u, 0x681630u, 144, false)]
    [InlineData(278, 0x2508188u, 0x681630u, 145, true)]
    [InlineData(278, 0x2508188u, 0x681630u, 283, true)]
    [InlineData(291, 0x25562A8u, 0x6ED900u, 253, false)]
    [InlineData(291, 0x25562A8u, 0x6ED900u, 254, true)]
    [InlineData(291, 0x25562A8u, 0x6ED900u, 480, true)]
    [InlineData(191, 0x24FC330u, 0x641EA0u, 241, false)]
    [InlineData(191, 0x24FC330u, 0x641EA0u, 242, true)]
    [InlineData(191, 0x24FC330u, 0x63F2D0u, 405, false)]
    [InlineData(191, 0x24FC330u, 0x63F2D0u, 406, true)]
    [InlineData(199, 0x2259A08u, 0x5A8C20u, 86, false)]
    [InlineData(199, 0x2259A08u, 0x5A8C20u, 87, true)]
    public void Native_scene_counter_survives_task_release(int effectId, uint descriptor, uint callback, int counter, bool expected)
    {
        NativeBattleEffectTaskPool pool = TaskPool(descriptor, callback);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0xC, checked((ushort)counter))) };
        NativeBattleOriginalEffectState effect = Effect(effectId) with { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(effect, memory));

        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Flags = 0 }) };
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(effect with { Pools = [pool] }, memory));
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Callback = callback + 4 }) };
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(effect with { Pools = [pool] }, memory));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(13, true)]
    public void Tonberry_preserves_the_knife_animation_and_its_return(int phase, bool expected)
    {
        NativeBattleEffectTaskPool pool = TaskPool(0x25A3F90, 0x7674E0);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteByte(0x29, checked((byte)phase))) };
        Assert.Equal(expected, NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(90) with { Pools = [pool] }, NativeBattleEffectMemory.FromColdProcess()));
    }

    [Fact]
    public void Cactuar_terminal_flag_remembers_the_released_body()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Cactuar199 = NativeBattleCactuar199State.FromColdProcess() with { TerminalFlags = 1 } };
        Assert.True(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(199), memory));
    }

    [Theory]
    [InlineData(6, 0x80)]
    [InlineData(203, 0x200)]
    [InlineData(205, 0x800)]
    public void Cinematic_release_uses_its_current_header_flag(int effectId, int flag)
    {
        uint header = NativeBattleScriptedEffectMemory.Address + 0x500;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x279744C, BitConverter.GetBytes(header));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = core };
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(effectId), memory));
        memory = memory with { ScriptedEffects = core.Write(header + 2, BitConverter.GetBytes(checked((ushort)flag))) };
        Assert.True(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(effectId), memory));
    }

    [Theory]
    [InlineData(202, 0x238, 0x2C0)]
    [InlineData(204, 0x1B8, 0x1E8)]
    [InlineData(206, 0x41C, 0x45E)]
    public void Cinematic_main_phase_uses_the_oldest_return_address(int effectId, int threshold, int lastInstruction)
    {
        var fixture = Cinematic(effectId);
        uint file = NativeBattleEffectNodeMemory.ArenaAddress + 0x10000;
        foreach (int depth in new[] { 0, 1, 2 })
        {
            foreach (int relative in new[] { threshold - 2, threshold, lastInstruction, lastInstruction + 2 })
            {
                NativeBattleEffectArena arena = fixture.Memory.Arena!
                    .Write(0x6000, BitConverter.GetBytes(file + (uint)(depth == 0 ? relative : 0x4000)))
                    .Write(0x6024, BitConverter.GetBytes(file + (uint)relative))
                    .Write(0x6028, BitConverter.GetBytes(file + (uint)(threshold - 2)))
                    .Write(0x6044, [checked((byte)depth)]);
                Assert.Equal(relative >= threshold && relative <= lastInstruction, NativeBattleGuardianAnimationFinale.HasReachedFinale(fixture.Effect, fixture.Memory with { Arena = arena }));
            }
        }
    }

    [Fact]
    public void An_unrelated_slot_cannot_advance_the_owned_root_phase()
    {
        var fixture = Cinematic(202);
        uint file = NativeBattleEffectNodeMemory.ArenaAddress + 0x10000;
        NativeBattleEffectArena arena = fixture.Memory.Arena!.Write(0x6000, BitConverter.GetBytes(file + 0x1C8)).Write(0x6100, BitConverter.GetBytes(file + 0x238));
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(fixture.Effect, fixture.Memory with { Arena = arena }));
    }

    [Fact]
    public void Cinematic_root_and_source_extents_are_required()
    {
        var fixture = Cinematic(202);
        uint manager = NativeBattleScriptedEffectMemory.Address + 0x200;
        NativeBattleScriptedEffectMemory core = fixture.Memory.ScriptedEffects!.Write(manager + 0x90, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x100));
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianAnimationFinale.HasReachedFinale(fixture.Effect, fixture.Memory with { ScriptedEffects = core }));
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianAnimationFinale.HasReachedFinale(fixture.Effect with { ResourcePointers = [] }, fixture.Memory));
    }

    [Fact]
    public void Cold_and_unrelated_effects_do_not_report_a_finale()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        foreach (int effectId in new[] { 6, 90, 191, 199, 202, 203, 204, 205, 206, 278, 291 })
        {
            Assert.True(NativeBattleGuardianAnimationFinale.IsSupported(effectId));
            Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(effectId), memory));
        }
        Assert.False(NativeBattleGuardianAnimationFinale.IsSupported(1));
        Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(Effect(1), memory));
    }

    private static NativeBattleEffectTaskPool TaskPool(uint descriptor, uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, 0x1000, 0x148, 1);
        (pool, _) = pool.Append(callback);
        return pool;
    }

    private static (NativeBattleOriginalEffectState Effect, NativeBattleEffectMemory Memory) Cinematic(int effectId)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effectId);
        NativeBattleEffectBinding binding = new(checked((ushort)effectId), profile.Entry, profile.Initializer)
        {
            InitializerResources = [new("script", profile.File0Publication), new("slots", profile.File1Publication)],
            InitializerAssets = [new("script", new byte[0x5000].ToImmutableArray()), new("slots", new byte[0x1000].ToImmutableArray())]
        };
        NativeBattleOriginalEffectState effect = Effect(effectId) with { Binding = binding, ResourcePointers = [new(profile.File0Publication, 0x10000), new(profile.File1Publication, 0x6000)] };
        uint manager = NativeBattleScriptedEffectMemory.Address + 0x200;
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x27973EC, BitConverter.GetBytes(manager)).Write(manager + 0x90, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x6000));
        return (effect, NativeBattleEffectMemory.FromColdProcess() with { ScriptedEffects = core, Arena = NativeBattleEffectArena.Clear() });
    }

    private static NativeBattleOriginalEffectState Effect(int effectId) => new(checked((ushort)effectId), effectId - 1, new(checked((ushort)effectId), 0, 0), null, null);
}
