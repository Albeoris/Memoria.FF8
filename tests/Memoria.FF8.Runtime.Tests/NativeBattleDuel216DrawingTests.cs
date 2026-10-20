using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216DrawingTests
{
    [Fact]
    public void MeshTemporaryRetainsBasisZeroPaddingComposedUnknownPaddingAndUnwrittenTail()
    {
        byte[] data = new byte[NativeBattleDuel216Resources.EndAddress - NativeBattleDuel216Resources.Address];
        int offset = checked((int)(0x1140AA4 - NativeBattleDuel216Resources.Address));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), 8);
        NativeBattleDuel216Resources resources = new(data.ToImmutableArray(), [], new("unused", []), []);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel216 = new(true, new(0, 0, 8, 0, 0, 0), resources) };
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Main);
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(temporary, Enumerable.Repeat((byte)0xA5, 152).ToArray());
        NativeBattleDuel216Frame frame = new(memory, effect, staging, NativeCrtRandomState.FromColdProcess());
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleDuel216DrawingStep result = NativeBattleDuel216Drawing.Draw69A410(frame, new(0x1140AA4, identity, identity with { X = 10, Y = 20, Z = 30 }, 0, 0, 4096), identity, null!, null!);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(temporary, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging.Read(temporary + 138, 2)));
        Assert.Throws<NotSupportedException>(() => result.Frame.Staging.Read(temporary + 106, 2));
        Assert.All(result.Frame.Staging.Read(temporary + 140, 12), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(temporary + 108, 4)));
        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(temporary + 112, 4)));
        Assert.Equal(30, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(temporary + 116, 4)));
    }

    [Fact]
    public void MorphWritesOnlyThreeWordsInEachPhysicalEightByteVertex()
    {
        byte[] data = new byte[NativeBattleDuel216Resources.EndAddress - NativeBattleDuel216Resources.Address];
        int offset = checked((int)(0x113C3CC - NativeBattleDuel216Resources.Address));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 8), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 12), -123);
        NativeBattleDuel216Resources resources = new(data.ToImmutableArray(), [], new("unused", []), []);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(11480, 8), Duel216 = new(true, new(0, 0, 8, 0, 0, 0), resources) };
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Main);
        NativeBattleDuel216Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
        frame = NativeBattleDuel216Drawing.Morph69A670(frame, 0x113C3CC, 4096, 0);
        Assert.Equal((short)1024, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(11480, 2)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena.Read(11482, 2)));
        Assert.Equal((short)-123, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena.Read(11484, 2)));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(11486, 2));
    }

    [Theory]
    [InlineData(0, 0x2A808080u)]
    [InlineData(1, 0x2A000000u)]
    public void FlashUsesActualTwoPassIntensityAndPhysical24ByteQuad(int phase, uint color)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDuel216Drawing.Flash699EE0(phase);
        Assert.Equal(24, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(8, attempt.Primitive!.Bucket);
        Assert.Equal(new uint[] { 0x05000000, color, 0, 320, 216u << 16, (216u << 16) | 320 }, attempt.Primitive.Words);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }
}
