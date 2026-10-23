using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelExchangeTests
{
    [Fact]
    public void NativeBattleMaterialGroupingRetainsPageClutAndDepthFlagTemplates()
    {
        BattleVertex[] vertices = [new(Vector3.Zero, 0), new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)];
        BattleFace Face(ushort page, ushort clut, byte flags) => new BattleFace([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, new Rgba32(128, 128, 128))
        {
            NativeMaterial = new NativeBattleFaceMaterial(page, clut, [Vector2.Zero, Vector2.Zero, Vector2.Zero]),
            NativePolygon = [0, flags, 0, 0]
        };
        BattleModel native = new("materials", [], [new BattleMesh(vertices, [Face(1, 2, 0x10), Face(1, 3, 0x10), Face(2, 2, 0x10), Face(1, 2, 0x20), Face(1, 2, 0x10)])], [new PixelImage(1, 1, [new Rgba32(255, 255, 255)])], [], 1);
        EditableModel model = ModelExchange.FromBattle(native);
        Assert.Equal(4, model.Meshes.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, model.Meshes.Select(mesh => mesh.NativeMaterial));
        Assert.All(model.Meshes, mesh => Assert.Equal(0, mesh.NativeTexture));
        Assert.All(model.Meshes, mesh => Assert.Equal(0, mesh.NativePart));
        Assert.Equal(6, model.Meshes[0].Vertices.Count);
        EditableModel imported = ModelExchange.ImportGlb(ModelExchange.ExportGlb(model)).Model;
        Assert.Equal(new[] { 0, 1, 2, 3 }, imported.Meshes.Select(mesh => mesh.NativeMaterial));
    }

    [Fact]
    public void NativeTextureSlotsSurviveImageTextureAndMaterialReordering()
    {
        EditableModel source = CreateModel();
        source = source with
        {
            Textures = [new PixelImage(1, 1, [new Rgba32(255, 0, 0)]), new PixelImage(1, 1, [new Rgba32(0, 255, 0)])],
            Meshes = [source.Meshes[0] with { Name = "first", Texture = 0, NativeTexture = 7, NativeMaterial = 13 }, source.Meshes[0] with { Name = "second", Texture = 1, NativeTexture = 2, NativeMaterial = 29 }]
        };
        byte[] glb = ChangeJson(ModelExchange.ExportGlb(source), document =>
        {
            foreach (string property in new[] { "images", "textures", "materials" })
            {
                document[property] = new JsonArray(document[property]!.AsArray().Reverse().Select(node => node!.DeepClone()).ToArray());
            }
            foreach (JsonNode? texture in document["textures"]!.AsArray())
            {
                texture!["source"] = 1 - texture["source"]!.GetValue<int>();
            }
            foreach (JsonNode? material in document["materials"]!.AsArray())
            {
                JsonNode reference = material!["pbrMetallicRoughness"]!["baseColorTexture"]!;
                reference["index"] = 1 - reference["index"]!.GetValue<int>();
            }
            foreach (JsonNode? mesh in document["meshes"]!.AsArray())
            {
                foreach (JsonNode? primitive in mesh!["primitives"]!.AsArray())
                {
                    primitive!["material"] = 1 - primitive["material"]!.GetValue<int>();
                }
            }
            foreach (JsonNode? node in document["nodes"]!.AsArray())
            {
                node?["extras"]?.AsObject().Remove("memoria_native_texture");
                node?["extras"]?.AsObject().Remove("memoria_native_material");
            }
        });
        EditableModel imported = ModelExchange.ImportGlb(glb).Model;
        Assert.Equal(new[] { 7, 2 }, imported.Meshes.Select(mesh => mesh.NativeTexture));
        Assert.Equal(new[] { 13, 29 }, imported.Meshes.Select(mesh => mesh.NativeMaterial));
        Assert.Equal(new[] { 1, 0 }, imported.Meshes.Select(mesh => mesh.Texture));
        Assert.Equal(source.Textures[0].Pixels, imported.Textures[imported.Meshes[0].Texture].Pixels);
        Assert.Equal(source.Textures[1].Pixels, imported.Textures[imported.Meshes[1].Texture].Pixels);
        EditableModel repeated = ModelExchange.ImportGlb(ModelExchange.ExportGlb(imported)).Model;
        Assert.Equal(new[] { 7, 2 }, repeated.Meshes.Select(mesh => mesh.NativeTexture));
        Assert.Equal(new[] { 13, 29 }, repeated.Meshes.Select(mesh => mesh.NativeMaterial));
    }

    [Fact]
    public void NativeBattleConversionCapturesOriginalTextureSlots()
    {
        BattleVertex[] vertices = [new(Vector3.Zero, 0), new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)];
        BattleFace Face(int texture) => new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], texture, new Rgba32(128, 128, 128));
        BattleModel original = new("slots", [], [new BattleMesh(vertices, [Face(1), Face(0)])], [new PixelImage(1, 1, [new Rgba32(255, 0, 0)]), new PixelImage(1, 1, [new Rgba32(0, 255, 0)])], [], 1);
        EditableModel model = ModelExchange.FromBattle(original);
        Assert.Equal(new[] { 1, 0 }, model.Meshes.Select(mesh => mesh.NativeTexture));
        Assert.Equal(model.Meshes.Select(mesh => mesh.Texture), model.Meshes.Select(mesh => mesh.NativeTexture));
    }

    [Fact]
    public void GlbRoundTripPreservesTextureSkinAnimationAndNativeMetadata()
    {
        EditableModel original = CreateModel();
        byte[] glb = ModelExchange.ExportGlb(original);
        ModelExchangeImportResult imported = ModelExchange.ImportGlb(glb, original);
        Assert.False(imported.RequiresBoneCountApproval);
        Assert.Empty(imported.Warnings);
        Assert.Equal(2, imported.Model.Bones.Count(bone => bone.IsJoint));
        Assert.Equal(original.NativeMetadata.GetRawText(), imported.Model.NativeMetadata.GetRawText());
        Assert.Equal(original.Textures[0].Pixels, imported.Model.Textures[0].Pixels);
        Assert.Equal(original.Meshes[0].Indices, imported.Model.Meshes[0].Indices);
        AssertPoseEqual(ModelExchange.Pose(original, 0, 0.5f), ModelExchange.Pose(imported.Model, 0, 0.5f));
    }

    [Fact]
    public void ChangedJointCountRequiresExplicitApproval()
    {
        EditableModel original = CreateModel();
        EditableModel changed = original with { Bones = original.Bones.Concat([new EditableBone("extra", 0, Vector3.UnitY, Quaternion.Identity, Vector3.One)]).ToArray() };
        byte[] glb = ModelExchange.ExportGlb(changed);
        ModelExchangeImportResult pending = ModelExchange.ImportGlb(glb, original);
        Assert.True(pending.RequiresBoneCountApproval);
        Assert.Contains(pending.Warnings, message => message.Contains("2 to 3", StringComparison.Ordinal));
        Assert.False(ModelExchange.ImportGlb(glb, original, true).RequiresBoneCountApproval);
    }

    [Fact]
    public void RejectsTruncatedBuffersCyclesAndExternalResources()
    {
        byte[] glb = ModelExchange.ExportGlb(CreateModel());
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(glb.AsSpan(0, glb.Length - 4)));
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(ChangeJson(glb, root => root["bufferViews"]![0]!["byteLength"] = int.MaxValue)));
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(ChangeJson(glb, root => root["nodes"]![1]!["children"] = new JsonArray(0))));
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(ChangeJson(glb, root => root["images"]![0]!["uri"] = "texture.png")));
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(ChangeJson(glb, root => root["buffers"]![0]!["uri"] = "mesh.bin")));
    }

    [Fact]
    public void RejectsInvalidAnimationTimesAndSkinWeights()
    {
        EditableModel model = CreateModel();
        EditableAnimation animation = model.Animations[0] with { Channels = [model.Animations[0].Channels[0] with { Times = [1, 0] }] };
        Assert.Throws<InvalidDataException>(() => ModelExchange.ExportGlb(model with { Animations = [animation] }));
        EditableMesh mesh = model.Meshes[0] with { Vertices = model.Meshes[0].Vertices.Select(vertex => vertex with { Weights = new Vector4(0.2f, 0, 0, 0) }).ToArray() };
        Assert.Throws<InvalidDataException>(() => ModelExchange.ExportGlb(model with { Meshes = [mesh] }));
    }

    [Fact]
    public void CorruptEmbeddedImageIsReportedAsInvalidModelData()
    {
        byte[] content = ModelExchange.ExportGlb(CreateModel());
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(12));
        using JsonDocument document = JsonDocument.Parse(content.AsMemory(20, jsonLength));
        int imageView = document.RootElement.GetProperty("images")[0].GetProperty("bufferView").GetInt32();
        JsonElement view = document.RootElement.GetProperty("bufferViews")[imageView];
        int offset = 28 + jsonLength + view.GetProperty("byteOffset").GetInt32();
        int length = view.GetProperty("byteLength").GetInt32();
        content.AsSpan(offset + 8, length - 8).Fill(0);
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(content));
    }

    [Fact]
    public void StaticNodeTransformsAndAnimationSurviveRepeatedExchange()
    {
        EditableModel source = CreateModel();
        EditableModel model = source with { Meshes = [source.Meshes[0] with { Skinned = false, Node = 1 }], Bones = source.Bones.Select(bone => bone with { IsJoint = false }).ToArray() };
        EditableModel once = ModelExchange.ImportGlb(ModelExchange.ExportGlb(model)).Model;
        EditableModel twice = ModelExchange.ImportGlb(ModelExchange.ExportGlb(once)).Model;
        AssertPoseEqual(ModelExchange.Pose(model, 0, 0.6f), ModelExchange.Pose(once, 0, 0.6f));
        AssertPoseEqual(ModelExchange.Pose(model, 0, 0.6f), ModelExchange.Pose(twice, 0, 0.6f));
    }

    [Fact]
    public void FieldAdapterMatchesNativePoseAndPixelUvs()
    {
        PixelImage texture = new(16, 8);
        FieldModel original = new("field", [new ModelBone(-1, 2), new ModelBone(0, 1)], [new ModelVertex(Vector3.Zero, 0), new ModelVertex(Vector3.UnitX, 0), new ModelVertex(Vector3.UnitY, 1)], [new ModelFace([0, 1, 2, 0], [Vector2.Zero, new Vector2(16, 0), new Vector2(0, 8), Vector2.Zero], 0, false)], [texture], [new ModelAnimation([new AnimationFrame(new Vector3(3, 4, 5), [new Vector3(0.2f, 0.1f, 0.3f), Vector3.Zero])])]);
        EditableModel editable = ModelExchange.FromField(original);
        Assert.Equal(3, editable.Meshes[0].Vertices.Count);
        Assert.Equal(Vector2.UnitX, editable.Meshes[0].Vertices[1].Uv);
        FieldModel imported = ModelExchange.ToField(ModelExchange.ImportGlb(ModelExchange.ExportGlb(editable), editable).Model, original);
        AssertPoseEqual([original.Pose(0, 0)], [imported.Pose(0, 0)]);
        Assert.Equal(new Vector2(16, 0), imported.Faces[0].TextureCoordinates[1]);
        Assert.Equal(original.ControlBones, imported.ControlBones);
    }

    [Fact]
    public void BattleAdapterMatchesNativeScaledPose()
    {
        PixelImage texture = new(8, 8);
        BattleMesh mesh = new([new BattleVertex(Vector3.Zero, 0), new BattleVertex(Vector3.UnitX, 0), new BattleVertex(Vector3.UnitY, 1)], [new BattleFace([0, 1, 2], [Vector2.Zero, new Vector2(8, 0), new Vector2(0, 8)], 0, new Rgba32(128, 128, 128))]);
        BattleModel original = new("battle", [new BattleBone(-1, 2), new BattleBone(0, 1)], [mesh], [texture], [new BattleAnimation([new BattlePose(new Vector3(3, 4, 5), [new Vector3(0.2f, 0.1f, 0.3f), Vector3.Zero], [Vector3.One, Vector3.One])])], 0.3f);
        EditableModel editable = ModelExchange.FromBattle(original);
        BattleModel imported = ModelExchange.ToBattle(ModelExchange.ImportGlb(ModelExchange.ExportGlb(editable), editable).Model, original);
        AssertPoseEqual(original.Pose(0, 0), imported.Pose(0, 0));
        Assert.Equal(new Rgba32(128, 128, 128), imported.Meshes[0].Faces[0].Color);
        Memoria.FF8.Runtime.NativeBattleDecodedPose native = new(new Memoria.FF8.Runtime.NativeBattleVector(300, 400, 500), [new(new Memoria.FF8.Runtime.NativeBattleVector(128, 64, 32), new Memoria.FF8.Runtime.NativeBattleVector(1024, 1024, 1024)), new(default, new Memoria.FF8.Runtime.NativeBattleVector(1024, 1024, 1024))], true);
        AssertPoseEqual(original.PoseNative(native), imported.PoseNative(native));
        EditableBone[] reorderedBones = [editable.Bones[1] with { Parent = 1 }, editable.Bones[0] with { Parent = -1 }];
        EditableMesh[] reorderedMeshes = editable.Meshes.Select(value => value with { Vertices = value.Vertices.Select(vertex => vertex with { Joints = new Vector4(1 - vertex.Joints.X, 0, 0, 0) }).ToArray() }).ToArray();
        EditableAnimation[] reorderedAnimations = editable.Animations.Select(animation => animation with { Channels = animation.Channels.Select(channel => channel with { Bone = 1 - channel.Bone }).ToArray() }).ToArray();
        BattleModel reordered = ModelExchange.ToBattle(editable with { Bones = reorderedBones, Meshes = reorderedMeshes, Animations = reorderedAnimations }, original);
        AssertPoseEqual(original.PoseNative(native), reordered.PoseNative(native));
    }

    [Fact]
    public void UntexturedImportsReceiveRuntimeFallbackTexture()
    {
        EditableModel model = CreateModel();
        model = model with { Meshes = [model.Meshes[0] with { Texture = -1 }] };
        BattleModel original = new("battle", [], [], [], [], 1);
        BattleModel converted = ModelExchange.ToBattle(model, original);
        Assert.InRange(converted.Meshes[0].Faces[0].Texture, 0, converted.Textures.Count - 1);
        Assert.Equal(new Rgba32(255, 255, 255), converted.Textures[^1].Pixels[0]);
    }

    [Fact]
    public void ImportedMaterialFactorAndVertexGradientReachBattleCpuAndGpuRendering()
    {
        EditableModel source = CreateModel() with
        {
            Bones = [],
            Animations = [],
            Textures = [new PixelImage(1, 1, [new Rgba32(255, 255, 255)])],
            Meshes = [new EditableMesh("colored", [Colored(new Vector3(-1, 0, 1), new Vector4(1, 0, 0, 1)), Colored(new Vector3(0, 0, -1), new Vector4(0, 1, 0, 1)), Colored(new Vector3(1, 0, 1), new Vector4(0, 0, 1, 1))], [0, 1, 2], 0, Matrix4x4.Identity)]
        };
        EditableModel imported = ImportWithColorFactor(source);
        BattleStage stage = ModelExchange.ToStage(imported, new BattleStage(0, [], []));
        BattleCamera camera = new(new Vector3(0, 0, 5), Vector3.Zero);
        BattleRenderer cpu = new(64, 64);
        byte[] pixels = cpu.Render(stage, [], camera);
        int center = (32 * 64 + 32) * 4;
        Assert.InRange(pixels[center], 20, 45);
        Assert.InRange(pixels[center + 1], 75, 110);
        Assert.InRange(pixels[center + 2], 8, 25);
        Assert.NotEqual(pixels[(36 * 64 + 23) * 4], pixels[(36 * 64 + 41) * 4]);
        ColorRecorder recorder = new();
        new BattleRenderer(64, 64) { TriangleRenderer = recorder }.Render(stage, [], camera);
        Assert.Equal(new[] { new Vector4(0.5f, 0, 0, 1), new Vector4(0, 0.75f, 0, 1), new Vector4(0, 0, 0.25f, 1) }, recorder.Colors);
        Assert.Equal(recorder.Colors, ModelExchange.FromStage(stage).Meshes[0].Vertices.Select(vertex => vertex.Color));
        WorldStaticModel world = ModelExchange.ToWorld(imported, new WorldStaticModel(0, 0, 0, [], [], []));
        Assert.Equal(recorder.Colors, Assert.Single(world.Faces).VertexColors);
        Assert.Equal(recorder.Colors, ModelExchange.FromWorld(world).Meshes[0].Vertices.Select(vertex => vertex.Color));
    }

    [Fact]
    public void ImportedMaterialFactorAndVertexGradientReachFieldCpuAndGpuRendering()
    {
        FieldModel original = new("colored", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(0, -10, -10), 0), new ModelVertex(new Vector3(0, 10, -10), 0), new ModelVertex(new Vector3(0, 0, 10), 0)], [new ModelFace([0, 2, 1], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [new PixelImage(1, 1, [new Rgba32(255, 255, 255)])], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        EditableModel source = ModelExchange.FromField(original);
        Vector4[] colors = [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1)];
        source = source with { Meshes = [source.Meshes[0] with { Vertices = source.Meshes[0].Vertices.Select((vertex, index) => vertex with { Color = colors[index] }).ToArray() }] };
        FieldModel imported = ModelExchange.ToField(ImportWithColorFactor(source), original);
        FieldAssets field = new(0, "colors", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [imported]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128))];
        byte[] pixels = new FieldRenderer(field).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        int center = (121 * 320 + 161) * 4;
        Assert.InRange(pixels[center], 1, 127);
        Assert.InRange(pixels[center + 1], 1, 191);
        Assert.InRange(pixels[center + 2], 1, 63);
        ColorRecorder recorder = new();
        new FieldRenderer(field) { TriangleRenderer = recorder }.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        Assert.Equal(Assert.Single(imported.Faces).VertexColors, recorder.Colors);
    }

    private static EditableVertex Colored(Vector3 position, Vector4 color) => new(position, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.Zero, color);

    private static EditableModel ImportWithColorFactor(EditableModel source)
    {
        byte[] content = ChangeJson(ModelExchange.ExportGlb(source), root => root["materials"]![0]!["pbrMetallicRoughness"]!["baseColorFactor"] = new JsonArray(0.5f, 0.75f, 0.25f, 1));
        return ModelExchange.ImportGlb(content, source).Model;
    }

    private sealed class ColorRecorder : ITriangleRenderer
    {
        public List<Vector4> Colors { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Colors.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Colors.AddRange([a.Color, b.Color, c.Color]);
        }

        public void End()
        {
        }
    }

    private static EditableModel CreateModel()
    {
        EditableBone[] bones = [new("root", -1, Vector3.Zero, Quaternion.Identity, Vector3.One), new("child", 0, Vector3.UnitY, Quaternion.Identity, Vector3.One)];
        EditableVertex[] vertices =
        [
            new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, new Vector4(1, 0, 0, 0), Vector4.One),
            new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX, new Vector4(0, 1, 0, 0), new Vector4(0.5f, 0.5f, 0, 0), Vector4.One),
            new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY, new Vector4(1, 0, 0, 0), new Vector4(1, 0, 0, 0), Vector4.One)
        ];
        EditableMesh mesh = new("triangle", vertices, [0, 1, 2], 0, Matrix4x4.Identity) { Skinned = true };
        EditableAnimation animation = new("motion", [new EditableAnimationChannel(1, "translation", [0, 1], [new Vector4(0, 1, 0, 0), new Vector4(0, 2, 0, 0)])]);
        PixelImage texture = new(2, 2, [new Rgba32(255, 0, 0), new Rgba32(0, 255, 0), new Rgba32(0, 0, 255), new Rgba32(255, 255, 255, 128)]);
        return new EditableModel("test", "field", bones, [mesh], [texture], [animation], JsonSerializer.SerializeToElement(new { control = 37, polygon = new byte[] { 1, 2, 3 } }));
    }

    private static void AssertPoseEqual(IReadOnlyList<Vector3[]> expected, IReadOnlyList<Vector3[]> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int mesh = 0; mesh < expected.Count; mesh++)
        {
            Assert.Equal(expected[mesh].Length, actual[mesh].Length);
            for (int vertex = 0; vertex < expected[mesh].Length; vertex++)
            {
                Assert.InRange(Vector3.Distance(expected[mesh][vertex], actual[mesh][vertex]), 0, 0.0001f);
            }
        }
    }

    private static byte[] ChangeJson(byte[] glb, Action<JsonNode> edit)
    {
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        JsonNode json = JsonNode.Parse(Encoding.UTF8.GetString(glb, 20, jsonLength))!;
        edit(json);
        byte[] content = Encoding.UTF8.GetBytes(json.ToJsonString());
        int padded = (content.Length + 3) & ~3;
        byte[] result = new byte[glb.Length - jsonLength + padded];
        glb.AsSpan(0, 20).CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), result.Length);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(12), padded);
        content.CopyTo(result, 20);
        result.AsSpan(20 + content.Length, padded - content.Length).Fill(32);
        glb.AsSpan(20 + jsonLength).CopyTo(result.AsSpan(20 + padded));
        return result;
    }
}
