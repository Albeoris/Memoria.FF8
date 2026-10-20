using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216MeshPacketsTests
{
    [Theory]
    [InlineData(false, 60)]
    [InlineData(true, 84)]
    public void FadedLeavesEmitOriginalPolygonOverlayAndModeAfter(bool quad, int bytes)
    {
        Dictionary<int, uint> observed = [];
        NativeBattleGfGeometryBatch batch = Draw(quad, quad ? 0x80u : 0x40u, 1024, (offset, value) => observed[offset] = value);
        Assert.Equal(bytes, batch.AllocationBytes);
        Assert.Equal(3, batch.Attempts.Length);
        int primaryBytes = quad ? 52 : 32;
        int overlayBytes = quad ? 24 : 20;
        Assert.Equal(new[] { 0, primaryBytes, primaryBytes + overlayBytes }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(0x02000000u, batch.Attempts[0].Primitive!.Words[1] & 0x02000000);
        Assert.Equal(quad ? 0x2A606060u : 0x22606060u, batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000240 }, batch.Attempts[2].Primitive!.Words.ToArray());
        Assert.Equal(0u, observed[48]);
        Assert.True(observed.ContainsKey(36));
        Assert.True(observed.ContainsKey(44));
        if (quad)
        {
            Assert.Equal(Points[2].Screen, observed[52]);
            Assert.Equal(new uint[] { Points[1].Screen, Points[2].Screen, Points[2].Screen, Points[3].Screen }, batch.Attempts[1].Primitive!.Words.Skip(2).ToArray());
        }
        else
        {
            Assert.False(observed.ContainsKey(52));
            Assert.Equal(Points.Take(3).Select(point => point.Screen), batch.Attempts[1].Primitive!.Words.Skip(2));
        }
    }

    [Theory]
    [InlineData(false, 32)]
    [InlineData(true, 52)]
    public void UnfadedLeavesHaveNoOverlayAndIgnoreUnrelatedHighContextBits(bool quad, int bytes)
    {
        NativeBattleGfGeometryBatch batch = Draw(quad, 0xF00, null, null);
        Assert.Equal(bytes, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(batch.Attempts);
        Assert.Equal(quad ? 0x3C102030u : 0x24102030u, packet.Primitive!.Words[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedFadeKeepsOnlyNativePrefixAndDoesNotForceSemiBit(bool quad)
    {
        List<int> observed = [];
        NativeBattleGfGeometryBatch batch = Draw(quad, quad ? 0x80u : 0x40u, null, (offset, _) => observed.Add(offset), true);
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(quad ? 0x3C102030u : 0x24102030u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(new[] { 48 }, observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlayIntensityUsesSignedArithmeticShift(bool quad)
    {
        NativeBattleGfGeometryBatch batch = Draw(quad, quad ? 0x80u : 0x40u, -33, null);
        Assert.Equal(quad ? 0x2A828282u : 0x22828282u, batch.Attempts[1].Primitive!.Words[1]);
    }

    private static NativeBattleGfGeometryBatch Draw(bool quad, uint flags, int? fade, Action<int, uint>? observe, bool rejected = false)
    {
        byte[] face = new byte[quad ? 36 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, quad ? 0x3C102030u : 0x24102030u);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x00112233);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(28), 0x00445566);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(32), 0x00778899);
        }
        NativeBattleMeshPacketContext context = new(flags, 0, fade, null, null, 0, 0, 17, 2);
        return NativeBattleDuel216MeshPackets.Emit(quad ? 7 : 2, face, index => rejected && index == 0 ? Points[index] with { Flags = 0x20000 } : Points[index], context, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, observe);
    }
    private static readonly NativeBattleModuleProjectedVertex[] Points = [new(0x00100010, 1000, 0), new(0x00100030, 1001, 0), new(0x00300010, 1002, 0), new(0x00300030, 1003, 0)];
}
