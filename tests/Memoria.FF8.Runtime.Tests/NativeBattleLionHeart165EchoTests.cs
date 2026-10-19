using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLionHeart165EchoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EchoUsesThePhysicalVertexTailAndPublishesTheNativeAreaBeforeClipping(bool reverse)
    {
        const uint temporary = NativeBattleTemporaryMemory.Address;
        const uint vertices = 0x196A018;
        const uint packets = 0x1901018;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 424).ToArray()).Write(vertices, Enumerable.Repeat((byte)0xA5, 24).ToArray());
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(165, 164, new(165, 0x5E9470, 0x5E9450), parameters, 0x23C34B0);
        NativeBattleLionHeart165Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, staging, NativeCrtRandomState.FromColdProcess());
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), reverse ? (ushort)2 : (ushort)1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), reverse ? (ushort)1 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0x3040);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x38001020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x01402030);
        NativeBattleRawMesh mesh = new([new(new(-10, -20, 1024), 0), new(new(10, -20, 1024), 0), new(new(0, 0, 1024), 0)], [new(false, face.ToImmutableArray())]);
        NativeBattleActionMatrix identity = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        NativeBattleLionHeart165EchoDraw result = NativeBattleLionHeart165Controller.DrawEchoMesh5EF0C0(frame, temporary, vertices, 0x1939100, mesh, [identity], identity, new(identity, 256, 160, 108), new(0, -4096, 0, 0), Enumerable.Repeat(0, 56).ToImmutableArray(), 0x808080, 0x1D8E09C, packets, 341, 256, 0, 0);
        Assert.Equal(vertices, Dword(result.Frame.Staging, temporary + 4));
        Assert.Equal(reverse ? -1600 : 1600, unchecked((int)Dword(result.Frame.Staging, temporary + 72)));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Frame.Staging, temporary + 76));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Frame.Staging.Read(vertices + 6, 2));
        Assert.Equal(-10, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.Staging.Read(vertices, 2)));
        Assert.Equal(reverse ? 0 : 32, result.Batch.AllocationBytes);
        Assert.Equal(packets + (reverse ? 0u : 32u), result.PacketCursor);
        Assert.Equal(result.PacketCursor, Dword(result.Frame.Staging, temporary + 68));
        if (!reverse)
        {
            Assert.Equal(255u, Dword(result.Frame.Staging, temporary + 80));
            Assert.Equal(0u, Dword(result.Frame.Staging, temporary + 44));
            Assert.Equal(0x24808080u, Assert.Single(result.Batch.Attempts).Primitive!.Words[1]);
        }
        else
        {
            Assert.Equal(0xA5A5A5A5u, Dword(result.Frame.Staging, temporary + 44));
        }
    }

    private static uint Dword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
