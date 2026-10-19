using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel212CaptureTests
{
    [Fact]
    public void CaptureKeepsOriginalQuotientCorrectionYawAndIndependentProjectionFocal()
    {
        NativeBattleThundaga105CaptureTransform capture = NativeBattleDuel212Capture.Transform6A12B0(new(82, 0, 164), new(new(-2560, 0, -5120), new(2560, 0, 5120)), new(null, 901, 160, 112), -222);
        Assert.Equal(2, capture.Matrix.X);
        Assert.Equal(-4, capture.Matrix.Y);
        Assert.Equal(-222, capture.Matrix.Z);
        Assert.Equal((short)-102, capture.Matrix.Rotation[0]);
        Assert.Equal((short)0, capture.Matrix.Rotation[1]);
        Assert.Equal((short)0, capture.Matrix.Rotation[4]);
        Assert.Equal((short)0, capture.Matrix.Rotation[7]);
        Assert.Equal<ushort?>(901, capture.Projection.FocalLength);
        Assert.Equal<short?>(64, capture.Projection.OffsetX);
        Assert.Equal<short?>(128, capture.Projection.OffsetY);
        Assert.Equal((short)160, capture.PriorOffsetX);
        Assert.Equal((short)112, capture.PriorOffsetY);
    }

    [Fact]
    public void CaptureUsesPrivateCursorAndRetainsBothUnwrittenEnvironmentTailAndOffsetPadding()
    {
        byte[] environment = new byte[92];
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(6), 216);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { Buffer80 = 0, Software = process.Software! with { EnvironmentBuffers = new(environment.ToImmutableArray(), environment.ToImmutableArray(), null) } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = process, Arena = NativeBattleEffectArena.Clear().MarkUnknown(8192, 112) };
        NativeBattleOriginalEffectState effect = new(212, 211, new(212, 0x6A0D20, 0x6A0D30), new(0, 0, 8, 0, 0, 0), NativeBattleDuel212Controller.Root) { DrawArenaOffset = 8192 };
        NativeBattleDuel212Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));
        NativeBattleThundaga105CaptureTransform transform = NativeBattleDuel212Capture.Transform6A12B0(new(0, 0, 0), new(new(-2560, 0, -5120), new(2560, 0, 5120)), new(null, 288, 160, 112), 288);
        NativeBattleDuel212CaptureStep result = NativeBattleDuel212Capture.Append6A12B0(frame, NativeBattleLogicalOrdering.Clear(), transform, (current, _, ordering) =>
        {
            Assert.Equal(8256, current.Effect.DrawArenaOffset);
            Assert.Single(ordering.Packets);
            return new(current, ordering);
        });
        Assert.Equal(8304, result.Frame.Effect.DrawArenaOffset);
        Assert.Equal(new[] { 8192, 8256, 8280, 8292 }, result.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.All(result.Ordering.Packets, packet => Assert.Equal("20DFAB8", packet.Reference.Bank));
        Assert.Equal((ushort)1, result.Ordering.Proxies[1].Flags20);
        Assert.Equal(0x28000000u, result.Ordering.Packets[1].Words[1]);
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(8192 + 24, 1));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(8292 + 8, 4));
        Assert.Equal(0x20DFAB8u + 8304, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(0x2510258, 4)));
    }
}
