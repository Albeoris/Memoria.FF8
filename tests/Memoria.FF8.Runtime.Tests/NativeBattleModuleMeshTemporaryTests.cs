using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleMeshTemporaryTests
{
    [Fact]
    public void Empty572200GroupsPublishActualPointersAndPreserveUnwrittenSuffixAfterReleaseAndSnapshot()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        const uint mesh = 0x1000000;
        byte[] bytes = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 8);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(address, Enumerable.Repeat((byte)0xA5, 88).ToArray()).Write(address, BitConverter.GetBytes(mesh)).Write(address + 28, new byte[4]).MarkUnknown(address + 8, 4);
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

        NativeBattleModuleMeshTemporaryResult result = NativeBattleModulePackets.EmitRetainedBatch572200(allocated.Staging, address, mesh, bytes.ToImmutableArray(), identity, null!, new(0, null, null, null, null, null, 17, 2), null!);
        staging = NativeBattleTemporaryMemory.Release5082D0(result.Staging, 88);
        staging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;

        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(address, BitConverter.ToUInt32(staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(mesh + 8, BitConverter.ToUInt32(staging.Read(address + 4, 4)));
        Assert.Equal(mesh + 40, BitConverter.ToUInt32(staging.Read(address + 32, 4)));
        Assert.Equal(0u, BitConverter.ToUInt32(staging.Read(address + 24, 4)));
        Assert.Equal(0xA5A5A5A5u, BitConverter.ToUInt32(staging.Read(address + 40, 4)));
        Assert.Equal(0xA5A5A5A5u, BitConverter.ToUInt32(staging.Read(address + 48, 4)));
        Assert.Throws<NotSupportedException>(() => staging.Read(address + 8, 4));
    }

    [Fact]
    public void Retained572200DoesNotInventASelectedVertexOwner()
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, BitConverter.GetBytes(0x1000000u)).Write(address + 28, BitConverter.GetBytes(0x2000u));
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitRetainedBatch572200(staging, address, 0x1000000, [], null!, null!, new(0x2000, null, null, null, null, null, 17, 2), null!));
    }
}
