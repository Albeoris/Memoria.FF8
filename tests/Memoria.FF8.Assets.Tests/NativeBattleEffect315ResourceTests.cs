using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect315ResourceTests
{
    [Fact]
    public void Original_projected_frames_and_static_meshes_keep_their_distinct_native_formats()
    {
        using GameAssets game = Open();
        NativeBattleEffect315Resources resources = game.ReadNativeEffect315Resources();
        resources.Validate();
        int[] frames = [16, 14, 21];
        for (int index = 0; index < frames.Length; index++)
        {
            NativeBattleModuleResource sprite = resources.ReadSprite(SpriteAddresses[index]);
            Assert.Equal(SpriteLengths[index], sprite.Bytes.Length);
            for (int phase = 0; phase < frames[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
                Assert.Equal(20, frame.Quads.Length);
                Assert.Equal(index == 0, frame.ExtendedPacket);
            }
        }
        int[][] expected = [[0, 0, 0, 64, 0, 0, 0, 0], [0, 0, 0, 0, 0, 33, 0, 0], [0, 0, 0, 64, 0, 0, 0, 0], [0, 0, 24, 72, 0, 0, 0, 24]];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(MeshAddresses[index]);
            Assert.Equal(MeshLengths[index], mesh.Bytes.Length);
            Assert.Equal((uint)MeshVertexCounts[index], Dword(mesh.Bytes, 4));
            int cursor = checked((int)Dword(mesh.Bytes, 0));
            Assert.Equal(8 + MeshVertexCounts[index] * 8, cursor);
            for (int group = 0; group < 8; group++)
            {
                Assert.Equal((uint)expected[index][group], Dword(mesh.Bytes, cursor));
                cursor += 4 + expected[index][group] * strides[group];
            }
            Assert.Equal(mesh.Bytes.Length, cursor);
        }
        Assert.Equal(121, MeshVertexCounts.Max());
        Assert.Contains((short)103, Enumerable.Range(0, MeshVertexCounts[0]).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.Meshes[0].Bytes.AsSpan().Slice(8 + index * 8 + 6))));
        Assert.Contains((short)119, Enumerable.Range(0, MeshVertexCounts[3]).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.Meshes[3].Bytes.AsSpan().Slice(8 + index * 8 + 6))));
    }

    [Fact]
    public void Static_tables_sounds_vibration_and_address_domains_survive_JSON()
    {
        using GameAssets game = Open();
        NativeBattleEffect315Resources resources = game.ReadNativeEffect315Resources();
        NativeBattleEffect315Resources restored = JsonSerializer.Deserialize<NativeBattleEffect315Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal(0x641F1u, resources.ReadSound(SoundAAddress));
        Assert.Equal(0x642BCu, resources.ReadSound(SoundBAddress));
        Assert.Equal(new[] { 0, 256 }, resources.Vibration.Offsets.ToArray());
        Assert.Equal((ushort)22, resources.Vibration.ReadWord(256));
        Assert.Equal((ushort)0, resources.Vibration.ReadWord(258));
        Assert.Equal(282, resources.Vibration.Bytes.Length);
        Assert.Equal(resources.PointRows24.ToArray(), resources.ReadStatic(PointRowsAddress, 24).ToArray());
        Assert.Equal(resources.VectorRows48.ToArray(), resources.ReadStatic(VectorRowsAddress, 48).ToArray());
        Assert.Equal(resources.CasterColors48.ToArray(), resources.ReadStatic(CasterColorsAddress, 48).ToArray());
        Assert.Equal(resources.RecipientColors96.ToArray(), resources.ReadStatic(RecipientColorsAddress, 96).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SoundBAddress + 4, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(TextureNameAddress, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(VibrationAddress + VibrationLength, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ParticleAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(SpriteAddresses[0]));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(MeshAddresses[0]));
    }

    [Fact]
    public void Truncated_redirected_or_invalid_geometry_is_rejected_at_its_original_front()
    {
        using GameAssets game = Open();
        NativeBattleEffect315Resources resources = game.ReadNativeEffect315Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = resources.Meshes[0].Bytes.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprites = resources.Sprites.SetItem(0, resources.Sprites[0] with { Address = SpriteAddresses[1] }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SoundA = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { PointRows24 = resources.PointRows24.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { RecipientColors96 = resources.RecipientColors96.RemoveAt(0) }).Validate());
        byte[] bytes = resources.Meshes[0].Bytes.ToArray();
        int firstFace = checked((int)Dword(resources.Meshes[0].Bytes, 0)) + 4 * 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(firstFace + 4), ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = bytes.ToImmutableArray() }) }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect315 resources.");
        return new(directory!);
    }
}
