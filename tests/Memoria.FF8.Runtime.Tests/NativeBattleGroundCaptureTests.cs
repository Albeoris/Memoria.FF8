using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroundCaptureTests
{
    [Fact]
    public void CaptureUsesGlobalFocalAndIndependentProjectionHWithNativeQuotientCorrection()
    {
        byte[] anchor = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(anchor, 127);
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(4), -127);
        NativeBattleThundaga105CaptureTransform result = NativeBattleThundaga105CaptureGeometry.TransformCapture(anchor.ToImmutableArray(), new(new(-384, 0, -384), new(384, 0, 384)), new(null, 901, -5, 7), 128, 256, -222);
        Assert.Equal(-18, result.Matrix.X);
        Assert.Equal(-31, result.Matrix.Y);
        Assert.Equal(-222, result.Matrix.Z);
        Assert.Equal((short)682, result.Matrix.Rotation[0]);
        Assert.Equal((short)-1365, result.Matrix.Rotation[5]);
        Assert.Equal<ushort?>(901, result.Projection.FocalLength);
        Assert.Equal<short?>(64, result.Projection.OffsetX);
        Assert.Equal<short?>(128, result.Projection.OffsetY);
        Assert.Equal((short)-5, result.PriorOffsetX);
        Assert.Equal((short)7, result.PriorOffsetY);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void OrdinaryProfilePublishesBothCurrentEnvironmentCopiesWithoutEffectAllocation(byte buffer, int parity)
    {
        NativeBattleEffectMemory memory = Memory(buffer, true);
        NativeBattleGroundCaptureMemory original = new(NativeBattleEffectArena.Clear().MarkUnknown(0, 0x280));
        memory = memory with { GroundCapture = original, PrimitiveProcess = memory.PrimitiveProcess! with { ByteCursor = 128 } };
        int callbacks = 0;
        NativeBattleGroundCaptureWrite written = NativeBattleGroundCapturePackets.Append8DE1F0(memory, NativeBattleLogicalOrdering.Clear(), 31, current =>
        {
            callbacks++;
            Assert.Single(current.Ordering.Packets);
            Assert.Equal(128, current.Memory.PrimitiveProcess!.ByteCursor);
            Assert.Equal((byte)0, current.Memory.GroundCapture!.Read(0x140 + 92 * parity + 0x18, 1)[0]);
            return current;
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(128, written.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(2, written.Ordering.Packets.Length);
        Assert.Equal(parity * 64, written.Ordering.Packets[0].Reference.ByteOffset);
        Assert.Equal(0x200 + parity * 64, written.Ordering.Packets[1].Reference.ByteOffset);
        Assert.Equal(0x05000000u, written.Ordering.Packets[0].Words[0]);
        Assert.Equal(0x08000000u, written.Ordering.Packets[1].Words[0]);
        Assert.Equal(0x02000000u, written.Ordering.Packets[1].Words[6]);
        Assert.Throws<NotSupportedException>(() => written.Memory.GroundCapture!.Read(parity * 64 + 24, 1));
        string json = NativeBattleTestSnapshots.Json(written.Memory.GroundCapture);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleGroundCaptureMemory>(json)));
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void SpecialProfileAllocatesNativeExtentsAndPreservesUnwrittenPacketBytes(bool clear, uint restoreWords)
    {
        NativeBattleEffectMemory memory = Memory(0, clear) with { GroundCapture = null, Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 148) };
        NativeBattleGroundCaptureWrite written = NativeBattleGroundCapturePackets.Append8DE1F0(memory, NativeBattleLogicalOrdering.Clear(), 0x8E, current => current);
        Assert.Equal(148, written.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Null(written.Memory.GroundCapture);
        Assert.Same(memory.Arena, written.Memory.Arena);
        Assert.All(written.Ordering.Packets, packet => Assert.Equal("off_B6D070", packet.Reference.Bank));
        Assert.Equal(new[] { 0, 64, 88, 100, 124, 136 }, written.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(restoreWords, written.Ordering.Packets[0].Words[0] >> 24);
        Assert.Equal(0x2A000000u, written.Ordering.Packets[1].Words[1]);
        Assert.Equal(0x28000000u, written.Ordering.Packets[3].Words[1]);
        Assert.Equal(0xE1000020u, written.Ordering.Packets[2].Words[1]);
        Assert.Equal((ushort)1, written.Ordering.Proxies[1].Flags20);
        Assert.Equal((ushort)1, written.Ordering.Proxies[3].Flags20);
        Assert.Throws<NotSupportedException>(() => written.Memory.PrimitiveProcess!.ReadCommonPacket(144, 4));
        Assert.Throws<NotSupportedException>(() => written.Memory.PrimitiveProcess!.ReadCommonPacket(clear ? 36 : 24, 1));
        Assert.Equal(new byte[] { 0x7F, 0, 0xFF, 0 }, written.Memory.PrimitiveProcess!.ReadCommonPacket(84, 4));
    }

    [Fact]
    public void OrdinaryWarmMissingStaticOwnerRemainsUnsupported()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleGroundCapturePackets.Append8DE1F0(Memory(0, false) with { GroundCapture = null }, NativeBattleLogicalOrdering.Clear(), 31, current => current));
    }

    private static NativeBattleEffectMemory Memory(byte buffer, bool clear)
    {
        byte[] environment = new byte[92];
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(6), 216);
        environment[0x18] = clear ? (byte)1 : (byte)0;
        environment[0x19] = 17;
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { Buffer80 = buffer, Software = process.Software! with { EnvironmentBuffers = new(environment.ToImmutableArray(), environment.ToImmutableArray(), null) } };
        return NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = process, Arena = NativeBattleEffectArena.Clear(), GroundCapture = NativeBattleGroundCaptureMemory.FromColdProcess() };
    }
}
