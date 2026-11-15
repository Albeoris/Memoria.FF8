using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SharedAutoPlayBattlePolicyTests
{
    [Fact]
    public void OrdinaryAttackPreservesReadyActorAndCombatantOrder()
    {
        AutoPlayBattleActor[] actors =
        [
            new(false, true, true, false, true),
            new(true, false, true, false, true),
            new(true, true, false, false, true),
            new(true, true, true, true, true),
            new(true, true, true, false, false),
            new(true, true, true, false, true),
            new(true, true, true, false, true)
        ];
        AutoPlayBattleTarget[] targets =
        [
            new(false, true, true, false),
            new(true, false, true, false),
            new(true, true, false, false),
            new(true, true, true, true),
            new(true, true, true, false),
            new(true, true, true, false)
        ];

        Assert.Equal(new(AutoPlayBattleDecisionKind.Attack, 5, 4), Choose(actors, targets));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NativeEntryAndPauseGateAttack(bool entryReady, bool paused)
    {
        Assert.Equal(AutoPlayBattleDecisionKind.Tick, AutoPlayBattlePolicy.Choose(false, true, "Active", entryReady, paused, [new(true, true, true, false, true)], [new(true, true, true, false)]).Kind);
    }

    [Fact]
    public void ReturnPrecedesTerminalFailureAndMissingTargetWaits()
    {
        Assert.Equal(AutoPlayBattleDecisionKind.Return, AutoPlayBattlePolicy.Choose(true, false, "Victory", false, true, [], []).Kind);
        Assert.Equal("Automatic combat stopped at its actual Defeat outcome.", Assert.Throws<NotSupportedException>(() => AutoPlayBattlePolicy.Choose(false, false, "Defeat", true, false, [], [])).Message);
        Assert.Equal(AutoPlayBattleDecisionKind.Tick, Choose([new(true, true, true, false, true)], []).Kind);
    }

    [Fact]
    public void Ff7UsesNativeAttackArgumentAndValidTargetsInActorOrder()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 17, 0xC0, true, true) { CandidateTargets = 0xF0 };
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4), Actor(7), Actor(6)]);

        Assert.NotNull(selected);
        Assert.Same(attack, selected.Value.Command);
        Assert.Equal(7, selected.Value.TargetSlot);
        Assert.Equal((ushort)0x80, Ff7Scene.AutoPlayBattleTargetMask(selected.Value.Command, selected.Value.TargetSlot));
    }

    [Fact]
    public void Ff7SkipsDeadAndUnsupportedTargetsWithoutSelectingFriendlyActors()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0xF1, true, true);
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(0), Actor(4, 0), Actor(5), Actor(6), Actor(7)], (_, slot) => slot != 5);

        Assert.Equal(6, selected!.Value.TargetSlot);
    }

    [Fact]
    public void Ff7PrefersAttackAndUsesAvailableLimitWhenNativeMenuReplacesAttack()
    {
        Ff7BattleCommandOption limit = new(Ff7BattleCommand.Limit, 0x87, 0x30, false, true);
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x10, true, true);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([limit, attack], [Actor(4), Actor(5)])!.Value.Command);
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([limit], [Actor(4), Actor(5)])!.Value;
        Assert.Same(limit, selected.Command);
        Assert.Equal((ushort)0x30, Ff7Scene.AutoPlayBattleTargetMask(selected.Command, selected.TargetSlot));
        Assert.Throws<NotSupportedException>(() => Ff7Scene.PlanAutoPlayBattleCommand([limit with { UnsupportedReason = "Unavailable native contract" }], [Actor(4)]));
    }

    [Fact]
    public void Ff7WaitsWhenNoLivingSupportedNativeTargetExists()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x10, true, true);
        Assert.Null(Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4, 0)]));
        Assert.Null(Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4)], (_, _) => false));
    }

    [Fact]
    public void Ff7ValidatesEveryTargetOfAnAutomaticGroupCommand()
    {
        Ff7BattleCommandOption limit = new(Ff7BattleCommand.Limit, 0x87, 0x30, false, true);
        Assert.Throws<NotSupportedException>(() => Ff7Scene.PlanAutoPlayBattleCommand([limit], [Actor(4), Actor(5)], (_, slot) => slot == 4));
    }

    private static AutoPlayBattleDecision Choose(AutoPlayBattleActor[] actors, AutoPlayBattleTarget[] targets)
    {
        return AutoPlayBattlePolicy.Choose(false, true, "Active", true, false, actors, targets);
    }

    private static Ff7BattleActorState Actor(int slot, uint hp = 100)
    {
        return new(slot, new(slot, 0, 8, 0, 0, hp, 100), 0, []);
    }
}
