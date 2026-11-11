using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleActorGeometryTests
{
    [Fact]
    public void Original_actor_geometry_capture_preserves_native_pose_material_and_world_coordinates()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original game installation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleActorVisual actor = new("rt", new(0, 0, 1700), 0, 0, 0, 0);
        Ff7BattleActorGeometry captured = renderer.CaptureActorGeometry(actor);
        Assert.NotEmpty(captured.Faces);
        Assert.NotEmpty(captured.SkeletonPoints);
        Vector3 translation = new(113, -79, 241);
        Ff7BattleActorGeometry moved = renderer.CaptureActorGeometry(actor with { Position = actor.Position + translation });
        Assert.Equal(captured.Faces.Length, moved.Faces.Length);
        Assert.Equal(captured.SkeletonPoints.Length, moved.SkeletonPoints.Length);
        for (int index = 0; index < captured.Faces.Length; index++)
        {
            Ff7BattleActorFace original = captured.Faces[index];
            Ff7BattleActorFace current = moved.Faces[index];
            Assert.True(Vector3.Distance(original.A + translation, current.A) < 0.001f);
            Assert.True(Vector3.Distance(original.B + translation, current.B) < 0.001f);
            Assert.True(Vector3.Distance(original.C + translation, current.C) < 0.001f);
            Assert.Equal((original.Part, original.Group, original.Polygon), (current.Part, current.Group, current.Polygon));
            Assert.Equal(original.Material.Raw.ToArray(), current.Material.Raw.ToArray());
        }
        for (int index = 0; index < captured.SkeletonPoints.Length; index++)
        {
            Assert.True(Vector3.Distance(captured.SkeletonPoints[index] + translation, moved.SkeletonPoints[index]) < 0.001f);
        }
        Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(renderer.CaptureActorGeometry(actor)));
    }
}
