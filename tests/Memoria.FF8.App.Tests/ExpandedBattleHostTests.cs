using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleHostTests
{
    [Fact]
    public void Expanded_random_entry_activates_installed_clock_and_advances_the_fourth_ally_at_native_speed()
    {
        const ushort formation = 87;
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets assets = new(directory);
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory, sharedAssets: assets))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        GameSession session = new(initial.State with { FieldId = 133, FieldName = "cavern-component", EventState = null, DepartingFieldState = initial.State.EventState, FieldLoadContext = null, FieldRequestPacket = null });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        byte[] rows = [0, 1, 2, 3];
        foreach (byte row in rows)
        {
            vitals.Records[row] = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1) };
        }
        session.SetCharacterVitals(vitals.WithBattleParty(rows, rows.Length));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4, AdditionalEnemies = 4 });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));

        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.Normal, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.UsesExpandedRoster);
        Assert.Null(battle.Presentation?.Graphics);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleDefinition definition = battle.ReadDefinition();
        NativeBattleClockRules rules = world.BattleResources.ReadNativeClockRules();
        byte configuredSpeed = (session.State.InputConfiguration?.NativeBattleSpeedByte ?? assets.ReadNewGameInputConfiguration().NativeBattleSpeedByte)!.Value;
        Assert.Equal(rules, definition.NativeClockRules);
        Assert.Equal(configuredSpeed, definition.NativeBattleSpeed);
        Assert.All(battle.Combatants, actor => Assert.Equal(4000 * (configuredSpeed + 1), Assert.IsType<NativeBattleAtbState>(actor.NativeAtb).Maximum));

        BattleCombatant ally = Assert.Single(battle.Combatants, actor => actor.Id == "party-3");
        ally.Atb = 0;
        int gain = rules.AtbSpeedMultiplier * (ally.NativeAtbSpeed + 30) / 10;
        Assert.True(gain > 0);
        Assert.False(battle.NativeEntryReady);
        Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);
        for (int callback = 0; callback < 1200 && process.Graphics.EntryLifecycle != 3; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.False(battle.NativeEntryReady);
            Assert.Equal(0, ally.NativeAtb!.Current);
        }
        Assert.Equal((byte)3, process.Graphics.EntryLifecycle);
        Assert.Equal((byte)0, battle.Capture().NativeClock!.Active);

        world.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.True(battle.NativeEntryReady);
        Assert.Equal((byte)1, battle.Capture().NativeClock!.Active);
        Assert.Equal(gain, ally.NativeAtb!.Current);
    }

    [Theory]
    [InlineData(4, 2, false)]
    [InlineData(4, 4, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 6, true)]
    [InlineData(7, 7, true)]
    [InlineData(8, 8, true)]
    [InlineData(9, 9, true)]
    [InlineData(10, 10, true)]
    [InlineData(11, 11, true)]
    public void Random_entry_uses_actual_ordered_selection_up_to_its_capacity(int capacity, int selectedCount, bool fullNativeParty)
    {
        const ushort formation = 0;
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory, sharedAssets: assets))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        GameSession session = new(initial.State with { FieldId = 133, FieldName = "cavern-component", EventState = null, DepartingFieldState = initial.State.EventState, FieldLoadContext = null, FieldRequestPacket = null });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets, expandedBattleAllyCount: capacity);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));

        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        if (selectedCount > NativeCharacterVitals.NativeRecordCount)
        {
            vitals = vitals.WithExtendedRoster(true);
        }
        byte[] rows = fullNativeParty ? Enumerable.Range(0, selectedCount).Select(row => (byte)row).ToArray() : [0, 3];
        foreach (byte row in rows)
        {
            vitals.Records[row] = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1) };
        }
        vitals.Records[rows[^1]] = vitals.Records[rows[^1]] with { HitPoints = new(0, 0), Status = 1 };
        vitals = fullNativeParty ? vitals.WithBattleParty(rows, capacity) : vitals with { CombatParty = [0, 255, 255], AdditionalCombatParty = [3, 4] };
        if (!fullNativeParty)
        {
            vitals.Records[4] = vitals.Records[4] with { MemberFlags = 0 };
        }
        session.SetCharacterVitals(vitals);
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));

        BattleDefinition definition = world.CreateEncounterBattle(formation);
        Assert.True(definition.UsesExpandedRoster);
        Assert.Equal(rows.Select(row => (int?)row), definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId));
        Assert.Equal(0, definition.Combatants.Single(actor => actor.MagicRecordId == rows[^1]).CurrentHp);
        Assert.Equal(rows, ExpandedBattlePartyRows.ReadBoundRows(definition, vitals));
        Assert.Equal(vitals.CombatParty, session.State.CharacterVitals!.CombatParty);
    }

    [Theory]
    [InlineData(87, false, false, 4)]
    [InlineData(0, true, false, 4)]
    [InlineData(89, false, true, 4)]
    [InlineData(87, false, false, 8)]
    [InlineData(0, true, false, 8)]
    [InlineData(89, false, true, 8)]
    [InlineData(87, false, false, 11)]
    [InlineData(87, false, false, 11, 10)]
    [InlineData(0, true, false, 11)]
    [InlineData(89, false, true, 11)]
    public void ExpandedRandomBattleUsesInstalledContactsAndChargesEachCommandOnce(ushort formation, bool mass, bool exerciseAi, int allyCount, int requestedDonors = 0)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets assets = new(directory);
        GameSession initialSession = GameWorld.CreateColdProcess(assets);
        initialSession.StartNewGame();
        using (GameWorld bootstrap = new(initialSession, directory))
        {
            bootstrap.EnsureField();
            initialSession.SetEventState(bootstrap.Events.Capture());
        }
        NativeCharacterVitals initial = initialSession.State.CharacterVitals!.Rest();
        if (allyCount == 11)
        {
            initial = initial.WithExtendedRoster(true);
        }
        NativeCharacterVital[] records = initial.Records.ToArray();
        BattleNativeAction fire;
        BattleNativeAction groupMagic;
        BattleNativeAction potion;
        using (BattleAssets battleAssets = new(directory))
        {
            fire = Assert.Single(battleAssets.NativeActions, action => action.Command == 2 && action.Name == "Fire");
            groupMagic = Assert.Single(battleAssets.NativeActions, action => action.Command == 2 && action.Name == "Ultima");
            Assert.Equal((byte)0, groupMagic.Element);
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
        stock[1] = new((byte)groupMagic.ActionId, 5);
        byte magicCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2));
        byte itemCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4));
        byte drawCommand = (byte)(20 + initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6));
        records[casterRow] = records[casterRow] with { MagicInventory = stock, HitPoints = new NativeHpWord(100, 100), Junction = records[casterRow].Junction with { Commands = (uint)(magicCommand | itemCommand << 8 | drawCommand << 16) } };
        if (mass)
        {
            NativeCharacterVital vital = records[casterRow];
            records[casterRow] = vital.WithBonusSources(vital.Growth.Sources.Add(NativeStatBonusSources.Items, 0, [0, 0, 255, 0, 0, 0], initial.LevelBasedStatBonuses));
        }
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
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        byte[] selectedRows = Enumerable.Range(0, allyCount).Select(row => (byte)row).ToArray();
        session.SetCharacterVitals((initial with { Records = records }).WithBattleParty(selectedRows, allyCount));
        session.SetEconomy(economy with { Items = items });
        BattleDefinition original = world.CreateEncounterBattle(88);
        Assert.True(original.UsesOriginalEnemySlots);
        Assert.Null(original.NativeGraphics);
        Assert.Contains(original.Combatants, actor => actor.NativeAi is not null);
        bool guardianFormsEnabled = requestedDonors == 10;
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allyCount, AdditionalEnemies = requestedDonors, GuardianFormsEnabled = guardianFormsEnabled });
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
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        Assert.Equal(guardianFormsEnabled, battle.ReadDefinition().GuardianFormsEnabled);
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        int enemyCount = enemies.Length;
        string lastEnemy = enemies[^1].Id;
        Assert.Equal(allyCount, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(new byte[] { 0, 1, 2 }, session.State.CharacterVitals!.CombatParty);
        BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition());
        string casterId = $"party-{casterRow}";
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        Assert.Equal(allyCount, hud.Party.Count);
        if (allyCount == 11)
        {
            Assert.Equal(Enumerable.Range(0, 11), hud.Party.Select(actor => actor.ModelId).Order());
            Assert.True(session.State.CharacterVitals!.ExtendedRosterEnabled);
        }
        Assert.Equal(battle.Combatants.Count, BattleSession.Restore(battle.Capture()).Combatants.Count);
        Assert.Equal(5, MagicCount(fire.ActionId));
        Assert.Equal(5, MagicCount(groupMagic.ActionId));
        Assert.Equal(enemies[^1].Slot, battle.PreviewSmartAttack(casterId, lastEnemy, forcePhysical: true).TargetSlot);
        if (allyCount >= 8)
        {
            _ = presentation.Render(battle, 640, 448);
            AssertActorBounds(battle, presentation, new(0, 0, 640, 448));
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
            Run(lastEnemy, BattleAbility.Magic, groupMagic.ActionId, enemies.Select(enemy => enemy.Id).ToArray());
            Assert.All(enemies.Select((enemy, index) => (enemy, index)), pair => Assert.InRange(pair.enemy.CurrentHp, 0, before[pair.index] - 1));
            Assert.Equal(4, MagicCount(groupMagic.ActionId));
            Assert.Equal(5, MagicCount(fire.ActionId));
            Assert.All(enemies, enemy => Assert.False(enemy.IsAlive));
            for (int callback = 0; callback < 1200 && !battle.CanReturnToField; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(battle.CanReturnToField);
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal(enemyCount, battle.EnemyDefeats.Count);
            Assert.Equal(enemies.Select(enemy => enemy.SceneSlot!.Value).Order(), battle.EnemyDefeats.Select(defeat => defeat.Slot).Order());
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
            Assert.Equal(4, session.State.CharacterVitals.Records[casterRow].MagicInventory.Single(spell => spell.SpellId == groupMagic.ActionId).Count);
            if (allyCount == 11)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(session.PrepareSnapshot());
                GameState restored = System.Text.Json.JsonSerializer.Deserialize<GameState>(json)!;
                Assert.Equal(11, restored.GameplayPreferences.BattleAllyCount);
                NativeCharacterVitals compact = GameplaySaveCharacters.Capture(restored.CharacterVitals!).Restore(assets.ReadNewGameCharacterVitals());
                Assert.Equal(11, compact.Records.Length);
                Assert.Equal(4, compact.Records[casterRow].MagicInventory.Single(spell => spell.SpellId == groupMagic.ActionId).Count);
                GameState reduced = restored with { GameplayPreferences = restored.GameplayPreferences with { BattleAllyCount = 3 } };
                Assert.True(PlayerSaveStore.RequiresMemoriaFormat(reduced));
                Assert.Throws<NotSupportedException>(() => PlayerSaveStore.ValidateOriginalBattleRoster(reduced));
            }
        }
        else
        {
            int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
            Assert.NotEqual((byte)0, fire.Element);
            int fireElement = System.Numerics.BitOperations.TrailingZeroCount((uint)fire.Element);
            int fireTargetIndex = Array.FindLastIndex(enemies, enemy => enemy.IsAlive && !enemy.Untargetable && enemy.ElementDefenses[fireElement] < 900);
            Assert.InRange(fireTargetIndex, 0, enemies.Length - 1);
            BattleCombatant fireTarget = enemies[fireTargetIndex];
            Run(fireTarget.Id, BattleAbility.Magic, fire.ActionId, [fireTarget.Id]);
            Assert.InRange(fireTarget.CurrentHp, 0, before[fireTargetIndex] - 1);
            Assert.Equal(before.Where((_, index) => index != fireTargetIndex), enemies.Where((_, index) => index != fireTargetIndex).Select(enemy => enemy.CurrentHp));
            Assert.Equal(4, MagicCount(fire.ActionId));
            Assert.Equal(5, MagicCount(groupMagic.ActionId));
            int beforePotion = caster.CurrentHp;
            Assert.Equal(3, battle.Items.Single(item => item.Id == potion.ActionId).Count);
            Run(casterId, BattleAbility.Item, potion.ActionId, [casterId]);
            Assert.True(caster.CurrentHp > beforePotion);
            Assert.Equal(2, battle.Items.Single(item => item.Id == potion.ActionId).Count);
            if (allyCount == 11)
            {
                BattleCombatant drawSource = enemies.Last(enemy => enemy.IsActive && enemy.IsAlive && !enemy.Untargetable && enemy.NativeDrawSlots.Any(slot => slot.Id is > 0 and < 64));
                NativeBattleDrawSlot draw = drawSource.NativeDrawSlots.First(slot => slot.Id is > 0 and < 64);
                caster.Atb = BattleSession.AtbMaximum;
                for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                    world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                }
                Assert.True(caster.IsReady);
                hud.SelectParty(caster.Id);
                Assert.Contains(BattleAbility.Draw, hud.Commands);
                Assert.True(hud.DrawCastAvailable);
                int beforeDraw = battle.Events.Count;
                world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = caster.Id, TargetId = drawSource.Id, Ability = BattleAbility.Draw, ActionId = draw.Id });
                NativeBattleOriginalEffectState? stockPresentation = null;
                for (int frame = 0; frame < 512 && caster.IsBusy; frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    stockPresentation ??= battle.Presentation?.Graphics.OriginalEffect is { RequestedEffect: 15, ReturnedDescriptor: > 0 } stockEffect ? stockEffect : null;
                }
                Assert.False(caster.IsBusy);
                BattleEvent drawResult = Assert.Single(battle.Events.Skip(beforeDraw), entry => entry.Kind == "Draw" && entry.ActorId == caster.Id && entry.TargetId == drawSource.Id);
                if (drawResult.Amount > 0)
                {
                    Assert.NotNull(stockPresentation);
                    Assert.Equal((ushort)15, stockPresentation.RequestedEffect);
                    Assert.Null(battle.Presentation!.ActiveActionToken);
                    Assert.Single(battle.Events.Skip(beforeDraw), entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == drawSource.Id);
                }
                if (enemyCount == 11)
                {
                    BattleActionDefinition spell = battle.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && battle.SupportsExpandedAction(action) && drawSource.NativeDrawSlots.Any(slot => slot.Id == action.Id));
                    BattleCombatant recipient = battle.Combatants.Last(actor => actor.Id != drawSource.Id && BattleSession.IsValidSelectedActionTarget(caster, spell, actor));
                    caster.Atb = BattleSession.AtbMaximum;
                    for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                    }
                    Assert.True(caster.IsReady);
                    int stockBefore = caster.MagicStocks.FirstOrDefault(stock => stock.Id == spell.Id)?.Count ?? 0;
                    world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = caster.Id, TargetId = drawSource.Id, Ability = BattleAbility.Draw, ActionId = spell.Id, DrawCast = true, DrawCastTargetId = recipient.Id });
                    BattleSnapshot windup = battle.Capture();
                    bool presented = false;
                    for (int frame = 0; frame < 512 && caster.IsBusy; frame++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        presented |= battle.Presentation is { ActiveActionToken: not null };
                    }
                    Assert.False(caster.IsBusy);
                    Assert.True(presented);
                    Assert.Equal(stockBefore, caster.MagicStocks.FirstOrDefault(stock => stock.Id == spell.Id)?.Count ?? 0);
                    Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.TargetId == recipient.Id && entry.Frame >= windup.Frame);
                }
            }

        }

        int MagicCount(int id) => caster.MagicStocks.Single(spell => spell.Id == id).Count;

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
                BattleScenePresentation gpuPresentation = new(world.BattleResources, active.ReadDefinition());
                foreach ((int width, int height) in new[] { (640, 448), (1280, 720), (1920, 1080) })
                {
                    _ = gpuPresentation.Render(active, width, height, gpu);
                    Assert.Equal(width, gpu.Target.Width);
                    Assert.Equal(height, gpu.Target.Height);
                    Assert.Contains(GpuTestDevice.Read(gpu.Target), pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                    AssertActorBounds(active, gpuPresentation, new(0, 0, width, height));
                }
            }
        }

        static void AssertActorBounds(BattleSession active, BattleScenePresentation scene, UiRect viewport)
        {
            Assert.All(active.Combatants.Where(actor => actor.IsActive && !actor.Hidden), actor =>
            {
                UiRect? bounds = scene.GetActorBounds(actor.Id, viewport);
                Assert.True(bounds is { } model && !model.Intersect(viewport).IsEmpty, $"Visible actor {actor.Id} (model {actor.ModelId}) lacks bounds intersecting {viewport}.");
            });
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
            if (mass && allyCount == 11)
            {
                CheckHud(battle);
            }
            long first = battle.Frame;
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = casterId, TargetId = target, Ability = ability, ActionId = action });
            bool rendered = false;
            for (int i = 0; i < 2048 && caster.IsBusy; i++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (!rendered && battle.Presentation?.PreparedPresentation?.Primitives is { LastCommands.IsEmpty: false })
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
