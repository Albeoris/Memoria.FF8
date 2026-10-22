using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRayBomb307BindingTests
{
    [Fact]
    public void Original_catalog_selects_Blue_Ray_Bomb_and_actual_initializer_global()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([307])[306];
        Assert.Equal((ushort)307, binding.Effect);
        Assert.Equal(0x5A1170u, binding.Entry);
        Assert.Equal(0x5A1150u, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag009.tim", 0x2242578), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag009.tim", binding.InitializerAsset!.Name);
        Assert.Equal(199200, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeRayBomb307Resources(), binding.RayBomb307Resources);
    }

    [Fact]
    public void Binding_JSON_keeps_original_mesh_sequence_sprites_tables_and_caster_source()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding source = game.ReadNativeEffectCatalogForEffects([307])[306];
        string json = JsonSerializer.Serialize(source);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleRayBomb307Resources resources = restored.RayBomb307Resources!;
        resources.Validate();
        Assert.Equal("FF8_EN:CDF6D8", resources.Camera.Identity);
        Assert.Equal(404, resources.Camera.Data.Length);
        Assert.Equal("FF8_EN:CDF86C", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(new uint[] { 0xCE190C, 0xCE2CE4, 0xCE2F9C, 0xCE3254, 0xCE350C, 0xCE3BB4 }, resources.Meshes.Select(mesh => mesh.Address).ToArray());
        Assert.Equal(new int[] { 5080, 696, 696, 696, 1704, 1304 }, resources.Meshes.Select(mesh => mesh.Bytes.Length).ToArray());
        Assert.Equal(new uint[] { 0xCE2CE4, 0xCE2F9C, 0xCE3254 }, resources.MeshSequence.ToArray());
        Assert.Equal(new uint[] { 0xCE13E0, 0xCE158C, 0xCE15B4, 0xCE1760 }, resources.Sprites.Select(sprite => sprite.Address).ToArray());
        Assert.Equal(new int[] { 16, 1, 16, 16 }, resources.Sprites.Select(sprite => (int)BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).ToArray());
        Assert.Equal(10, resources.SpawnX.Length);
        Assert.Equal(10, resources.SpawnZ.Length);
        Assert.Equal(2, resources.BirthAnchors.Length);
        Assert.Equal(2, resources.BirthDirections.Length);
        Assert.Equal(new uint[] { 0x61A89, 0x61A8C }, resources.Sounds.Select(sound => sound.Value).ToArray());
        Assert.Equal(source.InitializerAsset!.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the Blue Ray-Bomb binding.");
        return new(directory!);
    }
}
