using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using System.Text.Json;

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
    [InlineData(87, false, false, 11)]
    [InlineData(87, false, false, 11, false, 11)]
    [InlineData(0, true, false, 11)]
    [InlineData(89, false, true, 11)]
    [InlineData(0, true, false, 11, true)]
    [InlineData(0, true, false, 11, true, 11)]
    public void ExpandedRandomBattleUsesInstalledContactsAndChargesEachCommandOnce(ushort formation, bool mass, bool exerciseAi, int allyCount, bool coldRestore = false, int largeEnemyCount = 8)
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
        if (allyCount == 11)
        {
            initial = initial.WithGuestBattleRecords();
        }
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
        for (int row = 0; row < Math.Min(allyCount, NativeCharacterVitals.NativeRecordCount); row++)
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
        byte drawCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6));
        records[casterRow] = records[casterRow] with { MagicInventory = stock, HitPoints = new NativeHpWord(100, 100), Junction = records[casterRow].Junction with { Commands = (uint)(magicCommand | itemCommand << 8 | drawCommand << 16) } };
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
        using GameWorld world = new(session, directory, expandedBattleEnemyCount: largeEnemyCount);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        session.SetCharacterVitals(initial with { Records = records, CombatParty = [0, 1, 2] });
        session.SetEconomy(economy with { Items = items });
        if (allyCount == 11)
        {
            session.SetCharacterVitals(initialSession.State.CharacterVitals! with { CombatParty = [0, 1, 2] });
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allyCount });
            Assert.Equal(11, session.State.CharacterVitals!.Records.Length);
            session.SetCharacterVitals(initial with { Records = records, CombatParty = [0, 1, 2] });
        }
        bool guardianFormsEnabled = largeEnemyCount == 11;
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allyCount, GuardianFormsEnabled = guardianFormsEnabled });
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
        Assert.False(battle.ReadDefinition().GuardianFormsEnabled);
        BattleDefinition original = world.CreateEncounterBattle(88);
        Assert.False(original.UsesExpandedRoster);
        Assert.NotNull(original.NativeGraphics);
        Assert.Contains(original.Combatants, actor => actor.NativeAi is not null);
        int enemyCount = allyCount == 11 ? largeEnemyCount : ExpandedBattleFormation.DefaultEnemyCount(allyCount);
        string lastEnemy = $"enemy-{enemyCount - 1}";
        Assert.Equal(allyCount + enemyCount, battle.Combatants.Count);
        Assert.True(battle.EscapeDisabled);
        Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
        BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition(), true);
        string casterId = $"party-{casterRow}";
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        Assert.Equal(allyCount, hud.Party.Count);
        if (allyCount == 11)
        {
            Assert.Equal(Enumerable.Range(0, 11), hud.Party.Select(actor => actor.ModelId).Order());
            Assert.False(session.State.CharacterVitals!.ExtendedRosterEnabled);
        }
        Assert.Equal(allyCount + enemyCount, BattleSession.Restore(battle.Capture()).Combatants.Count);
        Assert.Equal(5, MagicCount(fire.ActionId));
        Assert.Equal(5, MagicCount(quake.ActionId));
        if (allyCount >= 8)
        {
            _ = presentation.Render(battle, 640, 448);
            Assert.All(battle.Combatants, actor =>
            {
                UiRect bounds = Assert.IsType<UiRect>(presentation.GetActorBounds(actor.Id, new(0, 0, 640, 448)));
                Assert.InRange(bounds.X + bounds.Width / 2, 0, 640);
                Assert.InRange(bounds.Y + bounds.Height / 2, 0, 448);
            });
        }
        if (allyCount == 11 && exerciseAi)
        {
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3 });
            Assert.Equal(3, session.State.PendingGameplayPreferences!.BattleAllyCount);
            Assert.Equal(11, session.State.GameplayPreferences.BattleAllyCount);
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
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
            if (coldRestore)
            {
                CheckColdContinuations();
            }
            Run(lastEnemy, BattleAbility.Magic, quake.ActionId, enemies.Select(enemy => enemy.Id).ToArray());
            Assert.All(enemies.Select((enemy, index) => (enemy, index)), pair => Assert.InRange(pair.enemy.CurrentHp, 0, before[pair.index] - 1));
            Assert.Equal(4, MagicCount(quake.ActionId));
            Assert.Equal(5, MagicCount(fire.ActionId));
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal(enemyCount, battle.EnemyDefeats.Count);
            Assert.Equal(Enumerable.Range(0, enemyCount), battle.EnemyDefeats.Select(defeat => defeat.Slot).Order());
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
            Assert.Equal(guardianFormsEnabled, session.State.GameplayPreferences.GuardianFormsEnabled);
            Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
            Assert.Equal(4, session.State.CharacterVitals.Records[casterRow].MagicInventory.Single(spell => spell.SpellId == quake.ActionId).Count);
            if (allyCount == 11)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(session.PrepareSnapshot());
                GameState restored = System.Text.Json.JsonSerializer.Deserialize<GameState>(json)!;
                Assert.Equal(11, restored.GameplayPreferences.BattleAllyCount);
                NativeCharacterVitals compact = GameplaySaveCharacters.Capture(restored.CharacterVitals!).Restore(assets.ReadNewGameCharacterVitals());
                Assert.Equal(11, compact.Records.Length);
                Assert.Equal(4, compact.Records[casterRow].MagicInventory.Single(spell => spell.SpellId == quake.ActionId).Count);
                GameState reduced = restored with { GameplayPreferences = restored.GameplayPreferences with { BattleAllyCount = 3 } };
                Assert.True(PlayerSaveStore.RequiresMemoriaFormat(reduced));
                Assert.Throws<NotSupportedException>(() => PlayerSaveStore.ValidateOriginalBattleRoster(reduced));
            }
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
            if (allyCount == 11)
            {
                BattleCombatant drawSource = enemies[^1];
                NativeBattleDrawSlot draw = drawSource.NativeDrawSlots.First(slot => slot.Id is > 0 and < 64);
                for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                    world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                }
                Assert.True(caster.IsReady);
                hud.SelectParty(caster.Id);
                Assert.Contains(BattleAbility.Draw, hud.Commands);
                Assert.True(hud.DrawCastAvailable);
                world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = caster.Id, TargetId = drawSource.Id, Ability = BattleAbility.Draw, ActionId = draw.Id });
                for (int frame = 0; frame < 512 && caster.IsBusy; frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.False(caster.IsBusy);
                Assert.Contains(battle.Events, entry => entry.Kind == "Draw" && entry.ActorId == caster.Id && entry.TargetId == drawSource.Id);
                if (enemyCount == 11)
                {
                    BattleActionDefinition spell = battle.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && battle.SupportsExpandedAction(action) && drawSource.NativeDrawSlots.Any(slot => slot.Id == action.Id));
                    BattleCombatant recipient = battle.Combatants.Last(actor => actor.Id != drawSource.Id && BattleSession.IsValidSelectedActionTarget(caster, spell, actor));
                    for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                    }
                    Assert.True(caster.IsReady);
                    int stockBefore = caster.MagicStocks.FirstOrDefault(stock => stock.Id == spell.Id)?.Count ?? 0;
                    world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = caster.Id, TargetId = drawSource.Id, Ability = BattleAbility.Draw, ActionId = spell.Id, DrawCast = true, DrawCastTargetId = recipient.Id });
                    BattleSnapshot windup = battle.Capture();
                    using var checkpoint = new TemporaryBattleCheckpoint();
                    session.Save(checkpoint.Path, compact: true);
                    GameSession resumed = new(GameSession.Load(checkpoint.Path));
                    using GameWorld resumedWorld = new(resumed, directory, expandedBattleEnemyCount: 11);
                    resumedWorld.EnsureField();
                    BattleSession restored = Assert.IsType<BattleSession>(resumedWorld.Battle);
                    Assert.Equal(recipient.Id, restored.ActiveAction?.DrawCastTargetId ?? Assert.Single(restored.QueuedActions).DrawCastTargetId);
                    bool presented = false;
                    int impacts = 0;
                    for (int frame = 0; frame < 512 && caster.IsBusy; frame++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                        presented |= battle.ExpandedEffect is not null;
                        BattleImpactCue[] cues = BattleImpactPresentation.Collect(battle).Where(cue => cue.Event.Frame >= windup.Frame && cue.Event.ActorId == caster.Id).ToArray();
                        Assert.All(cues, cue =>
                        {
                            Assert.Equal(BattleAbility.Draw, cue.Event.Ability);
                            Assert.Equal(recipient.Id, cue.Event.TargetId);
                            Assert.True(cue.Event.DeferredImpact);
                            Assert.Contains(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == recipient.Id && entry.Frame == cue.Event.Frame);
                        });
                        impacts = Math.Max(impacts, cues.Length);
                    }
                    Assert.False(caster.IsBusy);
                    Assert.True(presented);
                    Assert.Equal(1, impacts);
                    Assert.Equal(stockBefore, caster.MagicStocks.FirstOrDefault(stock => stock.Id == spell.Id)?.Count ?? 0);
                    Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == recipient.Id && entry.Frame >= windup.Frame);
                    AssertEquivalentBattle(battle, restored);
                }
            }

        }

        int MagicCount(int id) => caster.MagicStocks.Single(spell => spell.Id == id).Count;

        void CheckColdContinuations()
        {
            GameState initialBattle = session.PrepareSnapshot();
            GameSession uninterrupted = new(initialBattle);
            int differentEnemyCount = largeEnemyCount == 11 ? 8 : 11;
            using GameWorld uninterruptedWorld = new(uninterrupted, directory, expandedBattleEnemyCount: differentEnemyCount);
            uninterruptedWorld.EnsureField();
            AssertEquivalentBattle(battle, uninterruptedWorld.Battle!);
            BattleSession active = uninterruptedWorld.Battle!;
            BattleCombatant ward = active.Combatants.Single(actor => actor.Id == casterId);
            for (int i = 0; i < 32 && !ward.IsReady; i++)
            {
                uninterruptedWorld.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                uninterruptedWorld.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            Assert.True(ward.IsReady);
            CheckHud(active);
            uninterruptedWorld.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = casterId, TargetId = lastEnemy, Ability = BattleAbility.Magic, ActionId = quake.ActionId });
            BattleSnapshot windup = active.Capture();
            Assert.True(windup.ActiveAction?.ActorId == casterId || windup.QueuedActions.Any(action => action.ActorId == casterId));
            Assert.Null(active.ExpandedEffect);

            using var checkpoint = new TemporaryBattleCheckpoint();
            uninterrupted.Save(checkpoint.Path, compact: true);
            GameSession resumed = new(GameSession.Load(checkpoint.Path));
            using GameWorld resumedWorld = new(resumed, directory, expandedBattleEnemyCount: differentEnemyCount);
            resumedWorld.EnsureField();
            BattleSession restored = Assert.IsType<BattleSession>(resumedWorld.Battle);
            AssertEquivalentBattle(active, restored);
            bool observedPresentation = false;
            for (int frame = 0; frame < 2048 && ward.IsBusy; frame++)
            {
                uninterruptedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                if (!observedPresentation && active.ExpandedEffect is not null)
                {
                    Assert.Throws<NotSupportedException>(() => uninterrupted.PrepareSnapshot());
                    observedPresentation = true;
                }
            }
            Assert.True(observedPresentation);
            Assert.False(ward.IsBusy);
            Assert.Equal(BattleOutcome.Victory, active.Outcome);
            AssertEquivalentBattle(active, restored);
            Assert.Equal(uninterrupted.ExamTimer!.ClockCalls, resumed.ExamTimer!.ClockCalls);
            Assert.Equal(4, ward.MagicStocks.Single(spell => spell.Id == quake.ActionId).Count);

            resumed.Save(checkpoint.Path, compact: true);
            GameSession terminal = new(GameSession.Load(checkpoint.Path));
            using GameWorld terminalWorld = new(terminal, directory, expandedBattleEnemyCount: differentEnemyCount);
            terminalWorld.EnsureField();
            AssertEquivalentBattle(active, terminalWorld.Battle!);
            uninterruptedWorld.ApplyBattleInput(new(BattleInputKind.Return));
            terminalWorld.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(terminalWorld.Battle);
            Assert.Equal(11, terminal.State.CharacterVitals!.Records.Length);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(uninterrupted.State.CharacterVitals), JsonSerializer.SerializeToElement(terminal.State.CharacterVitals)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(uninterrupted.State.Economy), JsonSerializer.SerializeToElement(terminal.State.Economy)));
            Assert.Null(terminal.State.PendingBattleEncounter);
            GameState settled = terminal.State;
            Assert.Throws<InvalidOperationException>(() => terminalWorld.ApplyBattleInput(new(BattleInputKind.Return)));
            Assert.Same(settled, terminal.State);
        }

        void CheckHud(BattleSession active)
        {
            BattleHudController controls = new(active, (_, _) => { }, () => { }, () => { });
            controls.SelectParty(casterId);
            Assert.Equal(casterId, controls.SelectedActor!.Id);
            for (int i = 0; i < controls.Commands.Count && controls.SelectedCommand != BattleAbility.Item; i++)
            {
                controls.Navigate(MenuInput.Down);
            }
            Assert.Equal(BattleAbility.Item, controls.SelectedCommand);
            controls.Navigate(MenuInput.Confirm);
            controls.Navigate(MenuInput.Confirm);
            Assert.True(controls.SelectingTarget);
            Assert.Equal(11, controls.SelectableTargets.Count(target => target.IsPlayer));
            foreach (BattleCombatant ally in controls.Party)
            {
                controls.SelectTarget(ally.Id, false);
                Assert.Equal(ally.Id, controls.SelectedTarget!.Id);
            }
            controls.Navigate(MenuInput.Cancel);
            controls.Navigate(MenuInput.Cancel);
            foreach ((int width, int height) in new[] { (640, 448), (1280, 720), (1920, 1080) })
            {
                UiView view = controls.BuildView();
                view.Resize(width, height);
                BattleUiTests.TestCanvas canvas = new();
                view.Draw(canvas, new UiTheme());
                Assert.All(controls.Party, ally =>
                {
                    var draw = Assert.Single(canvas.TextDraws, draw => draw.Text == ally.Name);
                    Assert.InRange(draw.Bounds.Right, 0, width);
                    Assert.InRange(draw.Bounds.Bottom, 0, height);
                });
            }
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1")
            {
                using GpuTestDevice device = new();
                using GpuTriangleRenderer gpu = new(device.Device);
                BattleScenePresentation gpuPresentation = new(world.BattleResources, active.ReadDefinition(), true);
                foreach ((int width, int height) in new[] { (640, 448), (1280, 720), (1920, 1080) })
                {
                    _ = gpuPresentation.Render(active, width, height, gpu);
                    Assert.Equal(width, gpu.Target.Width);
                    Assert.Equal(height, gpu.Target.Height);
                    Assert.Contains(GpuTestDevice.Read(gpu.Target), pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                    Assert.All(active.Combatants, actor =>
                    {
                        UiRect bounds = Assert.IsType<UiRect>(gpuPresentation.GetActorBounds(actor.Id, new(0, 0, width, height)));
                        Assert.InRange(bounds.X + bounds.Width / 2, 0, width);
                        Assert.InRange(bounds.Y + bounds.Height / 2, 0, height);
                    });
                }
            }
        }

        void Run(string target, BattleAbility ability, int action, string[] recipients)
        {
            // This fixture isolates the selected command. The AI cases above exercise autonomous turns.
            if (!exerciseAi)
            {
                caster.Atb = BattleSession.AtbMaximum;
            }
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

    private static void AssertEquivalentBattle(BattleSession expected, BattleSession actual)
    {
        BattleSnapshot left = expected.Capture();
        BattleSnapshot right = actual.Capture();
        Assert.Equal(left.Frame, right.Frame);
        Assert.Equal(left.Random?.Lane, right.Random?.Lane);
        Assert.Equal(left.Random?.Indexes.ToArray(), right.Random?.Indexes.ToArray());
        Assert.Equal(left.NativeCrtRandom, right.NativeCrtRandom);
        Assert.Equal(left.NativeGlobalRandom, right.NativeGlobalRandom);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.Equal(left.Events, right.Events);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left.QueuedActions), JsonSerializer.SerializeToElement(right.QueuedActions)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left.ActiveAction), JsonSerializer.SerializeToElement(right.ActiveAction)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left.Combatants), JsonSerializer.SerializeToElement(right.Combatants)));
    }

    private sealed class TemporaryBattleCheckpoint : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"memoria-expanded-battle-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
