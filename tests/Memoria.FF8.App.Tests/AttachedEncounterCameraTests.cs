using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AttachedEncounterCameraTests
{
    [Fact]
    public void Installed_encounter_publishes_its_compiled_camera_elevation()
    {
        const double degrees = 12;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) =>
        {
            BattleCombatantDefinition original = definition.Combatants.First(actor => !actor.IsPlayer && actor.InitialActive);
            EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), []);
            EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", original.Id, 11, 11, [], true));
            return definition with { EncounterMod = compiled with { Camera = new(degrees) } };
        });
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, 92, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        for (int callback = 0; callback < 1200 && (!battle.NativeEntryReady || battle.QueuedActions.Count != 0 || battle.ActiveAction is not null); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        Assert.True(battle.NativeEntryReady);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.NotNull(battle.Presentation);
        Assert.Equal(degrees, battle.ReadDefinition().EncounterMod!.Camera!.ElevationDegrees);
        NativeBattleCameraState camera = battle.Presentation!.Camera;
        NativeBattleCameraPose source = Assert.IsType<NativeBattleCameraPose>(camera.CurrentPose) with { Roll = camera.Roll };
        NativeBattleCameraPose expected = EncounterCameraPresentation.Elevate(source, degrees);
        Assert.NotEqual(source.Eye, expected.Eye);
        Assert.Equal(expected, camera.PublishedPresentationPose);
        NativeBattleActionMatrix expectedView = NativeBattlePoseMatrices.CameraView(expected);
        NativeBattleActionMatrix actualView = Assert.IsType<NativeBattleActionMatrix>(camera.Projection!.View);
        Assert.Equal(expectedView.Rotation.ToArray(), actualView.Rotation.ToArray());
        Assert.Equal(expectedView with { Rotation = actualView.Rotation }, actualView);
    }
}
