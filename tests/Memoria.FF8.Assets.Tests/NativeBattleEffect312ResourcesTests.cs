using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect312ResourcesTests
{
    [Fact]
    public void Original_meshes_sprite_camera_TIM_and_constants_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleEffect312Resources resources = game.ReadNativeEffect312Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect312Resources());
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(MeshLengths.ToArray(), MeshAddresses.Select(address => resources.Mesh(address).Bytes.Length).ToArray());
        Assert.Equal(SpriteLength, resources.Sprite(SpriteAddress).Bytes.Length);
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite(SpriteAddress).Bytes.AsSpan(8, 2)));
        Assert.Equal(0x64209u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(SoundAddresses[0], 4)));
        Assert.Equal(0x6420Cu, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(SoundAddresses[1], 4)));
        Assert.Equal(new uint[] { 0xCDEE24, 0xCDF0DC, 0xCDF394 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(MeshTableAddress + (uint)index * 4, 4))).ToArray());
        Assert.Equal(24, resources.CapturePacket.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(CapturePacketAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(CapturePacketAddress - 1, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xCDF654, 4));
        Assert.Throws<NotSupportedException>(() => resources.Mesh(MeshTableAddress));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(CameraAddress));
    }

    [Fact]
    public void Addressed_resources_survive_JSON_and_reject_malformed_original_structures()
    {
        using GameAssets game = Open();
        NativeBattleEffect312Resources resources = game.ReadNativeEffect312Resources();
        NativeBattleEffect312Resources restored = JsonSerializer.Deserialize<NativeBattleEffect312Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { Camera = restored.Camera with { Data = restored.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { MainTim = restored.MainTim with { Data = restored.MainTim.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { StaticFronts = restored.StaticFronts.Remove(CapturePacketAddress) }).Validate());
        NativeBattleStaticMeshResource mesh = restored.Mesh(0xCDAF9C);
        byte[] bytes = mesh.Bytes.ToArray();
        int faces = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 12 + 4), 352);
        NativeBattleEffect312Resources badIndex = restored with { Meshes = restored.Meshes.SetItem(mesh.Address, mesh with { Bytes = bytes.ToImmutableArray() }) };
        Assert.Throws<InvalidDataException>(() => badIndex.Validate());
        bytes = mesh.Bytes.ToArray();
        bytes[faces + 12 + 3] = 0x2C;
        NativeBattleEffect312Resources badCommand = restored with { Meshes = restored.Meshes.SetItem(mesh.Address, mesh with { Bytes = bytes.ToImmutableArray() }) };
        Assert.Throws<InvalidDataException>(() => badCommand.Validate());
        NativeBattleModuleResource sprite = restored.Sprite(SpriteAddress);
        byte[] spriteBytes = sprite.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(spriteBytes.AsSpan(32), 0x80000001);
        Assert.Throws<InvalidDataException>(() => (restored with { Sprites = restored.Sprites.SetItem(SpriteAddress, sprite with { Bytes = spriteBytes.ToImmutableArray() }) }).Validate());
        byte[] texture = restored.MainTim.Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(texture.AsSpan(12), 321);
        Assert.Throws<InvalidDataException>(() => (restored with { MainTim = restored.MainTim with { Data = texture.ToImmutableArray() } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original Effect312 resources.");
        return new(directory!);
    }
}
