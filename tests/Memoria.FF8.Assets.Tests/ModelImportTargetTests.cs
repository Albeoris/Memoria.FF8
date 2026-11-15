using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelImportTargetTests
{
    [Theory]
    [InlineData("model", "embedded", null)]
    [InlineData("geometry", "embedded", null)]
    [InlineData("clip", "combat", 42)]
    public void DestinationSurvivesExportImportAndReexport(string mode, string bank, int? slot)
    {
        ModelImportTarget target = new(1, "field/test/0", mode, bank, slot);
        byte[] content = ModelExchange.ExportGlb(CreateModel() with { ImportTarget = target });
        using JsonDocument json = JsonDocument.Parse(content.AsMemory(20, BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(12))));
        string asset = json.RootElement.GetProperty("asset").GetProperty("extras").GetProperty("memoria_import").GetString()!;
        Assert.Equal(target, JsonSerializer.Deserialize<ModelImportTarget>(asset));
        Assert.Contains(json.RootElement.GetProperty("nodes").EnumerateArray(), node => node.TryGetProperty("extras", out JsonElement extras) && extras.TryGetProperty("memoria_import", out JsonElement value) && value.GetString() == asset);
        EditableModel imported = ModelExchange.ImportGlb(content).Model;
        Assert.Equal(target, imported.ImportTarget);
        Assert.Equal(target, ModelExchange.ImportGlb(ModelExchange.ExportGlb(imported)).Model.ImportTarget);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DestinationCanBeAuthoredOnlyOnAssetOrOnlyOnNode(bool assetOnly)
    {
        ModelImportTarget target = new(1, "battle/c0m000.dat", "geometry");
        byte[] content = ChangeJson(ModelExchange.ExportGlb(CreateModel()), root =>
        {
            JsonObject extras = assetOnly ? root["asset"]!["extras"]!.AsObject() : root["nodes"]!.AsArray().Last()!["extras"]!.AsObject();
            extras["memoria_import"] = JsonSerializer.Serialize(target);
        });
        Assert.Equal(target, ModelExchange.ImportGlb(content).Model.ImportTarget);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Mode\":\"model\"}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"world/0\"}")]
    [InlineData("{\"Version\":2,\"ModelId\":\"world/0\",\"Mode\":\"model\"}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"../escape\",\"Mode\":\"model\"}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"world/0\",\"Mode\":\"clip\"}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"world/0\",\"Mode\":\"clip\",\"ClipSlot\":65536}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"world/0\",\"Mode\":\"clip\",\"Bank\":\"../bad\",\"ClipSlot\":0}")]
    [InlineData("{\"Version\":1,\"ModelId\":\"world/0\",\"Mode\":\"geometry\",\"ClipSlot\":0}")]
    public void InvalidDestinationsAreRejected(string metadata)
    {
        byte[] content = ChangeJson(ModelExchange.ExportGlb(CreateModel()), root => root["asset"]!["extras"]!["memoria_import"] = metadata);
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(content));
    }

    [Fact]
    public void ConflictingDestinationsAndNonStringPropertiesAreRejected()
    {
        byte[] source = ModelExchange.ExportGlb(CreateModel() with { ImportTarget = new(1, "world/0", "geometry") });
        byte[] conflicting = ChangeJson(source, root => root["asset"]!["extras"]!["memoria_import"] = JsonSerializer.Serialize(new ModelImportTarget(1, "world/1", "geometry")));
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(conflicting));
        byte[] nonString = ChangeJson(source, root => root["asset"]!["extras"]!["memoria_import"] = new JsonObject());
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(nonString));
    }

    [Fact]
    public void LinkedManifestCanBeReadFromAssetAndConflictsAreRejected()
    {
        const string manifest = "{\"Version\":1}";
        byte[] content = ModelExchange.ExportGlb(CreateModel() with { ExchangeManifest = manifest });
        byte[] assetOnly = ChangeJson(content, root =>
        {
            foreach (JsonNode? node in root["nodes"]!.AsArray())
            {
                (node?["extras"] as JsonObject)?.Remove("memoria_ff8");
            }
        });
        Assert.Equal(manifest, ModelExchange.ImportGlb(assetOnly).Model.ExchangeManifest);
        byte[] conflict = ChangeJson(content, root => root["asset"]!["extras"]!["memoria_ff8"] = "{}");
        Assert.Throws<InvalidDataException>(() => ModelExchange.ImportGlb(conflict));
    }

    private static EditableModel CreateModel()
    {
        EditableVertex[] vertices = [new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.One), new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX, Vector4.Zero, Vector4.Zero, Vector4.One), new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY, Vector4.Zero, Vector4.Zero, Vector4.One)];
        return new("test", "field", [], [new("triangle", vertices, [0, 1, 2], -1, Matrix4x4.Identity)], [], [], JsonSerializer.SerializeToElement(new { }));
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
