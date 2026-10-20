using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89ParticleDrawingTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 48)]
    public void SparkQuadsUseFixedBucketAndRetainedExtendedFlag(bool extended, int expectedBytes)
    {
        NativeBattleRinoa89Frame frame = Frame();
        uint temporary = NativeBattleTemporaryMemory.Address;
        byte[] context = new byte[180];
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(44), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(48), 1);
        context[37] = extended ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(132), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(140), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(148), 4096);
        BinaryPrimitives.WriteInt32LittleEndian(context.AsSpan(160), 4096);
        frame = frame with { Staging = frame.Staging.Write(temporary, context) };
        byte[] quad = Quad();
        NativeBattleRinoa89ParticleDraw result = NativeBattleRinoa89ParticleDrawing.DrawQuads6FC9E0(frame, temporary, Projection(), (_, _) => quad);
        Assert.Equal(expectedBytes, result.Packets.AllocationBytes);
        Assert.All(result.Packets.Attempts, attempt => Assert.Equal(19, attempt.Primitive!.Bucket));
        NativeBattleGfGeometryAttempt textured = result.Packets.Attempts.Last();
        Assert.Equal(0, textured.ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, textured.WriteMask[38]);
        Assert.Equal((byte)(extended ? 1 : 0), result.Frame.Staging.Read(temporary + 37, 1)[0]);
        if (extended)
        {
            Assert.Equal(40, result.Packets.Attempts[0].ByteOffsetRelative);
            Assert.Equal(8, result.Packets.Attempts[0].PacketLength);
        }
    }

    [Fact]
    public void RejectedSparkRetainsEarlyPacketStoresWithoutAdvancingCursor()
    {
        NativeBattleRinoa89Frame frame = Frame();
        uint temporary = NativeBattleTemporaryMemory.Address;
        byte[] context = new byte[180];
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(44), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(48), 1);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(132), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(140), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(148), 4096);
        frame = frame with { Staging = frame.Staging.Write(temporary, context).MarkUnknown(temporary + 37, 1) };
        NativeBattleRinoa89ParticleDraw result = NativeBattleRinoa89ParticleDrawing.DrawQuads6FC9E0(frame, temporary, Projection(), (_, _) => Quad());
        Assert.Equal(0, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => result.Frame.Staging.Read(temporary + 37, 1));
    }

    [Fact]
    public void InactiveParticlePoolsDoNotReadUnknownTaskPaddingOrResources()
    {
        NativeBattleRinoa89Frame frame = Frame();
        NativeBattleRinoa89ParticleDraw emitters = NativeBattleRinoa89ParticleDrawing.DrawEmitters6FC500(frame, Identity(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => throw new InvalidOperationException());
        NativeBattleRinoa89ParticleDraw sparks = NativeBattleRinoa89ParticleDrawing.DrawSparks6FC8B0(emitters.Frame, Identity(), Projection(), (_, _) => throw new InvalidOperationException());
        Assert.Empty(emitters.Packets.Attempts);
        Assert.Empty(sparks.Packets.Attempts);
        Assert.Equal(frame.Memory, sparks.Frame.Memory);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(sparks.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static NativeBattleRinoa89Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Beams);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(0x2556600, BitConverter.GetBytes(0x20DFAB8u))), NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection() => new(Identity(), 256, 160, 108);
    private static byte[] Quad()
    {
        byte[] quad = new byte[20];
        quad[0] = 4;
        quad[1] = 128;
        quad[2] = 8;
        quad[3] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(16), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(quad.AsSpan(18), 4096);
        return quad;
    }
}

