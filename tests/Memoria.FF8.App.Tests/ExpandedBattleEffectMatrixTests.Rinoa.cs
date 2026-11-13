using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    internal static (string Status, string Detail) RunExpandedCombine(string directory, GameState field, BattleNativeAction action)
    {
        using GameAssets game = new(directory);
        NativeSavedBattleFlags flags = field.SavedBattleFlags ?? game.ReadNewGameSavedBattleFlags();
        Assert.Equal(0x30, flags.EffectiveKnownMask & 0x30);
        Assert.Equal(0, flags.Value & 0x10);
        NativeLimitProgress progress = Assert.IsType<NativeLimitProgress>(field.LimitProgress);
        byte learned = (byte)((progress.AngeloFlags & 0x0F) | 0x10 | (0x10 << action.ActionId));
        field = field with { LimitProgress = progress with { AngeloFlags = learned }, SavedBattleFlags = flags };
        foreach (byte seed in new byte[] { 0, 5, 9, 11 })
        {
            using GameWorld world = StartBattle(directory, field, 89, limit: action, nativeSeed: seed);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == 4);
            for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            Assert.True(caster.IsReady);
            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
            hud.SelectParty(caster.Id);
            if (!hud.NativeLimitAvailable || battle.GetNativeLimitLevel(caster.Id) <= action.ActionId)
            {
                continue;
            }

            hud.SetNativeCommandButtons(0x2000);
            Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
            hud.Choose(0);
            BattleActionDefinition selected = Assert.Single(hud.ActionChoices);
            Assert.Equal(action.ActionId, selected.NativeAction!.ActionId);
            hud.Choose(0);
            Assert.True(hud.SelectingTarget);
            BattleSide side = (action.TargetFlags!.Value & 0x40) != 0 ? BattleSide.Enemies : BattleSide.Allies;
            string[] eligible = battle.Combatants.Where(target => target.Side == side && target.IsAlive && !target.Untargetable).Select(target => target.Id).ToArray();
            int start = battle.Events.Count;
            hud.Attack();
            BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
            Assert.Equal(action.ActionId, queued.NativeAction!.ActionId);
            Assert.Contains(queued.TargetId, eligible);
            Assert.Equal(0, queued.NativeTargetMask);

            for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.False(caster.IsBusy);
            BattleEvent[] events = battle.Events.Skip(start).Where(entry => entry.ActorId == caster.Id).ToArray();
            Assert.Single(events, entry => entry.Kind == "ActionComplete");
            string?[] contacts = events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId).ToArray();
            string[] expected = (action.TargetFlags.Value & 0x30) == 0x10
                ? [queued.TargetId]
                : Enumerable.Range(0, action.HitCount).SelectMany(_ => eligible).ToArray();
            Assert.InRange(contacts.Length, 1, expected.Length);
            Assert.Equal(expected.Take(contacts.Length), contacts);
            if (contacts.Length < expected.Length)
            {
                Assert.False(battle.Combatants.Single(target => target.Id == expected[contacts.Length]).IsAlive);
            }
            return ("logical", $"Rinoa used learned {action.Name} through Combine in formation 89 with {contacts.Length} roster contacts.");
        }
        return ("blocked", $"{action.Name} did not reach its required native crisis level in the bounded original entries.");
    }
}

public sealed class ExpandedBattleCombineTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InstalledCombineRunsEachLearnedFinisherThroughTheExpandedHud(ushort row)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 19 && action.KernelSection == 25 && action.ActionId == row);
        (string status, string detail) = ExpandedBattleEffectMatrixTests.RunExpandedCombine(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), action);
        Assert.True(status == "logical", detail);
    }
}
