using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7OcclusionContractTests
{
    [Fact]
    public void SelectedNativeAndMappedActorExposeTheirActualProjectionAndDepthContractsWithoutATick()
    {
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF7_OCCLUSION_CHECKPOINT");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(checkpoint) || string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Select the original-resource occlusion regression state.");
        byte[] originalBytes = File.ReadAllBytes(checkpoint!);
        using MemoryStream source = new(originalBytes, false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(source);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7Actor nativeActor = session.Host.Process.Actors.First(actor => actor.Visible && actor.PartyCharacter == 0);
        Ff7Actor actor = session.PrepareActorPresentation(nativeActor);
        PcFieldModelDescriptor descriptor = session.Field.Models.Models[actor.Model];
        Ff7FieldModelResources original = session.Models[actor.Model];
        using GameAssets assets = new(ff8!);
        IFf7ActorAppearance appearance = Ff7AppearanceSelection.Create(envelope.Mappings, installation, assets)!;
        Ff7ActorVisual visual = new(actor.Entity, actor.Model, actor.X4096, actor.Y4096, actor.Z4096, checked((byte)actor.Direction), actor.Animation, actor.AnimationFrame16) { PartyCharacter = actor.PartyCharacter };
        Assert.True(appearance.TryCreate(visual, descriptor, original, out var replacement));
        Matrix4x4 basis = Ff7FieldRenderer.ModelBasis(descriptor);
        var animation = original.Animations[Path.ChangeExtension(descriptor.Animations[actor.Animation], ".a")];
        var pose = Ff7FieldRenderer.Pose(original, animation, actor.AnimationFrame16 >> 4, basis.M11);
        Matrix4x4 world = basis * Matrix4x4.CreateRotationZ(actor.Direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
        Vector3[] nativeVertices = original.Geometry.SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, pose[part.BoneIndex] * world))).ToArray();
        Vector3[] mappedVertices = replacement.SelectMany(triangle => new[] { triangle.A.Position, triangle.B.Position, triangle.C.Position }).ToArray();
        PcFieldCamera camera = session.Field.Cameras[0];
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        float near = executable.NativePresentation.NearPlane(session.MapId);
        Ff7ProjectionDepth nativeDepth = new(near, executable.FieldFarPlane);
        Ff7ProjectionDepth oldDefaultDepth = new(executable.FieldNearPlane, executable.FieldFarPlane);
        Vector2 offset = Ff7Scene.FieldCameraOffset(session.Host.Process);
        Vector3 Project(Vector3 point) => PcFieldProjection.Project(point, camera, new Vector2(160, 120) + offset);
        object Bounds(Vector3[] vertices)
        {
            Vector3[] projected = vertices.Select(Project).ToArray();
            return new { Minimum = new[] { vertices.Min(point => point.X), vertices.Min(point => point.Y), vertices.Min(point => point.Z) }, Maximum = new[] { vertices.Max(point => point.X), vertices.Max(point => point.Y), vertices.Max(point => point.Z) }, ScreenMinimum = new[] { projected.Min(point => point.X), projected.Min(point => point.Y), projected.Min(point => point.Z) }, ScreenMaximum = new[] { projected.Max(point => point.X), projected.Max(point => point.Y), projected.Max(point => point.Z) } };
        }
        Vector3[] mappedScreen = mappedVertices.Select(Project).ToArray();
        var tiles = session.Field.Background.Layers.SelectMany(layer => layer).Where(tile => tile.Layer > 0 && (tile.State == 0 || (session.Host.Process.BackgroundStates[tile.Parameter] & tile.State) != 0)).Where(tile => 160 + tile.DestinationX + offset.X < mappedScreen.Max(point => point.X) && 176 + tile.DestinationX + offset.X > mappedScreen.Min(point => point.X) && 120 + tile.DestinationY + offset.Y < mappedScreen.Max(point => point.Y) && 136 + tile.DestinationY + offset.Y > mappedScreen.Min(point => point.Y)).Select(tile => new { tile.Layer, tile.Index, tile.DestinationX, tile.DestinationY, tile.Z, tile.Blending }).ToArray();
        var material = original.Geometry.SelectMany(part => part.Polygon.RenderStates.Select(state => PcRenderState.Read(state.Span))).Select(state => new { state.Features, state.Options }).Distinct().ToArray();
        Assert.Equal("nmkin_1", session.Field.Name);
        Assert.Equal(512, near);
        Assert.NotEmpty(tiles);
        float foreground = tiles.Select(tile => tile.Z / 10000000f).Max();
        float foregroundNearest = tiles.Select(tile => tile.Z / 10000000f).Min();
        float nativeNearest = nativeVertices.Min(vertex => Project(vertex).Z);
        float mappedNearest = mappedVertices.Min(vertex => Project(vertex).Z);
        float nativeFarthest = nativeVertices.Max(vertex => Project(vertex).Z);
        float mappedFarthest = mappedVertices.Max(vertex => Project(vertex).Z);
        Assert.True(nativeDepth.Project(nativeFarthest) < foregroundNearest);
        Assert.True(nativeDepth.Project(mappedFarthest) < foregroundNearest);
        Assert.True(oldDefaultDepth.Project(nativeNearest) > foreground);
        Assert.True(oldDefaultDepth.Project(mappedNearest) > foreground);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF7_OCCLUSION_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            File.WriteAllText(output, JsonSerializer.Serialize(new { session.Field.Name, session.FieldGeneration, session.Host.Process.Tick, Actor = actor, NearPlane = near, FarPlane = executable.FieldFarPlane, Depth = new { NativeNearest = nativeDepth.Project(nativeNearest), MappedNearest = nativeDepth.Project(mappedNearest), NativeFarthest = nativeDepth.Project(nativeFarthest), MappedFarthest = nativeDepth.Project(mappedFarthest), ForegroundMinimum = foregroundNearest, ForegroundMaximum = foreground, OldDefaultNativeNearest = oldDefaultDepth.Project(nativeNearest), OldDefaultMappedNearest = oldDefaultDepth.Project(mappedNearest) }, Native = Bounds(nativeVertices), Mapped = Bounds(mappedVertices), Material = material, Tiles = tiles }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Equal(originalBytes, File.ReadAllBytes(checkpoint!));
        Assert.All(mappedScreen, point => Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z)));
    }
}
