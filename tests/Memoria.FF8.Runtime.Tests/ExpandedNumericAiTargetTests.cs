using System.Collections.Immutable;
using System.Text.Json;

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
        Assert.Contains("unowned native target", Assert.Throws<NotSupportedException>(() => Targets(disabled, 1 << 7)).Message);
        Assert.Empty(Targets(disabled, 1 << 1));
        BattleSession exhausted = Create([0], enemyCount: 1);
        Assert.Contains("unowned native target", Assert.Throws<NotSupportedException>(() => Targets(exhausted, 1 << 12)).Message);
    }

    [Fact]
    public void RandomAndGroupMaskQueriesUseLogicalRecipientsWithoutConsumingRng()
    {
        BattleSession battle = Create([0], knockedOutFourth: true);
        string before = JsonSerializer.Serialize(battle.Capture().Random);
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 1 << 13));
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 1 << 15));
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, Targets(battle, 1 << 15, Action() with { Command = 2, TargetFlags = 1 }));
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, Targets(battle, 1 << 15, Action() with { AttackFlags = 0x80 }));
        Assert.Equal(new[] { "party-0", "party-1", "party-2" }, Targets(battle, 0, Action() with { Command = 4, TargetFlags = 2 }));
        Assert.Empty(Targets(battle, 1 << 14));
        Assert.Empty(Targets(battle, 0));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture().Random));
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
        BattleAttackCommand queued = Assert.Single(snapshot.QueuedActions);
        Assert.Equal("enemy-0", queued.TargetId);
        Assert.Equal(unchecked((ushort)(1u << value)), queued.AiRecipients!.NativeSelectionMask);
        BattleSession restored = Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot.Random), JsonSerializer.Serialize(restored.Capture().Random));
        for (int frame = 0; frame < 40 && !battle.Events.Any(item => item.Kind == "ActionComplete"); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        string?[] targets = battle.Events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId).ToArray();
        Assert.Equal(singleTarget is null ? ["party-0", "party-1", "party-2", "party-3"] : new[] { singleTarget }, targets);
        NativeBattleRandomState expectedRandom = value == 13 ? snapshot.Random!.Draw(out _) : snapshot.Random!;
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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
            Combatants = snapshot.Combatants.Select(actor => actor.Id == enemy.Id ? actor with { NativeParticipant = actor.NativeParticipant! with { ActorReferences = new() { [0] = "party-3" } } } : actor).ToArray()
        };
        BattleSession restored = Restore(snapshot);
        restored.Tick();
        Assert.Equal(9u, Enemy(restored.Capture()).NativeParticipant!.Variables[1]);
        Assert.Equal("party-3", restored.ActiveAction?.TargetId);
        Assert.Null(restored.ActiveAction!.AiRecipients!.NativeSelectionMask);
    }

    [Fact]
    public void AdmissionRequiresOriginalSlotsAndRestoreRejectsContradictoryNativeMaskPayloads()
    {
        BattleNativeAction action = Action();
        NativeBattleAiDefinition numeric = Program([4, 220, 2, 7, 220, 0, 7, 0, 0, 0, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(numeric, [action], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(numeric, [action], 0, originalEnemySlots: true));

        BattleSnapshot saved = Create([4, 220, 7, 1, 6, 0]).Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions);
        BattleAiRecipients recipients = queued.AiRecipients!;
        BattleAiRecipients[] invalid =
        [
            recipients with { NativeSelectionMask = null },
            recipients with { Selection = BattleAiSelection.Selected },
            recipients with { ActorIds = ["party-0"] },
            recipients with { TargetPolicy = BattleAiTargetPolicy.DrawnMagic },
            recipients with { RandomSide = BattleSide.Allies },
            recipients with { DeathSelfTargetId = queued.ActorId }
        ];
        foreach (BattleAiRecipients candidate in invalid)
        {
            Assert.Throws<InvalidDataException>(() => Restore(saved with { QueuedActions = [queued with { AiRecipients = candidate }] }));
        }
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
        Assert.Throws<InvalidDataException>(() => Targets(battle, 1));

        BattleSnapshot saved = Create([14, 220, 255, 4, 220, 7, 1, 6, 0], originalEnemySlots: false).Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions);
        Assert.Equal((ushort)0, queued.AiRecipients!.NativeSelectionMask);
        Assert.Throws<InvalidDataException>(() => Restore(saved with { QueuedActions = [queued with { AiRecipients = queued.AiRecipients with { NativeSelectionMask = 1 } }] }));
    }

    private static string[] Targets(BattleSession battle, ushort word, BattleNativeAction? action = null)
    {
        return battle.CurrentExpandedAiTargets(new([]) { Selection = BattleAiSelection.NativeMask, NativeSelectionMask = word }, action).Select(actor => actor.Id).ToArray();
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
        return new(new BattleDefinition
        {
            UsesExpandedRoster = true, UsesOriginalEnemySlots = originalEnemySlots, MagicVitals = frozen, Combatants = [.. allies, .. enemies],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), InitialRandom = NativeBattleRandomState.ForecastPhase(3), NativeActions = [Action()]
        }, BattleAtbMode.Normal);
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
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
    }
}
