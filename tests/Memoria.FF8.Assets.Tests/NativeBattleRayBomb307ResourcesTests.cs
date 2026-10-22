using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRayBomb307ResourcesTests
{
    [Fact]
    public void Original_mesh_sprite_camera_and_scalar_sources_have_complete_bounds()
    {
        using GameAssets game = Open();
        NativeBattleRayBomb307Resources resources = game.ReadNativeRayBomb307Resources();
        Assert.Same(resources, game.ReadNativeRayBomb307Resources());
        NativeBattleRayBomb307ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 5080, 696, 696, 696, 1704, 1304 }, resources.Meshes.Select(mesh => mesh.Bytes.Length));
        Assert.Equal(new[] { 428, 40, 428, 428 }, resources.Sprites.Select(sprite => sprite.Bytes.Length));
        Assert.Equal(new uint[] { 0xCE2CE4, 0xCE2F9C, 0xCE3254 }, resources.MeshSequence);
        Assert.Equal(404, resources.Camera.Data.Length);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(10, resources.SpawnX.Length);
        Assert.Equal(10, resources.SpawnZ.Length);
        Assert.Equal(new NativeBattleModuleAnchor(90, 50, -140, 0), resources.BirthAnchors[0]);
        Assert.Equal(new NativeBattleModuleAnchor(200, 300, -1500, 0), resources.BirthDirections[0]);
        Assert.Equal(new uint[] { 0x61A89, 0x61A8C }, resources.Sounds.Select(sound => sound.Value));
    }

    [Fact]
    public void Typed_original_resources_repeat_JSON_without_creating_a_mutable_model_or_packet_clone()
    {
        using GameAssets game = Open();
        NativeBattleRayBomb307Resources resources = game.ReadNativeRayBomb307Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleRayBomb307Resources restored = JsonSerializer.Deserialize<NativeBattleRayBomb307Resources>(json)!;
        NativeBattleRayBomb307ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.Meshes[1].Bytes.ToArray(), restored.ReadMesh(0xCE2CE4).Bytes.ToArray());
        Assert.Equal(resources.Sprites[1].Bytes.ToArray(), restored.ReadSprite(0xCE158C).Bytes.ToArray());
        Assert.Throws<NotSupportedException>(() => restored.ReadMesh(0xCE13E0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_selected_vertices_sources_and_tables_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleRayBomb307Resources resources = game.ReadNativeRayBomb307Resources();
        NativeBattleRayBomb307Resources altered;
        if (selected == 0)
        {
            altered = resources with { CasterDat = resources.CasterDat with { Data = resources.CasterDat.Data.RemoveAt(0) } };
        }
        else if (selected == 1)
        {
            byte[] mesh = resources.Meshes[1].Bytes.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(136, 2), 24);
            altered = resources with { Meshes = resources.Meshes.SetItem(1, resources.Meshes[1] with { Bytes = mesh.ToImmutableArray() }) };
        }
        else if (selected == 2)
        {
            altered = resources with { Sprites = resources.Sprites.SetItem(1, resources.Sprites[1] with { Bytes = resources.Sprites[1].Bytes.RemoveAt(0) }) };
        }
        else
        {
            altered = resources with { SpawnX = resources.SpawnX.RemoveAt(9) };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleRayBomb307ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Ray-Bomb ingress.");
        return new(directory!);
    }
}
