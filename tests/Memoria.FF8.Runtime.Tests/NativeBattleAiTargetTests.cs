namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiTargetTests
{
    [Theory]
    [InlineData(200, 8)]
    [InlineData(203, 1)]
    [InlineData(204, 0x8007)]
    [InlineData(205, 0x80F8)]
    [InlineData(206, 0x80FF)]
    [InlineData(208, 0xA007)]
    [InlineData(209, 0)]
    [InlineData(17, 8)]
    [InlineData(99, 0)]
    public void NativeTargetSelectorsRetainExactPhysicalMasks(byte selector, ushort expected)
    {
        BattleSession battle = Create([4, selector]);

        Assert.Equal(expected, ReadPublishedTargetMask(battle.Capture()));
    }

    [Fact]
    public void DeadStatusFilterCanSelectAnAllocatedDeadPartyMember()
    {
        BattleSession battle = Create([38, 1, 200, 0, 0], partyStatus: 1, partyHp: 0);

        Assert.Equal((ushort)0x8001, ReadPublishedTargetMask(battle.Capture()));
    }

    [Fact]
    public void InvertedDeadStatusFilterRetainsAllocatedLivingPartyMembers()
    {
        BattleSession battle = Create([38, 1, 200, 3, 0]);

        Assert.Equal((ushort)0x8001, ReadPublishedTargetMask(battle.Capture()));
    }

    [Theory]
    [InlineData(203)]
    [InlineData(205)]
    [InlineData(221)]
    public void ExtremaAreComputedWithinTheRequestedPhysicalGroup(byte criterion)
    {
        BattleSession battle = Create([38, 1, 200, 0, criterion]);

        Assert.Equal((ushort)0x8001, ReadPublishedTargetMask(battle.Capture()));
    }

    [Theory]
    [InlineData(203, 0x8000)]
    [InlineData(204, 0x8008)]
    public void HpExtremaUseOriginalSignedComparisonsAfterRawDwordHpArithmetic(byte criterion, ushort mask)
    {
        //Native60 adds signed WORD-501 to the enemy HP DWORD500, preserving raw KO clear.
        BattleSession battle = Create([60, 11, 254, 38, 1, 201, 0, criterion]);

        Assert.Equal(-1, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(mask, ReadPublishedTargetMask(battle.Capture()));
    }

    [Fact]
    public void RandomEnemyTargetUsesOriginalRejectionDrawsAndSurvivesSnapshot()
    {
        BattleSession battle = Create([4, 202]);
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0);
        byte value;
        do
        {
            expected = expected.Draw(out value);
        }
        while ((value & 3) != 0);
        //The same admitted callback then evaluates its zero-power spell's48FE20 damage formula.
        expected = expected.Draw(out _);

        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal((ushort)8, ReadPublishedTargetMask(snapshot));
        Assert.Equal(expected.Indexes.ToArray(), snapshot.Random!.Indexes.ToArray());
        Assert.Equal(expected.Lane, snapshot.Random.Lane);
        Assert.Equal((ushort)8, ReadPublishedTargetMask(BattleSession.Restore(snapshot).Capture()));
    }

    [Fact]
    public void EmptyRandomFilterFailsAtItsNonterminatingNativeBoundary()
    {
        Assert.Contains("no eligible physical slot", Assert.Throws<NotSupportedException>(() => Create([38, 0, 200, 0, 1])).Message);
    }

    [Fact]
    public void BerserkEnemyUsesLevelBandAbilityZeroInsteadOfExecutingItsTurnProgram()
    {
        BattleSnapshot snapshot = Create([0]).Capture();
        BattleCombatantDefinition enemy = snapshot.Definition.Combatants.Single(actor => actor.Id == "enemy");
        enemy = enemy with { NativeAi = enemy.NativeAi! with { Scripts = [[0], [255], [0], [0], [0]], Abilities = Enumerable.Repeat(new NativeBattleAiAbility(2, 7, 1), 48).ToArray() } };
        snapshot = snapshot with { Definition = snapshot.Definition with { Combatants = [snapshot.Definition.Combatants[0], enemy] } };
        int index = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "enemy");
        snapshot.Combatants[index] = snapshot.Combatants[index] with { Atb = BattleSession.AtbMaximum, NativeStatus = snapshot.Combatants[index].NativeStatus! with { Saved = 0x20 } };
        BattleSession battle = BattleSession.Restore(snapshot);

        battle.Tick();

        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Null(battle.ActiveAction);
        battle.Tick();
        Assert.Equal("enemy", battle.ActiveAction!.ActorId);
        Assert.Equal((ushort)1, battle.ActiveAction.NativeTargetMask);
        Assert.Equal((byte)7, battle.ActiveAction.NativeSequence);
    }

    private static ushort ReadPublishedTargetMask(BattleSnapshot snapshot)
    {
        Assert.Equal((byte)1, snapshot.NativeActionPackets!.ActionCount);
        BattleAttackCommand[] queued = snapshot.QueuedActions.Where(command => command.NativeHeaderIndex is not null).ToArray();
        bool active = snapshot.ActiveAction?.NativeHeaderIndex is not null;
        Assert.Equal(1, queued.Length + (active ? 1 : 0));
        return active ? snapshot.ActiveAction!.NativeTargetMask : Assert.Single(queued).NativeTargetMask;
    }

    private static BattleSession Create(byte[] select, ushort partyStatus = 0, int partyHp = 100)
    {
        byte[] initial = [.. select, 3, 1, 6, 0];
        NativeBattleAiDefinition ai = new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleNativeAction action = new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Name = "Native target component",
            NativeActions = [action],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = partyHp, NativeStatus0 = partyStatus, Attack = 5 },
                .. (partyHp == 0 ? new BattleCombatantDefinition[] { new() { Id = "living-party", Name = "Living party", IsPlayer = true, Speed = 1 } } : []),
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 500, CurrentHp = 500, Attack = 200, NativeAi = ai }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }
}
