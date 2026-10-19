using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianWorkPacketArenaTests
{
    [Fact]
    public void WorkPacketsUseTheExistingOwnerAndRetainUnwrittenExtendedPadding()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = new NativeBattleEffectArena([]).Write(0, [1, 2, 3, 4]), GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess().Write(0, Enumerable.Repeat((byte)0xA5, 32).ToArray()) };
        NativeBattleGfGeometryBatch batch = Batch();
        NativeBattleEffectPacketWrite written = NativeBattleGuardianWorkPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(12, written.Cursor);
        Assert.Same(memory.Arena, written.Memory.Arena);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, written.Memory.GuardianWorkBuffer!.Read(8, 4));
        Assert.Equal(new(NativeBattleGuardianWorkPacketArena.PacketBank, 0, 0), Assert.Single(written.Ordering.Packets).Reference);
    }

    [Fact]
    public void WorkPacketWritesRespectThePhysicalQuarterMegabyteExtent()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => NativeBattleGuardianWorkPacketArena.Append(memory, NativeBattleGuardianWorkBuffer.Length - 8, NativeBattleLogicalOrdering.Clear(), Batch()));
    }

    private static NativeBattleGfGeometryBatch Batch()
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000220], [0, 0, 0, 0]);
        byte[] bytes = new byte[12];
        BitConverter.GetBytes(0x01000000u).CopyTo(bytes, 0);
        BitConverter.GetBytes(0xE1000220u).CopyTo(bytes, 4);
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).Concat(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 4)).ToArray();
        return new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive)], 12);
    }
}
