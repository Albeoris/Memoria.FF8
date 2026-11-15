using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(112)]
    [InlineData(212)]
    [InlineData(215)]
    [InlineData(216)]
    [InlineData(274)]
    public void Installed_expanded_Duel_runs_real_button_sequences_and_roster_targets(ushort effect)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedDuel(directory, PrepareField(directory), assets, effect);
        Assert.True(status == "logical", detail);
    }

    private static (string Status, string Detail) RunExpandedDuel(string directory, GameState field, BattleAssets assets, ushort effect)
    {
        const int invocation = 11 << 16;
        const byte zell = 1;
        BattleNativeAction opening = Assert.Single(assets.NativeActions, move => move is { Command: 241, ActionId: 0 });
        BattleNativeAction selected = effect == opening.MagicId ? opening : Assert.Single(assets.NativeActions, move => move is { Command: 239, ActionId: >= 6 } && move.MagicId == effect);
        using GameWorld world = StartBattle(directory, field, 71, limit: opening with { CharacterId = zell }, prepareHigherBand: true);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == zell);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        Assert.True(hud.NativeLimitAvailable);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        BattleCombatant target = hud.Targets.Last();
        hud.SelectTarget(target.Id, false);
        Assert.Equal(10, target.Slot!.Value.Position);
        int first = battle.Events.Count;
        hud.Attack();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, action => action.ActorId == caster.Id);
        Assert.Equal(invocation, queued.ActionId);
        Assert.Equal(target.Id, queued.TargetId);
        Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(queued.TargetRequest.Selection);

        Until(() => battle.GetNativeDuel(caster.Id)?.Phase == BattleNativeDuelPhase.Ready);
        BattleEvent actualOpening = Assert.Single(battle.Events.Skip(first), entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id);
        Assert.Equal(target.Id, actualOpening.TargetId);
        Assert.Equal(BattleAbility.LimitBreak, actualOpening.Ability);
        BattleNativeDuelState ready = Assert.IsType<BattleNativeDuelState>(battle.GetNativeDuel(caster.Id));
        string[] expected = [target.Id];
        if (selected.Command == 239)
        {
            BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(zell);
            foreach (BattleNativeDuelChoice choice in FindDuelRoute(assets, rules, ready.MoveSetId, checked((byte)selected.ActionId)))
            {
                Assert.Contains(battle.GetNativeDuelChoices(caster.Id), option => option.MoveSetId == choice.MoveSetId);
                foreach (ushort button in choice.Buttons)
                {
                    battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(166667), button);
                    battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(166667), 0);
                }
                BattleAttackCommand move = Assert.Single(battle.QueuedActions, action => action.ActorId == caster.Id);
                Assert.Equal(choice.ActionId, move.NativeAction!.ActionId);
                Assert.Equal(target.Id, move.TargetId);
                Assert.IsNotType<NativeBattleTargetRequestAdapter.DeferredOperands>(move.TargetRequest.Selection);
                Until(() => battle.GetNativeDuel(caster.Id)?.Phase is BattleNativeDuelPhase.Ready or null);
            }
            expected = (selected.TargetFlags!.Value & 0x10) == 0 ? battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.Id).Order().ToArray() : [target.Id];
            Assert.Equal(expected, battle.Events.Skip(first).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Message == selected.Name).Select(entry => entry.TargetId).Order());
        }
        else
        {
            battle.AdvanceNativeDuelUi(ready.RemainingTimer);
        }
        Assert.Null(battle.GetNativeDuel(caster.Id));
        Assert.False(caster.IsBusy);
        return ("logical", $"Duel used original command 11 and its installed effect {effect}, completing with {expected.Length} logical roster recipient(s).");

        void Until(Func<bool> done)
        {
            for (int frame = 0; frame < 300 && !done(); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(done());
        }
    }

    private static BattleNativeDuelChoice[] FindDuelRoute(BattleAssets assets, BattleNativeLimitDefinition rules, byte initial, byte finisher)
    {
        Queue<(byte MoveSet, BattleNativeDuelChoice[] Steps)> pending = new();
        pending.Enqueue((initial, []));
        HashSet<byte> visited = [];
        while (pending.TryDequeue(out var branch))
        {
            if (!visited.Add(branch.MoveSet))
            {
                continue;
            }
            foreach (BattleNativeDuelChoice choice in NativeBattleDuelRules.Choices(rules, branch.MoveSet, 0, assets.NativeActions))
            {
                if (choice.Finisher && choice.ActionId == finisher)
                {
                    return [.. branch.Steps, choice];
                }
                if (!choice.Finisher)
                {
                    pending.Enqueue((choice.MoveSetId, [.. branch.Steps, choice]));
                }
            }
        }
        throw new InvalidDataException($"The installed Duel move table does not reach finisher {finisher} from {initial}.");
    }
}
