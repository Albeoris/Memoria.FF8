using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175WindowedMeshPacketsTests
{
    [Theory]
    [InlineData(false, 48, 11, 9)]
    [InlineData(true, 60, 14, 12)]
    public void AcceptedQuadIsOnePhysicalProxyContainingThreeCommands(bool gouraud, int bytes, int payloadWords, int innerWords)
    {
        NativeBattleGfGeometryBatch batch = Draw(gouraud);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(bytes, batch.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal((uint)payloadWords << 24, primitive.Words[0]);
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(new(0, 128, 64, 128)), primitive.Words[1]);
        Assert.Equal(0xE2000000u, primitive.Words[^1]);
        Assert.Equal(67, primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4.ToArray());
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(primitive.Bucket, new("compound", 0, 0), primitive.Words, 0, primitive.VertexPayload4);
        byte[] lengths = new byte[256];
        lengths[0xE2] = 1;
        lengths[gouraud ? 0x3E : 0x2E] = (byte)innerWords;
        NativeBattleLogicalCommand[] commands = ordering.Traverse(lengths.ToImmutableArray()).ToArray();
        Assert.Single(ordering.Proxies);
        Assert.Equal(new byte[] { 0xE2, gouraud ? (byte)0x3E : (byte)0x2E, 0xE2 }, commands.Select(command => command.Opcode).ToArray());
        Assert.All(commands, command => Assert.Equal(new[] { 0, 0, 0, 0 }, command.VertexPayload4.ToArray()));
    }

    [Fact]
    public void SignedRemaindersAndAggregateAxisAdjustmentPreserveOriginalPackedUv3UpperWord()
    {
        NativeBattleHoly175WindowedMeshContext context = Context() with { UOffset = -65, VOffset = 160 };
        NativeBattleGeometryPrimitive primitive = Draw(false, context).Attempts[0].Primitive!;
        Assert.Equal(63u, primitive.Words[4] & 255);
        Assert.Equal(144u, primitive.Words[4] >> 8 & 255);
        Assert.Equal(0x1234u, primitive.Words[4] >> 16);
        Assert.Equal(0xF0FAu, primitive.Words[8] >> 16);
        Assert.Equal(0x9039u, primitive.Words[10] & 0xFFFF);
        Assert.Equal(0u, primitive.Words[10] >> 16);
        NativeBattleGeometryPrimitive zero = Draw(false, Context() with { UOffset = 64, VOffset = 128 }).Attempts[0].Primitive!;
        Assert.Equal(0x1234F000u, zero.Words[4]);
        Assert.Throws<NotSupportedException>(() => Draw(false, Context() with { Window = new(0, 0, 0, 128) }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstFlagOrZeroNclipRejectsBeforeFourthProjectionAndRetainsOnlyPrefix(bool gouraud)
    {
        int calls = 0;
        NativeBattleModuleProjectedVertex Read(int corner)
        {
            calls++;
            return Vertex(corner, flags: corner == 0 ? 0x60000u : 0);
        }
        NativeBattleGfGeometryBatch rejected = NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(gouraud), Read, gouraud, Context() with { Flags = 0xF3, Fade = null });
        Assert.Equal(3, calls);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(rejected.Attempts);
        Assert.Equal(0, rejected.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(gouraud ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        calls = 0;
        NativeBattleGfGeometryBatch zero = NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(gouraud), corner =>
        {
            calls++;
            return new(Screen(10 + corner * 10, 10), 800, 0);
        }, gouraud, Context());
        Assert.Equal(3, calls);
        Assert.Null(Assert.Single(zero.Attempts).Primitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonzeroUvAdjustmentPrecedesZeroOrNegativeNclipRejectionWithoutProjectingFourth(bool negative)
    {
        int calls = 0;
        NativeBattleGfGeometryBatch batch = NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(true), corner =>
        {
            calls++;
            if (corner == 3)
            {
                throw new InvalidOperationException("Rejected Nclip must not project the fourth corner.");
            }
            return negative ? new(Screen(corner == 0 ? 100 : 10, corner == 2 ? 100 : 10), 800, 0) : new(Screen(10 + corner * 10, 10), 800, 0);
        }, true, Context() with { Flags = 0, VOffset = 32 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(3, calls);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal((byte)144, attempt.Data[17]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[17]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
    }

    [Fact]
    public void NegativeNclipUsesCallerFlagsButFourthFlagsAreNeverRejectionInputs()
    {
        NativeBattleModuleProjectedVertex Negative(int corner) => corner switch { 0 => new(Screen(100, 10), 800, 0), 1 => new(Screen(10, 10), 800, 0), 2 => new(Screen(10, 100), 800, 0), _ => new(Screen(100, 100), 800, 0x60000) };
        Assert.Null(NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(false), Negative, false, Context() with { Flags = 0 }).Attempts[0].Primitive);
        Assert.NotNull(NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(false), Negative, false, Context() with { Flags = 0x10 }).Attempts[0].Primitive);
        Assert.NotNull(NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(true), Negative, true, Context() with { Flags = 0x20 }).Attempts[0].Primitive);
    }

    [Fact]
    public void MixedOutsideXUnionRejectsAfterSxyAndUvStoresBeforeE2OrGouraudColors()
    {
        int[] x = [-1, 3000, -2, 3001];
        NativeBattleGfGeometryBatch batch = NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(true), corner => new(Screen(x[corner], corner < 2 ? 10 : 100), 800, 0), true, Context() with { VOffset = 32 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[48]);
        Assert.Equal(144, attempt.Data[17]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[56]);
    }

    [Fact]
    public void ColorClearWinsAndDepthCueIsConsumedOnlyForAcceptedFlaggedFaces()
    {
        Assert.Equal(0x2C112233u, Draw(false, Context() with { Flags = 5 }).Attempts[0].Primitive!.Words[2]);
        Assert.Equal(0x3C808080u, Draw(true, Context() with { Flags = 10 }).Attempts[0].Primitive!.Words[2]);
        NativeBattleHoly175WindowedMeshContext cue = Context() with { Flags = 0xF3, Fade = 4096 };
        Assert.Equal(0x2E000000u, Draw(false, cue).Attempts[0].Primitive!.Words[2]);
        Assert.Equal(0x3E000000u, Draw(true, cue).Attempts[0].Primitive!.Words[2]);
        Assert.Throws<NotSupportedException>(() => Draw(false, Context() with { Flags = 0xF3, Fade = null }));
    }

    private static NativeBattleGfGeometryBatch Draw(bool gouraud, NativeBattleHoly175WindowedMeshContext? context = null) => NativeBattleHoly175WindowedMeshPackets.EmitQuad(Face(gouraud), corner => Vertex(corner), gouraud, context ?? Context(), NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleHoly175WindowedMeshContext Context() => new(0x33, 0, null, 0x00112233, 0, 0, new(0, 128, 64, 128), 17, 2);
    private static NativeBattleModuleProjectedVertex Vertex(int corner, uint flags = 0) => new(Screen(corner is 0 or 2 ? 10 : 100, corner < 2 ? 10 : 100), 800, flags);
    private static uint Screen(int x, int y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;

    private static byte[] Face(bool gouraud)
    {
        byte[] face = new byte[gouraud ? 36 : 24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, gouraud ? 0x3C808080u : 0x2C808080u);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x1234F000);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x5678F0FA);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0xF0FAF014);
        if (gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(28), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(32), 0x00808080);
        }
        return face;
    }
}
