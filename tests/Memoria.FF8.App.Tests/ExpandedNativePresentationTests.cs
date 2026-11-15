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

    private static void VerifyScenario(ushort effect, Func<GameWorld, DiagnosticBattleRequest> createScenario)
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
            for (int tick = 0; tick < 1400 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || battle.GuardianForceCasts.Count != 0 || battle.ObserveDiagnosticDrainBlockers().Count != 0); tick++)
            {
                battle.Tick();
                if (effect == 6 && !rendered && battle.ExpandedPresentationProcess?.Graphics.OriginalEffect?.RequestedEffect == effect &&
                    battle.PreparedPresentation is { } prepared && prepared.Primitives.LastCommands.Any(command => command.DrawMode.TexturePage is ushort page && (page & 0x180) == 0x100))
                {
                    Assert.Equal(NativeBattlePcDirectTextureFormat.Rgb555, prepared.PrimitiveCapabilities?.DirectTextureFormat);
                    Assert.NotEmpty(scene.Render(battle, 320, 240));
                    rendered = true;
                }
            }
            Assert.Empty(battle.ObserveDiagnosticDrainBlockers());
            if (effect == 6)
            {
                Assert.True(rendered);
            }
            else
            {
                string?[] recipients = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == driver.Input.ActorId).Select(entry => entry.TargetId).ToArray();
                Assert.Equal(11, recipients.Length);
                Assert.Equal(11, recipients.Distinct().Count());
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
}
