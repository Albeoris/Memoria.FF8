using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowWorldPreviewTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP authored snow-world preview: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Fixture snow = new(installation, 3);
        Set(snow.World, "snow", new Ff7WorldSnowState(0, 64, 64, 16, 283));
        Ff7AutoPlayWorldMovementPreview windy = CompareNativePhases(snow, new(Vertical: -1));
        Check(snow.World.Process.Snow is { Countdown: 63, Layers.Length: 2 }, "The actual wind boundary did not create its native wind state and layer setup.");
        Check(windy.State!.Input.CameraHeadingTarget == 16, "The projected wind boundary lost its actual camera-heading correction.");
        CheckDetachedCapture(snow);
        Ff7AutoPlayWorldMovementPreview next = CompareNativePhases(snow, default, windy.State);
        Check(snow.World.Process.Snow is { Countdown: 62 }, "An idle projected step did not retain the already-active storm.");

        Set(snow.World, "snow", new Ff7WorldSnowState(0, 1, 64, -16, 283));
        Ff7AutoPlayWorldMovementState beforeCycle = snow.World.CaptureAutoPlayMovementState();
        Ff7AutoPlayWorldMovementPreview cycle = CompareNativePhases(snow, new(Horizontal: 1));
        Check(snow.World.Process.Snow is { Countdown: >= 128, WindLength: >= 64 and <= 95 }, "The actual expired wind cycle did not generate its next native cycle.");
        Check(cycle.State!.Random is not null && (beforeCycle.Random is null || cycle.State.RandomIndex != beforeCycle.RandomIndex || !cycle.State.Random.SequenceEqual(beforeCycle.Random)), "The projected new wind cycle omitted its actual RNG continuation.");
        Set(snow.World, "scriptCamera", new Ff7WorldScriptCameraState(1, 16, 1, 2, 1200, 2000));
        Ff7AutoPlayWorldMovementPreview scripted = CompareNativePhases(snow, default);
        Check(scripted.State!.ScriptCamera is { TargetPitch: 1201, TargetDistance: 2002 }, "The native pre-VM script-camera continuation was omitted from the projection.");

        string unchanged = JsonSerializer.Serialize(snow.World.Process);
        Check(!snow.World.PreviewAutoPlayInput(default, next.State! with { WorldId = 0 }).Supported, "An unrelated world allocation authorized a snow preview.");
        Check(unchanged == JsonSerializer.Serialize(snow.World.Process), "Rejecting an unrelated source changed the authoritative world.");
        Set(snow.World, "snow", new Ff7WorldSnowState(0, 130, 64, 16, 283));
        Ff7AutoPlayWorldMovementState missingPole = snow.World.CaptureAutoPlayMovementState();
        string beforeMarker = JsonSerializer.Serialize(new { Process = snow.World.Process, Vm = snow.World.Machine.Capture(), Source = Snapshot(missingPole) });
        Check(!snow.World.PreviewAutoPlayInput(new(Held: 32), missingPole).Supported, "A new native marker allocation was silently admitted without its required VM boundary.");
        Check(beforeMarker == JsonSerializer.Serialize(new { Process = snow.World.Process, Vm = snow.World.Machine.Capture(), Source = Snapshot(missingPole) }), "Rejecting a new marker allocation changed native state or its projected source.");
        snow.AttachPole();
        Ff7AutoPlayWorldMovementPreview marker = CompareNativePhases(snow, new(Held: 32));
        Ff7AutoPlayWorldMovementState markerState = marker.State!;
        Check(markerState.Snow is { PoleIndex: 1 } && markerState.SnowPoles is { Length: 1 } && markerState.SnowPoles[0].Model == 21 && markerState.SnowPoles[0].PresentationEpoch > 0 && (markerState.SnowPoles[0].X != 7000 || markerState.SnowPoles[0].Z != 7000), "The supported existing marker reposition lost its native pole continuation.");
        CheckDetachedCapture(snow);
        CheckKeys(markerState);
        using Fixture foot = new(installation, 0);
        CompareNativePhases(foot, new(Horizontal: 1));
        Check(foot.World.Process.Snow is null, "Ordinary foot movement acquired snow-world state.");
        Ff7AutoPlayWorldMovementState footState = foot.World.CaptureAutoPlayMovementState();
        Ff7AutoPlayWorldMovementState facing = footState with { Player = footState.Player with { Direction = footState.Player.Direction + 1, TurnOffset = footState.Player.TurnOffset + 1 } };
        Check(Ff7AutoplayDriver.WorldSearchKey(footState) == Ff7AutoplayDriver.WorldSearchKey(facing) && Ff7AutoplayDriver.WorldMovementKey(footState) != Ff7AutoplayDriver.WorldMovementKey(facing), "Ordinary foot search must retain its existing visual-facing merge while exact route validation preserves facing.");
        Console.WriteLine("Snow-world preview matches actual native physical phases across wind boundaries without mutating the source.");
    }

    private static Ff7AutoPlayWorldMovementPreview CompareNativePhases(Fixture fixture, Ff7AutoplayInput action, Ff7AutoPlayWorldMovementState? projectedSource = null)
    {
        Ff7WorldRuntime world = fixture.World;
        Ff7AutoPlayWorldMovementState source = projectedSource ?? world.CaptureAutoPlayMovementState();
        string before = JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), Source = Snapshot(source) });
        Ff7AutoPlayWorldMovementPreview preview = world.PreviewAutoPlayInput(action, source);
        Check(preview is { Supported: true, RequiresVmPhase: true, State: not null }, $"A supported authored physical phase was rejected: {preview.Reason}");
        Check(before == JsonSerializer.Serialize(new { Process = world.Process, Vm = world.Machine.Capture(), Source = Snapshot(source) }), "Preview changed authoritative wind, RNG, input, camera, models, VM or its retained source.");
        Check(fixture.Meshes.Count == 1 && ReferenceEquals(fixture.Meshes[(0, 0)], fixture.Mesh), "Preview changed the retained collision mesh allocation.");
        Ff7AutoPlayWorldMovementPreview repeat = world.PreviewAutoPlayInput(action, source);
        Check(JsonSerializer.Serialize(preview) == JsonSerializer.Serialize(repeat) && Snapshot(preview.State) == Snapshot(repeat.State), "Identical snow preview sources did not reproduce the same native phase.");
        world.SetButtons(action.Held, (ushort)(action.Held & ~source.Input.Held));
        world.SetMovement(action.Horizontal, action.Vertical, action.Run);
        Invoke(world, "AdvancePlayerInputCore", false);
        Invoke(world, "AdvanceWorldCameraHeading");
        Invoke(world, "AdvanceScriptCamera");
        Invoke(world, "AdvanceWorldScriptFacing");
        Invoke(world, "ResolvePlayerTerrain", false, null, false);
        string projected = PhysicalSnapshot(preview.State!);
        string actual = PhysicalSnapshot(world.CaptureAutoPlayMovementState());
        Check(projected == actual, "Projected player, wind, RNG, input or camera differs from the actual native physical phases: " + FirstDifference(projected, actual));
        Check(JsonSerializer.Serialize(preview.State!.Snow) == JsonSerializer.Serialize(world.Process.Snow), "The projected physical state omitted its actual snow and layer continuation.");
        return preview;
    }

    private static string Snapshot(Ff7AutoPlayWorldMovementState? state)
    {
        return JsonSerializer.Serialize(new { Movement = state, Snow = state?.Snow, Poles = state?.SnowPoles, ScriptCamera = state?.ScriptCamera });
    }

    private static string PhysicalSnapshot(Ff7AutoPlayWorldMovementState state)
    {
        // Actual terrain resolution also advances display animation after the predicted physical phases.
        Ff7AutoPlayWorldMovementState physical = state with
        {
            Player = state.Player with { AnimationFrame = 0, PoseAnimation = 0, PoseFrame = 0, FrameRemainder = 0 },
            SnowPoles = null
        };
        return JsonSerializer.Serialize(new
        {
            Movement = physical,
            Poles = state.SnowPoles?.Select(pole => new { pole.Model, pole.X, pole.Z, pole.Direction, pole.TargetDirection, pole.ScriptDirection, pole.TurnOffset }).ToArray()
        });
    }

    private static string FirstDifference(string projected, string actual)
    {
        using JsonDocument left = JsonDocument.Parse(projected);
        using JsonDocument right = JsonDocument.Parse(actual);
        return Find(left.RootElement, right.RootElement, "state") ?? "none";

        static string? Find(JsonElement left, JsonElement right, string path)
        {
            if (left.ValueKind == right.ValueKind && left.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(property.Name, out JsonElement value))
                    {
                        return path + "." + property.Name + " missing";
                    }
                    string? difference = Find(property.Value, value, path + "." + property.Name);
                    if (difference is not null)
                    {
                        return difference;
                    }
                }
                return null;
            }
            if (left.ValueKind == right.ValueKind && left.ValueKind == JsonValueKind.Array && left.GetArrayLength() == right.GetArrayLength())
            {
                for (int index = 0; index < left.GetArrayLength(); index++)
                {
                    string? difference = Find(left[index], right[index], path + "[" + index + "]");
                    if (difference is not null)
                    {
                        return difference;
                    }
                }
                return null;
            }
            string a = left.GetRawText();
            string b = right.GetRawText();
            return a == b ? null : $"{path}: preview={a[..Math.Min(a.Length, 100)]}, actual={b[..Math.Min(b.Length, 100)]}";
        }
    }

    private static void CheckDetachedCapture(Fixture fixture)
    {
        string before = JsonSerializer.Serialize(fixture.World.Process);
        Ff7AutoPlayWorldMovementState detached = fixture.World.CaptureAutoPlayMovementState();
        if (detached.Snow?.Layers is { Length: > 0 } layers)
        {
            layers[0] = layers[0] with { Brightness = 127 };
        }
        if (detached.SnowPoles is { Length: > 0 } poles)
        {
            poles[0] = poles[0] with { X = poles[0].X + 1 };
        }
        if (detached.Random is { Length: > 0 } random)
        {
            random[0] ^= 1;
        }
        Check(before == JsonSerializer.Serialize(fixture.World.Process), "Captured wind layers, marker records or RNG bytes alias authoritative state.");
    }

    private static void CheckKeys(Ff7AutoPlayWorldMovementState state)
    {
        string key = Ff7AutoplayDriver.WorldSearchKey(state);
        Ff7WorldSnowState snow = state.Snow!;
        Ff7WorldEntity pole = state.SnowPoles![0];
        byte[] changedRandom = state.Random!.ToArray();
        changedRandom[0] ^= 1;
        Ff7AutoPlayWorldMovementState[] different =
        [
            state with { Snow = snow with { Countdown = snow.Countdown - 1 } },
            state with { RandomIndex = state.RandomIndex + 1 },
            state with { Random = changedRandom },
            state with { Camera = state.Camera with { Heading = state.Camera.Heading + 1 } },
            state with { Player = state.Player with { Direction = state.Player.Direction + 1 } },
            state with { Player = state.Player with { TurnOffset = state.Player.TurnOffset + 1 } },
            state with { SnowPoles = [pole with { X = pole.X + 1 }] },
            state with { ScriptCamera = state.ScriptCamera! with { RotationSpeed = state.ScriptCamera!.RotationSpeed + 1 } }
        ];
        Check(different.All(candidate => Ff7AutoplayDriver.WorldSearchKey(candidate) != key), "Snow search merged different native wind, RNG, facing, camera or marker continuations.");
    }

    private static void Set(Ff7WorldRuntime world, string field, object value)
    {
        typeof(Ff7WorldRuntime).GetField(field, Private)!.SetValue(world, value);
    }

    private static void Invoke(Ff7WorldRuntime world, string method, params object?[] arguments)
    {
        typeof(Ff7WorldRuntime).GetMethod(method, Private)!.Invoke(world, arguments);
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(Ff7Installation installation, int world)
        {
            World = new(installation, new byte[1280], 0, [0, 1, 2], worldId: world);
            Set(World, "controlledModel", 0);
            Set(World, "initialized", true);
            Set(World, "mainState", 1);
            Set(World, "movementEnabled", true);
            var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(World)!;
            entities[0] = new(0, 4096, 0, 4096, TerrainResolved: true);
            World.Machine.Restore(World.Machine.Capture() with { Models = [new WorldEntitySnapshot(0, 0, 0, 0, false, 0, [])] });
            Mesh = new(0, 0, [new(0, 0, 0), new(8192, 0, 0), new(8192, 0, 8192), new(0, 0, 8192)], [], [new(0, 1, 2, 0, default, default, default, 0), new(0, 2, 3, 0, default, default, default, 0)]);
            Meshes = (Dictionary<(int, int), PcWorldMesh>)typeof(Ff7WorldRuntime).GetField("collisionMeshes", Private)!.GetValue(World)!;
            Meshes[(0, 0)] = Mesh;
        }

        internal Ff7WorldRuntime World { get; }
        internal PcWorldMesh Mesh { get; }
        internal Dictionary<(int, int), PcWorldMesh> Meshes { get; }

        internal void AttachPole()
        {
            var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(World)!;
            entities[21] = new(21, 7000, 0, 7000, TerrainResolved: true);
            WorldSnapshot vm = World.Machine.Capture();
            World.Machine.Restore(vm with { Models = [.. vm.Models.Where(model => model.Model != 21), new WorldEntitySnapshot(21, 0, 0, 0, false, 0, [])] });
        }

        public void Dispose()
        {
            World.Dispose();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
