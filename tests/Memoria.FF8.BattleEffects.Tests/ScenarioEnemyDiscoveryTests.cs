using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyDiscoveryTests
{
    [Theory]
    [InlineData(new byte[] { 14, 220, 0, 14, 221, 0, 14, 222, 0, 36, 0, 0 }, true)]
    [InlineData(new byte[] { 14, 227, 255, 0 }, true)]
    [InlineData(new byte[] { 36, 0 }, true)]
    [InlineData(new byte[] { 0, 59, 200, 64 }, true)]
    [InlineData(new byte[] { 14, 219, 0, 0 }, false)]
    [InlineData(new byte[] { 14, 228, 0, 0 }, false)]
    [InlineData(new byte[] { 14, 220, 203, 0 }, false)]
    [InlineData(new byte[] { 14, 220 }, false)]
    [InlineData(new byte[] { 14, 220, 0 }, false)]
    [InlineData(new byte[] { 59, 200, 64, 0 }, false)]
    [InlineData(new byte[] { 2, 0 }, false)]
    [InlineData(new byte[] { }, false)]
    public void InitializationStatusProofAcceptsOnlyTerminatedLiteralLocalWritesAndAtbSetup(byte[] code, bool expected)
    {
        Assert.Equal(expected, ScenarioEnemyDiscovery.InitializationPreservesParticipantStatuses(code));
    }

    [Theory]
    [InlineData(new byte[] { 48, 36, 0 }, true)]
    [InlineData(new byte[] { 36, 48, 0 }, true)]
    [InlineData(new byte[] { 48, 47, 0 }, false)]
    [InlineData(new byte[] { 47, 48, 0 }, true)]
    [InlineData(new byte[] { 48, 2, 0 }, false)]
    [InlineData(new byte[] { 48, 35, 0 }, false)]
    [InlineData(new byte[] { 48 }, false)]
    [InlineData(new byte[] { 0, 48, 0 }, false)]
    public void InitializationTargetabilityProofRequiresAKnownTerminatedStraightLinePrefix(byte[] code, bool expected)
    {
        Assert.Equal(expected, ScenarioEnemyDiscovery.InitializationUnconditionallyDisablesTargeting(code));
    }

    [Fact]
    public void ScriptDiscoveryFindsPreparedDirectActionsGraphicsAndDynamicDrawWithoutInventedIds()
    {
        EnemyScenarioDiscoveryResult result = DiscoverScript([3, 17, 5, 9, 6, 7, 18, 6, 30, 111, 42, 0]);

        Assert.Empty(result.Issues);
        Assert.Equal(12, result.ScriptedActions.Length);
        EnemyScenarioScriptedAction magic = result.ScriptedActions.Single(action => action.Offset == 4 && action.LevelBand == 0);
        Assert.Equal((byte?)2, magic.Command);
        Assert.Equal((ushort?)17, magic.ActionId);
        Assert.Equal(101, magic.NativeEffectId);
        Assert.Equal((byte?)9, magic.Sequence);
        EnemyScenarioScriptedAction enemy = result.ScriptedActions.Single(action => action.Offset == 7 && action.LevelBand == 0);
        Assert.Equal((byte?)8, enemy.Command);
        Assert.Equal(102, enemy.NativeEffectId);
        EnemyScenarioScriptedAction graphic = result.ScriptedActions.Single(action => action.Offset == 8 && action.LevelBand == 0);
        Assert.Equal((byte?)252, graphic.Command);
        Assert.Equal((ushort?)1, graphic.ActionId);
        Assert.Equal(111, graphic.NativeEffectId);
        Assert.Null(graphic.NativeAction);
        EnemyScenarioScriptedAction draw = result.ScriptedActions.Single(action => action.Offset == 10 && action.LevelBand == 0);
        Assert.Null(draw.ActionId);
        Assert.Null(draw.NativeEffectId);
        Assert.Contains(draw.Blockers, blocker => blocker.Contains("drawn-spell byte", StringComparison.Ordinal));
        Assert.All(result.ScriptedActions, action => Assert.Contains(action.MappingEvidence, evidence => evidence.Contains("ai-callback:0:sha256:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ScriptDiscoveryTraversesBothConditionalEdgesAndTerminatesBackwardLoops()
    {
        EnemyScenarioDiscoveryResult branched = DiscoverScript([2, 0, 0, 0, 0, 0, 6, 0, 3, 17, 6, 35, 3, 0, 7, 18, 6, 0]);
        Assert.Empty(branched.Issues);
        Assert.Equal(6, branched.ScriptedActions.Length);
        Assert.Contains(branched.ScriptedActions, action => action.Offset == 10 && action.Command == 2);
        Assert.Contains(branched.ScriptedActions, action => action.Offset == 16 && action.Command == 8);

        EnemyScenarioDiscoveryResult loop = DiscoverScript([3, 17, 6, 35, 250, 255]);
        Assert.Empty(loop.Issues);
        Assert.Equal(3, loop.ScriptedActions.Length);
    }

    [Fact]
    public void ScriptDiscoveryPreservesAbilityBandSelectionsAndSkipsZeroPreparedActions()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 3, 18), 48).ToArray();
        abilities[0] = new(2, 4, 17);
        abilities[16] = new(8, 5, 18);
        abilities[32] = new(8, 6, 0);
        EnemyScenarioDiscoveryResult result = DiscoverScript([11, 0, 253, 0, 12, 0, 0], abilities);

        Assert.Empty(result.Issues);
        Assert.Equal(4, result.ScriptedActions.Length);
        Assert.DoesNotContain(result.ScriptedActions, action => action.LevelBand == 2);
        Assert.Contains(result.ScriptedActions, action => action.LevelBand == 0 && action.Sequence == 4 && action.NativeEffectId == 101);
        Assert.Contains(result.ScriptedActions, action => action.LevelBand == 1 && action.Sequence == 5 && action.NativeEffectId == 102);
    }

    [Theory]
    [InlineData(new byte[] { 7 }, 0, "Opcode 7")]
    [InlineData(new byte[] { 35, 127, 0 }, 130, "outside-callback")]
    [InlineData(new byte[] { 12, 16, 0 }, 0, "ability slot 16")]
    public void ScriptDiscoveryReportsExactUnresolvedBoundary(byte[] code, int offset, string reason)
    {
        EnemyScenarioDiscoveryResult result = DiscoverScript(code);

        Assert.Equal(3, result.Issues.Length);
        Assert.Equal(3, result.ScriptedActions.Length);
        Assert.All(result.ScriptedActions, action =>
        {
            Assert.Equal(offset, action.Offset);
            Assert.Null(action.Command);
            Assert.Null(action.NativeEffectId);
            Assert.Contains(action.Blockers, blocker => blocker.Contains(reason, StringComparison.Ordinal));
            Assert.Contains(action.MappingEvidence, evidence => evidence.Contains("ai-callback:0:sha256:", StringComparison.Ordinal));
        });
    }

    private static EnemyScenarioDiscoveryResult DiscoverScript(byte[] code, NativeBattleAiAbility[]? abilities = null)
    {
        abilities ??= Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        EnemyScenarioModel model = new(11, "Synthetic enemy", new([code, [], [], [], []], abilities, 0, []));
        return ScenarioEnemyDiscovery.Discover([], [model], [Action(2, 17, 101), Action(8, 18, 102)]);
    }

    [Fact]
    public void ScriptTargetSelectorRemainsAttachedToItsActualDispatch()
    {
        EnemyScenarioDiscoveryResult result = DiscoverScript([4, 201, 7, 18, 6, 4, 200, 6, 0]);
        Assert.Equal(6, result.ScriptedActions.Length);
        Assert.Equal(3, result.ScriptedActions.Count(action => action.TargetSelector == 201 && action.Offset == 4));
        Assert.Equal(3, result.ScriptedActions.Count(action => action.TargetSelector == 200 && action.Offset == 7));
        Assert.All(result.ScriptedActions, action => Assert.Contains(action.MappingEvidence, evidence => evidence.Contains("target-selector:", StringComparison.Ordinal)));
    }

    [Fact]
    public void DiscoveryRetainsAllBandsAndDisabledHiddenOwnerContexts()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        abilities[0] = new(8, 3, 17);
        abilities[16] = new(8, 4, 18);
        abilities[47] = new(8, 5, 17);
        EnemyScenarioModel model = new(11, "Synthetic enemy", new([[], [], [], [], []], abilities, 0, []));
        BattleEnemyPlacement active = new(0, 11, Vector3.Zero, 25, false, false);
        BattleEnemyPlacement reinforcement = new(7, 11, Vector3.Zero, 40, true, true) { Enabled = false, InitiallySuspended = true };
        BattleEncounter formation = new(42, 9, 0, 0, 0, [active]) { Slots = [active, reinforcement] };
        EnemyScenarioDiscoveryResult result = ScenarioEnemyDiscovery.Discover([formation], [model], [Action(8, 17, 101), Action(8, 18, 102)]);

        Assert.Empty(result.Issues);
        Assert.Equal(48, result.Owners.Length);
        EnemyScenarioActionOwner high = result.Owners.Single(owner => owner.LevelBand == 2 && owner.AbilitySlot == 15);
        Assert.Equal((ushort)101, high.NativeAction!.MagicId);
        Assert.Equal((byte)5, high.Sequence);
        Assert.Equal(2, high.Formations.Length);
        EnemyScenarioFormation reserve = high.Formations.Single(context => context.SceneSlot == 7);
        Assert.False(reserve.Enabled);
        Assert.True(reserve.Hidden);
        Assert.True(reserve.Suspended);
        Assert.True(reserve.Untargetable);
        Assert.Equal(40, reserve.SceneLevel);
        Assert.Contains(high.MappingEvidence, evidence => evidence.Contains("section:3:row:17:effect:101", StringComparison.Ordinal));
        Assert.Contains(high.Blockers, blocker => blocker.Contains("AI reachability", StringComparison.Ordinal));
        Assert.Equal((ushort)102, result.Owners[16].NativeAction!.MagicId);
    }

    [Fact]
    public void DiscoveryDoesNotBorrowKernelRowsFromAnotherCommandOrInventSceneOwners()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 3, 17), 48).ToArray();
        EnemyScenarioModel model = new(11, "Synthetic enemy", new([[], [], [], [], []], abilities, 0, []));
        EnemyScenarioDiscoveryResult result = ScenarioEnemyDiscovery.Discover([], [model], [Action(2, 17, 101)]);

        Assert.All(result.Owners, owner =>
        {
            Assert.Null(owner.NativeAction);
            Assert.Empty(owner.Formations);
            Assert.Contains(owner.Blockers, blocker => blocker.Contains("No installed kernel mapping", StringComparison.Ordinal));
            Assert.Contains(owner.Blockers, blocker => blocker.Contains("No scene.out slot", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void DiscoveryReportsMissingModelBanksAndAmbiguousKernelVariants()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 3, 17), 48).ToArray();
        EnemyScenarioModel model = new(11, "Synthetic enemy", new([[], [], [], [], []], abilities, 0, []));
        BattleEncounter formation = new(42, 9, 0, 0, 0, [new(0, 12, Vector3.Zero, 25, false, false)]);
        EnemyScenarioDiscoveryResult result = ScenarioEnemyDiscovery.Discover([formation], [model], [Action(8, 17, 101), Action(8, 17, 102)]);

        Assert.Contains(result.Issues, issue => issue.Identity == "enemy-model:12");
        Assert.All(result.Owners, owner =>
        {
            Assert.Null(owner.NativeAction);
            Assert.Contains(owner.Blockers, blocker => blocker.Contains("variant selector", StringComparison.Ordinal));
        });
    }

    private static BattleNativeAction Action(byte command, ushort id, ushort effect) => new(command, id, 3, id)
    {
        MagicId = effect,
        AttackType = 0,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 0,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 0,
        Statuses0 = 0,
        Statuses1 = 0,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = ImmutableArray.Create((byte)effect),
        SupplementalRecord = []
    };
}
