using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRenzokukenFinisherCameraContinuationTests
{
    [Fact]
    public void Named_standalone_finisher_moves_camera_and_preserves_midpoint_continuation()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_RENZOKUKEN_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select a standalone Renzokuken save, installed resources and a distinct evidence directory.");
        GameState source = GameSession.Load(input!);
        using GameAssets game = new(directory!);
        BattleSession battle = BattleSession.Restore(Assert.IsType<BattleSnapshot>(source.Battle));
        BattleSnapshot selected = battle.Capture();
        byte crisis = Assert.Single(selected.NativeLimits, limit => limit.ActorId == "party-0").CrisisLevel;
        BattleCombatantDefinition actor = selected.Definition.Combatants.Single(combatant => combatant.Id == "party-0");
        Assert.Equal((byte)1, actor.NativePlayerStatRules!.WeaponRecord[2]);
        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
        {
            candidate.Draw(out _).Draw(out byte finish);
            return NativeBattleRenzokukenRules.HasFinisher(crisis, finish);
        });
        battle = BattleSession.Restore(selected with { Random = random });
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        Assert.Equal(JsonSerializer.Serialize(source.Battle!.Definition), JsonSerializer.Serialize(battle.Capture().Definition));
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, battle.Capture().Definition, true);
        string output = Path.Combine(evidence!, "renzokuken-finisher-camera-" + Guid.NewGuid().ToString("N"));
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
                if (battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == 163)
                {
                    Assert.NotNull(battle.NativeEffectMemory!.Textures!.Animation);
                    Assert.Equal(JsonSerializer.Serialize(battle.NativeEffectMemory.Textures.Animation), JsonSerializer.Serialize(battle.NativeCamera!.ControlResource));
                    Assert.Null(battle.NativeCamera.ControlStagingSource);
                    registeredCamera = true;
                }
                started |= battle.GetNativeRenzokuken("party-0") is not null;
                if (battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 163, ReturnedDescriptor: > 0 })
                {
                    poses.Add(JsonSerializer.Serialize(battle.NativeCamera!.CurrentPose));
                }
                if (!midpoint && battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 163, ReturnedDescriptor: > 0 } live && live.Pools[1].Slots[0].ReadWord(12) >= 50)
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
                    File.WriteAllText(Path.Combine(evidence!, "renzokuken-finisher-completed-snapshot.txt"), Path.Combine(output, "completed.json"));
                    Console.WriteLine("Completed Renzokuken standalone: " + Path.Combine(output, "completed.json"));
                    completed = true;
                    for (int next = 0; next < 4096 && !battle.CanChoosePlayerCommand("party-0"); next++)
                    {
                        battle.Tick();
                    }
                    Assert.True(battle.CanChoosePlayerCommand("party-0"));
                    battle.QueueNativePlayerCommand("party-0", "enemy-0", 1, 0);
                    bool nextStarted = false;
                    bool nextCompleted = false;
                    for (int next = 0; next < 4096; next++)
                    {
                        battle.Tick();
                        nextStarted |= battle.ActiveAction?.ActorId == "party-0";
                        if (nextStarted && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                        {
                            nextCompleted = true;
                            Publish("following-attack-completed");
                            break;
                        }
                    }
                    Assert.True(nextCompleted, "The following physical attack must release after the finisher camera.");
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
