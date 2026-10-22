using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGatlingGun305ResourcesTests
{
    [Fact]
    public void Original_meshes_sprites_camera_and_three_section_binding_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleGatlingGun305Resources resources = game.ReadNativeGatlingGun305Resources();
        Assert.Same(resources, game.ReadNativeGatlingGun305Resources());
        NativeBattleGatlingGun305ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 768, 712, 712 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length, resources.MeshC.Bytes.Length });
        Assert.Equal(new[] { 428, 508, 404 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal(new ushort[] { 16, 16, 15 }, new[] { resources.SpriteA, resources.SpriteB, resources.SpriteC }.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))));
        Assert.Equal(780, resources.Camera.Data.Length);
        Assert.Equal(0x61AD6u, resources.Sound);
        Assert.Equal(new[] { 0x14, 0x1648, 0x1698, 0x1B74 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), index)));
        Assert.Equal(game.ReadNativeBadBreath300Resources().CasterDat.Data.AsSpan().ToArray(), resources.CasterDat.Data.AsSpan().ToArray());
    }

    [Fact]
    public void Addressed_original_payloads_survive_JSON_without_reconstructed_sources()
    {
        using GameAssets game = Open();
        NativeBattleGatlingGun305Resources resources = game.ReadNativeGatlingGun305Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleGatlingGun305Resources restored = JsonSerializer.Deserialize<NativeBattleGatlingGun305Resources>(json)!;
        NativeBattleGatlingGun305ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(restored.MeshA, restored.ReadMesh(0xCED698));
        Assert.Equal(restored.SpriteC, restored.ReadSprite(0xCED504));
        Assert.Throws<NotSupportedException>(() => restored.ReadSprite(0xCED508));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_original_payload_extents_and_vertex_indices_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleGatlingGun305Resources resources = game.ReadNativeGatlingGun305Resources();
        NativeBattleGatlingGun305Resources altered;
        if (selected == 0)
        {
            altered = resources with { MeshC = resources.MeshC with { Bytes = resources.MeshC.Bytes.RemoveAt(resources.MeshC.Bytes.Length - 1) } };
        }
        else if (selected == 1)
        {
            altered = resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(resources.Camera.Data.Length - 1) } };
        }
        else if (selected == 2)
        {
            altered = resources with { CasterDat = resources.CasterDat with { Data = resources.CasterDat.Data.RemoveAt(resources.CasterDat.Data.Length - 1) } };
        }
        else
        {
            byte[] bytes = resources.MeshA.Bytes.ToArray();
            int faces = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 16), 50);
            altered = resources with { MeshA = resources.MeshA with { Bytes = bytes.ToImmutableArray() } };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleGatlingGun305ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Gatling Gun ingress.");
        return new(directory!);
    }
}
