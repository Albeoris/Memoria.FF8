using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    internal static (string Status, string Detail) RunExpandedAngelWing(string directory, GameState field, GameAssets game, BattleAssets assets, BattleNativeAction angelWing, byte stockedSpell = 17)
    {
        const int rinoa = 4;
        const int rapture = 53;
        const int stockCount = 2;
        bool castsSpell = stockedSpell != 0;
        NativeSavedBattleFlags flags = field.SavedBattleFlags ?? game.ReadNewGameSavedBattleFlags();
        field = field with { SavedBattleFlags = flags with { Value = (byte)(flags.Value | 0x30) } };
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, 0, limit: angelWing, prepareParty: records =>
        {
            NativeMagicStock[] stock = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
            if (stockedSpell != 0)
            {
                stock[31] = new(stockedSpell, stockCount);
            }
            records[rinoa] = records[rinoa] with { MagicInventory = stock };
        });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.ModelId == rinoa);
        for (int turn = 0; turn < 24 && !battle.CanChoosePlayerCommand(caster.Id); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(battle.CanChoosePlayerCommand(caster.Id));
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        if (stockedSpell == rapture)
        {
            Assert.False(battle.SupportsPlayerLimitSelection(caster.Id));
            Assert.False(hud.NativeLimitAvailable);
            return ("blocked", "The eligible stocked spell requires a contact contract outside the expanded battle host.");
        }
        Assert.True(hud.NativeLimitAvailable);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.Commands[0]);
        hud.Choose(0);
        Assert.Equal(angelWing.ActionId, Assert.Single(hud.ActionChoices).NativeAction!.ActionId);
        hud.Choose(0);
        Assert.Equal(caster.Id, Assert.Single(hud.Targets).Id);
        int before = battle.Events.Count;
        hud.Attack();
        Assert.Contains(battle.QueuedActions, command => command.ActorId == caster.Id && command.TargetId == caster.Id && command.NativeTargetMask == 0);

        for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
        {
            battle.Tick();
        }
        Assert.False(caster.IsBusy);
        Assert.NotEqual(0u, battle.Capture().Combatants.Single(actor => actor.Id == caster.Id).NativeStatus!.Battle & 0x2000000);
        Assert.Single(battle.Events.Skip(before), entry => entry is { Kind: "EffectContact", Ability: BattleAbility.LimitBreak } && entry.TargetId == caster.Id);

        caster.Atb = BattleSession.AtbMaximum;
        string[] enemyIds = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable).Select(actor => actor.Id).ToArray();
        Assert.Equal(11, enemyIds.Length);
        Assert.False(battle.CanChoosePlayerCommand(caster.Id));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAttack(caster.Id, enemyIds[^1]));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction(caster.Id, enemyIds[^1], BattleAbility.Magic, 17));
        before = battle.Events.Count;
        battle.Tick();
        BattleSnapshot captured = battle.Capture();
        BattleAbility expected = castsSpell ? BattleAbility.EnemyAbility : BattleAbility.Attack;
        BattleAttackCommand? queued = captured.QueuedActions.SingleOrDefault(command => command.ActorId == caster.Id);
        BattleActionPresentation? current = captured.ActiveAction is { } active && active.ActorId == caster.Id ? active : null;
        Assert.Equal(expected, queued?.Ability ?? current?.Ability);
        Assert.Equal(castsSpell, queued?.FreeNativeMagic ?? current?.FreeNativeMagic);
        string snapshot = JsonSerializer.Serialize(captured);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        GameWorld.AttachExpandedEffects(restored, game, assets);
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
        for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.False(caster.IsBusy);
        if (castsSpell)
        {
            Assert.Equal(stockCount, Assert.Single(caster.MagicStocks, stock => stock.Id == stockedSpell).Count);
            BattleEvent[] contacts = battle.Events.Skip(before).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id).ToArray();
            Assert.Equal(enemyIds.Order(), contacts.Select(contact => contact.TargetId!).Order());
            Assert.Contains(contacts, contact => contact.Amount is > 0 and < 9999 && contact.Amount % 5 == 0);
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        return ("logical", castsSpell
            ? "Rinoa applied Angel Wing to herself and automatically cast stocked Quake against 11 logical recipients without spending stock, with JSON continuation."
            : "Rinoa applied Angel Wing to herself and automatically used Attack with no eligible stocked spell, with JSON continuation.");
    }
}

public sealed class ExpandedBattleAngelWingTests
{
    [Theory]
    [InlineData(17, "logical")]
    [InlineData(0, "logical")]
    [InlineData(53, "blocked")]
    public void Installed_Angel_Wing_uses_its_stock_target_and_free_cast_contract(byte stockedSpell, string expected)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction angelWing = Assert.Single(assets.NativeActions, action => action is { Command: 19, KernelSection: 25, Statuses1: 0x2000000 });
        (string status, string detail) = ExpandedBattleEffectMatrixTests.RunExpandedAngelWing(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), game, assets, angelWing, stockedSpell);
        Assert.True(status == expected, detail);
    }
}
