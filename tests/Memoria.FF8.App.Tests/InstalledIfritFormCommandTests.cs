using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledIfritFormCommandTests
{
    private const string CasterId = "party-3";
    private const int Ifrit = 2;
    private const int OtherGuardian = 0;
    private const int OtherGuardianAbility = 36;
    private const int DarksideAbility = 30;
    private const int FlareSpell = 15;
    private const int LearnedCommandBase = 0x100;
    private const byte DefendCommand = 23;
    private const byte KamikazeCommand = 31;
    private const byte MiniMogCommand = 38;
    private const string KnockedOutAllyId = "party-1";

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void Installed_Ifrit_projects_only_his_learned_commands_and_dispatches_Darkside(int casterRow)
    {
        RunInForm(99, (world, battle, caster, vitals) =>
        {
            string casterId = caster.Id;
            Assert.Equal(casterRow, caster.ModelId);
            Assert.Equal(casterRow == 0, caster.WeaponProfile!.Supports(BattleWeaponCapabilities.Gunblade));
            if (casterRow == 0)
            {
                Assert.Equal((byte)10, caster.NativeAttack!.AttackType);
                battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
            }
            var expected = vitals.MenuDefinitions.CommandAbilityCommands.Select((command, index) => (Ability: index + 20, Command: command))
                .Where(entry => entry.Command is not (0 or 2 or 3 or 4 or 6 or MiniMogCommand) && entry.Ability != OtherGuardianAbility).ToArray();
            IReadOnlyList<BattleGuardianFormCommand> commands = battle.GetGuardianFormCommands(casterId);
            BattleGuardianFormCommand[] learned = commands.Where(command => command.LearnedAbilityId.HasValue).ToArray();
            Assert.Equal(12, expected.Length);
            Assert.Equal(expected.OrderBy(entry => entry.Ability), learned.Select(command => (Ability: command.LearnedAbilityId!.Value, Command: command.NativeCommand!.Value)).OrderBy(entry => entry.Ability));
            Assert.All(learned, command => Assert.Equal(LearnedCommandBase + command.LearnedAbilityId!.Value, command.Id));
            Assert.DoesNotContain(commands, command => command.Id == (int)BattleGuardianFormAction.Flare);
            Assert.Contains(vitals.GuardianForceAbilities(OtherGuardian), ability => ability is { Ability: OtherGuardianAbility, Learned: true });
            Assert.DoesNotContain(vitals.GuardianForceAbilities(Ifrit), ability => ability is { Ability: OtherGuardianAbility, Learned: true });

            BattleGuardianFormCommand darkside = Assert.Single(learned, command => command.LearnedAbilityId == DarksideAbility);
            Assert.True(darkside.Action.Available);
            Assert.Equal((byte)28, darkside.NativeCommand);
            BattleNativeAction weaponAction = Assert.IsType<BattleNativeAction>(darkside.Action.NativeAction);
            Assert.Equal(caster.ModelId, weaponAction.CharacterId);
            Assert.Equal(caster.WeaponId, weaponAction.KernelIndex);
            Assert.DoesNotContain(caster.NativeCommands, command => command.Command == darkside.NativeCommand);
            IReadOnlyList<BattleCombatant> targets = battle.GetGuardianFormCommandTargets(casterId, darkside.Id);
            BattleCombatant target = targets.Last(candidate => !candidate.IsPlayer);
            Assert.Contains(targets, candidate => candidate.Id == casterId);

            BattleAttackCommand[] queueBefore = battle.QueuedActions.ToArray();
            Assert.Throws<InvalidOperationException>(() => battle.QueueGuardianFormAction(casterId, target.Id, LearnedCommandBase + OtherGuardianAbility));
            Assert.Throws<InvalidOperationException>(() => battle.QueueGuardianFormAction(casterId, target.Id, (int)BattleGuardianFormAction.Flare));
            Assert.True(battle.CanChoosePlayerCommand(casterId));
            Assert.Equal(queueBefore, battle.QueuedActions.ToArray());

            int hpBefore = caster.CurrentHp;
            int humanHp = caster.HumanHitPoints;
            BattleEvent[] events = RunAction(world, battle, darkside, target, queued =>
            {
                Assert.Equal((byte)28, queued.NativeQueuedCommand);
                Assert.Equal(darkside.Action.NativeAction, queued.NativeAction);
            }, casterId);
            Assert.Contains(events, entry => entry.Kind == "EffectContact" && entry.ActorId == casterId && entry.TargetId == target.Id);
            Assert.DoesNotContain(events, entry => entry.Kind == "GunbladeTrigger" && entry.ActorId == casterId);
            Assert.Equal(hpBefore - caster.MaximumHp / 10, caster.CurrentHp);
            Assert.Equal(humanHp, caster.HumanHitPoints);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(casterId)?.Phase);
        }, casterRow: casterRow);
    }

    [Fact]
    public void Installed_Ifrit_queues_every_permitted_learned_native_command()
    {
        RunInForm(100, (world, battle, caster, _) =>
        {
            BattleGuardianFormCommand[] learned = battle.GetGuardianFormCommands(CasterId).Where(command => command.LearnedAbilityId.HasValue).ToArray();
            Assert.Equal(13, learned.Length);
            BattleSnapshot boundary = battle.Capture();
            foreach (BattleGuardianFormCommand command in learned)
            {
                byte nativeCommand = command.NativeCommand!.Value;
                BattleNativeCommandBinding binding = world.BattleResources.ReadNativeCommand(nativeCommand);
                BattleSession branch = BattleSession.Restore(boundary);
                Assert.True(command.Action.Available, $"{binding.Name} is unavailable in the installed admission fixture.");
                IReadOnlyList<BattleCombatant> targets = branch.GetGuardianFormCommandTargets(CasterId, command.Id);
                BattleCombatant target = nativeCommand is DefendCommand or 27 ? Assert.Single(targets) : targets.First();
                if (nativeCommand == DefendCommand)
                {
                    Assert.Equal(CasterId, target.Id);
                }
                if (nativeCommand == 27)
                {
                    Assert.Equal(KnockedOutAllyId, target.Id);
                    Assert.False(target.IsAlive);
                }
                GameWorld.ExecuteBattleCommand(branch, new(BattleInputKind.GuardianFormAction, ActorId: CasterId, TargetId: target.Id, ActionId: command.Id));
                BattleAttackCommand queued = Assert.Single(branch.QueuedActions, queued => queued.ActorId == CasterId);
                Assert.Equal(BattleAbility.NativeCommand, queued.Ability);
                Assert.Equal(target.Id, queued.TargetId);
                Assert.Equal(command.LearnedAbilityId, queued.GuardianFormLearnedAbility);
                Assert.Equal(nativeCommand, queued.NativeQueuedCommand);
                Assert.Equal(nativeCommand, queued.NativeAction?.Command);
                Assert.Equal(nativeCommand == 28 ? (ushort)0 : binding.ActionId, queued.NativeAction?.ActionId);
                Assert.Equal(nativeCommand == 27, queued.TargetRequest.AllowsKnockedOutAllies);
                if (nativeCommand == 24)
                {
                    Assert.Equal(BattleTargetSide.Allies, Assert.IsType<BattleTargetSelection.SideGroup>(queued.TargetRequest.Selection).Side);
                    Assert.All(targets, candidate => Assert.True(candidate.IsAlive && candidate.IsPlayer));
                }
                else
                {
                    BattleActorReference selected = Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(queued.TargetRequest.Selection).Actors);
                    Assert.Equal(target.Id, selected.ActorId);
                }
                Assert.True(branch.IsGuardianSummonOccupied);
                if (nativeCommand == 28)
                {
                    BattleSession restored = BattleSession.Restore(branch.Capture());
                    BattleAttackCommand pending = Assert.Single(restored.QueuedActions, candidate => candidate.ActorId == caster.Id);
                    Assert.Equal(command.LearnedAbilityId, pending.GuardianFormLearnedAbility);
                    Assert.Equal(queued.ActionId, pending.ActionId);
                    Assert.Equal(queued.TargetId, pending.TargetId);
                    Assert.Equal(BattleGuardianFormPhase.Active, restored.GetGuardianForm(caster.Id)?.Phase);
                    Assert.True(restored.IsGuardianSummonOccupied);
                }
                Assert.Equal(BattleGuardianFormPhase.Active, branch.GetGuardianForm(CasterId)?.Phase);
            }

            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(CasterId)?.Phase);
            Assert.Equal(Assert.Single(boundary.Combatants, candidate => candidate.Id == CasterId).Atb, caster.Atb);
        }, prepareAdmission: true);
    }

    [Theory]
    [InlineData(DefendCommand)]
    [InlineData(KamikazeCommand)]
    public void Installed_Ifrit_learned_command_completes_on_the_attached_process(byte nativeCommand)
    {
        RunInForm(100, (world, battle, _, _) =>
        {
            BattleGuardianFormCommand command = Assert.Single(battle.GetGuardianFormCommands(CasterId), command => command.NativeCommand == nativeCommand);
            IReadOnlyList<BattleCombatant> targets = battle.GetGuardianFormCommandTargets(CasterId, command.Id);
            BattleCombatant target = nativeCommand == KamikazeCommand ? targets.First(candidate => !candidate.IsPlayer) : Assert.Single(targets, candidate => candidate.Id == CasterId);
            world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: CasterId, TargetId: target.Id, ActionId: command.Id));
            VerifyHostedCompletion(world, battle, nativeCommand, command.Action.Name, target.Id);
        }, prepareAdmission: true);
    }

    private static void VerifyHostedCompletion(GameWorld world, BattleSession branch, byte nativeCommand, string name, string targetId)
    {
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(branch.Presentation);
        BattleCombatant caster = Assert.Single(branch.Combatants, actor => actor.Id == CasterId);
        int begin = branch.Events.Count;
        BattleSession? ejectionFork = null;
        for (int frame = 0; frame < 3200 && !Completed(); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (nativeCommand == KamikazeCommand && ejectionFork is null && !caster.IsActive && branch.ActiveAction?.NativeQueuedCommand == KamikazeCommand)
            {
                ejectionFork = branch.ForkForContinuation();
            }
        }
        Assert.True(Completed(), $"{name} did not complete on its hosted process by frame {branch.Frame}.");
        Assert.Same(process, branch.Presentation);
        BattleEvent[] events = branch.Events.Skip(begin).ToArray();
        Assert.Single(events, entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
        Assert.Contains(events, entry => entry is { Kind: "EffectContact", ActorId: CasterId } && entry.TargetId == targetId);
        switch (nativeCommand)
        {
            case DefendCommand:
                Assert.NotEqual(0u, caster.NativeStatus1 & 0x80000u);
                Assert.Equal(BattleGuardianFormPhase.Active, branch.GetGuardianForm(CasterId)?.Phase);
                Assert.True(branch.IsGuardianSummonOccupied);
                break;
            case KamikazeCommand:
                Assert.Contains(events, entry => entry is { Kind: "Damage", ActorId: CasterId, Amount: > 0 } && entry.TargetId == targetId);
                Assert.NotNull(ejectionFork);
                for (int frame = 0; frame < 3200 && (ejectionFork.IsGuardianSummonOccupied || ejectionFork.ActiveAction is not null); frame++)
                {
                    ejectionFork.Tick();
                }
                Assert.False(ejectionFork.IsGuardianSummonOccupied);
                Assert.Null(ejectionFork.ActiveAction);
                Assert.False(caster.IsActive);
                Assert.Null(branch.GetGuardianForm(CasterId));
                Assert.Null(process.Graphics.GuardianFormPresentation);
                Assert.False(branch.IsGuardianSummonOccupied);
                break;
        }

        bool Completed()
        {
            return branch.ActiveAction is null && process.ActiveActionToken is null &&
                branch.Events.Skip(begin).Any(entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
        }
    }

    [Fact]
    public void Installed_Ifrit_at_level_100_dispatches_innate_Flare_without_magic_stock()
    {
        RunInForm(100, (world, battle, caster, _) =>
        {
            IReadOnlyList<BattleGuardianFormCommand> commands = battle.GetGuardianFormCommands(CasterId);
            Assert.Contains(commands, command => command.LearnedAbilityId == OtherGuardianAbility);
            BattleGuardianFormCommand flare = Assert.Single(commands, command => command.Id == (int)BattleGuardianFormAction.Flare);
            Assert.Null(flare.LearnedAbilityId);
            Assert.True(flare.Action.Available);
            Assert.Equal(FlareSpell, flare.Action.Id);
            Assert.DoesNotContain(caster.MagicStocks, stock => stock.Id == FlareSpell && stock.Count > 0);
            var stockBefore = caster.MagicStocks.ToArray();
            Assert.DoesNotContain(caster.NativeCommands, command => command.Command == 2);
            BattleCombatant target = battle.GetGuardianFormCommandTargets(CasterId, flare.Id).Last();

            BattleEvent[] events = RunAction(world, battle, flare, target, queued =>
            {
                Assert.True(queued.FreeNativeMagic);
                Assert.Equal((byte)2, queued.NativeQueuedCommand);
                Assert.Equal(flare.Action.NativeAction, queued.NativeAction);
            });
            Assert.Contains(events, entry => entry is { Kind: "Damage", ActorId: CasterId, Amount: > 0 } && entry.TargetId == target.Id);
            Assert.Equal(stockBefore, caster.MagicStocks.ToArray());
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(CasterId)?.Phase);
        });
    }

    [Fact]
    public void Installed_Ifrit_full_command_grid_shows_all_rows_and_submits_a_third_column_choice()
    {
        RunInForm(100, (_, battle, _, vitals) =>
        {
            List<BattleInput> submitted = [];
            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: submitted.Add, readPresentationUi: BattleHudTestPresentation.Visible);
            hud.SelectParty(CasterId);
            Assert.Equal(CasterId, hud.SelectedActor?.Id);
            IReadOnlyList<BattleGuardianFormCommand> commands = battle.GetGuardianFormCommands(CasterId);
            Assert.Equal(17, commands.Count);
            Assert.Equal(13, commands.Count(command => command.LearnedAbilityId.HasValue));
            byte[] hiddenCommands = [4, 6, 2, 3, MiniMogCommand]; // Item, Draw, Magic, GF, MiniMog
            var learned = vitals.GuardianForceAbilities(Ifrit).Where(ability => ability.Learned).Select(ability => ability.Ability).ToHashSet();
            foreach (byte hidden in hiddenCommands)
            {
                int ability = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf(hidden) + 20;
                Assert.Contains(ability, learned);
                Assert.DoesNotContain(commands, row => row.NativeCommand == hidden);
            }
            string[] labels = hud.ChoiceLabels.ToArray();
            Assert.Equal(17, labels.Length);
            UiView view = hud.BuildView();
            view.Resize(960, 672);
            UiTheme theme = new();
            BattleUiTests.TestCanvas canvas = new();
            view.Draw(canvas, theme);
            var rows = canvas.TextDraws.Where(draw => labels.Contains(draw.Text)).ToArray();
            Assert.Equal(labels, rows.Select(row => row.Text));
            Assert.Equal(new[] { 6, 6, 5 }, rows.GroupBy(row => row.Bounds.X).Select(column => column.Count()));
            Assert.Equal(6, rows.Select(row => row.Bounds.Y).Distinct().Count());
            Assert.Equal(rows[4].Bounds.Y, rows[16].Bounds.Y);
            Assert.True(rows[16].Bounds.X > rows[11].Bounds.X);
            for (int index = 0; index < rows.Length; index++)
            {
                UiRect bounds = rows[index].Bounds;
                Assert.InRange(bounds.X, 0, 960);
                Assert.InRange(bounds.Right, 0, 960);
                Assert.InRange(bounds.Y, 0, 672);
                Assert.InRange(bounds.Bottom, 0, 672);
                Assert.Equal(commands[index].Action.Available ? theme.Foreground : theme.Muted, rows[index].Color);
            }

            view.Key(UiKey.Up);
            Assert.Equal(5, hud.ChoiceIndex);
            view.Key(UiKey.Right);
            Assert.Equal(11, hud.ChoiceIndex);
            view.Key(UiKey.Right);
            Assert.Equal(16, hud.ChoiceIndex);
            view.Pointer(rows[16].Bounds.X + 1, rows[11].Bounds.Y + 1, false);
            view.Pointer(rows[16].Bounds.X + 1, rows[11].Bounds.Y + 1, true);
            Assert.Equal(16, hud.ChoiceIndex);
            Assert.False(hud.SelectingTarget);

            int choice = Enumerable.Range(12, 5).Last(index => commands[index].LearnedAbilityId.HasValue && commands[index].Action.Available);
            BattleGuardianFormCommand selected = commands[choice];
            UiRect cell = rows[choice].Bounds;
            view.Pointer(cell.X + 1, cell.Y + 1, false);
            Assert.Equal(choice, hud.ChoiceIndex);
            view.Pointer(cell.X + 1, cell.Y + 1, true);
            Assert.True(hud.SelectingTarget);
            Assert.Equal(battle.GetGuardianFormCommandTargets(CasterId, selected.Id).Select(target => target.Id).Order(), hud.Targets.Select(target => target.Id).Order());
            string target = Assert.IsType<BattleCombatant>(hud.SelectedTarget).Id;
            Assert.Empty(submitted);
            view.Key(UiKey.Confirm);
            BattleInput input = Assert.Single(submitted);
            Assert.Equal(BattleInputKind.GuardianFormAction, input.Kind);
            Assert.Equal(CasterId, input.ActorId);
            Assert.Equal(target, input.TargetId);
            Assert.Equal(selected.Id, input.ActionId);
        });
    }

    private static void RunInForm(int level, Action<GameWorld, BattleSession, BattleCombatant, NativeCharacterVitals> verify, bool prepareAdmission = false, int casterRow = 3)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory());
        if (prepareAdmission)
        {
            NativeEconomy economy = Assert.IsType<NativeEconomy>(field.Economy);
            NativePocketStationState pocket = Assert.IsType<NativePocketStationState>(field.PocketStation);
            field = field with
            {
                Economy = economy with { Gil = 10000, PastGil = 10000 },
                PocketStation = pocket with { Raw = pocket.Raw.SetItem(0, (byte)((pocket.Flags & 0xF0) | 0x0D)).SetItem(1, 20) }
            };
        }
        GameSession session = new(field);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, level, prepareAdmission, casterRow));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = true });
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        string casterId = $"party-{casterRow}";
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
        var humanStats = (caster.Level, caster.Attack, caster.Magic);
        Assert.Equal(level, Assert.Single(battle.ReadDefinition().ActionDefinitions, action => action is { Ability: BattleAbility.GuardianForce, Id: Ifrit }).Level);
        Assert.NotEqual(level, caster.Level);
        Ready(world, battle, casterId);
        world.ApplyBattleInput(new(BattleInputKind.GuardianFormActivate, ActorId: casterId, TargetId: casterId, ActionId: Ifrit));
        for (int frame = 0; frame < 1600 && battle.GetGuardianForm(casterId)?.Phase != BattleGuardianFormPhase.Active; frame++)
        {
            if (battle.GetGuardianForm(casterId)?.Phase == BattleGuardianFormPhase.Charging)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(casterId)?.Phase);
        Assert.Equal(humanStats, (caster.Level, caster.Attack, caster.Magic));
        Ready(world, battle, casterId);
        verify(world, battle, caster, session.State.CharacterVitals!);
    }

    private static void PrepareParty(GameSession session, int level, bool prepareAdmission, int casterRow)
    {
        ExpandedBattleGuardianTests.PrepareParty(session, learnedBoost: true, guardianId: Ifrit);
        NativeCharacterVitals vitals = session.State.CharacterVitals!.AcquireGuardianForce(OtherGuardian);
        int existing = Array.FindIndex(vitals.Records, record => record.Junction.GuardianForceAssignments.Contains(OtherGuardian));
        if (existing == -1)
        {
            vitals = vitals.ToggleGuardianForceJunction(3, OtherGuardian);
        }
        else if (existing != 3)
        {
            vitals = vitals.ReassignGuardianForceJunction(3, OtherGuardian, existing);
        }
        vitals = vitals.TeachGuardianForceAbility(OtherGuardian, OtherGuardianAbility);
        for (int index = 0; index < vitals.MenuDefinitions.CommandAbilityCommands.Length; index++)
        {
            int ability = index + 20;
            if (vitals.MenuDefinitions.CommandAbilityCommands[index] != 0 && (ability != OtherGuardianAbility || level == 100))
            {
                vitals = vitals.TeachGuardianForceAbility(Ifrit, ability);
            }
        }
        uint experience = NativeStatDefinitions.ExperienceForLevel(level, vitals.Definitions.GuardianForces[Ifrit]);
        vitals.GuardianForces[Ifrit] = vitals.GuardianForces[Ifrit] with { Experience = experience };
        if (casterRow != 3)
        {
            NativeCharacterVital source = vitals.Records[3];
            vitals = vitals.ReassignGuardianForceJunction(casterRow, Ifrit, 3).ReassignGuardianForceJunction(casterRow, OtherGuardian, 3);
            NativeCharacterVital caster = vitals.Records[casterRow];
            vitals.Records[casterRow] = caster with { Junction = caster.Junction with { Commands = source.Junction.Commands }, GuardianForceCompatibility = source.GuardianForceCompatibility?.ToArray() };
        }
        vitals = vitals.RefreshGuardianForces().Rest();
        if (prepareAdmission)
        {
            vitals.Records[1] = vitals.Records[1] with { HitPoints = new(0, 0), Status = 1 };
            vitals.GuardianForces[OtherGuardian] = vitals.GuardianForces[OtherGuardian] with { HitPoints = 1 };
        }
        session.SetCharacterVitals(vitals);
    }

    private static void Ready(GameWorld world, BattleSession battle, string casterId)
    {
        for (int turn = 0; turn < 32 && !battle.CanChoosePlayerCommand(casterId); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 24));
        }
        Assert.True(battle.CanChoosePlayerCommand(casterId), $"Could not ready {casterId} at frame {battle.Frame}.");
    }

    private static BattleEvent[] RunAction(GameWorld world, BattleSession battle, BattleGuardianFormCommand command, BattleCombatant target, Action<BattleAttackCommand> verifyQueue, string casterId = CasterId)
    {
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(casterId);
        int begin = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: casterId, TargetId: target.Id, ActionId: command.Id));
        verifyQueue(Assert.Single(battle.QueuedActions, queued => queued.ActorId == casterId));
        for (int frame = 0; frame < 900 && !battle.Events.Skip(begin).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == casterId); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            Assert.Null(process.Graphics.TriggerWidget);
            Assert.Equal((byte)77, process.Graphics.ActorModels.Get(owner).ModelId4);
        }
        BattleEvent[] events = battle.Events.Skip(begin).ToArray();
        Assert.Single(events, entry => entry.Kind == "ActionComplete" && entry.ActorId == casterId);
        return events;
    }
}
