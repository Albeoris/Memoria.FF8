using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenAttackTests
{
    [Fact]
    public void Initial_flare_is_unconditional_and_leaves_unwritten_raw_words_intact()
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58DED0, 0);
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenAttack.Advance(frame, ZantetsukenFixture.Address, ZantetsukenFixture.Inputs(default));
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[1].Slots[1];
        Assert.Equal(0x58E120u, child.Callback);
        Assert.Equal((ushort)0, child.ReadWord(12));
        Assert.Equal(unchecked((ushort)-170), child.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)-30), child.ReadWord(0x12));
        Assert.Equal((ushort)0x860, child.ReadWord(0x18));
        Assert.Equal((ushort)0x1C0, child.ReadWord(0x1C));
        Assert.Equal((ushort)0x1C0, child.ReadWord(0x1E));
        Assert.Equal((ushort)0x600, child.ReadWord(0x20));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(14));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x14));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x22));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(1, step.Frame.Effect.Pools[1].Slots[0].Next);
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(-4096)]
    public void Mixed_contacts_preserve_original_ordinals_and_draw_six_times_only_for_actor_pair(int height)
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58DED0, 16);
        List<NativeBattleGilgameshOperation> operations = [];
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs([new(1, 4), new(3, 0), new(2, 4)]) with
        {
            ReadModelWord26 = _ => (short)height,
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return ZantetsukenFixture.Execute(current, operation);
            }
        };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenAttack.Advance(frame, ZantetsukenFixture.Address, inputs);
        Assert.Collection(operations, operation => Assert.Equal(0, Assert.IsType<NativeBattleGilgameshZantetsukenContact>(operation).Ordinal), operation => Assert.Equal((byte)3, Assert.IsType<NativeBattleGilgameshZantetsukenAnchor>(operation).Actor), operation => Assert.Equal(2, Assert.IsType<NativeBattleGilgameshZantetsukenContact>(operation).Ordinal));
        NativeBattleModulePoolTask first = step.Frame.Effect.Pools[1].Slots[1];
        NativeBattleModulePoolTask second = step.Frame.Effect.Pools[1].Slots[2];
        Assert.Equal(0x58E3E0u, first.Callback);
        Assert.Equal(0x58E570u, second.Callback);
        Assert.Equal((ushort)3, first.ReadWord(14));
        Assert.Equal((ushort)3, second.ReadWord(14));
        Assert.Equal((ushort)1, first.ReadWord(0x16));
        Assert.Equal((ushort)1, second.ReadWord(0x16));
        Assert.Equal((ushort)1, second.ReadWord(0x1A));
        Assert.Equal((ushort)128, second.ReadWord(0x18));
        NativeCrtRandomState random = frame.Random.Draw(out ushort angleDraw);
        int angle = angleDraw % 1280 + 2816;
        Assert.Equal(unchecked((ushort)((angle & 1) != 0 ? -angle : angle)), first.ReadWord(0x18));
        random = random.Draw(out ushort velocityDraw);
        Assert.Equal(unchecked((ushort)(-150 - velocityDraw % 300)), first.ReadWord(0x20));
        Assert.Equal(first.ReadWord(0x20), first.ReadWord(0x22));
        random = random.Draw(out ushort radiusDraw);
        int radius = (radiusDraw % 256 + 128) * height >> 12;
        foreach (int offset in new[] { 0x10, 0x12, 0x14 })
        {
            random = random.Draw(out ushort draw);
            Assert.Equal(unchecked((ushort)(first.ReadWord(offset) + draw % (radius * 2) - radius)), second.ReadWord(offset));
        }
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(1, step.Frame.Effect.Pools[1].Slots[0].Next);
        Assert.Equal(2, first.Next);
        Assert.Null(second.Next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Zero_eligible_targets_create_one_delayed_stage_fallback_without_random_draws(bool hasMiss)
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58DED0, 16);
        List<NativeBattleGilgameshOperation> operations = [];
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs(hasMiss ? [new(3, 4)] : []) with { Execute = (current, operation) => { operations.Add(operation); return current; } };
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenAttack.Advance(frame, ZantetsukenFixture.Address, inputs);
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[1].Slots[1];
        Assert.Equal(0x58F940u, child.Callback);
        Assert.Equal((ushort)20, child.ReadWord(14));
        Assert.Equal((ushort)0, child.ReadWord(0x20));
        Assert.Equal((ushort)600, child.ReadWord(0x22));
        Assert.Equal(hasMiss ? 1 : 0, operations.Count);
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Native_zero_radius_fault_is_not_replaced_by_a_radius_floor(int height)
    {
        NativeBattleGilgameshZantetsukenInputs inputs = ZantetsukenFixture.Inputs([new(3, 0)]) with { ReadModelWord26 = _ => (short)height };
        Assert.Throws<DivideByZeroException>(() => NativeBattleGilgameshZantetsukenAttack.Advance(ZantetsukenFixture.Create(0x58DED0, 16), ZantetsukenFixture.Address, inputs));
    }

    [Theory]
    [InlineData(49, 50, 2)]
    [InlineData(32767, 32768, 0)]
    [InlineData(65535, 0, 0)]
    public void Terminal_counter_is_signed_after_word_wrap(int old, int next, int result)
    {
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenAttack.Advance(ZantetsukenFixture.Create(0x58DED0, (ushort)old), ZantetsukenFixture.Address, ZantetsukenFixture.Inputs(default));
        Assert.Equal((ushort)next, step.Frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(result, step.Result);
    }

    [Fact]
    public void Pause_precedes_counter_and_contact_owner_reads()
    {
        NativeBattleGilgameshFrame frame = ZantetsukenFixture.Create(0x58DED0, 16);
        frame = ZantetsukenFixture.Write(frame, ZantetsukenFixture.Address, frame.Effect.Pools[1].Slots[0].MarkPayloadUnknown(12, 2));
        NativeBattleGilgameshZantetsukenStep step = NativeBattleGilgameshZantetsukenAttack.Advance(frame, ZantetsukenFixture.Address, ZantetsukenFixture.Inputs(default) with { GraphicsFlags = 0x201 });
        Assert.Same(frame, step.Frame);
    }
}

