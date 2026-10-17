using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationNodeEngineTests
{
    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Context_reads_current_counts_after_the_combined_callback_and_completes_on_next_phase(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = fixture.Frame;
        int callbacks = 0;
        NativeBattleRestorationFrame Execute(NativeBattleRestorationFrame current, uint address)
        {
            callbacks++;
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address).WriteWord(0x34 + 0x14, 0).WriteWord(0x34 + 0x16, 0);
            return current with { Effect = current.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
        NativeBattleRestorationContextStep first = NativeBattleRestorationNodeEngine.Advance(frame, fixture.Task, Execute);
        Assert.False(first.Complete);
        Assert.Equal(0, callbacks);
        NativeBattleRestorationContextStep second = NativeBattleRestorationNodeEngine.Advance(first.Frame, fixture.Task, Execute);
        Assert.Equal(1, callbacks);
        Assert.False(second.Complete);
        Assert.Equal((ushort)2, Read(second.Frame, fixture.Task).ReadWord(0x34 + 0x20));
        NativeBattleRestorationFrame clone = JsonSerializer.Deserialize<NativeBattleRestorationFrame>(JsonSerializer.Serialize(second.Frame))!;
        NativeBattleRestorationContextStep third = NativeBattleRestorationNodeEngine.Advance(second.Frame, fixture.Task, Execute);
        Assert.True(third.Complete);
        Assert.Equal(1, callbacks);
        Assert.Equal(JsonSerializer.Serialize(third), JsonSerializer.Serialize(NativeBattleRestorationNodeEngine.Advance(clone, fixture.Task, Execute)));
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Signed_WORD_counter_wrap_and_late_live_counts_are_not_replaced_by_duration_shortcuts(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteWord(0x34 + 0x20, 1).WriteWord(0x34 + 0x18, 0x7FFF).WriteWord(0x34 + 0x1A, 0x8000));
        NativeBattleRestorationContextStep step = NativeBattleRestorationNodeEngine.Advance(frame, fixture.Task, (current, address) => Write(current, address, task => task.WriteWord(0x34 + 0x14, 1).WriteWord(0x34 + 0x16, 2)));
        Assert.Equal((ushort)0x8000, Read(step.Frame, fixture.Task).ReadWord(0x34 + 0x18));
        Assert.Equal((ushort)1, Read(step.Frame, fixture.Task).ReadWord(0x34 + 0x20));
        Assert.Equal((ushort)1, step.Frame.State.PassEmitterCount);
        Assert.Equal((ushort)2, step.Frame.State.PassGeometryCount);
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    public void Unknown_pass_count_and_wrong_spell_callback_remain_explicit_boundaries(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame unknown = fixture.Frame with { State = fixture.Frame.State with { PassEmitterCount = null } };
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationNodeEngine.Advance(unknown, fixture.Task, (frame, _) => frame));
        NativeBattleRestorationProfile other = NativeBattleRestorationProfile.ForEffect(effect == 24 ? (ushort)27 : (ushort)24);
        NativeBattleEffectTaskPool pool = fixture.Frame.Effect.Pools[2];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Callback = other.GenericCallback }) };
        NativeBattleRestorationFrame mismatched = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.SetItem(2, pool) } };
        Assert.Throws<InvalidDataException>(() => NativeBattleRestorationNodeEngine.Advance(mismatched, fixture.Task, (frame, _) => frame));
    }

    [Theory]
    [InlineData((sbyte)1, (ushort)0x12FE, (ushort)0, (ushort)0, (short)128)]
    [InlineData((sbyte)2, (ushort)127, (ushort)1, (ushort)1, (short)128)]
    [InlineData((sbyte)0, (ushort)0x12FE, (ushort)0x12FE, (ushort)0, (short)256)]
    public void Esuna_operand_adapter_updates_low_BYTE_UV_once_and_preserves_unselected_context(sbyte source, ushort prior, ushort next, ushort vertical, short period)
    {
        NativeBattleEsunaMeshOperands prepared = NativeBattleRestorationMeshPackets.Prepare88E090(prior, source, 0, 100, 0x102030);
        Assert.Equal(next, prepared.NextUvCounter);
        Assert.Equal(vertical, prepared.Context.VOffset);
        Assert.Equal(period, prepared.Context.WindowAfter.Height);
        Assert.Equal(0xF0u, prepared.Context.Flags);
        Assert.Equal(0x102030u, prepared.Context.FarColor);
        Assert.Equal(100, prepared.Context.Fade);
    }

    private static (NativeBattleRestorationFrame Frame, uint Task) Fixture(ushort effect)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, 4).Select(index => NativeBattleEffectTaskPool.Initialize(profile.Descriptors[index], profile.Banks[index], new ushort[] { 100, 88, 676, 64 }[index], new ushort[] { 2, 4, 3, 10 }[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.MainCallback, 100);
        NativeBattleEffectHierarchyAppend context = root.Hierarchy.Append8DC540(2, profile.GenericCallback, 676, root.Address, (_, _, _) => new(0, 0));
        hierarchy = context.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(context.Address).WriteWord(0x34 + 0x20, 0).WriteWord(0x34 + 0x18, 0).WriteWord(0x34 + 0x1A, 1).WriteWord(0x34 + 0x14, 1).WriteWord(0x34 + 0x16, 1);
        hierarchy = hierarchy.Write(context.Address, task);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, profile.Descriptors[0]) { Pools = hierarchy.Pools };
        NativeBattleRestorationFrame frame = new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1), selected, new(0, null, null, 0, null) { PassEmitterCount = 0, PassGeometryCount = 0 });
        return (frame, context.Address);
    }

    private static NativeBattleModulePoolTask Read(NativeBattleRestorationFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

    private static NativeBattleRestorationFrame Write(NativeBattleRestorationFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> write)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, write(hierarchy.Read(address))).Pools } };
    }
}
