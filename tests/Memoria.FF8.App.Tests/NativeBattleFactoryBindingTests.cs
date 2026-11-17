using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFactoryBindingTests
{
    [Fact]
    public void Encounter_factory_binds_original_graphics_clock_and_resources_to_the_supplied_process_owners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "The installed original battle resources are required.");
        using GameAssets assets = new(directory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).RebuildParty().Rest();
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        session.SetCharacterVitals(vitals);
        using GameWorld world = new(session, directory, sharedAssets: assets);

        BattleSession battle = world.StartDiagnosticBattle(new(512, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1 });
        BattleSnapshot entry = battle.CaptureForContinuation();
        BattleDefinition definition = entry.Definition;
        NativeBattleGraphicsDefinition graphics = Assert.IsType<NativeBattleGraphicsDefinition>(entry.ReadFixtureGraphicsDefinition());
        BattleEncounter encounter = world.BattleResources.LoadEncounter(512);
        BattleStage stage = world.BattleResources.LoadStage(encounter.StageId);
        Assert.Equal(world.BattleResources.ReadNativeClockRules(), definition.NativeClockRules);
        Assert.NotNull(graphics.PrimitiveTables);
        Assert.Equal(NativeBattlePrimitiveCapabilities.PrimaryRgba8(), graphics.PrimitiveCapabilities);
        Assert.Equal(session.State.NativeBattleMemory?.ColdExecutableSha256, definition.NativeRetainedMemory?.ColdExecutableSha256);
        Assert.Equal(battle.NativeGlobalSoundProcess, definition.NativeGlobalSoundProcess);
        Assert.Equal(battle.NativeVibrationProcess, definition.NativeVibrationProcess);
        Assert.Equal(stage.NativeSource, definition.NativeStageSource);
        Assert.Equal(stage.NativeCameraBank, definition.NativeStageCameraBank);
        Assert.Equal(stage.NativeCameraControl.ToArray(), definition.NativeStageCameraControl.ToArray());
        Assert.Equal(new[] { encounter.PrimaryCamera, encounter.AlternativeCamera }, definition.NativeStageCameraSelectors.ToArray());
        Assert.Equal(definition.Combatants.Where(actor => actor.IsPlayer || actor.InitialActive).Select(actor => actor.Id).Order(), graphics.Bindings.Select(binding => binding.ActorId).Order());
        Assert.Equal(definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).Concat(definition.Combatants.Where(actor => actor.IsPlayer)).Select(actor => actor.Id).ToArray(), graphics.InitialLoadOrder.ToArray());
        foreach (NativeBattleGraphicsBinding binding in graphics.Bindings)
        {
            BattleCombatantDefinition actor = definition.Combatants.Single(actor => actor.Id == binding.ActorId);
            binding.Resource.Validate();
            Assert.Equal(checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16)), binding.ModelId);
            Assert.NotNull(binding.InitialPosition);
            Assert.NotNull(binding.Resource.BodyTexturePack);
            Assert.NotNull(binding.Resource.Geometry);
            Assert.NotEmpty(binding.Resource.Geometry.Value);
            if (actor.IsPlayer)
            {
                Assert.NotNull(binding.Resource.ModelLoadSources);
                Assert.NotEmpty(binding.Resource.ModelLoadSources.Value);
            }
        }
    }
}
