using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModule5707A0PresentationTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(41)]
    public void Declared_original_module_on_loaded_Wedge_runs_wrapper_child_packets_and_cold_next_callback(byte resourceId)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleEncounter encounter = assets.LoadEncounter(29);
        Assert.Equal(3, encounter.StageId);
        BattleEnemyPlacement wedge = Assert.Single(encounter.Slots, row => row.Slot == 1);
        Assert.Equal(73, wedge.ModelId);
        Assert.True(wedge.Enabled);
        Assert.True(wedge.Hidden);
        Assert.True(wedge.InitiallySuspended);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        List<BattleCombatantDefinition> actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, Level = 7, InitialAtb = BattleSession.AtbMaximum }];
        foreach (BattleEnemyPlacement placement in encounter.Slots.Take(3))
        {
            actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot, InitialActive = placement.Enabled, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable });
        }
        BattleCombatantDefinition[] definitions = actors.ToArray();
        BattleDefinition definition = new()
        {
            EncounterId = encounter.Id,
            StageId = encounter.StageId,
            Combatants = definitions,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(0),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeScannedModels = game.ReadNewGameScannedModels(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(definitions, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, directory, 0, 5, -1);
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        for (int frame = 0; frame < 100 && !session.NativeGraphics!.EntryReady; frame++)
        {
            session.Tick();
        }
        Assert.True(session.NativeGraphics!.EntryReady);
        BattleSnapshot original = session.Capture();
        int actor = original.NativeGraphics!.ActorIds.IndexOf("enemy-1");
        Assert.Equal(4, actor);
        NativeBattleGraphicsModel model = original.NativeGraphics.Models[actor];
        Assert.Equal((byte)89, model.ModelId4);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.CurrentBodyBoneMatrices);
        Assert.Null(original.NativeGraphics.ModuleEffects.Head);
        NativeBattleModuleResource resource = original.Definition.NativeModuleResources[resourceId];
        Assert.Equal(0x570CC0u, resource.WrapperCallback);
        Assert.Equal(0x5707A0u, resource.Callback);
        // This is a declared component append; it does not insert an opcode into the original Wedge sequence.
        session = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(original with { NativeGraphics = original.NativeGraphics with { ModuleEffects = NativeBattleStorageSnapshots.Restore(original.NativeGraphics.ModuleEffects).AppendModelResource(actor, resource, 0x100000u | resourceId) } }));
        List<ushort> phases = [];
        List<object> receipts = [];
        int commands = 0;
        int callbacks = 0;
        while (session.NativeGraphics!.ModuleEffects.Head is not null)
        {
            Assert.True(callbacks < 12, "The selected original module did not retire within its four-phase component lifecycle.");
            BattleSnapshot before = session.Capture();
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
            session.AdvanceNativeGraphicsPresentation(Callback);
            restored.AdvanceNativeGraphicsPresentation(Callback);
            BattleSnapshot after = session.Capture();
            Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
            foreach (NativeBattleModuleDraw draw in after.NativeGraphics!.ModuleDraws.Where(draw => draw.Kind.ToString() == "Impact570860"))
            {
                phases.Add(draw.Phase);
            }
            NativeBattlePrimitiveFrame frame = after.NativeGraphics!.PrimitiveFrame ?? throw new InvalidDataException("The selected module requires its actual primitive frame.");
            commands += frame.LastCommands.Count(command => command.Command.ModuleDraw is not null);
            receipts.Add(new { Callback = callbacks, after.NativeGraphics.ModuleEffects, after.NativeGraphics.ModuleDraws, ModuleCommands = frame.LastCommands.Where(command => command.Command.ModuleDraw is not null).ToArray(), after.NativeEffectMemory });
            callbacks++;
        }
        Assert.Equal(new ushort[] { 0, 1, 2, 3 }, phases);
        Assert.True(commands > 0);
        Assert.Null(session.ActiveAction);
        Assert.Empty(session.QueuedActions);
        BattleSnapshot completed = session.Capture();
        session = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
        session.AdvanceNativeGraphicsPresentation(Callback);
        Assert.Null(session.NativeGraphics!.ModuleEffects.Head);
        Assert.DoesNotContain(session.NativeGraphics.ModuleDraws, draw => draw.Kind.ToString() == "Impact570860");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODULE5707A0_EVIDENCE");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"module-{resourceId}-before.battle.json"), JsonSerializer.Serialize(original));
            File.WriteAllText(Path.Combine(output, $"module-{resourceId}-completed.battle.json"), JsonSerializer.Serialize(completed));
            File.WriteAllText(Path.Combine(output, $"module-{resourceId}-next.battle.json"), JsonSerializer.Serialize(session.Capture()));
            File.WriteAllText(Path.Combine(output, $"module-{resourceId}-callbacks.json"), JsonSerializer.Serialize(new { Scenario = "Declared original common module on actual loaded Tower Wedge; original sequence and dormant actor flags retained", ResourceId = resourceId, Actor = actor, Model = 73, Callbacks = callbacks, Phases = phases, Commands = commands, Receipts = receipts }));
        }
    }

    private static readonly TimeSpan Callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);
}
