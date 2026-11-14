using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactTasksTests
{
    [Fact]
    public void Three_hundred_damage_contacts_keep_order_and_anchors_until_the_tenth_callback()
    {
        const int count = 300;
        NativeBattleGraphicsRoster roster = Roster();
        NativeBattleGraphicsModel[] models = Models(roster);
        NativeBattleGraphicsRules rules = Rules();
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear();
        for (int index = 0; index < count; index++)
        {
            byte row = (byte)(index % roster.ActorCount);
            state = Produce(state, models[row], rules, roster, (ushort)(100 + index));
        }
        int capacity = state.Slots.Length;
        Assert.True(capacity >= count);
        Assert.Equal(0, state.PublishedCount);
        List<byte> projected = [];
        for (int frame = 0; frame < 10; frame++)
        {
            NativeBattleEffectsStep step = state.Advance(models, Project, roster);
            Assert.Empty(step.ResetModelStatuses);
            state = step.State;
            Assert.Equal(frame < 9 ? count : 0, state.PublishedCount);
            Assert.Equal(count, state.TextDraws.Length);
            Assert.Equal(capacity, state.Slots.Length);
            if (frame is 0 or 9)
            {
                for (int drawIndex = 0; drawIndex < count; drawIndex++)
                {
                    int contact = count - 1 - drawIndex;
                    int row = contact % roster.ActorCount;
                    int amount = 100 + contact;
                    NativeBattleDamageTextDraw draw = state.TextDraws[drawIndex];
                    Assert.Equal(new byte[] { (byte)(56 + amount / 100), (byte)(56 + amount / 10 % 10), (byte)(56 + amount % 10), 0 }, draw.Glyphs);
                    Assert.Equal(108 + row, draw.X);
                    Assert.Equal(88, draw.Y);
                    Assert.Equal(frame == 0 ? 0x808080u : 0x04404040u, draw.Color);
                }
            }
        }
        Assert.Equal(Enumerable.Range(0, count).Reverse().Select(index => (byte)(index % roster.ActorCount)), projected);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Empty(state.Advance(models, roster: roster).State.TextDraws);

        // A busy publication is independent from the queue, as with the stage's synthetic 112 gate.
        NativeBattleEffectsState synthetic = state with { PublishedCount = count };
        synthetic.Validate(roster);
        Assert.Null(synthetic.Head);
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollHostedTask112(synthetic.PublishedCount, null, () => throw new InvalidOperationException("Busy effects must not read camera state."), ref completion, roster));
        Assert.Equal(17, completion);
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(synthetic.PublishedCount, null, 0, ref completion, roster));
        Assert.Equal(17, completion);

        NativeBattleEffectsState reused = Produce(state, models[24], rules, roster, 321);
        Assert.Equal(0, reused.Head);
        Assert.Equal(0, reused.Tail);
        Assert.Equal(capacity, reused.Slots.Length);

        NativeBattleDamageTextAnchor Project(byte row)
        {
            projected.Add(row);
            return new((short)(120 + row), 100);
        }
    }

    [Fact]
    public void Interleaved_extended_petrify_and_damage_keep_all_tasks_reachable_and_release_the_actual_rows()
    {
        byte[] rows = [7, 14, 15, 24];
        NativeBattleGraphicsRoster roster = Roster(rows);
        NativeBattleGraphicsModel[] models = Models(roster);
        NativeBattleGraphicsRules rules = Rules();
        NativeBattleEffectsState state = Produce(NativeBattleEffectsState.Clear(), models[7], rules, roster, 123);
        Assert.Equal(state.Head, state.Tail);

        // A retained text-first native shape can also enter the explicit hosted path.
        state = state with { Tail = null };
        List<byte> expectedResets = [];
        for (int index = 0; index < 16; index++)
        {
            byte row = rows[index % rows.Length];
            NativeBattleContactHalf half = new(row, 5, 0, 0, 4, 234, 0);
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, models[row], rules, 0);
            Assert.True(plan.DeferPetrify);
            state = NativeBattleContactEffects.ProduceTasks(state, half, plan, roster);
            expectedResets.Add(row);
        }
        Assert.Equal(33, state.Slots.Count(task => (task.Flags0 & 1) != 0));
        NativeBattleEffectsStep first = state.Advance(models, _ => new(160, 100), roster);
        Assert.Equal(33, first.State.PublishedCount);
        Assert.Equal(17, first.State.TextDraws.Length);
        Assert.Empty(first.ResetModelStatuses);
        Assert.Equal(new byte[] { 57, 58, 59, 0 }, first.State.TextDraws[^1].Glyphs);

        models[7] = models[7] with { Flags8 = 16 };
        models[14] = models[14] with { ResourceFlags44 = 0x40 };
        models[15] = models[15] with { ResourceFlags44 = 0x100 };
        models[24] = models[24] with { Flags8 = 2 };
        NativeBattleEffectsStep released = first.State.Advance(models, _ => throw new InvalidOperationException("Published text anchors should be reused."), roster);
        Assert.Equal(expectedResets, released.ResetModelStatuses);
        Assert.Equal(17, released.State.PublishedCount);
        Assert.Equal(17, released.State.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Equal(NativeBattleEffectKind.DamageText5069B0, released.State.Slots[released.State.Tail!.Value].Kind);
    }

    [Fact]
    public void Hosted_producers_and_callback_validation_reject_sparse_unbound_rows()
    {
        NativeBattleGraphicsRoster roster = Roster([7, 24]);
        NativeBattleGraphicsModel[] models = Models(roster);
        NativeBattleEffectsState state = Produce(NativeBattleEffectsState.Clear(), models[24], Rules(), roster, 123);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.AppendStatusReset(14, roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PrependDamageText(14, 1, 123, roster));
        Assert.Equal(0, state.PublishedCount);
        Assert.Single(state.Slots.Where(task => (task.Flags0 & 1) != 0));
        Assert.Equal((byte)24, state.Slots[state.Head!.Value].ModelIndex);

        NativeBattleEffectsState malformed = state with { Slots = state.Slots.SetItem(state.Head.Value, state.Slots[state.Head.Value] with { ModelIndex = 14 }) };
        Assert.Throws<InvalidDataException>(() => malformed.Validate(roster));
        Assert.Throws<InvalidDataException>(() => malformed.Advance(models, _ => throw new InvalidOperationException("A hole cannot project."), roster));
    }

    [Fact]
    public void Hosted_validation_rejects_a_nonterminal_tail_and_an_unreachable_active_slot()
    {
        NativeBattleGraphicsRoster roster = Roster([7, 14, 24]);
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear()
            .PrependDamageText(7, 0, 123, roster)
            .PrependDamageText(14, 0, 234, roster)
            .PrependDamageText(24, 0, 345, roster);
        state.Validate(roster);
        int head = state.Head!.Value;
        int next = state.Slots[head].Next!.Value;
        Assert.NotNull(state.Slots[next].Next);

        NativeBattleEffectsState nonterminalTail = state with { Tail = head };
        Assert.Throws<InvalidDataException>(() => nonterminalTail.AppendStatusReset(7, roster));
        Assert.Equal(next, nonterminalTail.Slots[head].Next);
        Assert.Equal(3, nonterminalTail.Slots.Count(task => (task.Flags0 & 1) != 0));

        NativeBattleEffectsState unreachable = state with { Head = next };
        Assert.Throws<InvalidDataException>(() => unreachable.Validate(roster));
        state.Validate(roster);
    }

    [Fact]
    public void Native_text_keeps_its_original_tail_and_twenty_slot_exhaustion_behavior()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().PrependDamageText(3, 0, 123);
        Assert.Equal(0, state.Head);
        Assert.Null(state.Tail);
        for (int index = 1; index < 20; index++)
        {
            state = state.PrependDamageText(3, 0, 123);
        }
        Assert.Equal(20, state.Slots.Length);
        Assert.Null(state.Tail);
        Assert.Same(state, state.PrependDamageText(3, 0, 234));
        Assert.Throws<InvalidOperationException>(() => state.AppendStatusReset(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PrependDamageText(7, 0, 123));
        Assert.Equal(20, state.Advance(NativeBattleGraphicsModels.ClearBank(), _ => new(160, 100)).State.PublishedCount);
    }

    [Fact]
    public void Synthetic_busy_publication_and_negative_counts_keep_distinct_contracts()
    {
        NativeBattleEffectsState synthetic = NativeBattleEffectsState.Clear() with { PublishedCount = 1 };
        Assert.Null(synthetic.Head);
        Assert.Null(synthetic.Tail);
        synthetic.Validate();
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(synthetic.PublishedCount, null, 0, ref completion));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsCompletion.PollTask112(-1, null, 0, ref completion));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsCompletion.PollHostedTask112(-1, null, () => throw new InvalidOperationException("Invalid counts cannot read camera state."), ref completion, Roster([7])));
        Assert.Throws<InvalidDataException>(() => (synthetic with { PublishedCount = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (synthetic with { PublishedCount = 21 }).Validate());
        Assert.Equal(17, completion);
    }

    private static NativeBattleGraphicsRoster Roster(byte[]? bound = null)
    {
        return new(Enumerable.Range(0, 25).Select(row => bound is null || bound.Contains((byte)row) ? new NativeBattleGraphicsActor($"actor-{row}", row is < 3 or >= 7 and <= 14) : null).ToImmutableArray());
    }

    private static NativeBattleGraphicsModel[] Models(NativeBattleGraphicsRoster roster)
    {
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        for (int row = 0; row < models.Length; row++)
        {
            if (roster.ContainsRow(row))
            {
                models[row] = models[row] with { ModelId4 = roster[row].IsParty ? (byte)1 : (byte)61, Flags0 = 3, Flags8 = 0, ResourceFlags44 = 1 };
            }
        }
        return models;
    }

    private static NativeBattleEffectsState Produce(NativeBattleEffectsState state, NativeBattleGraphicsModel model, NativeBattleGraphicsRules rules, NativeBattleGraphicsRoster roster, ushort amount)
    {
        NativeBattleContactHalf half = new((byte)model.Index, 5, 0, 0, 0, amount, 0);
        return NativeBattleContactEffects.ProduceTasks(state, half, NativeBattleContactEffects.Plan(half, model, rules, 0), roster);
    }

    private static NativeBattleGraphicsRules Rules()
    {
        ImmutableArray<sbyte> saved = Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray().SetItem(2, 4);
        ImmutableArray<sbyte> battle = Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray();
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved, battle);
    }
}
