using System.Numerics;
using System.Reflection;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignBodyTopologyTests
{
    [Fact]
    public void Shared_vertices_keep_their_source_identity_through_the_participant_bridge()
    {
        Vector3 a = Vector3.Zero;
        Vector3 b = new(200, 0, 0);
        Vector3 c = new(0, 200, 0);
        Vector3 d = new(200, 200, 0);
        NativeBattleForeignSurface surface = new(new NativeBattleForeignRenderMaterial(new(null), false), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        Ff7BattleActorGeometry geometry = new([
            new(a, b, c, 0, 0, 0, default!) { Surface = surface, VertexIndices = [0, 1, 2] },
            new(c, b, d, 0, 0, 1, default!) { Surface = surface, VertexIndices = [2, 1, 3] }
        ], [a, c]) { Vertices = [a, b, c, d], Bones = [(a, c)], BoneParents = [-1] };
        CrossoverNativeForeignTarget target = new(4, new(default, default, 0, 0, 4096), NativeBattleForeignModelGeometry.Identity, new(default, default, 0, new(default, default)), geometry, Matrix4x4.CreateScale(2));

        NativeBattleForeignModelGeometry bridged = target.ToActor(3).Geometry!;

        Assert.Equal(4, bridged.Vertices.Length);
        Assert.Equal(2, bridged.Faces.Length);
        foreach (NativeBattleForeignModelFace face in bridged.Faces)
        {
            Assert.Equal(3, face.VertexIndices.Length);
            Assert.Equal(face.A, bridged.Vertices[face.VertexIndices[0]]);
            Assert.Equal(face.B, bridged.Vertices[face.VertexIndices[1]]);
            Assert.Equal(face.C, bridged.Vertices[face.VertexIndices[2]]);
            Assert.Same(surface, face.Surface);
        }
        Assert.Equal(bridged.Faces[0].VertexIndices[1], bridged.Faces[1].VertexIndices[1]);
        Assert.Equal(bridged.Faces[0].VertexIndices[2], bridged.Faces[1].VertexIndices[0]);
    }

    [Fact]
    public void Original_host_body_and_weapon_export_their_actual_indexed_vertex_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVII installation.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession host = new(formation, original.Progress with { Party = [0, 1, 255] }, KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path)), executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (Ff7BattleActorState actor in host.Actors)
        {
            Ff7BattleActorGeometry geometry = renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, actor])!);
            Assert.NotEmpty(geometry.Vertices);
            Assert.NotEmpty(geometry.Faces);
            Assert.True(geometry.Faces.SelectMany(face => face.VertexIndices).Distinct().Count() < geometry.Faces.Length * 3);
            foreach (Ff7BattleActorFace face in geometry.Faces)
            {
                Assert.Equal(3, face.VertexIndices.Length);
                Assert.Equal(face.A, geometry.Vertices[face.VertexIndices[0]]);
                Assert.Equal(face.B, geometry.Vertices[face.VertexIndices[1]]);
                Assert.Equal(face.C, geometry.Vertices[face.VertexIndices[2]]);
                Assert.NotNull(face.Surface);
            }
        }
    }
}
