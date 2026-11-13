using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleHostTests
{
    [Theory]
    [InlineData(87, false, false, 4)]
    [InlineData(0, true, false, 4)]
    [InlineData(89, false, true, 4)]
    [InlineData(87, false, false, 8)]
    [InlineData(0, true, false, 8)]
    [InlineData(89, false, true, 8)]
    public void ExpandedRandomBattleUsesInstalledContactsAndChargesEachCommandOnce(ushort formation, bool mass, bool exerciseAi, int allyCount)
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
        for (int row = 0; row < allyCount; row++)
        {
            ushort hp = (ushort)Math.Max(100, (int)records[row].HitPoints.Minimum);
            records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1), HitPoints = new(hp, hp) };
        }
        int casterRow = allyCount - 1;
        NativeMagicStock[] stock = records[casterRow].MagicInventory.ToArray();
        stock[0] = new((byte)fire.ActionId, 5);
        stock[1] = new((byte)quake.ActionId, 5);
        byte magicCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2));
        byte itemCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4));
        records[casterRow] = records[casterRow] with { MagicInventory = stock, HitPoints = new NativeHpWord(100, 100), Junction = records[casterRow].Junction with { Commands = (uint)(magicCommand | itemCommand << 8) } };
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
        using GameWorld world = new(session, directory, expandedBattleAllyCount: allyCount);
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
        int enemyCount = allyCount == 8 ? 8 : 6;
        string lastEnemy = $"enemy-{enemyCount - 1}";
        Assert.Equal(allyCount + enemyCount, battle.Combatants.Count);
        Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
        BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition(), true);
        string casterId = $"party-{casterRow}";
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        Assert.Equal(allyCount, hud.Party.Count);
        Assert.Equal(allyCount + enemyCount, BattleSession.Restore(battle.Capture()).Combatants.Count);
        Assert.Equal(5, MagicCount(fire.ActionId));
        Assert.Equal(5, MagicCount(quake.ActionId));
        if (allyCount == 8)
        {
            _ = presentation.Render(battle, 640, 448);
            Assert.All(battle.Combatants, actor =>
            {
                UiRect bounds = Assert.IsType<UiRect>(presentation.GetActorBounds(actor.Id, new(0, 0, 640, 448)));
                Assert.InRange(bounds.X + bounds.Width / 2, 0, 640);
                Assert.InRange(bounds.Y + bounds.Height / 2, 0, 448);
            });
        }
        if (exerciseAi)
        {
            Assert.All(battle.ReadDefinition().Combatants.Where(actor => !actor.IsPlayer), actor => Assert.NotNull(actor.NativeAi));
            for (int turn = 0; turn < 20 && !battle.Events.Any(entry => entry.Kind == "EffectContact" && entry.ActorId == lastEnemy); turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 60));
            }
            Assert.Contains(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == lastEnemy);
            Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == lastEnemy && entry.Ability == BattleAbility.EnemyAbility);
            return;
        }
        if (mass)
        {
            int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
            Run(lastEnemy, BattleAbility.Magic, quake.ActionId, enemies.Select(enemy => enemy.Id).ToArray());
            Assert.All(enemies.Select((enemy, index) => (enemy, index)), pair => Assert.InRange(pair.enemy.CurrentHp, 0, before[pair.index] - 1));
            Assert.Equal(4, MagicCount(quake.ActionId));
            Assert.Equal(5, MagicCount(fire.ActionId));
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal(enemyCount, battle.EnemyDefeats.Count);
            BattleRewardSummary rewards = world.PreviewBattleRewards();
            Assert.Equal(allyCount, rewards.CharacterGrowth.Length);
            Assert.True(rewards.CharacterExperience.ContainsKey((byte)casterRow));
            BattleHudController results = new(battle, (_, _) => { }, () => { }, () => { }, previewRewards: () => rewards);
            results.AdvancePresentation(TimeSpan.FromSeconds(1));
            UiView view = results.BuildView();
            view.Resize(640, 448);
            BattleUiTests.TestCanvas canvas = new();
            view.Draw(canvas, new UiTheme());
            Assert.All(hud.Party, actor =>
            {
                var draw = Assert.Single(canvas.TextDraws, draw => draw.Text == actor.Name);
                Assert.InRange(draw.Bounds.X + draw.Bounds.Width, 0, 640);
                Assert.InRange(draw.Bounds.Y + draw.Bounds.Height, 0, 448);
            });
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
            Assert.Equal(4, session.State.CharacterVitals.Records[casterRow].MagicInventory.Single(spell => spell.SpellId == quake.ActionId).Count);
        }
        else
        {
            int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
            Run(lastEnemy, BattleAbility.Magic, fire.ActionId, [lastEnemy]);
            Assert.InRange(enemies[^1].CurrentHp, 0, before[^1] - 1);
            Assert.Equal(before.Take(enemyCount - 1), enemies.Take(enemyCount - 1).Select(enemy => enemy.CurrentHp));
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
            BattleEvent[] contacts = battle.Events.Where(entry => entry.Frame >= first && entry.Kind == "EffectContact" && entry.ActorId == casterId).ToArray();
            Assert.Equal(recipients.Order(), contacts.Select(entry => entry.TargetId).Order());
            Assert.Single(contacts.Select(entry => entry.Frame).Distinct());
        }
    }
}
