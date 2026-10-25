using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCompanionResourceTests
{
    [Fact]
    public void Special143_retains_own_two_sections_and_exact_model142_aliases_without_geometry_or_textures()
    {
        byte[] bytes = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), bytes.Length);
        for (int index = 16; index < bytes.Length; index++)
        {
            bytes[index] = checked((byte)index);
        }
        NativeBattleModelDatSource source = new(143, false, 0, 293, "c0m127.dat", bytes.ToImmutableArray());
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 512 };
        NativeBattleModelCpuResourceRow shared = cpu.Rows[2];
        foreach (int field in new[] { 12, 20, 24, 44 })
        {
            shared = shared.Set(field, new(3, field * 4)) with { Lengths = shared.Lengths.SetItem(field, field) };
        }
        cpu = cpu with { Rows = cpu.Rows.SetItem(2, shared) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Rebase(NativeBattleModelCpuResources.EnemyAddress);

        (NativeBattleModelCpuResources result, NativeBattleFileStaging memory) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, 5, 2, special143: true);

        NativeBattleModelCpuResourceRow companion = result.Rows[5];
        foreach (int field in new[] { 12, 20, 24, 44 })
        {
            Assert.Equal(shared.Require(field), companion.Require(field));
            Assert.Equal(shared.Lengths[field], companion.Lengths[field]);
        }
        Assert.Equal(new NativeBattleModelCpuReference(4, 0), companion.Require(16));
        Assert.Equal(4, companion.Lengths[16]);
        Assert.Null(companion.Fields[48]);
        Assert.Equal(0, companion.PrivateLength4);
        Assert.Equal(0, companion.SharedLength8);
        Assert.Equal((byte)255, companion.LinkedPhysicalIndex36);
        Assert.Equal(bytes[16..24], NativeBattleModelEnemyHeap.Read(result, memory, companion.Require(28), 8));
        Assert.Equal(bytes[24..28], NativeBattleModelEnemyHeap.Read(result, memory, companion.Require(32), 4));
        Assert.Equal(524, result.EnemyCursor);
        Assert.Equal(shared, result.Rows[2]);
        Assert.Equal((byte)143, source.ModelId);
        Assert.Equal("c0m127.dat", source.Name);
    }

    [Fact]
    public void Special143_requires_the_actual_shared_model142_owner()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 512 };
        Assert.Throws<NotSupportedException>(() => NativeBattleModelEnemyHeap.Load(cpu, NativeBattleFileStaging.Unknown(), new(143, false, 0, 293, "c0m127.dat", []), 5, null, special143: true));
    }
}
