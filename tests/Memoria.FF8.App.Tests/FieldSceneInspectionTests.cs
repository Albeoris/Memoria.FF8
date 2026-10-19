using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldSceneInspectionTests
{
    [Fact]
    public void Loaded_scene_inventory_matches_original_layers_objects_and_scripts_without_changing_vm_state()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.FieldSceneInspection.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        JsonElement before = world.Events.Capture();

        SceneInspectorSnapshot snapshot = Assert.IsType<SceneInspectorSnapshot>(world.ReadSceneInspection());
        FieldAssets field = Assert.IsType<FieldAssets>(world.Field);
        Assert.Equal(field.Id, snapshot.FieldId);
        Assert.Equal(field.Name, snapshot.FieldName);
        Assert.Equal(world.Events.Frame, snapshot.Frame);
        Assert.Equal(snapshot.Entries.Count, snapshot.Entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(field.Background.Select(tile => (tile.Layer, tile.AnimationId, tile.AnimationState)).Distinct().Count(), snapshot.Entries.Count(entry => entry.Kind == SceneInspectorKind.Layer));
        Assert.Equal(world.Events.Actors.Count, snapshot.Entries.Count(entry => entry.Kind == SceneInspectorKind.Object));
        Assert.Equal(world.Events.InspectScripts().Count, snapshot.Entries.Count(entry => entry.Kind == SceneInspectorKind.Event));
        foreach (ActorState actor in world.Events.Actors)
        {
            SceneInspectorEntry entry = Assert.Single(snapshot.Entries, entry => entry.Id == $"object:{actor.ObjectId}");
            Assert.Contains($"Native active: {actor.Active}; visible: {actor.Visible}", entry.Details);
            Assert.Contains($"Talk enabled: {actor.TalkEnabled}; native model: {actor.RenderModelId}", entry.Details);
        }
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Empty(world.Events.ActionLog);
    }

    [Fact]
    public void Temporary_layer_object_and_event_toggles_preserve_native_state_and_reset_restores_rendering()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.FieldSceneInspection.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        byte[] baseline = world.Render(TimeSpan.Zero).ToArray();
        SceneInspectorSnapshot snapshot = world.ReadSceneInspection()!;
        SceneInspectorEntry background = snapshot.Entries.First(entry => entry.Kind == SceneInspectorKind.Layer && entry.Enabled);
        SceneInspectorEntry actor = snapshot.Entries.First(entry => entry.Kind == SceneInspectorKind.Object && entry.Enabled);
        SceneInspectorEntry script = snapshot.Entries.First(entry => entry.Kind == SceneInspectorKind.Event);

        world.ApplySceneInspection(new(background.Id, false));
        world.ApplySceneInspection(new(actor.Id, false));
        world.ApplySceneInspection(new(script.Id, false));

        SceneInspectorSnapshot changed = world.ReadSceneInspection()!;
        Assert.All(new[] { background.Id, actor.Id, script.Id }, id => Assert.False(changed.Entries.Single(entry => entry.Id == id).Enabled));
        Assert.False(baseline.SequenceEqual(world.Render(TimeSpan.Zero)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Null(session.State.DebugView);

        world.ApplySceneInspection(new(Reset: true));
        Assert.Equal(baseline, world.Render(TimeSpan.Zero));
        Assert.Equal(snapshot.Entries.Select(entry => (entry.Id, entry.Enabled)), world.ReadSceneInspection()!.Entries.Select(entry => (entry.Id, entry.Enabled)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Empty(world.Events.ActionLog);
        Assert.Throws<ArgumentException>(() => world.ApplySceneInspection(new("object:2147483647", false)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    [Fact]
    public void Suspended_script_preserves_instruction_operands_and_wait_until_explicitly_resumed()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(600, Program([], [(Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300)]), []);
        engine.Step();
        engine.Step();
        JsonElement waiting = DefaultFiber(engine.Capture());
        Assert.Equal("Frames", waiting.GetProperty("WaitReason").GetString());
        Assert.Equal(new[] { 99 }, waiting.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()));

        engine.IsInspectionScriptEnabled = (_, script) => script != 1;
        for (int index = 0; index < 5; index++)
        {
            engine.Step();
        }
        Assert.True(JsonElement.DeepEquals(waiting, DefaultFiber(engine.Capture())));
        Assert.False(session.State.Variables.ContainsKey(1300));

        engine.IsInspectionScriptEnabled = null;
        engine.Step();
        Assert.Equal(100, session.State.Variables[1300]);
        FieldScriptInspection finished = Assert.Single(engine.InspectScripts(), script => script.ScriptId == 1);
        Assert.Equal(0, finished.RunningCount);
        Assert.Equal("Idle", finished.Status);
        Assert.Null(engine.LastFault);
    }

    [Fact]
    public void Installed_scene_has_an_isolated_picture_module_and_a_local_default_cid_model()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(175);
        Assert.Equal("bghall_6", field.Name);
        BackgroundTile[] tiles = field.Background.Where(tile => tile.AnimationId == 5).ToArray();
        Assert.Equal(11, tiles.Length);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(0, tile.Layer);
            Assert.Equal(0, tile.AnimationState);
            Assert.True(tile.Draw);
        });
        FieldModel model = field.Models[9];
        Assert.Equal("p006??x", model.Name);
        Assert.True(model.Faces.Count > 0);
        Assert.Equal(40, model.Animations[1].Frames.Count);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        Assert.Equal(5, program.Objects[21].BackgroundModuleIndex);
        Assert.Equal(73, program.Objects[8].Label);
        Assert.Equal(77, program.Objects[9].Label);
    }

    [Fact]
    public void Imported_model_debug_playback_uses_donor_animation_frames_and_mod_removal_restores_native_rendering()
    {
        string source = TestResources.FixturePath("App.Tests.FieldSceneInspection.1");
        string directory = TestResources.GameDirectory;
        GameSession baselineSession = new(GameSession.Load(source));
        using GameWorld baselineWorld = new(baselineSession, directory);
        baselineWorld.EnsureField();
        FieldAssets field = baselineWorld.Field!;
        ActorState original = baselineWorld.Events.Actors.First(actor => actor.Active && actor.Visible && actor.RenderModelId >= 0 && field.GetAnimationFrameCount(actor.RenderModelId, 1) < 40);
        JsonElement native = baselineWorld.Events.Capture();
        byte[] baseline = baselineWorld.Render(TimeSpan.Zero).ToArray();
        FieldScenePatch patch = new()
        {
            SchemaVersion = 1,
            FieldId = field.Id,
            FieldName = field.Name,
            Actors = [new FieldActorPatch { ObjectId = original.ObjectId, Model = new FieldModelReference { FieldId = 175, ModelId = 9 }, AnimationId = 1 }]
        };
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(patch, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        ModFile file = new("Memoria/animation-example", ModFormat.Memoria, $"fields/{field.Name}.json", 1, () => content);
        Assert.True(FieldSceneMods.TryPrepare(new ModCatalogSnapshot(1, [], [file]), out FieldSceneMods current, out var diagnostics));
        Assert.Empty(diagnostics);

        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory, readSceneMods: () => current);
        world.EnsureField();
        ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == original.ObjectId);
        Assert.True(world.CreateVisual(actor).ModelId >= field.Models.Count);
        Assert.Equal(1, world.CreateVisual(actor).AnimationId);
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));
        byte[] patchedBaseline = world.Render(TimeSpan.Zero).ToArray();

        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, AnimationId: 1, Frame: 39, Playing: true, FramesPerSecond: 40));
        world.AdvanceDebug(TimeSpan.FromSeconds(0.05));
        Assert.Equal(1, session.State.DebugView!.Actors[actor.ObjectId].Frame);
        Assert.Equal(1, world.CreateVisual(actor).AnimationFrame);
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));

        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Clear));
        Assert.Null(session.State.DebugView);
        Assert.Equal(patchedBaseline, world.Render(TimeSpan.Zero));
        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, X: actor.X + 1, Visible: true, AnimationId: 1, Frame: 39, Playing: true, FramesPerSecond: 40));
        current = FieldSceneMods.Empty;
        world.AdvanceDebug(TimeSpan.FromSeconds(0.05));
        world.Render(TimeSpan.Zero);
        DebugActorView retained = session.State.DebugView!.Actors[actor.ObjectId];
        Assert.Equal(actor.X + 1, retained.X);
        Assert.True(retained.Visible);
        Assert.Null(retained.AnimationId);
        Assert.Null(retained.Frame);
        Assert.False(retained.Playing);
        Assert.Equal(original.RenderModelId, world.CreateVisual(actor).ModelId);
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));
        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Clear));
        Assert.Equal(baseline, world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));
        Assert.Empty(world.Events.ActionLog);
    }

    private static JsonElement DefaultFiber(JsonElement snapshot)
    {
        return snapshot.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).Clone();
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        List<ushort> positions = [];
        for (int index = 0; index < scripts.Length; index++)
        {
            positions.Add(checked((ushort)operations.Count));
            operations.Add((Jsm.Opcode.LBL, index));
            operations.AddRange(scripts[index]);
            operations.Add((Jsm.Opcode.RET, 8));
        }
        positions.Add(checked((ushort)operations.Count));
        int offset = 10 + positions.Count * 2;
        byte[] bytes = new byte[offset + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)offset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)(scripts.Length - 1)));
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.LBL or Jsm.Opcode.CAL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
