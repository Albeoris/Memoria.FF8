using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    internal static (string Status, string Detail) RunExpandedLimit(string directory, GameState field, BattleNativeAction limit)
    {
        using GameWorld world = StartBattle(directory, field, 0, limit: limit);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == limit.CharacterId);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer && !actor.IsAlive));

        int actionId = (limit.Command << 16) | limit.ActionId;
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        if (!hud.NativeLimitAvailable)
        {
            Assert.False(battle.SupportsPlayerLimitSelection(caster.Id));
            return ("blocked", $"{limit.Name} requires a Limit invocation outside the expanded contact contract.");
        }
        Assert.InRange(battle.GetNativeLimitLevel(caster.Id), (byte)1, (byte)4);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);
        int actionIndex = hud.ActionChoices.ToList().FindIndex(action => action.Id == actionId);
        if (actionIndex < 0)
        {
            return ("blocked", $"{limit.Name} requires a formula, status or repeat process outside the expanded contact contract.");
        }
        hud.Choose(actionIndex);
        Assert.True(hud.SelectingTarget);
        BattleCombatant target = hud.Targets.Last();
        hud.SelectTarget(target.Id, false);
        target = Assert.IsType<BattleCombatant>(hud.SelectedTarget);
        string[] expected = hud.SelectingTargetGroup ? hud.Targets.Select(actor => actor.Id).Order().ToArray() : [target.Id];
        int start = battle.Events.Count;
        hud.Attack();

        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.Equal(BattleAbility.LimitBreak, command.Ability);
        Assert.Equal(target.Id, command.TargetId);
        Assert.Equal(0, command.NativeTargetMask);
        Assert.Equal(limit.Command, command.NativeQueuedCommand);
        Assert.Equal(limit.ActionId, command.NativeAction!.ActionId);
        NativeBattleEffectPresentation? presentation = null;
        if (limit.KernelSection == 19 && limit.ActionId == 1)
        {
            BattleSnapshot queued = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
            BattleSession restored = BattleSession.Restore(queued);
            for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        else
        {
            for (int frame = 0; frame < 1000 && caster.IsBusy; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.ExpandedEffect is { } live)
                {
                    presentation = live;
                    Assert.Equal(limit.MagicId, live.Graphics.OriginalEffect!.RequestedEffect);
                }
            }
        }

        Assert.False(caster.IsBusy);
        BattleEvent[] events = battle.Events.Skip(start).Where(entry => entry.ActorId == caster.Id).ToArray();
        Assert.Single(events, entry => entry.Kind == "Windup" && entry.Ability == BattleAbility.LimitBreak);
        Assert.Single(events, entry => entry.Kind == "ActionComplete");
        BattleEvent[] contacts = events.Where(entry => entry.Kind == "EffectContact").ToArray();
        Assert.Equal(expected, contacts.Select(entry => entry.TargetId).Order());
        Assert.True(presentation is null || presentation.Complete);
        return (presentation is null ? "logical" : "native", $"{limit.Name} used its installed Limit row for {contacts.Length} recipient(s).");
    }
}

public sealed class ExpandedBattleLimitTests
{
    [Theory]
    [InlineData(19, 0, "native")]
    [InlineData(19, 1, "logical")]
    [InlineData(19, 4, "blocked")]
    [InlineData(19, 11, "blocked")]
    [InlineData(18, 0, "logical")]
    [InlineData(18, 3, "blocked")]
    public void Installed_limits_use_the_expanded_hud_and_original_contact_contract(int section, int row, string expected)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction limit = Assert.Single(assets.NativeActions, action => action.KernelSection == section && action.KernelIndex == row &&
            (section == 19 ? action.CrisisLevel == 4 : action.CharacterId == row + 6));
        (string status, _) = ExpandedBattleEffectMatrixTests.RunExpandedLimit(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), limit);
        Assert.Equal(expected, status);
    }
}
