namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactTasksTests
{
    [Fact]
    public void Three_hundred_damage_contacts_keep_order_and_anchors_until_the_tenth_callback()
    {
        const int count = 300;
        BattleModelOwner[] owners = Enumerable.Range(0, 25).Select(Owner).ToArray();
        BattleActorModels models = Models(owners);
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena);
        for (int index = 0; index < count; index++)
        {
            state = state.PrependDamageText(owners[index % owners.Length], 0, 100 + index, models.Roster);
        }
        int capacity = state.Slots.Length;
        Assert.True(capacity >= count);
        Assert.Equal(0, state.PublishedCount);
        List<BattleModelOwner> projected = [];
        for (int frame = 0; frame < 10; frame++)
        {
            NativeBattleEffectsStep step = state.Advance(models, Project);
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
                    int row = contact % owners.Length;
                    int amount = 100 + contact;
                    NativeBattleDamageTextDraw draw = state.TextDraws[drawIndex];
                    Assert.Equal(new byte[] { (byte)(56 + amount / 100), (byte)(56 + amount / 10 % 10), (byte)(56 + amount % 10), 0 }, draw.Glyphs);
                    Assert.Equal(108 + row, draw.X);
                    Assert.Equal(88, draw.Y);
                    Assert.Equal(frame == 0 ? 0x808080u : 0x04404040u, draw.Color);
                }
            }
        }
        Assert.Equal(Enumerable.Range(0, count).Reverse().Select(index => owners[index % owners.Length]), projected);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Empty(state.Advance(models).State.TextDraws);

        NativeBattleEffectsState synthetic = state with { PublishedCount = count };
        synthetic.Validate(models.Roster);
        Assert.Null(synthetic.Head);
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(synthetic.PublishedCount, null, () => throw new InvalidOperationException("Busy effects must not read camera state."), ref completion));
        Assert.Equal(17, completion);

        NativeBattleEffectsState reused = state.PrependDamageText(owners[24], 0, 321, models.Roster);
        Assert.Equal(0, reused.Head);
        Assert.Equal(0, reused.Tail);
        Assert.Equal(capacity, reused.Slots.Length);

        NativeBattleDamageTextAnchor Project(BattleModelOwner owner)
        {
            projected.Add(owner);
            return new((short)(120 + Array.IndexOf(owners, owner)), 100);
        }
    }

    [Fact]
    public void Interleaved_status_and_damage_keep_all_tasks_reachable_and_release_the_actual_owners()
    {
        BattleModelOwner[] owners = [Owner(7), Owner(14), Owner(15), Owner(24)];
        BattleActorModels models = Models(owners);
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena).PrependDamageText(owners[0], 0, 123, models.Roster);
        Assert.Equal(state.Head, state.Tail);

        List<BattleModelOwner> expectedResets = [];
        for (int index = 0; index < 16; index++)
        {
            BattleModelOwner owner = owners[index % owners.Length];
            state = state.AppendStatusReset(owner, models.Roster).PrependDamageText(owner, 0, 234, models.Roster);
            expectedResets.Add(owner);
        }
        Assert.Equal(33, state.Slots.Count(task => (task.Flags0 & 1) != 0));
        NativeBattleEffectsStep first = state.Advance(models, _ => new(160, 100));
        Assert.Equal(33, first.State.PublishedCount);
        Assert.Equal(17, first.State.TextDraws.Length);
        Assert.Empty(first.ResetModelStatuses);
        Assert.Equal(new byte[] { 57, 58, 59, 0 }, first.State.TextDraws[^1].Glyphs);

        models = models.Set(owners[0], models.Get(owners[0]) with { Flags8 = 16 });
        models = models.Set(owners[1], models.Get(owners[1]) with { ResourceFlags44 = 0x40 });
        models = models.Set(owners[2], models.Get(owners[2]) with { ResourceFlags44 = 0x100 });
        models = models.Set(owners[3], models.Get(owners[3]) with { Flags8 = 2 });
        NativeBattleEffectsStep released = first.State.Advance(models, _ => throw new InvalidOperationException("Published text anchors should be reused."));
        Assert.Equal(expectedResets, released.ResetModelStatuses);
        Assert.Equal(17, released.State.PublishedCount);
        Assert.Equal(17, released.State.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.DoesNotContain(released.State.Slots, task => (task.Flags0 & 1) == 0 && task.ModelOwner is not null);
        Assert.Equal(NativeBattleEffectKind.DamageText5069B0, released.State.Slots[released.State.Tail!.Value].Kind);
    }

    [Theory]
    [InlineData(BattleContactTaskStorageContract.OriginalArena)]
    [InlineData(BattleContactTaskStorageContract.ExpandableArena)]
    public void Task_owners_must_remain_bound_and_cannot_be_redirected_to_a_replacement(BattleContactTaskStorageContract contract)
    {
        BattleModelOwner target = Owner(24);
        BattleModelOwner other = Owner(7);
        BattleModelOwner replacement = Owner(14);
        BattleActorModels models = Models([target, other]);
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(contract).PrependDamageText(target, 0, 123, models.Roster);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.AppendStatusReset(replacement, models.Roster));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PrependDamageText(replacement, 1, 123, models.Roster));
        Assert.Equal(0, state.PublishedCount);
        Assert.Single(state.Slots.Where(task => (task.Flags0 & 1) != 0));
        Assert.Equal(target, state.Slots[state.Head!.Value].ModelOwner);

        BattleActorModels rebound = models.Remove(target).Add(replacement, models.Get(target));
        Assert.Throws<InvalidDataException>(() => state.Advance(rebound, _ => throw new InvalidOperationException("A retired owner cannot project.")));
        NativeBattleEffectsState malformed = state with { Slots = state.Slots.SetItem(state.Head.Value, state.Slots[state.Head.Value] with { ModelOwner = null }) };
        Assert.Throws<InvalidDataException>(() => malformed.Validate(models.Roster));
    }

    [Fact]
    public void Expandable_validation_requires_a_terminal_tail_and_every_active_task()
    {
        BattleModelOwner[] owners = [Owner(7), Owner(14), Owner(24)];
        BattleModelRoster roster = Models(owners).Roster;
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena)
            .PrependDamageText(owners[0], 0, 123, roster)
            .PrependDamageText(owners[1], 0, 234, roster)
            .PrependDamageText(owners[2], 0, 345, roster);
        state.Validate(roster);
        int head = state.Head!.Value;
        int next = state.Slots[head].Next!.Value;
        Assert.NotNull(state.Slots[next].Next);

        NativeBattleEffectsState nonterminalTail = state with { Tail = head };
        Assert.Throws<InvalidDataException>(() => nonterminalTail.AppendStatusReset(owners[0], roster));
        Assert.Equal(next, nonterminalTail.Slots[head].Next);
        Assert.Equal(3, nonterminalTail.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Throws<InvalidDataException>(() => (state with { Tail = null }).Validate(roster));
        Assert.Throws<InvalidDataException>(() => (state with { Head = next }).Validate(roster));
    }

    [Fact]
    public void Original_text_keeps_its_tail_and_twenty_slot_exhaustion_with_a_typed_roster()
    {
        BattleModelOwner target = Owner(3);
        BattleActorModels models = Models([target]);
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).PrependDamageText(target, 0, 123, models.Roster);
        Assert.Equal(0, state.Head);
        Assert.Null(state.Tail);
        for (int index = 1; index < 20; index++)
        {
            state = state.PrependDamageText(target, 0, 123, models.Roster);
        }
        Assert.Equal(20, state.Slots.Length);
        Assert.Null(state.Tail);
        Assert.Same(state, state.PrependDamageText(target, 0, 234, models.Roster));
        Assert.Throws<InvalidOperationException>(() => state.AppendStatusReset(target, models.Roster));
        Assert.Equal(20, state.Advance(models, _ => new(160, 100)).State.PublishedCount);
    }

    [Fact]
    public void Synthetic_busy_publication_and_negative_counts_keep_distinct_contracts()
    {
        BattleModelRoster roster = Models([Owner(7)]).Roster;
        NativeBattleEffectsState synthetic = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena) with { PublishedCount = 1 };
        Assert.Null(synthetic.Head);
        Assert.Null(synthetic.Tail);
        synthetic.Validate(roster);
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(synthetic.PublishedCount, null, 0, ref completion));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsCompletion.PollTask112(-1, null, 0, ref completion));
        Assert.Throws<InvalidDataException>(() => (synthetic with { PublishedCount = -1 }).Validate(roster));
        Assert.Throws<InvalidDataException>(() => (synthetic with { PublishedCount = 21 }).Validate(roster));
        Assert.Equal(17, completion);
    }

    private static BattleModelOwner Owner(int index)
    {
        return index is < 3 or >= 7 and <= 14 ? new BattlePartyActorModel(new($"ally-{index}")) : new BattleEnemyActorModel(new($"enemy-{index}"));
    }

    private static BattleActorModels Models(IEnumerable<BattleModelOwner> owners)
    {
        return new(owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new(3) { Flags8 = 0, ResourceFlags44 = 1 })));
    }
}
