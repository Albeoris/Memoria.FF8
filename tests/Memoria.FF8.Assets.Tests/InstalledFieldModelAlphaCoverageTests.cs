using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class InstalledFieldModelAlphaCoverageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR", "bghoke_2", 1)]
    [InlineData("MEMORIA_FF8_GAME_DIR", "bghoke_3", 0)]
    [InlineData("MEMORIA_FF8_GAME_DIR", "bgroad_6", 0)]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR", "bghoke_2", 1)]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR", "bghoke_3", 0)]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR", "bgroad_6", 0)]
    public void NativeOpaqueFieldFacesRetainCoverageThroughExportAndImport(string installationVariable, string field, int modelIndex)
    {
        string? directory = Environment.GetEnvironmentVariable(installationVariable);
        Assert.SkipUnless(Directory.Exists(directory), $"Set {installationVariable} to run the optional installed field material check.");
        using GameAssets assets = new(directory!);
        FieldModel native = assets.LoadFieldModelSource(assets.GetFieldModelSource(field, modelIndex));
        EditableModel editable = ModelExchange.FromField(native);
        byte[] bytes = ModelExchange.ExportGlb(editable);
        using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(20, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12))));
        JsonElement root = document.RootElement;
        EditableModel imported = ModelExchange.ImportGlb(bytes).Model;
        var groups = native.Faces.GroupBy(face => (face.Texture, face.SemiTransparent)).ToArray();
        Assert.Equal(groups.Length, editable.Meshes.Count);
        Assert.Equal(groups.Length, imported.Meshes.Count);
        List<object> materials = [];
        for (int index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            EditableMesh originalMesh = editable.Meshes[index];
            EditableMesh importedMesh = imported.Meshes[index];
            int materialIndex = root.GetProperty("meshes")[index].GetProperty("primitives")[0].GetProperty("material").GetInt32();
            JsonElement material = root.GetProperty("materials")[materialIndex];
            string mode = material.GetProperty("alphaMode").GetString()!;
            Assert.Equal(group.Key.SemiTransparent, originalMesh.Transparent);
            Assert.Equal(group.Key.SemiTransparent, importedMesh.Transparent);
            Assert.Equal(group.Sum(face => face.Quad ? 6 : 3), importedMesh.Indices.Count);
            if (group.Key.SemiTransparent)
            {
                Assert.Equal("BLEND", mode);
            }
            else
            {
                Assert.Equal(1f / 255, originalMesh.AlphaCutoff);
                Assert.Equal("MASK", mode);
                Assert.Equal(1f / 255, material.GetProperty("alphaCutoff").GetSingle());
                Assert.Equal(1f / 255, importedMesh.AlphaCutoff);
            }
            materials.Add(new { texture = group.Key.Texture, faces = group.Count(), semiTransparent = group.Key.SemiTransparent, zeroAlphaTexels = native.Textures[group.Key.Texture].Pixels.Count(pixel => pixel.A == 0), partialAlphaTexels = native.Textures[group.Key.Texture].Pixels.Count(pixel => pixel.A is > 0 and < 255), mode });
        }
        FieldModel restored = ModelExchange.ToField(imported, native);
        Assert.Equal(native.Faces.Where(face => !face.SemiTransparent).Sum(face => face.Quad ? 2 : 1), restored.Faces.Count(face => !face.SemiTransparent));
        Assert.Equal(native.Faces.Where(face => face.SemiTransparent).Sum(face => face.Quad ? 2 : 1), restored.Faces.Count(face => face.SemiTransparent));
        output.WriteLine(JsonSerializer.Serialize(new { edition = assets.Resources.Source.Edition.ToString(), field, modelIndex, native.Name, opaqueFaces = native.Faces.Count(face => !face.SemiTransparent), semiTransparentFaces = native.Faces.Count(face => face.SemiTransparent), materials }));
    }
}
