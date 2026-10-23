using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleEditablePartTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MergedMaterialPrimitivesShareGeometryAndPosePartLayout(int nativePart)
    {
        EditableVertex Vertex(float x, float z) => new(new Vector3(x, 0, z), Vector3.UnitY, Vector2.Zero, Vector4.Zero, new Vector4(1, 0, 0, 0), Vector4.One);
        EditableMesh Primitive(string name, float x, int texture) => new(name, [Vertex(x - 0.5f, 0.5f), Vertex(x, -0.5f), Vertex(x + 0.5f, 0.5f)], [0, 1, 2], texture, Matrix4x4.Identity) { NativePart = nativePart, Skinned = true };
        EditableModel edited = new("materials", "battle", [new EditableBone("bone_0", -1, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 0 }], [Primitive("red", -0.75f, 0), Primitive("green", 0.75f, 1)], [new PixelImage(1, 1, [new Rgba32(255, 0, 0)]), new PixelImage(1, 1, [new Rgba32(0, 255, 0)])], [], JsonSerializer.SerializeToElement(new { }));
        BattleModel original = new("target", [new BattleBone(-1, 0)], [], [], [new BattleAnimation([new BattlePose(Vector3.Zero, [Vector3.Zero], [Vector3.One])])], 1);
        BattleModel converted = ModelExchange.ToBattle(edited, original);
        IReadOnlyList<Vector3[]> pose = converted.Pose(-1, 0);
        Assert.Equal(nativePart + 1, converted.Meshes.Count);
        Assert.Equal(converted.Meshes.Count, pose.Count);
        for (int part = 0; part < nativePart; part++)
        {
            Assert.Empty(converted.Meshes[part].Vertices);
            Assert.Empty(pose[part]);
        }
        Assert.Equal(6, pose[nativePart].Length);
        Assert.Equal(new[] { 0, 1, 2 }, converted.Meshes[nativePart].Faces[0].Vertices);
        Assert.Equal(new[] { 3, 4, 5 }, converted.Meshes[nativePart].Faces[1].Vertices);
        Assert.Equal(converted.Meshes[nativePart].Vertices.Select(vertex => vertex.Position), pose[nativePart]);
        NativeBattleDecodedPose native = new(new NativeBattleVector(100, 0, 0), [new NativeBattleBonePose(new(0, 0, 0), new(1024, 1024, 1024))], false);
        IReadOnlyList<Vector3[]> posedNative = converted.PoseNative(native);
        Assert.Equal(converted.Meshes.Count, posedNative.Count);
        for (int index = 0; index < pose[nativePart].Length; index++)
        {
            Assert.True(Vector3.Distance(pose[nativePart][index] - Vector3.UnitX, posedNative[nativePart][index]) < 0.0001f);
        }
        BattleRenderer renderer = new(128, 128);
        renderer.Render(new BattleStage(0, [], []), [new BattleActorDraw(converted, Vector3.Zero, Animation: -1)], new BattleCamera(new(0, 0, 5), Vector3.Zero));
        Assert.Equal(2, renderer.DrawnTriangles);
    }

    [Fact]
    public void OptionalInstalledMultiMaterialImportRendersAllPartVertices()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LINKED_MODEL_GLB");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(File.Exists(source) && Directory.Exists(game), "Set optional game and linked Blender GLB inputs.");
        ModelAssetImportInventory inventory = ModelAssetExchange.InspectGlb(File.ReadAllBytes(source));
        ModelAssetObject body = inventory.Objects.Single(item => item.Target.StartsWith("battle/d0c", StringComparison.Ordinal));
        Assert.Contains(body.Model.Meshes.GroupBy(mesh => mesh.NativePart), group => group.Key >= 0 && group.Count() > 1);
        using BattleAssets originals = new(game);
        BattleModel original = originals.LoadModel(body.Target[7..]);
        BattleModel converted = ModelExchange.ToBattle(body.Model, original);
        IReadOnlyList<Vector3[]> pose = converted.Pose(-1, 0);
        Assert.Equal(converted.Meshes.Count, pose.Count);
        for (int part = 0; part < converted.Meshes.Count; part++)
        {
            Assert.Equal(converted.Meshes[part].Vertices.Count, pose[part].Length);
        }
        BattleRenderer renderer = new(64, 64);
        renderer.Render(new BattleStage(0, [], []), [new BattleActorDraw(converted, Vector3.Zero, Animation: -1)], new BattleCamera(new(0, 12, -32), new(0, 12, 0)));
        Assert.True(renderer.DrawnTriangles > 0);
    }
}
