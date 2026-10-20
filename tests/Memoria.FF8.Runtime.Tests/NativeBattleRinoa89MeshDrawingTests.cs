using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89MeshDrawingTests
{
    [Fact]
    public void ApproachPreservesDwordAnchorsAndNativeRatioScratch()
    {
        NativeBattleRinoa89ParticleDraw draw = NativeBattleRinoa89MeshDrawing.DrawApproach6FA780(Frame(), 70000, -80000, 90000, 14, Identity(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, Resources());
        uint temporary = NativeBattleTemporaryMemory.Address;
        int ratio = (14 << 12) / 15;
        Assert.Equal((uint)ratio, Dword(draw.Frame, temporary));
        Assert.Equal(70000u, Dword(draw.Frame, temporary + 36));
        Assert.Equal(unchecked((uint)-80000), Dword(draw.Frame, temporary + 40));
        Assert.Equal((uint)(90000 - 13500 * ratio / 4096 + 15000), Dword(draw.Frame, temporary + 44));
        Assert.Equal(0u, Dword(draw.Frame, temporary + 76));
        Assert.Empty(draw.Packets.Attempts);
        Assert.Equal(temporary, Dword(draw.Frame, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void ReturnBurstUsesThreeOriginalMeshesAndRetainsWindowState()
    {
        NativeBattleRinoa89ParticleDraw draw = NativeBattleRinoa89MeshDrawing.DrawReturnBurst6FA780(Frame(), 70000, -80000, 90000, 14, Identity(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, Resources());
        uint temporary = NativeBattleTemporaryMemory.Address;
        Assert.Equal(0x149C730u, Dword(draw.Frame, temporary + 32));
        Assert.Equal(240u, Dword(draw.Frame, temporary + 60));
        Assert.Equal((uint)((14 << 12) / 15), Dword(draw.Frame, temporary + 44));
        Assert.Equal(unchecked((uint)-64), Dword(draw.Frame, temporary + 56));
        Assert.Equal(0u, Dword(draw.Frame, temporary + 64));
        Assert.Equal((ushort)128, BinaryPrimitives.ReadUInt16LittleEndian(draw.Frame.Staging.Read(temporary + 78, 2)));
        Assert.Equal((ushort)128, BinaryPrimitives.ReadUInt16LittleEndian(draw.Frame.Staging.Read(temporary + 82, 2)));
        Assert.Throws<NotSupportedException>(() => draw.Frame.Staging.Read(temporary + 18, 2));
        Assert.Equal(temporary, Dword(draw.Frame, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void SpriteMeshPublishesOriginalEulerOperandsAndZeroContextFlags()
    {
        NativeBattleVector angles = new(-16, -2048, 0);
        NativeBattleActionMatrix pose = NativeBattleGfMatrices.Euler56CD50(angles) with { X = 1, Y = 2, Z = 3 };
        NativeBattleRinoa89ParticleDraw draw = NativeBattleRinoa89MeshDrawing.DrawSpriteMesh6FD3C0(Frame(), pose, angles, Identity(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, Resources());
        uint temporary = NativeBattleTemporaryMemory.Address;
        Assert.Equal(unchecked((ushort)-16), BinaryPrimitives.ReadUInt16LittleEndian(draw.Frame.Staging.Read(temporary, 2)));
        Assert.Equal(unchecked((ushort)-2048), BinaryPrimitives.ReadUInt16LittleEndian(draw.Frame.Staging.Read(temporary + 2, 2)));
        Assert.Equal(0x14A04D8u, Dword(draw.Frame, temporary + 40));
        Assert.Equal(0u, Dword(draw.Frame, temporary + 68));
        Assert.Equal(temporary, Dword(draw.Frame, NativeBattleTemporaryMemory.Cursor));
    }

    [Fact]
    public void StandardBillboardRetainsItsNativeFiftyTwoByteExtendedReservation()
    {
        NativeBattleRinoa89ParticleDraw draw = NativeBattleRinoa89MeshDrawing.DrawBillboard6FD340(Frame(), new(0, 0, 4096), 4096, -1024, 0, Projection(), 0, Resources());
        Assert.Equal(52, draw.Packets.AllocationBytes);
        Assert.Equal(40, draw.Packets.Attempts[0].ByteOffsetRelative);
        Assert.Equal(12, draw.Packets.Attempts[0].PacketLength);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, draw.Packets.Attempts[0].WriteMask[8]);
        Assert.Equal(3072u, Dword(draw.Frame, 0x21DFEEC));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Dword(draw.Frame, NativeBattleTemporaryMemory.Cursor));
    }

    private static NativeBattleRinoa89Resources Resources()
    {
        byte[] bytes = new byte[NativeBattleRinoa89Resources.StaticEnd - NativeBattleRinoa89Resources.StaticAddress];
        foreach (uint address in new uint[] { 0x14A04D8, 0x1497140, 0x149D0F8, 0x1499FA8, 0x149C730 })
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(checked((int)(address - NativeBattleRinoa89Resources.StaticAddress))), 8);
        }
        int sprite = checked((int)(0x14A0538 - NativeBattleRinoa89Resources.StaticAddress));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sprite + 8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sprite + 10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sprite + 12), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sprite + 16), 0x80000001);
        bytes[sprite + 20] = 4;
        bytes[sprite + 21] = 128;
        bytes[sprite + 22] = 4;
        bytes[sprite + 23] = 0x2C;
        return new(new("mesh-operands", bytes.ToImmutableArray()), []);
    }
    private static uint Dword(NativeBattleRinoa89Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(address, 4));
    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection() => new(Identity(), 256, 160, 108);
    private static NativeBattleRinoa89Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Body);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), NativeCrtRandomState.FromColdProcess());
    }
}
