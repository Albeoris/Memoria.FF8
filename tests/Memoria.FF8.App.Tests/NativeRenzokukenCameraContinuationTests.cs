using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRenzokukenCameraContinuationTests
{
    [Fact]
    public void Renzokuken_from_a_pending_encounter_moves_camera_and_preserves_private_midpoint_continuation()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a pre-battle encounter save, installed resources and a distinct evidence directory.");
        GameState source = GameSession.Load(input!);
        Assert.SkipUnless(source.Battle is null && source.Scene is (SceneKind.Field or SceneKind.WorldMap), "Select a pre-battle field or world save with a pending encounter and an eligible Renzokuken command.");
        GameSession hostSession = new(source);
        using GameWorld world = new(hostSession, directory!);
        world.EnsureField();
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party-0"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party-0"));
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, battle.Capture().Definition);
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
                if (battle.Presentation?.Graphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect && battle.GetNativeRenzokuken("party-0")?.MagicId == effect.Binding.Effect)
                {
                    Assert.NotNull(battle.Presentation!.Memory.Textures!.Animation);
                    Assert.Equal(JsonSerializer.Serialize(battle.Presentation!.Memory.Textures.Animation), JsonSerializer.Serialize(battle.Presentation!.Camera.ControlResource));
                    Assert.Null(battle.Presentation!.Camera.ControlStagingSource);
                    registeredCamera = true;
                }
                started |= battle.GetNativeRenzokuken("party-0") is not null;
                poses.Add(JsonSerializer.Serialize(battle.CaptureForContinuation().ReadFixturePresentation().NativeCamera!.CurrentPose));
                if (!midpoint && battle.Presentation?.Graphics?.TriggerWidget?.Input.ContactIndex > 0)
                {
                    Publish("midpoint");
                    BattleSession clone = battle.ForkForContinuation();
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, clone.ReadDefinition()).Render(clone, 320, 224).ToArray());
                    midpoint = true;
                    Log("midpoint-next");
                }
                if (started && battle.GetNativeRenzokuken("party-0") is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Presentation?.Graphics?.OriginalEffect is { ReturnedDescriptor: 0 })
                {
                    Publish("completed");
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
        }

        void Log(string phase)
        {
            BattleSnapshot snapshot = battle.CaptureForContinuation();
            NativeBattleCameraState camera = snapshot.ReadFixturePresentation().NativeCamera!;
            string signature = JsonSerializer.Serialize(new
            {
                camera.CurrentPose,
                camera.Mode,
                camera.Mask,
                camera.ControlCursor,
                camera.BlendProgress,
                ControlResource = camera.ControlResource is { } control ? new { control.Source.Identity, control.BaseOffset, control.StreamOffset, control.ModelTableOffset } : null,
                Tasks = camera.Tasks.Select(task => task is null ? null : new { task.Channel, task.ProgramOffset, task.Elapsed, task.Duration }),
                Active = snapshot.ActiveAction is { } action ? new { NativeHeader = snapshot.ReadFixturePresentation().NativeGraphics?.ActionGraphics?.Header, action.NativeSequence, action.ActionId, action.NativeQueuedCommand } : null,
                Effect = snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect?.Binding.Effect,
                Models = snapshot.ReadFixturePresentation().NativeGraphics is { } graphics ? ReadOriginalModelCells(graphics).Select((model, slot) => new { Index = slot, model.Sequence, model.SequenceFrame, model.CameraSequence130, model.CameraAccumulator131 }) : null,
                Bindings = snapshot.ReadFixtureGraphicsDefinition()?.Bindings.Select(binding => new { binding.ActorId, binding.Resource.HasActionCamera, HasBank = binding.Resource.ActionCameraBank is not null })
            });
            if (signature != previous)
            {
                trace.WriteLine(JsonSerializer.Serialize(new { snapshot.Frame, phase, State = JsonSerializer.Deserialize<JsonElement>(signature) }));
                previous = signature;
            }
        }
    }
}
