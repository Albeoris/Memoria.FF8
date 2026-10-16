using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptStateTests
{
    [Fact]
    public void ScriptWaitCountersHoldGameplayAndResumeIndependentlyAcrossRestore()
    {
        BattleSession battle = Create([28, 1, 32, 1, 0]);
        //INIT is admitted during the core service; both16-bank callbacks run once later in that sameTick.
        Assert.Equal(new ushort[] { 29, 14 }, battle.NativeScriptState.WaitCounters);
        for (int frame = 0; frame < 20; frame++)
        {
            battle.Tick();
        }
        // Both callbacks write the same DFD byte. The shorter wait clears it
        // on its final callback even while the first physical task remains.
        Assert.Equal(200, battle.Combatants[0].Atb);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 11; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(battle.Combatants[0].Atb > 0);
        Assert.Empty(battle.NativeScriptState.WaitCounters);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ScriptPersistentReceiptsDoNotDisappearOnRestore()
    {
        BattleSession battle = Create([23, 1, 49, 2, 55, 4, 55, 4, 56, 1, 50, 61, 0]);
        NativeBattleScriptState state = battle.NativeScriptState;
        Assert.Equal(1, state.ResultFlags);
        Assert.Equal(new byte[] { 2 }, state.AcquiredGuardianForces);
        Assert.Equal(new byte[] { 4, 4 }, state.Cards);
        Assert.Equal(new[] { 127 }, state.UnlockedTutorials);
        Assert.True(state.DisableAutomaticTargetReplacement);
        BattleGuardianForceVital acquired = Assert.Single(battle.GuardianForces);
        Assert.True(acquired.Acquired);
        Assert.Equal((ushort)200, acquired.HitPoints);
        Assert.Equal(new BattleStock(1, 1), Assert.Single(battle.ScriptedRewards));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).NativeScriptState));
        state.Cards[0] = 90;
        Assert.Equal(4, battle.NativeScriptState.Cards[0]);
    }

    [Fact]
    public void ElementAndDisplayNameWritesOwnTheirAllocationState()
    {
        NativeBattleAiDefinition ai = Program([45, 2, 0xF6, 9, 37, 0, 0]) with { NameBuffers = [new byte[24]], DisplayNames = ["Changed"] };
        BattleSession battle = Create(ai);
        BattleCombatant enemy = battle.Combatants[1];
        Assert.Equal("Changed", enemy.Name);
        Assert.Equal(2550, enemy.ElementDefenses[2]);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal("Changed", restored.Combatants[1].Name);
        Assert.Equal(2550, restored.Combatants[1].ElementDefenses[2]);
    }

    [Fact]
    public void OriginalDefaultOpcodesConsumeOnlyTheirOwnByte()
    {
        BattleSession battle = Create([255, 10, 16, 20, 33, 13, 99, 25, 88, 36, 0]);
        Assert.True(battle.Combatants[1].IsReady);
    }

    private static BattleSession Create(byte[] init)
    {
        return Create(Program(init));
    }

    private static NativeBattleAiDefinition Program(byte[] init)
    {
        return new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["Changed"]);
    }

    private static BattleSession Create(NativeBattleAiDefinition ai)
    {
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), GuardianForces = [new(2, 200, 100, false)], Combatants = [new() { Id = "party-0", Name = "Party", IsPlayer = true }, new() { Id = "enemy-0", Name = "Enemy", NativeAi = ai }] }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }
}
