using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBladeShot12ResourcesTests
{
    [Fact]
    public void Original_five_meshes_five_sprites_four_morph_sources_camera_and_sounds_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleBladeShot12Resources resources = game.ReadNativeBladeShot12Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBladeShot12Resources());
        Assert.Equal(new[] { 0x5D0, 0x5D0, 0x828, 0x718, 0x6D0 }, new[] { resources.MeshA.Bytes.Length, resources.MeshB.Bytes.Length, resources.MeshC.Bytes.Length, resources.MeshD.Bytes.Length, resources.MeshE.Bytes.Length });
        Assert.Equal(new[] { 0x1BC, 0x90, 0x194, 0x160, 0x118 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length, resources.SpriteD.Bytes.Length, resources.SpriteE.Bytes.Length });
        foreach (uint address in new[] { MorphSourceA0, MorphSourceA1, MorphSourceB0, MorphSourceB1 })
        {
            NativeBattleModuleResource source = resources.ReadMorphSource(address);
            Assert.Equal(MorphSourceLength, source.Bytes.Length);
            Assert.Equal((uint)MutableVertexCount, BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(address + 4, 4).AsSpan()));
            Assert.Equal(source.Bytes.AsSpan().Slice(8).ToArray(), resources.Read(address + 8, MutableVerticesLength).ToArray());
        }
        Assert.Equal(new byte[] { 0, 1, 2, 0 }, resources.SpriteA.Bytes.AsSpan().Slice(4, 4).ToArray());
        Assert.Equal(new byte[] { 0, 1, 0, 0 }, resources.SpriteB.Bytes.AsSpan().Slice(4, 4).ToArray());
        Assert.Equal((uint)MutableVertexCount, BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(MeshAAddress + 4, 4).AsSpan()));
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x61AB1u, resources.OpeningSound);
        Assert.Equal(0x61AAFu, resources.EndingSound);
        Assert.Throws<InvalidDataException>(() => resources.Read(MutableVerticesA, 2));
        Assert.Throws<InvalidDataException>(() => resources.Read(MutableVerticesB, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadMorphSource(MeshAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(MorphSourceA0));
    }

    [Fact]
    public void Original_PE_vertices_seed_once_and_keep_actual_W_with_resource_and_state_JSON()
    {
        using GameAssets game = Open();
        NativeBattleBladeShot12Resources resources = game.ReadNativeBladeShot12Resources();
        NativeBattleBladeShot12Resources restoredResources = JsonSerializer.Deserialize<NativeBattleBladeShot12Resources>(JsonSerializer.Serialize(resources))!;
        restoredResources.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restoredResources));
        NativeBattleBladeShot12State loaded = NativeBattleBladeShot12State.FromColdProcess().EnsureVertexSources(restoredResources);
        Assert.Equal(resources.MeshA.Bytes.AsSpan().Slice(8, MutableVerticesLength).ToArray(), loaded.Read(MutableVerticesA, MutableVerticesLength));
        Assert.Equal(resources.MeshB.Bytes.AsSpan().Slice(8, MutableVerticesLength).ToArray(), loaded.Read(MutableVerticesB, MutableVerticesLength));
        for (uint vertex = 0; vertex < MutableVertexCount; vertex++)
        {
            Assert.Equal((ushort)0, loaded.ReadWord(MutableVerticesA + vertex * 8 + 6));
            Assert.Equal((ushort)0, loaded.ReadWord(MutableVerticesB + vertex * 8 + 6));
        }
        NativeBattleBladeShot12State warm = loaded.WriteWord(MutableVerticesA + 6, 0xDEAD).WriteWord(MutableVerticesB + 6, 0xBEEF);
        NativeBattleBladeShot12State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleBladeShot12State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(warm)))!);
        restored.Validate();
        Assert.Same(restored, restored.EnsureVertexSources(resources));
        Assert.Equal((ushort)0xDEAD, restored.ReadWord(MutableVerticesA + 6));
        Assert.Equal((ushort)0xBEEF, restored.ReadWord(MutableVerticesB + 6));
    }

    [Fact]
    public void Invalid_selected_faces_sources_sprite_palette_camera_and_sound_fail_at_bounded_fronts()
    {
        using GameAssets game = Open();
        NativeBattleBladeShot12Resources resources = game.ReadNativeBladeShot12Resources();
        byte[] mesh = resources.MeshD.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 110 * 8 + 24), 220);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshD = resources.MeshD with { Bytes = mesh.ToImmutableArray() } }).Validate());
        byte[] source = resources.SourceB1.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 50);
        Assert.Throws<InvalidDataException>(() => (resources with { SourceB1 = resources.SourceB1 with { Bytes = source.ToImmutableArray() } }).Validate());
        byte[] sprite = resources.SpriteE.Bytes.ToArray();
        int frame = BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan(10));
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(frame + 18), 0x4000);
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteE = resources.SpriteE with { Bytes = sprite.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { OpeningSound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { EndingSound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => NativeBattleBladeShot12State.FromColdProcess().EnsureVertexSources(resources with { MeshA = resources.MeshA with { Address = MeshBAddress } }));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Blade Shot12 resources.");
        return new(directory!);
    }
}
