using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfColumnScaleTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0x100u)]
    public void ContextCompositionScalesNativeColumnsAfterRotation(uint header)
    {
        NativeBattleActionMatrix context = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 20, 30, 40, 4096);
        NativeBattleActionMatrix result = NativeBattleGfMatrices.Compose6C53A0(Node(header, 0), context, null);

        Assert.Equal(new short[] { 0, -2048, 0, 4096, 0, 0, 0, 0, 1024 }, result.Rotation);
        Assert.Equal(20, result.X);
        Assert.Equal(30, result.Y);
        Assert.Equal(40, result.Z);
    }

    [Theory]
    [InlineData(0u, 4096, -2048)]
    [InlineData(0x100u, 2048, -4096)]
    public void FirstWindowCompositionSelectsRowOrColumnScalingBeforeViewProduct(uint header, short xy, short yx)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 20, 30, 40, 4096);
        NativeBattleActionMatrix result = NativeBattleGfMatrices.Compose6C6060(Node(header, 1024), view, null, new(0, 0, 0));

        Assert.Equal(new short[] { 0, xy, 0, yx, 0, 0, 0, 0, 1024 }, result.Rotation);
        Assert.Equal(20, result.X);
        Assert.Equal(30, result.Y);
        Assert.Equal(40, result.Z);
    }

    private static NativeBattleGfAnimationNode Node(uint header, short roll)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), roll);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(26), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28), 1024);
        return new(0, 0, data.ToImmutableArray());
    }
}
