using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDormantDirectScrollTests
{
    [Fact]
    public void ActualAuditoriumDirectRowZeroKeepsSelectedCameraOneIndependent()
    {
        string source = @"../fixtures/external-fixture.json";
        Assert.SkipUnless(File.Exists(source), "The genuine auditorium preaction snapshot is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement checkpoint = document.RootElement.GetProperty("EventState");
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = checkpoint.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        ActorState[] actors = checkpoint.GetProperty("Actors").Deserialize<ActorState[]>(options)!;
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        FieldAssets field = assets.LoadField(688);
        JsmProgram original = JsmProgram.Read(field.Jsm);
        Assert.Equal("DSCROLL", original.Operations[210].Opcode.ToString());
        Assert.Equal(1, scene.CameraIndex);
        Assert.Equal(0, field.Information!.BackgroundWrapMask);
        Assert.True(scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? prior));
        Assert.False(scene.BackgroundScrollPositions.ContainsKey(1));
        Assert.True(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(field, scene, 0, 0, -112));
        BackgroundScrollFrame before = NormalCameraCertificate.Prepare(field, scene, actors)!;
        foreach (bool completed in new[] { false, true })
        {
            JsonNode changed = JsonSerializer.SerializeToNode(scene)!;
            BackgroundScrollPosition pending = prior! with { TargetX = 31, TargetY = -91, Mode = 3, Status = 0, NativeLinearVersion = 0 };
            BackgroundScrollPosition position = completed ? pending with { X = 31, Y = -91, PreviousX = prior!.X, PreviousY = prior.Y, Counter = 0, Status = 2 } : pending;
            changed["BackgroundScrollPositions"]!["0"] = JsonSerializer.SerializeToNode(position);
            SceneEventState current = changed.Deserialize<SceneEventState>(options)!;
            Assert.True(NativeNonselectedBackgroundScrollCertificate.SupportsRetainedPosition(field, current, 0, position));
            Assert.True(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(field, current, 0, 0, -112));
            BackgroundScrollFrame after = NormalCameraCertificate.Prepare(field, current, actors)!;
            Assert.Equal(before.ActorOffset, after.ActorOffset);
            Assert.Equal(before.LayerOffsets.OrderBy(pair => pair.Key), after.LayerOffsets.OrderBy(pair => pair.Key));
            Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, current));
            Assert.Equal(position, current.BackgroundScrollPositions[0]);
            Assert.False(current.BackgroundScrollPositions.ContainsKey(1));
        }
        JsonNode malformed = JsonSerializer.SerializeToNode(scene)!;
        malformed["NativeFieldLoadVersion"] = 0;
        Assert.False(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(field, malformed.Deserialize<SceneEventState>(options)!, 0, 0, -112));
    }
}
