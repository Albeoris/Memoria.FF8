using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleHostTests
{
    [Theory]
    [InlineData(87, false)]
    [InlineData(0, true)]
    public void ExpandedRandomBattleUsesInstalledContactsAndChargesEachCommandOnce(ushort formation, bool mass)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets assets = new(directory);
        GameSession initialSession = GameSession.CreateColdProcess();
        initialSession.StartNewGame();
        using (GameWorld bootstrap = new(initialSession, directory))
        {
            bootstrap.EnsureField();
            initialSession.SetEventState(bootstrap.Events.Capture());
        }
        NativeCharacterVitals initial = initialSession.State.CharacterVitals!.Rest();
        NativeCharacterVital[] records = initial.Records.ToArray();
        BattleNativeAction fire;
        BattleNativeAction quake;
        BattleNativeAction potion;
        using (BattleAssets battleAssets = new(directory))
        {
            fire = Assert.Single(battleAssets.NativeActions, action => action.Command == 2 && action.Name == "Fire");
            quake = Assert.Single(battleAssets.NativeActions, action => action.Command == 2 && action.Name == "Quake");
            potion = Assert.Single(battleAssets.NativeActions, action => action.Command == 4 && action.Name == "Potion");
        }
        for (int row = 0; row < 4; row++)
        {
            records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1) };
        }
        NativeMagicStock[] stock = records[3].MagicInventory.ToArray();
        stock[0] = new((byte)fire.ActionId, 5);
        stock[1] = new((byte)quake.ActionId, 5);
        byte magicCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2));
        byte itemCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4));
        records[3] = records[3] with { MagicInventory = stock, HitPoints = new NativeHpWord(100, 100), Junction = records[3].Junction with { Commands = (uint)(magicCommand | itemCommand << 8) } };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeItemStock[] items = economy.Items.ToArray();
        int potionSlot = Array.FindIndex(items, item => item.ItemId == potion.ActionId);
        items[potionSlot < 0 ? Array.FindIndex(items, item => item.ItemId == 0) : potionSlot] = new((byte)potion.ActionId, 3);
        GameSession session = new(initialSession.State with
        {
            FieldId = 133,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initialSession.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, expandedRandomBattles: true);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        session.SetCharacterVitals(initial with { Records = records, CombatParty = [0, 1, 2] });
        session.SetEconomy(economy with { Items = items });
        if (!mass)
        {
            Assert.Contains(formation, world.Field!.RandomEncounters!.Formations);
        }
        // Formation 0 in the mass case supplies damageable ground targets; its occurrence is injected into the ordinary field host.
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.UsesExpandedRoster);
        BattleDefinition original = world.CreateEncounterBattle(88);
        Assert.False(original.UsesExpandedRoster);
        Assert.NotNull(original.NativeGraphics);
        Assert.Contains(original.Combatants, actor => actor.NativeAi is not null);
        Assert.Equal(10, battle.Combatants.Count);
        Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
        BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition(), true);
        string casterId = "party-3";
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        Assert.Equal(4, hud.Party.Count);
        Assert.Equal(10, BattleSession.Restore(battle.Capture()).Combatants.Count);
        Assert.Equal(5, MagicCount(fire.ActionId));
        Assert.Equal(5, MagicCount(quake.ActionId));
        if (mass)
        {
            int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
            Run("enemy-5", BattleAbility.Magic, quake.ActionId, enemies.Select(enemy => enemy.Id).ToArray());
            Assert.All(enemies.Select((enemy, index) => (enemy, index)), pair => Assert.InRange(pair.enemy.CurrentHp, 0, before[pair.index] - 1));
            Assert.Equal(4, MagicCount(quake.ActionId));
            Assert.Equal(5, MagicCount(fire.ActionId));
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal(6, battle.EnemyDefeats.Count);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
            Assert.Equal(4, session.State.CharacterVitals.Records[3].MagicInventory.Single(spell => spell.SpellId == quake.ActionId).Count);
        }
        else
        {
            int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
            Run("enemy-5", BattleAbility.Magic, fire.ActionId, ["enemy-5"]);
            Assert.InRange(enemies[5].CurrentHp, 0, before[5] - 1);
            Assert.Equal(before.Take(5), enemies.Take(5).Select(enemy => enemy.CurrentHp));
            Assert.Equal(4, MagicCount(fire.ActionId));
            Assert.Equal(5, MagicCount(quake.ActionId));
            int beforePotion = caster.CurrentHp;
            Assert.Equal(3, battle.Items.Single(item => item.Id == potion.ActionId).Count);
            Run(casterId, BattleAbility.Item, potion.ActionId, [casterId]);
            Assert.True(caster.CurrentHp > beforePotion);
            Assert.Equal(2, battle.Items.Single(item => item.Id == potion.ActionId).Count);
        }

        int MagicCount(int id) => caster.MagicStocks.Single(spell => spell.Id == id).Count;

        void Run(string target, BattleAbility ability, int action, string[] recipients)
        {
            for (int i = 0; i < 32 && !caster.IsReady; i++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            Assert.True(caster.IsReady);
            long first = battle.Frame;
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = casterId, TargetId = target, Ability = ability, ActionId = action });
            bool rendered = false;
            for (int i = 0; i < 2048 && caster.IsBusy; i++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (!rendered && battle.ExpandedEffect?.Overlay is { LastCommands.IsEmpty: false })
                {
                    byte[] pixels = presentation.Render(battle, 320, 240).ToArray();
                    Assert.Contains(pixels.Where((_, index) => index % 4 != 3), pixel => pixel != 0);
                    rendered = true;
                }
            }
            Assert.False(caster.IsBusy);
            if (action == fire.ActionId && ability == BattleAbility.Magic)
            {
                Assert.True(rendered);
            }
            Assert.Equal(recipients.Order(), battle.Events.Where(entry => entry.Frame >= first && entry.Kind == "EffectContact").Select(entry => entry.TargetId).Order());
        }
    }
}