internal static class ZantetsukenFixture
{
    internal const uint Address = 0x21FFD68;

    internal static NativeBattleGilgameshFrame Create(uint callback, ushort age)
    {
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x21FFD58, Address, 36, 100);
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
        int slot;
        (children, slot) = children.Append(callback);
        NativeBattleModulePoolTask task = children.Slots[slot].WriteWord(12, age).WriteWord(14, 3).WriteWord(0x10, 500).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x14, unchecked((ushort)-300)).WriteWord(0x16, 0).WriteWord(0x18, 128).WriteWord(0x1A, 2).WriteWord(0x20, unchecked((ushort)-300)).WriteWord(0x22, unchecked((ushort)-300));
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(slot, task) });
        NativeBattleOriginalEffectState effect = new(327, 326, new(327, 0x58D760, 0x58D6E0), null, null)
        {
            Pools = [NativeBattleEffectTaskPool.Initialize(0x2201000, 0x2201010, 100, 2), children, NativeBattleEffectTaskPool.Initialize(0x2202000, 0x2202010, 24, 2)]
        };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Gilgamesh = NativeBattleGilgameshProcess.FromColdProcess() };
        return new(memory, effect, new(0x12345678));
    }

    internal static NativeBattleGilgameshZantetsukenInputs Inputs(ImmutableArray<NativeBattleGilgameshZantetsukenContactRow> contacts) => new(0, contacts, _ => 4096, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Execute);

    internal static NativeBattleGilgameshFrame Execute(NativeBattleGilgameshFrame frame, NativeBattleGilgameshOperation operation)
    {
        if (operation is NativeBattleGilgameshZantetsukenAnchor anchor)
        {
            int index = checked((int)((anchor.Address - Address) / 36));
            NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[index].WriteWord(0x10, 500).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x14, unchecked((ushort)-300)).WriteWord(0x16, 123);
            return Write(frame, anchor.Address, task);
        }
        return frame;
    }

    internal static NativeBattleGilgameshFrame Write(NativeBattleGilgameshFrame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(checked((int)((address - Address) / 36)), task) })) } };
    }
}
