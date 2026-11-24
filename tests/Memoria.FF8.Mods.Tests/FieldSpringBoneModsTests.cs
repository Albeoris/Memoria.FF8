using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class FieldSpringBoneModsTests
{
    private const string ExactPath = "models/field/sample/0.springs.glb";
    private const string SharedPath = "models/field/main_chr/d024.springs.glb";
    private const string SecondEntryPath = "models/field/sample/1.springs.vrm";
    private const string Payload = """
        {
          "asset": {"version":"2.0"},
          "scene": 0,
          "scenes": [{"nodes":[0]}],
          "nodes": [
            {"name":"bone_0","children":[1]},
            {"name":"bone_1","children":[2],"translation":[0,0.25,0]},
            {"name":"bone_2","translation":[0,0.25,0]}
          ],
          "extensionsUsed": ["VRMC_springBone"],
          "extensions": {
            "VRMC_springBone": {
              "specVersion":"1.0",
              "springs":[{"joints":[{"node":1,"stiffness":12,"dragForce":0.5,"gravityPower":0.25},{"node":2}]}]
            }
          }
        }
        """;

    [Theory]
    [InlineData(ExactPath, true)]
    [InlineData(SharedPath, true)]
    [InlineData(SecondEntryPath, true)]
    [InlineData("MODELS/FIELD/MAIN_CHR/D024.SPRINGS.VRM", true)]
    [InlineData("models/field/sample/-1.springs.glb", false)]
    [InlineData("models/field/sample/00.springs.vrm", false)]
    [InlineData("models/field/main_chr/o024.springs.glb", false)]
    [InlineData("models/world/0.springs.glb", false)]
    [InlineData("models/field/sample/0.springs.json", false)]
    [InlineData("models/field/sample/0.glb", false)]
    public void IdentifiesFieldSpringDocuments(string path, bool valid)
    {
        Assert.Equal(valid, FieldSpringBoneMods.IsSpringDocumentPath(path));
    }

    [Fact]
    public void UsesPackageOrderAndPrefersAnExactTargetWithinTheSamePackage()
    {
        FieldSpringBoneMods mods = Prepare(File("top", SharedPath), File("top", ExactPath, WithStiffness(20)), File("next", SecondEntryPath, WithStiffness(30)), File("next", SharedPath, WithStiffness(40)));
        FieldModel original = Model("d024");
        Assert.Equal(20f, Assert.Single(mods.Apply("field/sample/0", original, original).SpringBones).Stiffness);
        Assert.Equal(12f, Assert.Single(mods.Apply("field/sample/1", original, original).SpringBones).Stiffness);
        Assert.Equal(new[] { "top", "next" }, mods.PackageIds);
    }

    [Fact]
    public void TargetsBorrowedGeometryOnlyThroughItsExactFieldEntry()
    {
        FieldSpringBoneMods mods = Prepare(File("one", SharedPath), File("one", ExactPath, WithStiffness(20)));
        FieldModel textureAlias = Model() with { MainCharacterTextures = true, TextureResourceName = "d024" };
        FieldModel original = Model("d024");
        Assert.Same(textureAlias, mods.Apply("field/sample/1", textureAlias, textureAlias));
        Assert.Equal(20f, Assert.Single(mods.Apply("field/sample/0", textureAlias, textureAlias).SpringBones).Stiffness);
        Assert.Same(original, mods.Apply("world/0", original, original));
        FieldModel applied = mods.Apply("field/sample/1", original, original);
        Assert.NotSame(original, applied);
        Assert.Same(applied, mods.Apply("field/sample/1", original, original));
        Assert.Empty(original.SpringBones);
    }

    [Theory]
    [InlineData("\"specVersion\":\"1.0\"", "\"specVersion\":\"2.0\"")]
    [InlineData("\"node\":2", "\"node\":3")]
    [InlineData("\"node\":2", "\"node\":0")]
    [InlineData("\"stiffness\":12", "\"stiffness\":-1")]
    [InlineData("\"dragForce\":0.5", "\"dragForce\":1.5")]
    [InlineData("\"gravityPower\":0.25", "\"gravityPower\":5e100")]
    [InlineData("\"VRMC_springBone\"", "\"other_extension\"")]
    public void RejectsInvalidSharedDefinitionsWithOwnerDiagnostic(string before, string after)
    {
        ModFile file = File("bad", SharedPath, Payload.Replace(before, after, StringComparison.Ordinal));
        Assert.False(FieldSpringBoneMods.TryPrepare(new(1, [], [file]), out FieldSpringBoneMods mods, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(mods.Definitions);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidFieldSpringBones", diagnostic.Code);
        Assert.Equal(file.PackageId, diagnostic.PackageId);
        Assert.Equal(file.RelativePath, diagnostic.RelativePath);
    }

    [Fact]
    public void RejectsIncompatibleTargetEvenWhenTheBoneCountMatches()
    {
        FieldSpringBoneMods mods = Prepare(File("one", SharedPath));
        FieldModel original = Model("d024");
        FieldModel replaced = original with { Bones = [new(-1, 0), new(0, 20), new(0, 20)] };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => mods.Apply("field/sample/0", original, replaced));
        Assert.Contains("hierarchy", error.Message);
        Assert.Empty(replaced.SpringBones);
    }

    [Fact]
    public void UsesSpringBonesEmbeddedInATargetedModelGlb()
    {
        const string modelId = "field/sample/1";
        JsonObject root = JsonNode.Parse(WithStiffness(25))!.AsObject();
        ModelImportTarget target = new(1, modelId, "model");
        root["asset"]!["extras"] = new JsonObject { ["memoria_import"] = JsonSerializer.Serialize(target) };
        root["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = 16 });
        root["bufferViews"] = JsonNode.Parse("""[{"buffer":0,"byteOffset":0,"byteLength":4},{"buffer":0,"byteOffset":4,"byteLength":12}]""");
        root["accessors"] = JsonNode.Parse("""[{"bufferView":0,"componentType":5126,"count":1,"type":"SCALAR","min":[0],"max":[0]},{"bufferView":1,"componentType":5126,"count":1,"type":"VEC3"}]""");
        root["animations"] = JsonNode.Parse("""[{"samplers":[{"input":0,"output":1}],"channels":[{"sampler":0,"target":{"node":1,"path":"translation"}}]}]""");
        ModFile file = new("model-package", ModFormat.Memoria, "models/custom/sample.glb", 1, () => Glb(root.ToJsonString(), includeBuffer: true));
        ModCatalogSnapshot catalog = new(1, [], [file, File("sidecar-package", SharedPath)]);
        Assert.True(ModelMods.TryPrepare(catalog, out ModelMods modelMods, out IReadOnlyList<ModDiagnostic> modelDiagnostics), string.Join("\n", modelDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(modelId, Assert.Single(modelMods.Variants).ModelId);

        Assert.True(FieldSpringBoneMods.TryPrepare(catalog, out FieldSpringBoneMods springMods, out IReadOnlyList<ModDiagnostic> springDiagnostics, modelMods.Variants), string.Join("\n", springDiagnostics.Select(diagnostic => diagnostic.Message)));
        FieldModel original = Model("d024");
        Assert.Equal(25f, Assert.Single(springMods.Apply(modelId, original, original).SpringBones).Stiffness);
        Assert.Equal(12f, Assert.Single(springMods.Apply("field/sample/0", original, original).SpringBones).Stiffness);
    }

    [Fact]
    public void SpringInstallationWritesAStandardGlbWithoutGeometryOrClips()
    {
        FieldSpringBoneAsset asset = ModelExchange.ReadFieldSpringBoneAsset(Glb(Payload))!;
        ModelModOutput output = Assert.Single(ModelModInstaller.PlanSpringBones("field/sample/1", asset).Files);
        Assert.Equal("models/field/sample/1.springs.glb", output.RelativePath);
        FieldSpringBoneAsset reimported = ModelExchange.ReadFieldSpringBoneAsset(output.Content)!;
        Assert.False(reimported.HasModelContent);
        Assert.Equal(asset.Nodes.Select(node => node.Parent), reimported.Nodes.Select(node => node.Parent));
        Assert.Equal(12f, Assert.Single(reimported.Bind(Model())).Stiffness);
    }

    private static FieldSpringBoneMods Prepare(params ModFile[] files)
    {
        Assert.True(FieldSpringBoneMods.TryPrepare(new(1, [], files), out FieldSpringBoneMods mods, out IReadOnlyList<ModDiagnostic> diagnostics), string.Join("\n", diagnostics.Select(diagnostic => diagnostic.Message)));
        return mods;
    }

    private static string WithStiffness(int stiffness) => Payload.Replace("\"stiffness\":12", FormattableString.Invariant($"\"stiffness\":{stiffness}"), StringComparison.Ordinal);

    private static ModFile File(string packageId, string path, string json = Payload) => new(packageId, ModFormat.Memoria, path, 1, () => Glb(json));

    private static byte[] Glb(string json, bool includeBuffer = false)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        int jsonLength = (payload.Length + 3) & ~3;
        byte[] glb = new byte[20 + jsonLength + (includeBuffer ? 24 : 0)];
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546c67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(8), glb.Length);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(12), jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4e4f534a);
        payload.CopyTo(glb.AsSpan(20));
        glb.AsSpan(20 + payload.Length, jsonLength - payload.Length).Fill((byte)' ');
        if (includeBuffer)
        {
            BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(20 + jsonLength), 16);
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(24 + jsonLength), 0x004e4942);
        }

        return glb;
    }

    private static FieldModel Model(string? mainCharacter = null) => new("sample", [new(-1, 0), new(0, 20), new(1, 20)], [], [], [], []) { MainCharacterSourceId = mainCharacter };
}
