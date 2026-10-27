using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareRectangleFillTests
{
    [Fact]
    public void OddWidthUsesUnalignedAbsoluteWordsAndRGB555WithoutMaskBit()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 1024 * 512 * 2).ToArray();
        Assert.True(NativeSoftwareRectangleFill.Fill466EF0(image, Packet(3, 7, 3, 2, 0x0781FF)));
        for (int y = 7; y < 9; y++)
        {
            Assert.Equal((ushort)0xA5A5, Word(image, 2, y));
            Assert.Equal((ushort)0x021F, Word(image, 3, y));
            Assert.Equal((ushort)0x021F, Word(image, 4, y));
            Assert.Equal((ushort)0x021F, Word(image, 5, y));
            Assert.Equal((ushort)0xA5A5, Word(image, 6, y));
        }
        Assert.Equal((ushort)0xA5A5, Word(image, 3, 9));
    }

    [Theory]
    [InlineData(-1, 0, 3, 2)]
    [InlineData(0, -1, 3, 2)]
    [InlineData(1022, 0, 3, 2)]
    [InlineData(0, 511, 3, 2)]
    [InlineData(0, 0, 0, 2)]
    [InlineData(0, 0, 3, 0)]
    public void WholeRectangleRejectionAndZeroExtentsLeaveEveryWordUntouched(short x, short y, short width, short height)
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 1024 * 512 * 2).ToArray();
        Assert.False(NativeSoftwareRectangleFill.Fill466EF0(image, Packet(x, y, width, height, 0xFFFFFF)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, image.Length).ToArray(), image);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void UnsafeNegativeExtentRemainsAnExplicitUnsupportedContract(short width, short height)
    {
        Assert.Throws<NotSupportedException>(() => NativeSoftwareRectangleFill.Fill466EF0(new byte[1024 * 512 * 2], Packet(0, 0, width, height, 0)));
    }

    [Fact]
    public void EdgeEqualityAndCurrentCaptureRectangleWriteThroughCanonicalTextureOwner()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        NativeSoftwareDrawingEnvironmentState unrelated = state.Environment with { TopLeftX = 700, TopLeftY = 300, OffsetX = -7, OffsetY = 99 };
        state = state with { Environment = unrelated };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(384, 256, 128, 256, Enumerable.Repeat((byte)0xFF, 128 * 256 * 2).ToArray());
        uint[] handlers = new uint[256];
        handlers[2] = 0x466EF0;
        NativeBattleSoftwarePrimitiveExecution execution = new(state, textures, new(handlers.ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 2, [0x02000000, 0x01000180, 0x01000080], 1, [], null);
        execution.ExecuteSoftware(command);
        Assert.Same(state, execution.State);
        execution.BeginPass(1);
        byte[] image = execution.Textures.ReadVram();
        Assert.Equal((ushort)0, Word(image, 384, 256));
        Assert.Equal((ushort)0, Word(image, 511, 511));
        Assert.Same(unrelated, execution.State.Environment);
        string retained = NativeBattleTestSnapshots.Json(execution.Textures);
        Assert.Equal(image, NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(retained)!.ReadVram());
        Assert.True(NativeSoftwareRectangleFill.Fill466EF0(image, Packet(1023, 511, 1, 1, 0xFFFFFF)));
        Assert.Equal((ushort)0x7FFF, Word(image, 1023, 511));
    }

    [Fact]
    public void NormalPreprocessingIsRetAndNormalFillUsesItsIndependentAbsoluteOriginGate()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        NativeBattleSoftwarePrimitiveExecution execution = new(state, NativeBattleEffectTextureState.FromColdProcess(), new(new uint[256].ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        NativeBattleLogicalCommand accepted = new(new("test", 0, 0), 1, 2, [0x020000FF, 0x00F00280, 0x00010001], 1, [], null);
        execution.ExecuteNormal(accepted, new(0), 0);
        execution.BeginPass(1);
        Assert.Equal((ushort)0, Word(execution.Textures.ReadVram(), 640, 240));
        execution.ExecuteNormal(accepted, new(0), 1);
        execution.BeginPass(1);
        Assert.Equal((ushort)31, Word(execution.Textures.ReadVram(), 640, 240));
        execution.ExecuteNormal(accepted with { Words = [0x020000FF, 0x01000180, 0x00010001] }, new(0), 1);
        execution.ExecuteNormal(accepted with { Words = [0x020000FF, 0x00EF0280, 0x00010001] }, new(0), 1);
        execution.BeginPass(1);
        Assert.Equal((ushort)0, Word(execution.Textures.ReadVram(), 384, 256));
        Assert.Equal((ushort)0, Word(execution.Textures.ReadVram(), 640, 239));
        Assert.Same(state, execution.State);
    }

    private static ushort Word(byte[] image, int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x)));
    private static byte[] Packet(short x, short y, short width, short height, uint color)
    {
        byte[] packet = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x02000000 | color);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(8), x);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(10), y);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(12), width);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(14), height);
        return packet;
    }
}
