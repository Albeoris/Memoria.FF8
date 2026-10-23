using System.Numerics;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelAssetExchangeTests
{
    [Fact]
    public void IndependentRigsAndUnequalComponentsRoundTripWithoutRescaling()
    {
        ModelAssetSelection selection = Selection();
        byte[] bytes = ModelAssetExchange.ExportGlb(selection);
        ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(bytes);
        Assert.Equal(2, result.Objects.Count);
        Assert.Equal(2, result.Clips.Count);
        Assert.Equal(0.1f, result.Clips[0].Animation.Duration, 5);
        Assert.Equal(0.2f, result.Clips[1].Animation.Duration, 5);
        Assert.All(result.Objects, item => Assert.Single(item.Model.Bones, bone => bone.IsJoint));
        Assert.Empty(result.Warnings);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_TEST_GLB");
        if (!string.IsNullOrEmpty(output))
        {
            File.WriteAllBytes(output, bytes);
        }
    }

    [Fact]
    public void GeometryAndClipsSelectIndependently()
    {
        ModelAssetSelection selection = Selection();
        ModelAssetImportInventory clips = ModelAssetExchange.InspectGlb(ModelAssetExchange.ExportGlb(selection with { Objects = selection.Objects.Select(item => item with { GeometrySelected = false }).ToArray() }));
        Assert.All(clips.Objects, item => Assert.Empty(item.Model.Meshes));
        string? clipOutput = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_CLIP_GLB");
        if (!string.IsNullOrEmpty(clipOutput))
        {
            File.WriteAllBytes(clipOutput, ModelAssetExchange.ExportGlb(selection with { Objects = selection.Objects.Select(item => item with { GeometrySelected = false }).ToArray() }));
        }
        Assert.Equal(2, clips.Clips.Count);
        ModelAssetImportInventory meshes = ModelAssetExchange.InspectGlb(ModelAssetExchange.ExportGlb(selection with { Clips = [] }));
        Assert.All(meshes.Objects, item => Assert.Single(item.Model.Meshes));
        Assert.Empty(meshes.Clips);
    }

    [Fact]
    public void OptionalBlenderRoundTripRetainsInventoryAndMotion()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_BLENDER_GLB");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        ModelAssetSelection source = Selection();
        ModelAssetImportInventory imported = ModelAssetExchange.InspectGlb(File.ReadAllBytes(path));
        Assert.Equal(2, imported.Objects.Count);
        Assert.Equal(2, imported.Clips.Count);
        foreach (ModelAssetClip clip in imported.Clips)
        {
            ModelAssetClip original = source.Clips.Single(item => item.Target == clip.Target);
            EditableModel before = source.Objects.Single(item => item.Target == clip.ModelTarget).Model with { Animations = [original.Animation] };
            EditableModel after = imported.Objects.Single(item => item.Target == clip.ModelTarget).Model with { Animations = [clip.Animation] };
            foreach (float seconds in new[] { 0, original.Animation.Duration / 2, original.Animation.Duration })
            {
                Vector3[] expected = ModelExchange.Pose(before, 0, seconds).SelectMany((vertices, mesh) => before.Meshes[mesh].Indices.Select(index => vertices[index])).ToArray();
                Vector3[] actual = ModelExchange.Pose(after, 0, seconds).SelectMany((vertices, mesh) => after.Meshes[mesh].Indices.Select(index => vertices[index])).ToArray();
                Assert.Equal(expected.Length, actual.Length);
                for (int index = 0; index < expected.Length; index++)
                {
                    Assert.True(Vector3.Distance(expected[index], actual[index]) < 0.0001f, $"{clip.Target} {seconds}: expected {expected[index]}, actual {actual[index]}");
                }
            }
        }
    }

    [Fact]
    public void OptionalBlenderClipOnlyRemainsEditableWithoutMeshes()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_CLIP_BLENDER_GLB");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        ModelAssetImportInventory imported = ModelAssetExchange.InspectGlb(File.ReadAllBytes(path));
        Assert.Equal(2, imported.Objects.Count);
        Assert.Equal(2, imported.Clips.Count);
        Assert.All(imported.Objects, item => Assert.Empty(item.Model.Meshes));
        Assert.All(imported.Objects, item => Assert.Single(item.Model.Bones, bone => bone.IsJoint));
        Assert.Equal(0.1f, imported.Clips[0].Animation.Duration, 5);
        Assert.Equal(0.2f, imported.Clips[1].Animation.Duration, 5);
    }

    [Fact]
    public void DuplicateTargetsAndMissingDependencyAreRejected()
    {
        ModelAssetSelection selection = Selection();
        Assert.Throws<InvalidDataException>(() => ModelAssetExchange.ExportGlb(selection with { Objects = [selection.Objects[0], selection.Objects[0] with { SourceVariant = "other" }] }));
        Assert.Throws<InvalidDataException>(() => ModelAssetExchange.ExportGlb(selection with { Objects = [selection.Objects[0]] }));
        Assert.Throws<InvalidDataException>(() => ModelAssetExchange.ExportGlb(selection with { Clips = [selection.Clips[0], selection.Clips[0]] }));
    }

    [Fact]
    public void StaleManifestDoesNotRestoreDeletedGeometryOrClips()
    {
        byte[] source = ModelAssetExchange.ExportGlb(Selection());
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(12));
        JsonObject document = JsonNode.Parse(source.AsSpan(20, jsonLength))!.AsObject();
        document.Remove("animations");
        foreach (JsonNode? node in document["nodes"]!.AsArray())
        {
            if (node?["extras"]?["memoria_target"]?.GetValue<string>() == "battle/weapon")
            {
                node.AsObject().Remove("mesh");
            }
        }
        byte[] json = Encoding.UTF8.GetBytes(document.ToJsonString());
        int padded = (json.Length + 3) & ~3;
        byte[] edited = new byte[20 + padded + source.Length - 20 - jsonLength];
        source.AsSpan(0, 20).CopyTo(edited);
        BinaryPrimitives.WriteInt32LittleEndian(edited.AsSpan(8), edited.Length);
        BinaryPrimitives.WriteInt32LittleEndian(edited.AsSpan(12), padded);
        edited.AsSpan(20, padded).Fill(32);
        json.CopyTo(edited.AsSpan(20));
        source.AsSpan(20 + jsonLength).CopyTo(edited.AsSpan(20 + padded));
        ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(edited);
        Assert.Empty(result.Clips);
        Assert.Single(result.Objects, item => item.GeometrySelected);
        Assert.Empty(result.Objects.Single(item => item.Target == "battle/weapon").Model.Meshes);
    }

    [Fact]
    public void OptionalInstalledBodyWeaponExport()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_INSTALLED_GLB");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        if (string.IsNullOrEmpty(output) || !Directory.Exists(game))
        {
            return;
        }
        using GameAssets assets = new(game);
        ModelCatalogEntry entry = assets.EnumerateModelCatalog().First(item => item.ResourcePath.StartsWith("battle/d0c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] discovered = assets.DiscoverModelAssets(entry, TestContext.Current.CancellationToken).ToArray();
        ModelAssetDiscoveryItem[] owners = discovered.Where(item => item.Kind == ModelAssetDiscoveryKind.Object && item.Load is not null).GroupBy(item => item.AlternativeGroup).Select(group => group.First()).Take(2).ToArray();
        Assert.Equal(2, owners.Length);
        ModelAssetObject[] objects = owners.Select(item => new ModelAssetObject(item.Id, "original", "installed", item.RigOwnerId ?? item.Id, item.Load!())).ToArray();
        ModelAssetClip[] clips = discovered.Where(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.Bank == "embedded" && owners.Any(owner => owner.Id == item.OwnerId) && item.Load is not null).Select(item => new ModelAssetClip(item.Id, item.OwnerId, objects.Single(owner => owner.Target == item.OwnerId).RigId, item.GroupId ?? item.Id, "original", "installed", item.Load!().Animations[item.ClipIndex], item.NativeFrameCount, item.SecondsPerFrame)).ToArray();
        Assert.NotEmpty(clips);
        File.WriteAllBytes(output, ModelAssetExchange.ExportGlb(new ModelAssetSelection(objects, clips) { Edition = "Classic" }));
    }

    [Fact]
    public void OptionalInstalledBlenderSamplesMatch()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_INSTALLED_GLB");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MODEL_EXCHANGE_INSTALLED_BLENDER_GLB");
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(output))
        {
            return;
        }
        ModelAssetImportInventory original = ModelAssetExchange.InspectGlb(File.ReadAllBytes(source));
        ModelAssetImportInventory edited = ModelAssetExchange.InspectGlb(File.ReadAllBytes(output));
        Assert.Equal(original.Clips.Count, edited.Clips.Count);
        foreach (ModelAssetClip clip in original.Clips)
        {
            ModelAssetClip result = edited.Clips.Single(item => item.Target == clip.Target);
            EditableModel before = original.Objects.Single(item => item.Target == clip.ModelTarget).Model with { Animations = [clip.Animation] };
            EditableModel after = edited.Objects.Single(item => item.Target == clip.ModelTarget).Model with { Animations = [result.Animation] };
            Assert.Equal(clip.Animation.Duration, result.Animation.Duration, 4);
            foreach (float seconds in new[] { 0, ((clip.NativeFrameCount - 1) / 2) * clip.SecondsPerFrame, clip.Animation.Duration })
            {
                Vector3[] expected = ModelExchange.Pose(before, 0, seconds).SelectMany((vertices, mesh) => before.Meshes[mesh].Indices.Select(index => vertices[index])).ToArray();
                Vector3[] actual = ModelExchange.Pose(after, 0, seconds).SelectMany((vertices, mesh) => after.Meshes[mesh].Indices.Select(index => vertices[index])).ToArray();
                Assert.Equal(expected.Length, actual.Length);
                // Native coordinates are large; Blender skin conversion has float32 roundoff.
                for (int index = 0; index < expected.Length; index++)
                {
                    float tolerance = MathF.Max(0.0001f, expected[index].Length() * 0.00001f);
                    Assert.True(Vector3.Distance(expected[index], actual[index]) < tolerance, $"{clip.Target} @ {seconds}, vertex {index}: expected {expected[index]}, actual {actual[index]}, tolerance {tolerance}");
                }
            }
        }
    }

    [Fact]
    public void ReducedWeaponSharesRigAndSuppressesOnlyDesignatedBodyPart()
    {
        ModelAssetSelection source = Selection();
        ModelAssetObject body = source.Objects[0];
        EditableMesh mesh = body.Model.Meshes[0];
        body = body with { Model = body.Model with { Meshes = [mesh with { NativePart = 0 }, mesh with { NativePart = 1 }] } };
        ModelAssetObject weapon = source.Objects[1] with { RigId = body.RigId, ReplacedPart = "mesh/1" };
        EditableModel composed = ModelAssetExchange.Compose(new ModelAssetSelection([body, weapon], []));
        Assert.Single(composed.Bones, bone => bone.IsJoint);
        Assert.Equal(2, composed.Meshes.Count);
        Assert.DoesNotContain(composed.Meshes, item => item.ObjectTarget == body.Target && item.NativePart == 1);
        Assert.Contains(composed.Meshes, item => item.ObjectTarget == body.Target && item.NativePart == 0);
        Assert.Contains(composed.Meshes, item => item.ObjectTarget == weapon.Target);
    }

    [Fact]
    public void WorldNativeTimingUsesItsOwnRateAndSurvivesBothExchangeForms()
    {
        FieldModel native = new("world", [new ModelBone(-1, 1)], [], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero]), new AnimationFrame(Vector3.UnitX, [Vector3.Zero])])]);
        EditableModel world = ModelExchange.FromField(native, ModelExchange.WorldFramesPerSecond);
        Assert.Equal(30.5f, world.NativeFramesPerSecond);
        Assert.Equal(1 / 30.5f, world.Animations[0].Duration);
        EditableModel legacy = ModelExchange.ImportGlb(ModelExchange.ExportGlb(world)).Model;
        Assert.Equal(30.5f, legacy.NativeFramesPerSecond);
        ModelAssetObject owner = new("world/0", "original", "revision", "world/0", world, false);
        ModelAssetClip clip = new("world/0/clips/embedded/0", owner.Target, owner.RigId, "world/0", "original", "revision", world.Animations[0], 2, 1 / 30.5f);
        ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(ModelAssetExchange.ExportGlb(new([owner], [clip])));
        Assert.Equal(30.5f, result.Objects[0].Model.NativeFramesPerSecond);
        Assert.Equal(1 / 30.5f, result.Clips[0].Animation.Duration);
    }

    [Fact]
    public void ClipOnlyManifestExcludesUnselectedNativePayloadAndRetainsRigContract()
    {
        ModelAssetSelection source = Selection();
        JsonElement full = JsonSerializer.SerializeToElement(new
        {
            Bones = new[] { new { Parent = -1, Length = 12 } },
            Scale = 0.1f,
            ControlBones = new { First = 0, Second = -1, Additional = -1, RawHeader = 1 },
            NativeWeaponUsesBodySkeleton = false,
            Meshes = new[] { "unselected-geometry-sentinel" },
            Vertices = new[] { "unselected-vertices-sentinel" },
            Textures = new[] { "unselected-texture-sentinel" },
            Animations = new[] { "unselected-clip-sentinel" },
            NativePoseSource = new { Animations = "unselected-compressed-bank-sentinel" },
            Sequences = "unselected-sequences-sentinel",
            NativeActionCameraBank = "unselected-camera-sentinel",
            NativeBodyTexturePack = "unselected-pack-sentinel",
            NativeEyes = new { Bank = "unselected-eye-bank-sentinel", Texture = new { Depth = 1, WidthWords = 2, Height = 3, Words = "unselected-eye-pixels-sentinel" } }
        });
        source = source with { Objects = source.Objects.Select(item => item with { GeometrySelected = false, Model = item.Model with { NativeMetadata = full } }).ToArray() };
        byte[] bytes = ModelAssetExchange.ExportGlb(source);
        ModelAssetImportInventory result = ModelAssetExchange.InspectGlb(bytes);
        Assert.DoesNotContain("unselected-", Encoding.UTF8.GetString(bytes));
        Assert.All(result.Objects, item =>
        {
            Assert.True(item.Model.NativeMetadata.TryGetProperty("Bones", out _));
            Assert.Equal(0.1f, item.Model.NativeMetadata.GetProperty("Scale").GetSingle());
            Assert.True(item.Model.NativeMetadata.TryGetProperty("ControlBones", out _));
            Assert.False(item.Model.NativeMetadata.TryGetProperty("NativeEyes", out _));
        });
        JsonElement geometry = ModelExchange.RetainedNativeMetadata(full);
        Assert.True(geometry.TryGetProperty("NativeEyes", out JsonElement eyes));
        Assert.Equal(2, eyes.GetProperty("Texture").GetProperty("WidthWords").GetInt32());
        Assert.False(eyes.TryGetProperty("Bank", out _));
        Assert.False(eyes.GetProperty("Texture").TryGetProperty("Words", out _));
        Assert.DoesNotContain("unselected-", geometry.GetRawText());
    }

    public static ModelAssetSelection Selection()
    {
        EditableModel Model(string name)
        {
            EditableVertex Vertex(Vector3 position) => new(position, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, new Vector4(1, 0, 0, 0), Vector4.One);
            return new EditableModel(name, "battle", [new EditableBone("bone_0", -1, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 0 }], [new EditableMesh("geometry", [Vertex(Vector3.Zero), Vertex(Vector3.UnitX), Vertex(Vector3.UnitY)], [0, 1, 2], -1, Matrix4x4.Identity) { Skinned = true }], [], [], JsonSerializer.SerializeToElement(new { }));
        }
        ModelAssetObject[] objects = [new("battle/body", "original", "revision", "rig/body", Model("body")), new("battle/weapon", "original", "revision", "rig/weapon", Model("weapon"), AlternativeGroup: "weapons")];
        ModelAssetClip Clip(int index, float duration) => new($"bank/{index}/clip/0", objects[index].Target, objects[index].RigId, "bank/clip/0", "original", "revision", new EditableAnimation("Clip 0", [new EditableAnimationChannel(0, "translation", [0, duration], [Vector4.Zero, new Vector4(index + 1, 0, 0, 0)])]), index + 2, 0.1f);
        return new ModelAssetSelection(objects, [Clip(0, 0.1f), Clip(1, 0.2f)]);
    }
}
