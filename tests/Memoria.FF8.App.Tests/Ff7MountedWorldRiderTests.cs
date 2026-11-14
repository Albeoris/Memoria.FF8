using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7MountedWorldRiderTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(19)]
    [InlineData(41)]
    [InlineData(42)]
    public void Mounted_human_is_a_child_of_the_bird_pose_and_not_a_second_world_entity(int model)
    {
        Ff7WorldProcess state = Mounted(model);
        string before = JsonSerializer.Serialize(state);
        Ff7WorldRenderFrame frame = Ff7Scene.WorldRenderFrame(state);
        Ff7WorldVisual bird = Assert.Single(frame.Models);
        Assert.Equal(model, bird.Model);
        Assert.Equal(0, bird.RiderModel);
        Ff7WorldVisual rider = Assert.IsType<Ff7WorldVisual>(bird.CreateRider());
        Assert.Equal(0, rider.Model);
        Assert.Equal(3, rider.Animation);
        Assert.Equal(7, rider.Frame);
        Assert.Equal(bird.Position, rider.Position);
        Assert.Equal(bird.Direction, rider.Direction);
        Assert.Equal((bird.TiltX, bird.TiltY, bird.TiltZ), (rider.TiltX, rider.TiltY, rider.TiltZ));
        Assert.Equal(bird.Elevation, rider.Elevation);
        Assert.Equal(bird.Flags, rider.Flags);
        Assert.Null(rider.RiderModel);
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Ff7WorldProcess cold = JsonSerializer.Deserialize<Ff7WorldProcess>(before)!;
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(Ff7Scene.WorldRenderFrame(cold)));
        Ff7WorldVisual idle = bird with { Animation = 0 };
        Assert.Equal(2, idle.CreateRider()!.Animation);
    }

    [Fact]
    public void Rider_uses_the_bird_committed_blend_and_attachment_changes_snap()
    {
        Ff7WorldRenderFrame previous = Ff7Scene.WorldRenderFrame(Mounted(4));
        Ff7WorldVisual first = previous.Models[0];
        Ff7WorldVisual second = first with { Position = first.Position + new Vector3(120, 0, 0), Frame = 8 };
        Ff7WorldRenderFrame current = previous with { Models = [second] };
        Ff7WorldVisual sampled = Ff7WorldRenderInterpolation.Interpolate(previous, current, 0.5f).Models[0];
        Ff7WorldVisual rider = sampled.CreateRider()!;
        Assert.Equal(new Ff7FieldPoseBlend(7, 8, 0.5f), rider.AnimationBlend);
        Assert.Equal(sampled.Position, rider.Position);
        Assert.Equal(3, rider.Animation);
        Ff7WorldRenderFrame released = current with { Models = [second with { RiderModel = null }] };
        Assert.False(Ff7WorldRenderInterpolation.Continuous(previous, released));
        Assert.Equal(second.Position, Ff7WorldRenderInterpolation.Interpolate(previous, released, 0.5f).Models[0].Position);
        Assert.Null(Ff7WorldRenderInterpolation.Interpolate(previous, released, 0.5f).Models[0].AnimationBlend);
    }

    [Fact]
    public void Other_vehicle_attachments_are_not_admitted_as_Chocobo_riders()
    {
        Ff7WorldProcess state = Mounted(5) with { Chocobo = null };
        Assert.Null(Assert.Single(Ff7Scene.WorldRenderFrame(state).Models).RiderModel);
        Ff7WorldProcess legacy = Mounted(4) with { Entities = [new(0), new(4, PoseAnimation: 1, PoseFrame: 7)] };
        Assert.Equal(0, Assert.Single(Ff7Scene.WorldRenderFrame(legacy).Models).RiderModel);
        Ff7WorldProcess corrupt = Mounted(4) with { Entities = [new(0), new(4) { AttachedModel = 1 }] };
        Assert.Throws<InvalidDataException>(() => Ff7Scene.WorldRenderFrame(corrupt));
    }

    [Fact]
    public void Original_mounted_standalone_retains_its_rider_without_mutating_saved_facts()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MOUNTED_WORLD_SAVE");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), "Select the optional standalone mounted world fixture.");
        byte[] original = File.ReadAllBytes(path!);
        using MemoryStream input = new(original);
        Ff7WorldProcess state = CombinedSessionCodec.Read(input).Ff7?.World ?? throw new InvalidDataException("The mounted fixture has no original world process.");
        string saved = JsonSerializer.Serialize(state);
        Ff7WorldRenderFrame frame = Ff7Scene.WorldRenderFrame(state);
        Ff7WorldVisual bird = Assert.Single(frame.Models, model => model.Model == state.Chocobo!.Model);
        Ff7WorldVisual rider = bird.CreateRider()!;
        Assert.Equal(state.Chocobo!.RiderModel, rider.Model);
        Assert.DoesNotContain(frame.Models, model => model.Model == rider.Model);
        Assert.Equal(bird.Frame, rider.Frame);
        Assert.Equal(bird.Animation == 0 ? 2 : 3, rider.Animation);
        Assert.Equal(saved, JsonSerializer.Serialize(state));
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(Ff7Scene.WorldRenderFrame(JsonSerializer.Deserialize<Ff7WorldProcess>(saved)!)));
        Assert.Equal(original, File.ReadAllBytes(path!));
    }

    private static Ff7WorldProcess Mounted(int model)
    {
        WorldEntitySnapshot rider = new(0, 0, 0, 0, false, 0, []) { Detached = true };
        WorldSnapshot vm = new(1, "presentation", 0, 0, false, [], [], new(-1, 0, 0, 0, false, 0, []), [rider, new(model, 0, 0, 0, false, 0, [])]);
        return new(0, 149, 0, vm, [new(0, X: 10, Y: 20, Z: 30), new(model, X: 1000, Y: 400, Z: 2000, Direction: 512, Flags: 2, Elevation: 3, PoseAnimation: 1, PoseFrame: 7) { AttachedModel = 0, TiltX = 4, TiltY = 5, TiltZ = 6 }], model, true, 0, 0, null, [], -1, new(), 0, 1) { Chocobo = new(0) { Model = model }, Initialized = true };
    }
}
