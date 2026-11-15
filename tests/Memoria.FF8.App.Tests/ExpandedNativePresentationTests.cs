using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedNativePresentationTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(11)]
    public void Installed_presentation_uses_the_owning_device_and_full_typed_roster(ushort effect)
    {
        VerifyScenario(effect, effect == 7 ? MegaPhoenixScenario : world => CreateScenario(world, effect));
    }

    [Fact]
    public void Installed_cinematic_branch_reads_the_live_typed_participant_status()
    {
        VerifyScenario(233, ArmadodoScenario);
    }

    [Theory]
    [InlineData(130, 11)]
    [InlineData(226, 6)]
    public void Installed_enemy_effect_delivers_every_scheduled_contact(ushort effect, int contacts)
    {
        VerifyScenario(effect, world => EnemyFanOutScenario(world, effect), contacts);
    }

    [Fact]
    public void Installed_breath_keeps_captured_body_triangles_live_for_all_fibres()
    {
        VerifyScenario(129, world => EnemyFanOutScenario(world, 129));
    }

    [Fact]
    public void Installed_mad_rush_finishes_every_target_act()
    {
        VerifyScenario(273, MadRushScenario, requireQuiescence: false);
    }

    [Theory]
    [InlineData(71, 1)]
    [InlineData(72, 5)]
    [InlineData(73, 11)]
    public void Installed_blue_magic_publishes_its_learned_native_limit(ushort effect, int contacts)
    {
        VerifyScenario(effect, world => BlueMagicScenario(world, effect), contacts);
    }

    [Fact]
    public void Installed_level_death_publishes_all_enemy_blue_magic_contacts()
    {
        VerifyScenario(74, world => BlueMagicScenario(world, 74), 5);
    }

    private static void VerifyScenario(ushort effect, Func<GameWorld, DiagnosticBattleRequest> createScenario, int expectedContacts = 11, bool requireQuiescence = true)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        GameWorld world = new(session, directory);
        try
        {
            DiagnosticBattleRequest request = createScenario(world);
            DiagnosticBattleDriverRecipe driver = request.Driver!;
            BattleSession battle = world.StartDiagnosticBattle(request);
            for (int tick = 0; tick < 1200 && !RecreateGame.DiagnosticCommandReady(battle, driver); tick++)
            {
                if (battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(RecreateGame.DiagnosticCommandReady(battle, driver));
            if (driver.LimitSelection is { } selection)
            {
                BattleDiagnosticLimitSelectionReceipt chosen = battle.SelectDiagnosticLimit(driver.Input.ActorId!, selection);
                Assert.True(chosen.Accepted);
                Assert.Equal(3, chosen.CommandOwner);
                Assert.Equal((byte)15, chosen.Command);
            }
            if (driver.EnemyAbility is { } enemy)
            {
                battle.SubmitDiagnosticEnemyAbility(driver.Input.ActorId!, enemy, driver.ExpectedEffect);
            }
            else
            {
                world.ApplyBattleInput(driver.Input);
            }
            BattleScenePresentation scene = new(world.BattleResources, battle.ReadDefinition(), true);
            bool rendered = false;
            bool entered = false;
            long? token = null;
            ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
            for (int tick = 0; tick < 1400 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || battle.GuardianForceCasts.Count != 0 || battle.ObserveDiagnosticDrainBlockers().Count != 0); tick++)
            {
                battle.Tick();
                bool current = process.Graphics.OriginalEffect?.RequestedEffect == effect;
                entered |= current;
                if (current)
                {
                    token ??= process.ActiveActionToken;
                }
                if (!requireQuiescence && token is long focal && process.IsActionComplete(focal))
                {
                    break;
                }
                if (effect == 6 && !rendered && current &&
                    battle.PreparedPresentation is { } prepared && prepared.Primitives.LastCommands.Any(command => command.DrawMode.TexturePage is ushort page && (page & 0x180) == 0x100))
                {
                    Assert.Equal(NativeBattlePcDirectTextureFormat.Rgb555, prepared.PrimitiveCapabilities?.DirectTextureFormat);
                    Assert.NotEmpty(scene.Render(battle, 320, 240));
                    rendered = true;
                }
            }
            Assert.True(entered);
            NativeBattleOriginalEffectState? remaining = process.Graphics.OriginalEffect;
            Assert.True(requireQuiescence ? battle.ObserveDiagnosticDrainBlockers().Count == 0 : token is long selected && process.IsActionComplete(selected),
                $"Effect {remaining?.RequestedEffect}, descriptor {remaining?.ReturnedDescriptor:X}, action {battle.ActiveAction?.Ability}/{battle.ActiveAction?.ActionId}, contacts {battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.ActorId == driver.Input.ActorId)}, events {string.Join(", ", battle.Events.TakeLast(10).Select(entry => $"{entry.Kind}/{entry.ActorId}/{entry.TargetId}"))}.");
            if (effect == 6)
            {
                Assert.True(rendered);
            }
            else
            {
                string?[] recipients = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == driver.Input.ActorId).Select(entry => entry.TargetId).ToArray();
                Assert.Equal(expectedContacts, recipients.Length);
                Assert.Equal(expectedContacts, recipients.Distinct().Count());
                if (effect == 7)
                {
                    Assert.Equal(Enumerable.Range(0, 11).Select(index => $"party-{index}"), recipients);
                    Assert.True(battle.Combatants.Single(actor => actor.Id == "party-1").IsAlive);
                    Assert.Contains(new BattleStock(8, 1), battle.Items);
                }
            }
            world.Dispose();
        }
        catch
        {
            try
            {
                world.Dispose();
            }
            catch (InvalidOperationException)
            {
                // The failed action may still own its published presentation.
            }
            throw;
        }
    }

    private static DiagnosticBattleRequest CreateScenario(GameWorld world, ushort effect)
    {
        bool guardian = effect == 6;
        int formation = guardian ? 585 : 284;
        int stage = guardian ? 102 : 49;
        int enemyModel = guardian ? 16 : 131;
        (int Formation, int Slot)[] donorSlots = guardian
            ? [(274, 0), (361, 2), (613, 2), (464, 1)]
            : [(323, 0), (459, 1), (356, 0), (142, 3), (39, 0)];
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(slot => new BattleEnemyRosterSource(slot.Formation, slot.Slot, world.BattleResources.FormationSourceHash(slot.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation character = guardian
            ? new(0, 0, [], [7, 0]) { HitPoints = 486, Commands = [new(0, 21)] }
            : new(0, 0, [], [0]) { HitPoints = 486, Level = 7, MagicCommand = new(0, 20) };
        DiagnosticGuardianForcePreparation[] forces = guardian ? [new(7, true), new(0, true)] : [new(0, true)];
        BattleInput command = guardian
            ? new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.GuardianForce, ActionId: 7)
            : new(BattleInputKind.Action, ActorId: "enemy-0", TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 9);
        return new(formation, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new(forces.ToImmutableArray(), [character], true),
            Target = new("enemy-0", stage, enemyModel, 0, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 0),
            Driver = new(command, effect)
            {
                ExpectedNativeCommand = guardian ? (byte)254 : (byte)8,
                ExpectedNativeAction = guardian ? 71 : 9,
                EnemyAbility = guardian ? null : new(131, 0, 0, 2, 8, 9, 14, 204)
            }
        };
    }

    private static DiagnosticBattleRequest MegaPhoenixScenario(GameWorld world)
    {
        (int Formation, int Slot)[] donorSlots = [(563, 0), (182, 0), (680, 1), (138, 1)];
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(slot => new BattleEnemyRosterSource(slot.Formation, slot.Slot, world.BattleResources.FormationSourceHash(slot.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation caster = new(0, 0, [], [0]) { HitPoints = 486, Commands = [new(0, 23)] };
        DiagnosticCharacterPreparation target = new(1, 1, [], []) { HitPoints = 0, KnockedOut = true };
        BattleInput command = new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-1", Ability: BattleAbility.Item, ActionId: 8);
        return new(657, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [caster, target], true) { CombatParty = [0, 1, 255], Items = [new(8, 2)] },
            Target = new("enemy-1", 128, 6, 1, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 1),
            Driver = new(command, 7) { ExpectedNativeCommand = 4, ExpectedNativeAction = 8 }
        };
    }

    private static DiagnosticBattleRequest ArmadodoScenario(GameWorld world)
    {
        (int Formation, int Slot)[] donorSlots = [(324, 1), (437, 1), (680, 1), (402, 0), (20, 0)];
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(slot => new BattleEnemyRosterSource(slot.Formation, slot.Slot, world.BattleResources.FormationSourceHash(slot.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation character = new(0, 0, [], [0]) { HitPoints = 486, Level = 7, MagicCommand = new(0, 20) };
        BattleInput command = new(BattleInputKind.Action, ActorId: "enemy-0", TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 49);
        return new(185, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [character], true),
            Target = new("enemy-0", 48, 27, 0, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 0),
            Driver = new(command, 233) { ExpectedNativeCommand = 8, ExpectedNativeAction = 49, EnemyAbility = new(27, 0, 0, 4, 8, 49, 12, 204) }
        };
    }

    private static DiagnosticBattleRequest EnemyFanOutScenario(GameWorld world, ushort effect)
    {
        (int Formation, int Stage, int Model, byte AbilitySlot, ushort Action, byte Sequence, byte Selector, (int Formation, int Slot)[] Donors) scenario = effect switch
        {
            129 => (553, 105, 53, 3, 102, 12, 204, [(388, 2), (659, 0), (92, 1), (414, 1), (673, 2)]),
            130 => (662, 105, 48, 2, 185, 13, 204, [(579, 1), (671, 1), (360, 1), (551, 1), (716, 0)]),
            226 => (548, 107, 46, 6, 276, 11, 205, [(408, 0), (574, 1), (370, 2), (527, 1), (36, 0)]),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        var (formation, stage, model, abilitySlot, action, sequence, selector, donorSlots) = scenario;
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(donor => new BattleEnemyRosterSource(donor.Formation, donor.Slot, world.BattleResources.FormationSourceHash(donor.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation character = new(0, 0, [], [0]) { HitPoints = 486, Level = 7, MagicCommand = new(0, 20) };
        const string caster = "enemy-0";
        BattleInput command = new(BattleInputKind.Action, ActorId: caster, TargetId: selector == 204 ? "party-0" : caster, Ability: BattleAbility.EnemyAbility, ActionId: action);
        return new(formation, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [character], true),
            Target = new(caster, stage, model, 0, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 0),
            Driver = new(command, effect) { ExpectedNativeCommand = 8, ExpectedNativeAction = action, EnemyAbility = new(model, 0, 0, abilitySlot, 8, action, sequence, selector) }
        };
    }

    private static DiagnosticBattleRequest MadRushScenario(GameWorld world)
    {
        (int Formation, int Slot)[] donorSlots = [(388, 2), (659, 0), (92, 1), (673, 2)];
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(donor => new BattleEnemyRosterSource(donor.Formation, donor.Slot, world.BattleResources.FormationSourceHash(donor.Formation))).ToImmutableArray();
        DiagnosticGuardianForcePreparation eden = new(15, true) { LearningAbility = 27, AbilityPoints = 60 };
        DiagnosticCharacterPreparation caster = new(0, 0, [], [15]) { HitPoints = 486, Commands = [new(0, 27)] };
        BattleInput command = new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-0", Ability: BattleAbility.NativeCommand, ActionId: 24 << 16 | 3);
        return new(625, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([eden], [caster], true),
            Target = new("enemy-0", 132, 33, 0, 3, 0xC3u, 1, null, null),
            Roster = new(donors, 0),
            Driver = new(command, 273) { ExpectedNativeCommand = 24, ExpectedNativeAction = 3 }
        };
    }

    private static DiagnosticBattleRequest BlueMagicScenario(GameWorld world, ushort effect)
    {
        (int Formation, int Stage, int Model, byte Slot, ushort Action, (int Formation, int Slot)[] Donors) scenario = effect switch
        {
            71 => (25, 2, 14, 1, 0, [(825, 2), (40, 3), (574, 1), (183, 1)]),
            72 => (605, 114, 33, 0, 5, [(391, 0), (666, 0), (679, 0), (834, 1)]),
            73 => (59, 12, 15, 1, 13, [(535, 0), (281, 0), (538, 0), (527, 1)]),
            74 => (655, 128, 6, 0, 3, [(657, 0), (570, 0), (529, 0), (112, 0)]),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        var (formation, stage, model, slot, action, donorSlots) = scenario;
        ImmutableArray<BattleEnemyRosterSource> donors = donorSlots.Select(donor => new BattleEnemyRosterSource(donor.Formation, donor.Slot, world.BattleResources.FormationSourceHash(donor.Formation))).ToImmutableArray();
        DiagnosticCharacterPreparation quistis = new(3, 3, [], [0]) { HitPoints = 6, MagicCommand = new(0, 20) };
        string enemy = $"enemy-{slot}";
        BattleInput command = new(BattleInputKind.Action, ActorId: "party-3", TargetId: effect == 73 ? "party-3" : enemy, Ability: BattleAbility.LimitBreak, ActionId: 15 << 16 | action);
        return new(formation, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [quistis], true)
            {
                CombatParty = [3, 255, 255],
                Limits = new() { LearnBlueMagic = (ushort)(1 << action) }
            },
            Target = new(enemy, stage, model, slot, 3, 0xC3u, 1, null, null),
            Roster = new(donors, slot),
            Driver = new(command, effect) { ExpectedNativeCommand = 15, ExpectedNativeAction = action, LimitSelection = new(1, 1, 256) }
        };
    }
}
