using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedAuthoredAiQueryTests
{
    [Fact]
    public void ModelQueriesKeepOriginalOwnershipThroughReleaseAndReplacement()
    {
        BattleSession battle = Start([
            .. Probe(8, 0, 44, 220), .. Probe(9, 0, 44, 221),
            29, 1,
            .. Probe(8, 0, 44, 222), .. Probe(9, 0, 44, 223),
            59, 2, 4,
            .. Probe(8, 0, 44, 224), .. Probe(9, 0, 44, 225),
            .. Probe(8, 0, 3, 226), .. Probe(9, 0, 3, 227), 0
        ]);
        BattleCombatant caller = Actor(battle, "caller");
        Assert.Equal(new uint[] { 1, 1, 1, 0, 0, 0, 1, 1 }, caller.AiVariables);
        Assert.Null(Actor(battle, "original").NativeActorIndex);
        Assert.Equal(4, Actor(battle, "native-allocation-1").NativeActorIndex);
        Assert.Null(Actor(battle, "additional").NativeActorIndex);
        Assert.True(Actor(battle, "additional").IsAlive);
    }

    [Fact]
    public void Model255IncludesAnInactiveOriginalAllyButExcludesAnInactiveAddedAlly()
    {
        BattleSession battle = Start([0], turn: [.. Probe(8, 0, 255, 220), 0]);
        Actor(battle, "ally-3").IsActive = false;
        battle.Tick();
        Assert.Equal(0u, Actor(battle, "caller").AiVariables[0]);

        BattleSession original = Start([0], turn: [.. Probe(8, 0, 255, 220), 0]);
        Actor(original, "ally-0").IsActive = false;
        original.Tick();
        Assert.Equal(1u, Actor(original, "caller").AiVariables[0]);
    }

    [Theory]
    [InlineData("original", true)]
    [InlineData("additional", false)]
    [InlineData("ally-3", true)]
    public void LastAttackerModelComparesOwnedIdentity(string attackerId, bool matches)
    {
        byte model = attackerId == "ally-3" ? (byte)3 : (byte)44;
        BattleSession battle = Start([0], turn: [.. Probe(10, 203, model, 220), 0], hiddenOriginal: true);
        BattleSnapshot saved = battle.Capture();
        BattleSession restored = Restore(saved with
        {
            Combatants = saved.Combatants.Select(actor => actor.Id == "caller"
                ? actor with { NativeParticipant = actor.NativeParticipant! with { LastAttackerId = attackerId } }
                : actor).ToArray()
        });
        restored.Tick();
        Assert.Equal(matches ? 1u : 0u, Actor(restored, "caller").AiVariables[0]);
    }

    [Theory]
    [InlineData(80, 44, "original")]
    [InlineData(150, 44, null)]
    [InlineData(80, 201, "additional")]
    public void EnemyExtremaUsesTheWholeNativeBankForExactModelAndLogicalSideFor201(int originalAllyHp, byte selector, string? target)
    {
        BattleSession battle = Start([38, 1, selector, 0, 203, 7, 1, 6, 0], originalAllyHp: originalAllyHp);
        if (target is null)
        {
            Assert.Empty(battle.QueuedActions);
        }
        else
        {
            Assert.Equal(target, Assert.Single(Assert.Single(battle.QueuedActions).AiRecipients!.ActorIds));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FreshPartyGroupUsesLateRawAvailabilityAndRestoresWithoutDrawing(bool originalSlots)
    {
        BattleNativeAction action = Action() with { AttackType = 1, AttackFlags = 0x83, Hit = 255 };
        BattleSession battle = Start([4, 204, 7, 1, 6, 0], allyStatuses: true, action: action, originalSlots: originalSlots);
        BattleSnapshot saved = battle.Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions);
        BattleAiRecipients recipients = Assert.IsType<BattleAiRecipients>(queued.AiRecipients);
        Assert.Equal((ushort)0x8007, recipients.NativeSelectionMask);
        Assert.Empty(recipients.ActorIds);
        Assert.Equal(new[] { "ally-0", "ally-2", "ally-3" }, Targets(battle, recipients, Action()));
        string[] expected = ["ally-0", "ally-1", "ally-2", "ally-3"];
        Assert.Equal(expected, Targets(battle, recipients, action));
        BattleSession restored = Restore(saved);
        Assert.Equal(JsonSerializer.Serialize(saved.Random), JsonSerializer.Serialize(restored.Capture().Random));
        Assert.Equal(expected, Targets(restored, recipients, action));
        Assert.Equal(JsonSerializer.Serialize(saved.Random), JsonSerializer.Serialize(restored.Capture().Random));
        AdvanceOneAction(battle, restored);
        Assert.Equal(expected, battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void EnemyGroupResolvesAddedAndNewlyAllocatedActors()
    {
        BattleSession battle = Start([4, 205, 7, 1, 6, 31, 2, 0]);
        BattleSnapshot saved = battle.Capture();
        BattleAiRecipients recipients = Assert.Single(saved.QueuedActions).AiRecipients!;
        Assert.Equal((ushort)0x80F8, recipients.NativeSelectionMask);
        string[] expected = ["caller", "original", "native-allocation-1", "additional"];
        BattleSession restored = Restore(saved);
        Assert.Equal(expected, Targets(battle, recipients, Action()));
        Assert.Equal(expected, Targets(restored, recipients, Action()));
        AdvanceOneAction(battle, restored);
        Assert.Equal(expected, battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void AllGroupResolvesBothLogicalSides()
    {
        BattleSession battle = Start([4, 206, 7, 1, 6, 0]);
        BattleAiRecipients recipients = Assert.Single(battle.QueuedActions).AiRecipients!;
        Assert.Equal((ushort)0x80FF, recipients.NativeSelectionMask);
        Assert.Equal(new[] { "ally-0", "ally-1", "ally-2", "ally-3", "caller", "original", "additional" }, Targets(battle, recipients, Action()));
    }

    [Fact]
    public void LegacyGroupKeepsCapturedIdsAndUsesLateRawAvailability()
    {
        BattleNativeAction action = Action() with { AttackType = 1, AttackFlags = 0x83, Hit = 255 };
        BattleSession battle = Start([4, 204, 7, 1, 6, 0], allyStatuses: true, action: action);
        BattleSnapshot saved = battle.Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions);
        BattleAiRecipients legacy = new(["ally-0", "ally-1", "ally-2"]) { Selection = BattleAiSelection.Group };
        BattleSession restored = Restore(saved with { QueuedActions = [queued with { TargetId = "ally-0", AiRecipients = legacy }] });
        Assert.Equal(new[] { "ally-0", "ally-2" }, Targets(restored, legacy, Action()));
        Assert.Equal(new[] { "ally-0", "ally-1", "ally-2" }, Targets(restored, legacy, action));
    }

    [Fact]
    public void DrawReadsFirstOriginalAllocationFromThePreviousQueuedGroupMask()
    {
        BattleSession battle = Start([4, 204, 7, 1, 6, 41, 0], drawStocks: true);
        BattleSnapshot saved = battle.Capture();
        BattleCombatant caller = Actor(battle, "caller");
        Assert.False(Actor(battle, "ally-0").IsAlive);
        Assert.Equal((ushort)0x8007, caller.NativePreviousTargetMask);
        Assert.Null(caller.ExpandedPreviousAiTargetId);
        Assert.Equal((byte)1, caller.NativeDrawnSpell);
        BattleSession restored = Restore(saved);
        Assert.Equal((ushort)0x8007, Actor(restored, "caller").NativePreviousTargetMask);
        Assert.Equal((byte)1, Actor(restored, "caller").NativeDrawnSpell);
        AdvanceOneAction(battle, restored);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static byte[] Probe(byte subject, byte selector, byte value, byte variable)
    {
        return [2, subject, selector, 0, value, 0, 3, 0, 14, variable, 1];
    }

    private static BattleSession Start(byte[] init, byte[]? turn = null, bool hiddenOriginal = false, int originalAllyHp = 80, bool allyStatuses = false, BattleNativeAction? action = null, bool originalSlots = true, bool drawStocks = false)
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", ModelId = index, IsPlayer = true, MaximumHp = 300, NativeDeathCount = 0,
            CurrentHp = drawStocks && index == 0 ? 0 : index == 0 ? originalAllyHp : allyStatuses && index == 1 ? 0 : index == 3 ? 250 : 80,
            NativeStatus0 = drawStocks && index == 0 ? (ushort)1 : allyStatuses ? (ushort)(index == 1 ? 1 : index == 2 ? 4 : 0) : (ushort)0,
            NativeMagicInventory = drawStocks && index is 0 or 3 ? [new NativeMagicStock(index == 0 ? (byte)1 : (byte)2, 10), .. Enumerable.Repeat(new NativeMagicStock(0, 0), 31)] : null
        }).ToArray();
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray())
        {
            NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray())
        };
        BattleCombatantDefinition Original(string id, int slot, int model, bool active) => new()
        {
            Id = id, Name = id, SceneSlot = slot, ModelId = model, InitialActive = active, Level = 5, NativeTemplate = originalSlots ? template : null,
            MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp, Attack = level.Strength, Defense = level.Vitality,
            Magic = level.Magic, Spirit = level.Spirit, Speed = level.Speed, Evasion = level.Evasion, ElementDefenses = level.ElementDefenses
        };
        BattleCombatantDefinition caller = Original("caller", 0, 29, true) with
        {
            InitialAtb = turn is null ? 0 : BattleSession.AtbMaximum,
            NativeAi = new([init, turn ?? [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, [])
        };
        BattleCombatantDefinition original = Original("original", 1, 28, true) with { InitialHidden = hiddenOriginal, InitialUntargetable = hiddenOriginal };
        BattleCombatantDefinition added = new()
        {
            Id = "additional", Name = "Additional", SceneSlot = 8, ModelId = 28, MaximumHp = 300, CurrentHp = 300,
            RewardSlot = 8, Rewards = new(10, 0, 20, 30, 255, false, 0, Enumerable.Repeat(new BattleStock(0, 0), 12).ToImmutableArray())
        };
        BattleCombatantDefinition[] enemies = originalSlots ? [caller, original, Original("reserve", 2, 30, false), added] : [caller, original];
        string?[] messages = new string?[119];
        messages[11] = " ";
        messages[19] = "Draw";
        messages[118] = "!";
        return new(new BattleDefinition
        {
            UsesExpandedRoster = true, UsesOriginalEnemySlots = originalSlots, Combatants = [.. allies, .. enemies],
            AddedEnemySources = originalSlots ? ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty.Add(8, new(2, 0, new string('A', 64))) : null,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), InitialRandom = NativeBattleRandomState.ForecastPhase(3),
            NativeActions = [action ?? Action(), Action() with { Command = 2, Name = "Fire" }],
            NativeGlobalMessages = messages, NativeMessageSpeed = 0
        }, BattleAtbMode.Normal);
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

    private static BattleCombatant Actor(BattleSession battle, string id) => Assert.Single(battle.Combatants, actor => actor.Id == id);

    private static string[] Targets(BattleSession battle, BattleAiRecipients recipients, BattleNativeAction action)
    {
        return battle.CurrentExpandedAiTargets(recipients, action).Select(actor => actor.Id).ToArray();
    }

    private static BattleSession Restore(BattleSnapshot saved)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
    }

    private static void AdvanceOneAction(BattleSession battle, BattleSession restored)
    {
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "ActionComplete"); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
    }
}
