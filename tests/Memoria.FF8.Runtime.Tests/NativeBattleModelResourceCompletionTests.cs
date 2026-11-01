using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceCompletionTests
{
    [Fact]
    public void Invalidating_resource_ownership_preserves_shared_memory_and_native_row_fields()
    {
        NativeBattleModelCpuResourceRow row = Rows()[0].Set(16, new(0, 128)) with { PrivateLength4 = 64, SharedLength8 = 256 };
        NativeBattleModelCpuResources original = NativeBattleModelCpuResources.Unknown() with { Rows = Enumerable.Repeat(row, 11).ToImmutableArray(), EnemyCursor = 123 };
        NativeBattleModelCpuResources invalidated = original.InvalidateResourceExtents();
        Assert.Same(original.Cpu, invalidated.Cpu);
        Assert.Equal(123, invalidated.EnemyCursor);
        Assert.All(invalidated.Rows, current =>
        {
            Assert.Empty(current.Lengths);
            Assert.Same(row.Fields, current.Fields);
            Assert.Equal(row.PrivateLength4, current.PrivateLength4);
            Assert.Equal(row.SharedLength8, current.SharedLength8);
        });
        Assert.NotEmpty(original.Rows[0].Lengths);
    }

    [Theory]
    [InlineData(0x5077B0u)]
    [InlineData(0x5079B0u)]
    public void Replacing_a_party_body_replaces_prior_section_extents(uint callback)
    {
        NativeBattleModelDatSource source = Body();
        ImmutableArray<NativeBattleModelCpuResourceRow> rows = Rows();
        NativeBattleModelResourceCompletion result = NativeBattleModelResourceCopies.Complete(Bind(callback, source), NativeBattleModelCpuBanks.Unknown(), rows, 4, null);
        NativeBattleModelCpuResourceRow body = result.Rows[4];
        Assert.Equal(16, body.Lengths[12]);
        Assert.Equal(32, body.Lengths[16]);
        Assert.Equal(16, body.Lengths[20]);
        Assert.Equal(16, body.Lengths[44]);
        Assert.False(body.Lengths.ContainsKey(48));
        Assert.Null(body.Fields[48]);
        Assert.False(body.Lengths.ContainsKey(8));
        Assert.False(body.Lengths.ContainsKey(28));
        Assert.Equal(callback == 0x5079B0, body.Lengths.ContainsKey(24));
        Assert.Equal(callback == 0x5079B0, body.Lengths.ContainsKey(40));
        if (callback == 0x5079B0)
        {
            Assert.Equal(16, body.Lengths[24]);
            Assert.Equal(16, body.Lengths[40]);
        }
    }

    [Fact]
    public void Replacing_a_linked_rig_replaces_its_extents_and_preserves_the_body_geometry_extent()
    {
        NativeBattleModelResourceCompletion first = NativeBattleModelResourceCopies.Complete(Bind(0x5077B0, Body()), NativeBattleModelCpuBanks.Unknown(), Rows(), 4, null);
        NativeBattleModelDatSource linked = Source([64, 80, 112, 128, 144, 160, 176, 192, 208]);
        NativeBattleModelResourceCompletion result = NativeBattleModelResourceCopies.Complete(Bind(0x507BF0, linked), first.Cpu, first.Rows, 4, 5);
        NativeBattleModelCpuResourceRow rig = result.Rows[5];
        Assert.Equal(3, rig.Lengths.Count);
        Assert.Equal(16, rig.Lengths[12]);
        Assert.Equal(32, rig.Lengths[16]);
        Assert.Equal(16, rig.Lengths[20]);
        Assert.Equal(32, result.Rows[4].Lengths[16]);
        Assert.Equal(16, result.Rows[4].Lengths[24]);
        Assert.Equal(16, result.Rows[4].Lengths[40]);
        Assert.Equal(new NativeBattleModelCpuReference(0, 96), rig.Require(16));
    }

    [Fact]
    public void Appended_weapon_geometry_is_readable_through_the_patched_body_directory_and_survives_a_snapshot()
    {
        NativeBattleModelResourceCompletion first = NativeBattleModelResourceCopies.Complete(Bind(0x5077B0, Body()), NativeBattleModelCpuBanks.Unknown(), Rows(), 4, null);
        NativeBattleModelDatSource weapon = Source([64, 88, 104, 120]);
        NativeBattleModelResourceCompletion result = NativeBattleModelResourceCopies.Complete(Bind(0x507E20, weapon), first.Cpu, first.Rows, 4, null);
        result = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelResourceCompletion>(NativeBattleTestSnapshots.Json(result))!;
        NativeBattleModelCpuResourceRow body = result.Rows[4];
        Assert.Equal(first.Rows[4].Require(16), body.Require(16));
        Assert.Equal(120, body.Lengths[16]);
        Assert.Equal(16, body.Lengths[24]);
        Assert.Equal(16, body.Lengths[40]);
        NativeBattleModelCpuReference geometry = body.Require(16);
        Assert.Equal(72, BinaryPrimitives.ReadInt32LittleEndian(result.Cpu.Banks[0].Read(geometry.Offset + 8, 4)));
        int maximumRead = 0;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, 1);
        Assert.Equal(2, meshes.Length);
        Assert.All(meshes, mesh => Assert.Empty(mesh.Polygons));
        Assert.True(maximumRead > first.Rows[4].Lengths[16]);
        Assert.Throws<NotSupportedException>(() => result.Cpu.Banks[0].Read(geometry.Offset + body.Lengths[16], 1));

        byte[] Read(int offset, int length)
        {
            Assert.InRange(offset, 0, body.Lengths[16] - length);
            maximumRead = Math.Max(maximumRead, offset + length);
            return result.Cpu.Banks[geometry.Bank].Read(geometry.Offset + offset, length);
        }
    }

    private static ImmutableArray<NativeBattleModelCpuResourceRow> Rows()
    {
        NativeBattleModelCpuResourceRow stale = NativeBattleModelCpuResourceRow.Unknown() with { Lengths = new[] { 8, 12, 16, 20, 24, 28, 40, 44, 48 }.ToImmutableDictionary(field => field, _ => 1) };
        return Enumerable.Repeat(stale, 11).ToImmutableArray();
    }

    private static NativeBattleModelResourceBind Bind(uint callback, NativeBattleModelDatSource source) => new(new(0, 0, 0, 0, callback, 2), source, new([]));

    private static NativeBattleModelDatSource Body()
    {
        NativeBattleModelDatSource source = Source([64, 80, 112, 128, 128, 144, 160, 176, 192, 208, 224]);
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(80), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(84), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(88), 16);
        return source with { Bytes = bytes.ToImmutableArray() };
    }

    private static NativeBattleModelDatSource Source(int[] sections)
    {
        byte[] bytes = new byte[sections[^1] + 16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, sections.Length);
        for (int section = 0; section < sections.Length; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + section * 4), sections[section]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + sections.Length * 4), bytes.Length);
        return new(0, false, 0, 0, "test.dat", bytes.ToImmutableArray());
    }
}
