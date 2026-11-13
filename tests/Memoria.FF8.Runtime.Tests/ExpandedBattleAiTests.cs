using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleAiTests
{
    [Theory]
    [InlineData(201, "ally-3")]
    [InlineData(202, "enemy-5")]
    [InlineData(207, "enemy-5")]
    [InlineData(208, "ally-3")]
    public void RandomSelectorsReachTheAdditionalSlots(byte selector, string target)
    {
        bool reached = false;
        for (byte seed = 0; seed < 32 && !reached; seed++)
        {
            BattleSession battle = Start([4, selector, 7, 1, 6, 0], seed);
            reached = battle.ActiveAction!.AiRecipients!.ActorIds.Contains(target);
        }
        Assert.True(reached);
    }

    [Theory]
    [InlineData(204, 4)]
    [InlineData(205, 6)]
    [InlineData(206, 10)]
    public void GroupTargetingAndPendingTurnSurviveSnapshot(byte selector, int count)
    {
        BattleSession battle = Start([4, selector, 7, 1, 6, 0]);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(count, battle.ActiveAction!.AiRecipients!.ActorIds.Length);
        Assert.Equal(0, battle.ActiveAction.NativeTargetMask);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        for (int i = 0; i < BattleSession.AttackWindupFrames + 1; i++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(count, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4"));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void HighestHpAndLivingCountIncludeFourthAlly()
    {
        // Continue only if the roster has four living allies. Then choose the highest HP.
        BattleSession battle = Start([2, 6, 200, 0, 4, 0, 9, 0, 38, 0, 200, 0, 203, 7, 1, 6, 0, 0]);
        Assert.Equal(new[] { "ally-3" }, battle.ActiveAction!.AiRecipients!.ActorIds);
    }

    [Fact]
    public void DirectOperandsAndAllocationAreRejectedUnlessUnreachableInTheFormation()
    {
        NativeBattleAiDefinition program = Ai([2, 3, 0, 0, 62, 0, 3, 0, 43, 4, 0, 4, 201, 7, 1, 6, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(program, [Physical], 62));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [Physical], 344));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([31, 0, 0]), [Physical], 236));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([4, 220, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([14, 96, 0, 0]), [Physical], 0));
    }

    private static BattleSession Start(byte[] turn, byte seed = 0)
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(i => new BattleCombatantDefinition { Id = $"ally-{i}", Name = $"Ally {i}", IsPlayer = true, ModelId = i, CurrentHp = 100 + i, MaximumHp = 100 + i, Speed = 1 }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 6).Select(i => new BattleCombatantDefinition
        {
            Id = $"enemy-{i}", Name = $"Enemy {i}", ModelId = 28, Level = 10, CurrentHp = 100, MaximumHp = 100, Speed = 1, Attack = 30,
            InitialAtb = i == 4 ? BattleSession.AtbMaximum : 0, NativeAi = i == 4 ? Ai(turn) : null
        }).ToArray();
        BattleSession battle = new(new() { UsesExpandedRoster = true, Combatants = [.. allies, .. enemies], NativeActions = [Physical], NativeSeed = seed }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }

    private static NativeBattleAiDefinition Ai(byte[] turn)
    {
        return new([[0], turn, [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
    }

    private static BattleNativeAction Physical => new(8, 1, 3, 0)
    {
        Name = "Physical", MagicId = 0, AttackType = 1, Power = 10, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = null,
        StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
    };
}
