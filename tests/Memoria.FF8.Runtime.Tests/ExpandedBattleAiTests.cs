using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleAiTests
{
    [Theory]
    [InlineData(4, 6, 201, 2, "ally-3")]
    [InlineData(4, 6, 202, 5, "enemy-5")]
    [InlineData(4, 6, 207, 6, "enemy-5")]
    [InlineData(4, 6, 208, 2, "ally-3")]
    [InlineData(11, 8, 201, 5, "ally-10")]
    [InlineData(11, 8, 202, 59, "enemy-7")]
    [InlineData(11, 8, 207, 7, "enemy-7")]
    [InlineData(11, 8, 208, 5, "ally-10")]
    public void RandomSelectorsReachTheLastRosterSlots(int allyCount, int enemyCount, byte selector, byte seed, string target)
    {
        BattleSession battle = Start([4, selector, 7, 1, 6, 0], allyCount, enemyCount, seed);
        Assert.Equal(new[] { target }, battle.ActiveAction!.AiRecipients!.ActorIds);
    }

    [Theory]
    [InlineData(204, 4, 6)]
    [InlineData(205, 4, 6)]
    [InlineData(206, 4, 6)]
    [InlineData(204, 11, 8)]
    [InlineData(205, 11, 8)]
    [InlineData(206, 11, 8)]
    public void GroupTargetingAndPendingTurnSurviveSnapshot(byte selector, int allyCount, int enemyCount)
    {
        BattleSession battle = Start([4, selector, 7, 1, 6, 0], allyCount, enemyCount);
        BattleSnapshot snapshot = battle.Capture();
        string[] targets = battle.Combatants.Where(actor => selector == 206 || actor.IsPlayer == (selector == 204)).Select(actor => actor.Id).ToArray();
        Assert.Equal(targets, battle.ActiveAction!.AiRecipients!.ActorIds);
        Assert.Equal(0, battle.ActiveAction.NativeTargetMask);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        for (int i = 0; i < BattleSession.AttackWindupFrames + 1; i++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(targets, battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4").Select(entry => entry.TargetId));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(4, 6)]
    [InlineData(11, 8)]
    public void HighestHpAndLivingCountIncludeLastAlly(int allyCount, int enemyCount)
    {
        // Continue only if the expected number of allies is alive. Then choose the highest HP.
        BattleSession battle = Start([2, 6, 200, 0, (byte)allyCount, 0, 9, 0, 38, 0, 200, 0, 203, 7, 1, 6, 0, 0], allyCount, enemyCount);
        Assert.Equal(new[] { $"ally-{allyCount - 1}" }, battle.ActiveAction!.AiRecipients!.ActorIds);
    }

    [Fact]
    public void ModelSelectorReadsAndTargetsTheEleventhAlly()
    {
        // Subject 0 reads model 10's HP. Opcode 4 targets the same model by identity.
        byte[] turn = [2, 0, 10, 0, 110, 0, 6, 0, 4, 10, 7, 1, 6, 0, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [Physical], 0));
        BattleSession battle = Start(turn, 11, 8);
        Assert.Equal(new[] { "ally-10" }, battle.ActiveAction!.AiRecipients!.ActorIds);
    }

    [Fact]
    public void DirectOperandsAndAllocationAreRejectedUnlessUnreachableInTheFormation()
    {
        NativeBattleAiDefinition program = Ai([2, 3, 0, 0, 62, 0, 3, 0, 43, 4, 0, 4, 201, 7, 1, 6, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(program, [Physical], 62));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [Physical], 344));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([43, 6, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([2, 15, 0, 0, 6, 0, 0, 0, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([31, 0, 0]), [Physical], 236));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([4, 220, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([14, 96, 0, 0]), [Physical], 0));
    }

    private static BattleSession Start(byte[] turn, int allyCount, int enemyCount, byte seed = 0)
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, allyCount).Select(i => new BattleCombatantDefinition { Id = $"ally-{i}", Name = $"Ally {i}", IsPlayer = true, ModelId = i, CurrentHp = 100 + i, MaximumHp = 100 + i, Speed = 1 }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, enemyCount).Select(i => new BattleCombatantDefinition
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
