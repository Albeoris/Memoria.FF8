using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMegaSpark279ResourcesTests
{
    [Fact]
    public void Original_four_programmes_have_exact_caches_geometry_groups_and_morph_bounds()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMegaSpark279Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMegaSpark279Resources());
        var sources = new[] { resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC, resources.ProgrammeD };
        int[] lengths = [14888, 4732, 3620, 3368];
        int[] nodes = [27, 5, 2, 4];
        int[] frames = [90, 16, 12, 18];
        int[] geometryCounts = [5, 3, 2, 2];
        int[] cacheLengths = [1844, 328, 136, 272];
        HashSet<int> groups = [];
        int maximumMorph = 0;
        for (int index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            Assert.Equal(lengths[index], source.Data.Length);
            Assert.Equal((uint)geometryCounts[index], Dword(source.Data, 0));
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal((uint)nodes[index], Dword(source.Data, table));
            Assert.Equal((uint)frames[index], Dword(source.Data, table + 4));
            var workspace = new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xA5, cacheLengths[index] + 16).ToImmutableArray()).Initialize7016B0(source, cacheLengths[index]);
            for (int frame = 0; frame < frames[index]; frame++)
            {
                int callbacks = 0;
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    callbacks++;
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int geometry = checked((int)Dword(source.Data, 8 + node.GeometryIndex * 4));
                        maximumMorph = Math.Max(maximumMorph, checked((int)Dword(source.Data, geometry + 4) * 8));
                    }
                    return retained;
                }).Workspace;
                Assert.Equal(nodes[index], callbacks);
                Assert.All(workspace.Data.Skip(cacheLengths[index]), value => Assert.Equal((byte)0xA5, value));
            }
            Assert.Equal(frames[index], workspace.Frame);
            for (int geometry = 0; geometry < geometryCounts[index]; geometry++)
            {
                int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
                var mesh = resources.ReadGeometry(source.Address + (uint)offset);
                Assert.Equal(source.Data.AsSpan().Slice(offset, mesh.Bytes.Length).ToArray(), mesh.Bytes.ToArray());
                Assert.InRange(Dword(mesh.Bytes, 4), 1u, 64u);
                int cursor = checked((int)Dword(mesh.Bytes, 0));
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(mesh.Bytes, cursor);
                    if (count != 0)
                    {
                        groups.Add(group);
                    }
                    cursor = checked(cursor + 4 + (int)count * strides[group]);
                }
                Assert.Equal(mesh.Bytes.Length, cursor);
            }
        }
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, groups.Order().ToArray());
        Assert.Equal(ScratchLength, maximumMorph);
        Assert.Equal(10, resources.Sprite.Kind);
        _ = resources.ReadSprite(SpriteAddress).ProjectedFrame571C80(7);
        Assert.Equal(0x641A3u, resources.Sound);
    }

    [Fact]
    public void Original_lookup_domains_points_rows_and_JSON_preserve_exact_consumed_fronts()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMegaSpark279Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleMegaSpark279Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal(24, restored.SourcePoints24.Length);
        Assert.Equal(8, restored.ExtraPoint8.Length);
        Assert.Equal(new short[] { 17, 18, 21 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(restored.SourcePoints24.AsSpan().Slice(index * 8 + 6))));
        Assert.Equal((short)10, BinaryPrimitives.ReadInt16LittleEndian(restored.ExtraPoint8.AsSpan().Slice(6)));
        for (int table = 0; table < 3; table++)
        {
            for (int row = 0; row < 8; row++)
            {
                Assert.Equal(restored.ReadStatic(AttachmentTablesAddress + (uint)(table * 192 + row * 24), 24).ToArray(), restored.ReadAttachmentRow(table, row).ToArray());
            }
        }
        Assert.Equal(16384, restored.Distance16384.Length);
        Assert.Equal(384, restored.NormalizationConsumed384.Length);
        Assert.Equal(4098, restored.SinePrefix4098.Length);
        Assert.Equal(restored.Distance16384[16383], restored.ReadDistanceByte(127, 127));
        foreach (int index in new[] { 64, 255 })
        {
            Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(restored.NormalizationConsumed384.AsSpan().Slice((index - 64) * 2)), restored.ReadNormalizationWord(index));
        }
        Assert.Equal(restored.ReadStatic(NormalizationAddress, 384).ToArray(), restored.ReadStatic(BodyNormalizationAddress, 384).ToArray());
        for (int index = 256; index <= 2048; index += 256)
        {
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(restored.SinePrefix4098.AsSpan().Slice(index * 2)), restored.ReadSineWord(index));
        }
        Assert.Throws<NotSupportedException>(() => restored.ReadSineWord(2049));
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(SineAddress, 8192));
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(ArenaAddress + ScratchOffset, 8));
        Assert.Throws<NotSupportedException>(() => restored.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadGeometry(SourcePointsAddress));
    }

    [Fact]
    public void Changed_programme_faces_camera_sprite_and_truncated_lookup_fronts_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMegaSpark279Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.ProgrammeA.Data.ToArray();
        int table = checked((int)Dword(resources.ProgrammeA.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 26);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygon = checked(geometry + (int)Dword(resources.ProgrammeA.Data, geometry));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < strides.Length; group++)
        {
            uint count = Dword(resources.ProgrammeA.Data, polygon);
            if (count != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygon + 8), 0xFFFF);
                break;
            }
            polygon = checked(polygon + 4 + (int)count * strides[group]);
        }
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourcePoints24 = resources.SourcePoints24.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { AttachmentTables = resources.AttachmentTables.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Distance16384 = resources.Distance16384.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { NormalizationConsumed384 = resources.NormalizationConsumed384.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SinePrefix4098 = resources.SinePrefix4098.RemoveAt(0) }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Mega Spark resources.");
        return new(directory!);
    }
}
