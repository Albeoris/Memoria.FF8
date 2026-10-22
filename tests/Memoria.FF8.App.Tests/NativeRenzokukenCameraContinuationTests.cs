using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRenzokukenCameraContinuationTests
{
    [Fact]
    public void Named_standalone_Renzokuken_moves_camera_and_preserves_midpoint_continuation()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a standalone Renzokuken save, installed resources and a distinct evidence directory.");
        GameState source = GameSession.Load(input!);
        using GameAssets game = new(directory!);
        BattleSession battle = BattleSession.Restore(Assert.IsType<BattleSnapshot>(source.Battle));
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        Assert.Equal(JsonSerializer.Serialize(source.Battle!.Definition), JsonSerializer.Serialize(battle.Capture().Definition));
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, battle.Capture().Definition, true);
        string output = Path.Combine(evidence!, "renzokuken-camera-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Renzokuken camera evidence: " + output);
        using StreamWriter trace = new(Path.Combine(output, "camera.jsonl")) { AutoFlush = true };
        HashSet<string> poses = [];
        bool midpoint = false;
        bool started = false;
        bool completed = false;
        bool registeredCamera = false;
        string? previous = null;
        BattleSnapshot before = battle.Capture();
        try
        {
            Publish("before-command");
            Log("before-command");
            battle.QueueNativePlayerCommand("party-0", "enemy-0", 5, 0);
            Log("queued");
            for (int callback = 0; callback < 4096; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                Log("tick");
                if (battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect is 141 or 160 or 259 or 332 or 333)
                {
                    Assert.NotNull(battle.NativeEffectMemory!.Textures!.Animation);
                    Assert.Equal(JsonSerializer.Serialize(battle.NativeEffectMemory.Textures.Animation), JsonSerializer.Serialize(battle.NativeCamera!.ControlResource));
                    Assert.Null(battle.NativeCamera.ControlStagingSource);
                    registeredCamera = true;
                }
                started |= battle.GetNativeRenzokuken("party-0") is not null;
                poses.Add(JsonSerializer.Serialize(battle.Capture().NativeCamera!.CurrentPose));
                if (!midpoint && battle.NativeGraphics?.TriggerWidget?.Input.ContactIndex > 0)
                {
                    Publish("midpoint");
                    BattleSnapshot saved = battle.Capture();
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
                    clone.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, saved.Definition, true).Render(clone, 320, 224).ToArray());
                    midpoint = true;
                    Log("midpoint-next");
                }
                if (started && battle.GetNativeRenzokuken("party-0") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    Publish("completed");
                    File.WriteAllText(Path.Combine(evidence!, "renzokuken-completed-snapshot.txt"), Path.Combine(output, "completed.json"));
                    Console.WriteLine("Completed Renzokuken standalone: " + Path.Combine(output, "completed.json"));
                    completed = true;
                    break;
                }
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { started, midpoint, completed, registeredCamera, DistinctCameraPoses = poses.Count, battle.Frame }));
            Assert.True(started);
            Assert.True(midpoint);
            Assert.True(registeredCamera);
            Assert.True(completed, "Renzokuken must release its owners within 4096 callbacks.");
            Assert.True(poses.Count > 1, "The original Renzokuken camera must publish more than one pose.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            throw;
        }

        void Publish(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, scene.Render(battle, 320, 224).ToArray());
            GameSession session = new(source);
            session.SetLiveBattle(battle);
            session.Save(Path.Combine(output, name + ".json"), compact: true);
        }

        void Log(string phase)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleCameraState camera = snapshot.NativeCamera!;
            string signature = JsonSerializer.Serialize(new
            {
                camera.CurrentPose,
                camera.Mode,
                camera.Mask,
                camera.ControlCursor,
                camera.BlendProgress,
                ControlResource = camera.ControlResource is { } control ? new { control.Source.Identity, control.BaseOffset, control.StreamOffset, control.ModelTableOffset } : null,
                Tasks = camera.Tasks.Select(task => task is null ? null : new { task.Channel, task.ProgramOffset, task.Elapsed, task.Duration }),
                Active = snapshot.ActiveAction is { } action ? new { action.NativeHeaderIndex, action.NativeSequence, action.ActionId, action.NativeQueuedCommand } : null,
                Effect = snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect,
                Models = snapshot.NativeGraphics?.Models.Select(model => new { model.Index, model.Sequence, model.SequenceFrame, model.CameraSequence130, model.CameraAccumulator131 }),
                Bindings = snapshot.Definition.NativeGraphics?.Bindings.Select(binding => new { binding.ActorId, binding.Resource.HasActionCamera, HasBank = binding.Resource.ActionCameraBank is not null })
            });
            if (signature != previous)
            {
                trace.WriteLine(JsonSerializer.Serialize(new { snapshot.Frame, phase, State = JsonSerializer.Deserialize<JsonElement>(signature) }));
                previous = signature;
            }
        }
    }
}
