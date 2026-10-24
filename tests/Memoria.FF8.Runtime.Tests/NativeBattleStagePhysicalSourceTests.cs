using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagePhysicalSourceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0x1980)]
    public void Specialized_load_reserves_from_the_actual_directory_before_enemy_loads(int directoryOffset)
    {
        NativeBattleStagePhysicalSource original = Source();
        byte[] prefix = new byte[directoryOffset + original.Prefix.Length];
        original.Prefix.AsSpan().CopyTo(prefix.AsSpan(directoryOffset));
        NativeBattleStagePhysicalSource source = original with { DirectoryOffset = directoryOffset, Prefix = prefix.ToImmutableArray() };
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 216 };
        (NativeBattleFileStaging staging, NativeBattleModelCpuResources published) = source.Publish500EA0(NativeBattleFileStaging.Unknown(), cpu);
        cpu = published;
        (NativeBattleModelCpuResources reserved, uint cursor) = source.Reserve512500(cpu);
        Assert.Equal(source.Address + (uint)directoryOffset + 0x7000, cursor);
        Assert.Equal(directoryOffset + 0x7000, reserved.EnemyCursor);
        Assert.Same(cpu.Cpu, reserved.Cpu);
        Assert.Equal(cpu.Rows, reserved.Rows);
        Assert.Equal(cpu.ScratchMaximum, reserved.ScratchMaximum);
        Assert.Throws<NotSupportedException>(() => staging.Read(cursor - 1, 1));
    }

    [Theory]
    [InlineData(4, 12)]
    [InlineData(20, 20)]
    public void Load_reserves_stage_prefix_before_enemy_allocations_and_keeps_shared_tail(int previousMaximum, int expectedMaximum)
    {
        NativeBattleStagePhysicalSource source = Source();
        NativeBattleFileStaging original = NativeBattleFileStaging.Unknown().Write(source.Address + (uint)source.Prefix.Length, new byte[] { 0xCA, 0xFE });
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 999, ScratchMaximum = (ushort)previousMaximum };
        (NativeBattleFileStaging staging, NativeBattleModelCpuResources updated) = source.Publish500EA0(original, cpu);
        Assert.Equal(source.Prefix.ToArray(), staging.Read(source.Address, source.Prefix.Length));
        Assert.Equal(new byte[] { 0xCA, 0xFE }, staging.Read(source.Address + (uint)source.Prefix.Length, 2));
        Assert.Equal(72, updated.EnemyCursor);
        Assert.Equal((ushort?)expectedMaximum, updated.ScratchMaximum);
        Assert.Same(cpu.Cpu, updated.Cpu);
        Assert.Equal(cpu.Rows, updated.Rows);
        Assert.Equal(999, cpu.EnemyCursor);
        Assert.Equal(source.Address + 52u, source.GroupGeometryAddress(0));
        Assert.Equal(12, source.GroupGeometryLength(0));
        Assert.Null(source.GroupGeometryAddress(1));
        Assert.Null(source.GroupGeometryLength(1));
    }

    [Fact]
    public void Source_roundtrip_preserves_addressed_bytes_and_rejects_invalid_directories()
    {
        NativeBattleStagePhysicalSource source = Source();
        string json = JsonSerializer.Serialize(source);
        NativeBattleStagePhysicalSource restored = JsonSerializer.Deserialize<NativeBattleStagePhysicalSource>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (restored with { Address = restored.Address + 4 }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { DirectoryOffset = restored.Prefix.Length }).Validate());
        byte[] invalid = restored.Prefix.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(24), invalid.Length - 4);
        Assert.Throws<InvalidDataException>(() => (restored with { Prefix = invalid.ToImmutableArray() }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.GroupGeometryAddress(4));
    }

    [Fact]
    public void Load_requires_the_existing_scratch_capacity_instead_of_a_cold_default()
    {
        NativeBattleStagePhysicalSource source = Source();
        Assert.Throws<NotSupportedException>(() => source.Publish500EA0(NativeBattleFileStaging.Unknown(), NativeBattleModelCpuResources.Unknown()));
    }

    private static NativeBattleStagePhysicalSource Source()
    {
        byte[] bytes = new byte[72];
        Put(0, 6);
        int[] sections = [32, 72, 72, 72, 72, 72, 80];
        for (int index = 0; index < sections.Length; index++)
        {
            Put(4 + index * 4, sections[index]);
        }
        Put(32, 3);
        Put(36, 16);
        Put(40, 20);
        Put(44, 32);
        Put(60, 12);
        return new(NativeBattleModelCpuResources.EnemyAddress, 0, bytes.ToImmutableArray());

        void Put(int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
