using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLevelDeath74ResourcesTests
{
    [Fact]
    public void Original_resources_have_their_own_camera_caster_directory_and_addressed_node_banks()
    {
        using GameAssets game = Open();
        NativeBattleLevelDeath74Resources resources = game.ReadNativeLevelDeath74Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeLevelDeath74Resources());
        Assert.Equal(440, resources.Animation.Data.Length);
        Assert.Equal(0x158233Cu, resources.Model.Address);
        Assert.Equal(28, resources.Model.PoseSource.BoneCount);
        NativeBattleRawMesh mesh = Assert.Single(resources.Model.Geometry);
        Assert.Equal(302, mesh.Vertices.Length);
        Assert.Equal(507, mesh.Polygons.Count(face => !face.Quad));
        Assert.Equal(19, mesh.Polygons.Count(face => face.Quad));
        Assert.Equal(0x15863B0u, resources.CasterResource.Address);
        Assert.Equal(0x1B74, resources.CasterResource.Bytes.Length);
        Assert.Equal(3u, U32(resources.CasterResource.Bytes, 0));
        Assert.Equal(new[] { 0x14u, 0x1648u, 0x1698u, 0x1B74u }, Enumerable.Range(0, 4).Select(index => U32(resources.CasterResource.Bytes, 4 + index * 4)));
        Assert.NotEmpty(NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterResource.Bytes.Slice(0x14, 0x1634).AsSpan()));
        Assert.Same(resources.AuraNodes, resources.Nodes(0x1771F5C));
        Assert.Same(resources.DarkNodes, resources.Nodes(0x1776950));
        Assert.Throws<NotSupportedException>(() => resources.Nodes(0x1801FD0));
        Assert.Equal(33824, resources.FirstTim.Data.Length);
        Assert.Equal(16928, resources.SecondTim.Data.Length);
        Assert.Equal(0x557B1u, resources.AppearanceSound);
        Assert.Equal(0x5581Du, resources.StrikeSound);
    }

    [Fact]
    public void Original_banks_close_all_directory_kinds_channels_mesh_groups_and_morph_vertex_counts()
    {
        using GameAssets game = Open();
        NativeBattleLevelDeath74Resources resources = game.ReadNativeLevelDeath74Resources();
        Check(resources.AuraNodes, [5, 3, 2, 2], [15, 16, 20]);
        Check(resources.DarkNodes, [3, 0, 4, 0], []);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        List<int> reachedGroups = [];
        foreach (NativeBattleCuragaExecutableSource source in new[] { resources.AuraNodes, resources.DarkNodes })
        {
            for (int index = 0; index < 16; index++)
            {
                int relative = checked((int)U32(source.Data, 0x84 + index * 4));
                if (relative == 0)
                {
                    continue;
                }
                int vertices = checked((int)U32(source.Data, relative + 4));
                int cursor = relative + checked((int)U32(source.Data, relative));
                Assert.InRange(vertices, 4, 128);
                for (int group = 0; group < 8; group++)
                {
                    int count = checked((int)U32(source.Data, cursor));
                    if (count != 0)
                    {
                        reachedGroups.Add(group);
                    }
                    cursor += 4 + count * strides[group];
                }
                Assert.InRange(cursor, relative + 8, source.Data.Length);
            }
        }
        Assert.Equal(new[] { 2, 3, 5, 7 }, reachedGroups.Distinct().Order());

        static void Check(NativeBattleCuragaExecutableSource source, int[] expectedCounts, int[] expectedFrames)
        {
            int[] counts = Enumerable.Range(0, 4).Select(table => Enumerable.Range(0, 16).Count(index => U32(source.Data, 4 + table * 64 + index * 4) != 0)).ToArray();
            Assert.Equal(expectedCounts, counts);
            List<int> frames = [];
            for (int index = 0; index < 16; index++)
            {
                int sprite = checked((int)U32(source.Data, 0x44 + index * 4));
                if (sprite != 0)
                {
                    frames.Add(BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(sprite + 8)));
                }
                int node = checked((int)U32(source.Data, 4 + index * 4));
                if (node != 0)
                {
                    int life = source.Data[node + 0x12];
                    for (int field = 0xC8; field <= 0x144; field += 4)
                    {
                        int pointer = checked((int)U32(source.Data, node + field));
                        Assert.InRange(pointer, 0, source.Data.Length - Math.Max(80, 2 * life));
                    }
                }
                int morph = checked((int)U32(source.Data, 0xC4 + index * 4));
                if (morph != 0)
                {
                    Assert.Equal(680u, U32(source.Data, morph));
                    Assert.Equal(84u, U32(source.Data, morph + 4));
                }
            }
            Assert.Equal(expectedFrames, frames);
        }
    }

    [Fact]
    public void Resource_state_is_independent_of_Death_and_survives_JSON_without_aliasing_mutable_banks()
    {
        using GameAssets game = Open();
        NativeBattleLevelDeath74Resources resources = game.ReadNativeLevelDeath74Resources();
        NativeBattleDeathResources death = game.ReadNativeDeathResources();
        Assert.Equal(death.Model.Bytes, resources.Model.Bytes);
        Assert.NotEqual(death.Animation.Identity, resources.Animation.Identity);
        Assert.NotEqual(death.Animation.Data.Length, resources.Animation.Data.Length);
        Assert.Equal(0u, U32(resources.AuraNodes.Data, 0));
        byte[] changed = resources.AuraNodes.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(changed, 1);
        NativeBattleCuragaExecutableSource changedSource = resources.AuraNodes with { Data = changed.ToImmutableArray() };
        Assert.Equal(1u, U32(changedSource.Data, 0));
        Assert.Equal(0u, U32(resources.AuraNodes.Data, 0));
        Assert.Equal(0u, U32(resources.DarkNodes.Data, 0));
        Assert.Equal(0u, U32(death.AuraActors, 0));
        string json = JsonSerializer.Serialize(resources);
        NativeBattleLevelDeath74Resources restored = JsonSerializer.Deserialize<NativeBattleLevelDeath74Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(0u, U32(game.ReadNativeLevelDeath74Resources().AuraNodes.Data, 0));
    }

    [Fact]
    public void Missing_truncated_or_foreign_sources_fail_without_materializing_other_module_resources()
    {
        using GameAssets game = Open();
        NativeBattleLevelDeath74Resources resources = game.ReadNativeLevelDeath74Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { AuraNodes = resources.AuraNodes with { ResourceAddress = 0x1801FD0 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { DarkNodes = resources.DarkNodes with { Data = resources.DarkNodes.Data.RemoveAt(resources.DarkNodes.Data.Length - 1) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Animation = resources.Animation with { Data = resources.Animation.Data.RemoveAt(439) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CasterResource = resources.CasterResource with { Bytes = resources.CasterResource.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { FirstTim = resources.FirstTim with { Identity = resources.SecondTim.Identity } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check LV?Death.");
        return new(directory!);
    }

    private static uint U32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
}
