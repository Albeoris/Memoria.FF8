using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModule5707A0ResourcesTests
{
    [Fact]
    public void Original_kind_one_descriptors_and_child_sprites_keep_their_exact_directories_after_JSON()
    {
        using GameAssets game = Open();
        ImmutableArray<NativeBattleModuleResource> resources = game.ReadNativeBattleModuleResources();
        Assert.Equal(42, resources.Length);
        foreach (byte identity in new byte[] { 32, 41 })
        {
            NativeBattleModuleResource resource = resources[identity];
            Assert.Equal(identity, resource.Id);
            Assert.Equal((byte)1, resource.Kind);
            Assert.Equal(0x570CC0u, resource.WrapperCallback);
            Assert.Equal(0x5707A0u, resource.Callback);
            Assert.Equal(new byte[] { 0, 0, 0, 1 }, resource.Bytes);
            NativeBattleModuleResource restored = JsonSerializer.Deserialize<NativeBattleModuleResource>(JsonSerializer.Serialize(resource))!;
            restored.Validate();
            Assert.Equal(JsonSerializer.Serialize(resource), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(0xC7E53Cu, resources[32].Address);
        Assert.Equal(0xC7E538u, resources[41].Address);
        Assert.Equal(0xC7E3E8u, resources[31].Address);
        Assert.Equal(116, resources[31].Bytes.Length);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(resources[31].Bytes.AsSpan().Slice(8)));
        Assert.Equal(0, resources[31].Bytes[3]);
        for (ushort phase = 0; phase < 4; phase++)
        {
            Assert.NotEmpty(resources[31].SpriteFrame(phase).Quads);
        }
        Assert.Equal(0xC78CA0u, resources[0].Address);
        Assert.Equal(220, resources[0].Bytes.Length);
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(resources[0].Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Original_child_mesh_five_is_loaded_with_all_G4_vertices_materials_and_bounds()
    {
        using GameAssets game = Open();
        NativeBattleStaticMeshResource mesh = Assert.Single(game.ReadNativeEffectMeshes(), row => row.Id == 5);
        Assert.Equal(0xC7FE80u, mesh.Address);
        Assert.Equal(424, mesh.Bytes.Length);
        ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan();
        Assert.Equal(24, BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4)));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        Assert.Equal(8 + 24 * 8, cursor);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor));
            Assert.Equal(group == 5 ? 8 : 0, count);
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                ReadOnlySpan<byte> row = bytes.Slice(cursor + face * strides[group], strides[group]);
                Assert.Equal(0x38, row[3] & 0xFC);
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(4 + corner * 2)) * 4;
                    Assert.Equal(0, vertex % 8);
                    Assert.InRange(vertex, 0, 23 * 8);
                }
            }
            cursor += count * strides[group];
        }
        Assert.Equal(bytes.Length, cursor);
        Assert.Same(mesh, Assert.Single(game.ReadNativeEffectMeshes(), row => row.Id == 5));
    }

    private static GameAssets Open()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new(directory);
    }
}
