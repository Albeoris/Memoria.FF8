using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedNumericAiTargetTests
{
    [Theory]
    [InlineData(0, "party-0")]
    [InlineData(1, "party-0")]
    [InlineData(2, "party-0")]
    [InlineData(3, "enemy-0")]
    [InlineData(6, "enemy-3")]
    [InlineData(7, "enemy-3")]
    [InlineData(12, "enemy-3")]
    [InlineData(16, null)]
    [InlineData(32, "party-0")]
    public void NumericSelectionsUseTheOriginalPhysicalMaskAndReplacement(byte value, string? target)
    {
        BattleSession battle = Create([0]);
        Assert.Equal(target is null ? [] : new[] { target }, Targets(battle, unchecked((ushort)(1u << value))));
    }

    [Fact]
    public void UnownedMaskFailsWhenReplacementIsDisabledOrExhaustsTheNativeBank()
    {
        BattleSession disabled = Create([50, 0]);
        Assert.Contains("leaves its participant bank", Assert.Throws<NotSupportedException>(() => Targets(disabled, 1 << 7)).Message);
        Assert.Contains("has no current participant", Assert.Throws<NotSupportedException>(() => Targets(disabled, 1 << 1)).Message);
        Assert.Equal(new[] { "party-0" }, Targets(disabled, 1));
        BattleSession exhausted = Create([0], enemyCount: 1);
        Assert.Contains("leaves its participant bank", Assert.Throws<NotSupportedException>(() => Targets(exhausted, 1 << 12)).Message);
    }

    [Fact]
    public void RandomAndGroupMaskQueriesUseLogicalRecipientsWithoutConsumingRng()
    {
        BattleSession battle = Create([0], knockedOutFourth: true);
        NativeBattleRandomState? before = battle.Capture().Random;
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 1 << 13));
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 1 << 15));
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, Targets(battle, 1 << 15, Action() with { Command = 2, TargetFlags = 1 }));
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, Targets(battle, 1 << 15, Action() with { AttackFlags = 0x80 }));
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 0, Action() with { Command = 4, TargetFlags = 2 }));
        Assert.Empty(Targets(battle, 1 << 14));
        Assert.Empty(Targets(battle, 0));
        DetachedExpandedAiFixture.AssertSameRandom(before, battle.Capture().Random);
    }

    [Theory]
    [InlineData(0, "party-0")]
    [InlineData(13, "party-3")]
    [InlineData(15, null)]
    [InlineData(32, "party-0")]
    public void QueuedNumericTargetRoundTripsAndCommitsThePlannedContacts(byte value, string? singleTarget)
    {
        BattleSession battle = Create([14, 220, value, 4, 220, 7, 1, 6, 0]);
        BattleSnapshot snapshot = battle.Capture();
        BattleAttackCommand queued = Assert.Single(snapshot.QueuedActions, command => command.NativeAiCallbackStage == 0);
        Assert.Equal("enemy-0", queued.TargetId);
        switch (value)
        {
            case 13:
                Assert.Equal(BattleSide.Allies, Assert.IsType<BattleTargetSelection.RandomOnSide>(queued.TargetRequest.Selection).Side);
                break;
            case 15:
                Assert.Equal(BattleTargetSide.Allies, Assert.IsType<BattleTargetSelection.SideGroup>(queued.TargetRequest.Selection).Side);
                break;
            default:
                Assert.Equal(unchecked((ushort)(1u << value)), NativeBattleTargetRequestAdapter.ProjectOriginalWord(queued.TargetRequest, _ => null));
                break;
        }
        BattleSession restored = Restore(snapshot);
        DetachedExpandedAiFixture.AssertSameRandom(snapshot.Random, restored.Capture().Random);
        DetachedExpandedAiFixture.AdvanceTo(battle, () => battle.Events.Any(item => item.Kind == "ActionComplete"), "The queued numeric selection must complete its original command.");
        DetachedExpandedAiFixture.AdvanceTo(restored, () => restored.Events.Any(item => item.Kind == "ActionComplete"), "The private copy must complete the same command.");
        string?[] targets = battle.Events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId).ToArray();
        Assert.Equal(singleTarget is null ? ["party-0", "party-1", "party-2", "party-3"] : new[] { singleTarget }, targets);
        NativeBattleRandomState expectedRandom = value == 13 ? snapshot.Random!.Draw(out _) : snapshot.Random!;
        DetachedExpandedAiFixture.AssertSameRandom(expectedRandom, battle.Capture().Random);
        Assert.Equal(targets, restored.Events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId));
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }

    [Fact]
    public void NumericConditionUsesFullIndexAndTypedReferenceKeepsLogicalIdentity()
    {
        byte[] condition = [2, 7, 220, 0, 7, 0, 3, 0, 14, 221, 9, 0];
        BattleSession battle = Create(condition);
        Assert.Equal(9u, Enemy(battle.Capture()).NativeParticipant!.Variables[1]);
        Assert.Contains("actor index 32", Assert.Throws<NotSupportedException>(() => Create([14, 220, 32, .. condition])).Message);
        Assert.Contains("actor index 1", Assert.Throws<NotSupportedException>(() => Create([14, 220, 1, .. condition])).Message);

        BattleSession reference = Create([0], turn: [.. condition[..^1], 4, 220, 7, 1, 6, 0]);
        BattleSnapshot snapshot = reference.Capture();
        BattleCombatantSnapshot enemy = Enemy(snapshot);
        snapshot = snapshot with
        {
            Combatants = snapshot.Combatants.Select(actor => actor.Id == enemy.Id ? actor with { NativeParticipant = actor.NativeParticipant! with { ActorReferences = new() { [0] = new("party-3", null) } } } : actor).ToArray()
        };
        BattleSession restored = Restore(snapshot);
        DetachedExpandedAiFixture.AdvanceTo(restored, () => restored.ActiveAction is { ActorId: "enemy-0", NativeAiCallbackStage: 1 }, "The restored target reference must reach its turn command.");
        Assert.Equal(9u, Enemy(restored.Capture()).NativeParticipant!.Variables[1]);
        Assert.Equal("party-3", restored.ActiveAction?.TargetId);
        BattleTargetSelection.Explicit selected = Assert.IsType<BattleTargetSelection.Explicit>(restored.ActiveAction!.TargetRequest.Selection);
        Assert.Equal(new BattlePartyActorReference("party-3"), Assert.Single(selected.Actors));
    }

    [Fact]
    public void AdmissionRequiresOriginalSlotsAndPrivateRestoreRejectsUnknownCanonicalTarget()
    {
        BattleNativeAction action = Action();
        NativeBattleAiDefinition numeric = Program([4, 220, 2, 7, 220, 0, 7, 0, 0, 0, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(numeric, [action], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(numeric, [action], 0, originalEnemySlots: true));

        BattleSnapshot saved = Create([4, 220, 7, 1, 6, 0]).Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions, command => command.NativeAiCallbackStage == 0);
        BattleTargetRequest missing = new(new BattleTargetSelection.Explicit([new BattlePartyActorReference("retired")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
        Assert.Throws<InvalidDataException>(() => Restore(saved with { QueuedActions = saved.QueuedActions.Select(command => ReferenceEquals(command, queued) ? command with { TargetRequest = missing } : command).ToArray() }));
    }

    [Fact]
    public void UnsupportedAdmissionTakesPrecedenceOverOriginalSlotRequirements()
    {
        foreach (byte[] script in new byte[][] { [7, 2, 43, 0, 4, 220, 0], [43, 0, 4, 220, 7, 2, 0] })
        {
            Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Program(script), [Action()], 0));
        }
    }

    [Fact]
    public void GeneralModeAllowsZeroSelectionAndModifiersButRejectsPhysicalMasks()
    {
        BattleSession battle = Create([0], originalEnemySlots: false);
        Assert.Empty(Targets(battle, 0));
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, Targets(battle, 0, Action() with { Command = 2, TargetFlags = 2 }));
        Assert.Throws<InvalidDataException>(() => Create([14, 220, 0, 4, 220, 7, 1, 6, 0], originalEnemySlots: false));

        BattleSnapshot saved = Create([14, 220, 255, 4, 220, 7, 1, 6, 0], originalEnemySlots: false).Capture();
        Assert.Equal(255u, Enemy(saved).NativeParticipant!.Variables[0]);
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions, command => command.NativeAiCallbackStage == 0);
        Assert.True(Assert.IsType<NativeBattleTargetRequestAdapter.DeferredOperands>(queued.TargetRequest.Selection).IsEmpty);
        Assert.Empty(Resolve(Restore(saved), queued.TargetRequest));
    }

    private static string[] Targets(BattleSession battle, ushort word, BattleNativeAction? action = null)
    {
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.Decode(word, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: false);
        bool allowsKnockedOutAllies = request.AllowsKnockedOutAllies || action is { Command: 8 or 252 } && (action.AttackFlags & 0x80) != 0;
        BattleTargetSelection selection = request.Selection;
        if (action is { Command: 2 or 4 })
        {
            byte flags = action.TargetFlags.GetValueOrDefault();
            allowsKnockedOutAllies |= (flags & 1) != 0;
            if ((flags & 2) != 0)
            {
                selection = NativeBattleTargetRequestAdapter.RandomizeOriginalSelection(selection, samplesOriginalSlots: false);
            }
        }
        return Resolve(battle, new(selection, BattleTargetEligibility.SavedStatus, allowsKnockedOutAllies, request.Replacement));
    }

    private static string[] Resolve(BattleSession battle, BattleTargetRequest request)
    {
        MethodInfo resolve = typeof(BattleSession).GetMethod("ResolveBattleTargets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            return ((BattleCombatant[])resolve.Invoke(battle, [request, false])!).Select(actor => actor.Id).ToArray();
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error.InnerException);
            throw;
        }
    }

    private static BattleCombatantSnapshot Enemy(BattleSnapshot snapshot)
    {
        return Assert.Single(snapshot.Combatants, actor => actor.Id == "enemy-0");
    }

    private static BattleSession Create(byte[] init, int enemyCount = 4, bool knockedOutFourth = false, byte[]? turn = null, bool originalEnemySlots = true)
    {
        NativeCharacterVitals frozen = MagicSystemFixture.Create() with { CombatParty = [0, 255, 255] };
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}", Name = $"Party {row}", ModelId = row, IsPlayer = true, MagicRecordId = row, Level = 7,
            CurrentHp = knockedOutFourth && row == 3 ? 0 : 100, NativeStatus0 = knockedOutFourth && row == 3 ? (ushort)1 : (ushort)0
        }).ToArray();
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, enemyCount).Select(row => new BattleCombatantDefinition
        {
            Id = $"enemy-{row}", Name = $"Enemy {row}", ModelId = 20 + row, SceneSlot = row, Level = 5, NativeTemplate = template,
            MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp, Attack = level.Strength, Defense = level.Vitality, Magic = level.Magic, Spirit = level.Spirit,
            Speed = level.Speed, Evasion = level.Evasion, ElementDefenses = level.ElementDefenses, DrawSpells = level.DrawSpells, DrawGuardianForces = level.DrawGuardianForces,
            InitialAtb = row == 0 && turn is not null ? BattleSession.AtbMaximum : 0, NativeAi = row == 0 ? Program(init, turn) : Program([0])
        }).ToArray();
        BattleSession battle = new(new BattleDefinition
        {
            UsesOriginalEnemySlots = originalEnemySlots, MagicVitals = frozen, Combatants = [.. allies, .. enemies],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), InitialRandom = NativeBattleRandomState.ForecastPhase(3), NativeActions = [Action()]
        }, BattleAtbMode.Normal);
        DetachedExpandedAiFixture.DispatchLeadingInitializers(battle);
        return battle;
    }

    private static NativeBattleAiDefinition Program(byte[] init, byte[]? turn = null)
    {
        return new([init, turn ?? [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }

    private static BattleNativeAction Action()
    {
        return new(8, 1, 3, 0)
        {
            Name = "Zero contact", MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0,
            Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null,
            LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }

    private static BattleSession Restore(BattleSnapshot snapshot)
    {
        return DetachedExpandedAiFixture.Restore(snapshot);
    }
}
