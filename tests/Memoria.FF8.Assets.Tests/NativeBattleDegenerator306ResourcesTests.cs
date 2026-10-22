using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDegenerator306ResourcesTests
{
    [Fact]
    public void Original_meshes_sprites_materials_camera_and_three_section_binding_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleDegenerator306Resources resources = game.ReadNativeDegenerator306Resources();
        Assert.Same(resources, game.ReadNativeDegenerator306Resources());
        NativeBattleDegenerator306ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 920, 856, 5616, 2224, 10424 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length, resources.MeshC.Bytes.Length, resources.MeshD.Bytes.Length, resources.CustomMesh.Bytes.Length });
        Assert.Equal(new[] { 60, 404 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length });
        Assert.Equal((byte)8, resources.SpriteA.Kind);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteA.Bytes.AsSpan().Slice(8)));
        Assert.Equal(15, BinaryPrimitives.ReadUInt16LittleEndian(resources.SpriteB.Bytes.AsSpan().Slice(8)));
        Assert.Equal(1000, resources.Camera.Data.Length);
        Assert.Equal(12, resources.MaterialCycle.Length);
        Assert.Equal(0x55775u, resources.Sound);
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(resources.CasterDat.Data.AsSpan()));
        Assert.Equal(new[] { 0x14, 0x1648, 0x1698, 0x1B74 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), index)));
        Assert.Equal(game.ReadNativeBadBreath300Resources().CasterDat.Data.AsSpan().ToArray(), resources.CasterDat.Data.AsSpan().ToArray());
    }

    [Fact]
    public void Original_payloads_survive_independent_JSON_without_mutable_source_aliases()
    {
        using GameAssets game = Open();
        NativeBattleDegenerator306Resources resources = game.ReadNativeDegenerator306Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleDegenerator306Resources restored = JsonSerializer.Deserialize<NativeBattleDegenerator306Resources>(json)!;
        NativeBattleDegenerator306ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(restored.MeshA, restored.ReadMesh(0xCEAB94));
        Assert.Equal(restored.SpriteA, restored.ReadSprite(0xCE60C0));
        Assert.Throws<NotSupportedException>(() => restored.ReadMesh(0xCEAB98));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_original_payload_boundaries_and_selected_indices_fail(int selected)
    {
        using GameAssets game = Open();
        NativeBattleDegenerator306Resources resources = game.ReadNativeDegenerator306Resources();
        NativeBattleDegenerator306Resources altered;
        if (selected == 0)
        {
            altered = resources with { CustomMesh = resources.CustomMesh with { Bytes = resources.CustomMesh.Bytes.RemoveAt(resources.CustomMesh.Bytes.Length - 1) } };
        }
        else if (selected == 1)
        {
            altered = resources with { MaterialCycle = resources.MaterialCycle.RemoveAt(11) };
        }
        else if (selected == 2)
        {
            altered = resources with { CasterDat = resources.CasterDat with { Data = resources.CasterDat.Data.RemoveAt(resources.CasterDat.Data.Length - 1) } };
        }
        else
        {
            byte[] bytes = resources.MeshA.Bytes.ToArray();
            int faces = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 8), 52);
            altered = resources with { MeshA = resources.MeshA with { Bytes = bytes.ToImmutableArray() } };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleDegenerator306ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Degenerator ingress.");
        return new(directory!);
    }
}
