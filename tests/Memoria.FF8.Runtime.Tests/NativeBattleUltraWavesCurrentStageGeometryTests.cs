using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWavesCurrentStageGeometryTests
{
    [Fact]
    public void Capture_geometry_follows_reloaded_directory_instead_of_original_group_offset()
    {
        const uint directory = 0x193A998;
        const uint loaded = 0x2100000;
        byte[] original = Directory(32, 96);
        BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(64), 1);
        byte[] replacement = Directory(64, 120);
        BinaryPrimitives.WriteUInt32LittleEndian(replacement.AsSpan(64), 0x3C000480);
        BinaryPrimitives.WriteUInt32LittleEndian(replacement.AsSpan(96), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(replacement.AsSpan(100), 8);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(directory, original).Write(loaded + 0x137C, replacement);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = directory };
        Assert.Equal((directory + 64, 64), stage.CurrentGroupGeometryRange(1, staging.Read)!.Value);
        NativeBattleStageReloadCopy copy = NativeBattleStageReload512AC0.Plan(stage, 1, loaded, staging.Read);
        staging = staging.Write(copy.Destination, staging.Read(copy.Source, copy.Length));
        stage = NativeBattleTestSnapshots.RestoreJson<NativeBattleStageState>(NativeBattleTestSnapshots.Json(stage))!;

        (uint address, int length) = stage.CurrentGroupGeometryRange(1, staging.Read)!.Value;
        Assert.Equal(directory + 96, address);
        Assert.Equal(56, length);
        Assert.Equal(address, stage.CurrentGroupGeometryAddress(1, staging.Read));
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, 1);
        Assert.Empty(Assert.Single(meshes).Vertices);
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryReader.Read((offset, count) => staging.Read(directory + 64 + (uint)offset, count), 1));

        byte[] Read(int offset, int count)
        {
            Assert.InRange(offset, 0, length - count);
            return staging.Read(address + (uint)offset, count);
        }
    }

    [Fact]
    public void Empty_current_group_does_not_borrow_original_geometry()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x500000 };
        byte[] bytes = Directory(64, 120);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x500000, bytes);
        Assert.Null(stage.CurrentGroupGeometryRange(0, staging.Read));
        Assert.Null(stage.CurrentGroupGeometryAddress(0, staging.Read));
    }

    [Fact]
    public void Unknown_current_directory_requires_its_actual_producer()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = null };
        Assert.Throws<NotSupportedException>(() => stage.CurrentGroupGeometryRange(1, (_, _) => throw new InvalidOperationException("An unknown pointer cannot select resource bytes.")));
    }

    [Fact]
    public void Current_geometry_extent_cannot_leave_its_group()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x500000 };
        byte[] bytes = Directory(64, 129);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x500000, bytes);
        Assert.Throws<InvalidDataException>(() => stage.CurrentGroupGeometryRange(1, staging.Read));
    }

    [Fact]
    public void Address_only_consumer_keeps_its_existing_unsigned_extent_contract()
    {
        const uint directory = 0x500000;
        byte[] bytes = Directory(64, 0x80000040);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x80000080);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = directory };
        List<(uint Address, int Length)> reads = [];
        Assert.Equal(directory + 96, stage.CurrentGroupGeometryAddress(1, Read));
        Assert.Equal(new (uint, int)[] { (directory, 32), (directory + 32, 16) }, reads);
        Assert.Throws<OverflowException>(() => stage.CurrentGroupGeometryRange(1, Read));

        byte[] Read(uint address, int length)
        {
            reads.Add((address, length));
            return bytes.AsSpan(checked((int)(address - directory)), length).ToArray();
        }
    }

    private static byte[] Directory(uint geometry, uint animation)
    {
        byte[] bytes = new byte[160 + 0x2020];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 6);
        uint[] sections = [32, 32, 160, 160, 160, 160, (uint)bytes.Length];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), geometry);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), animation);
        return bytes;
    }
}
