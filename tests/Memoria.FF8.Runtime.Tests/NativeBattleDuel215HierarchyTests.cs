using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215HierarchyTests
{
    [Fact]
    public void AnchoredAndMatrixCallbacksKeepTheirDifferentScaleCompositionOrder()
    {
        NativeBattleGfAnimationNode node = Node(0x9000);
        NativeBattleDuel215Program context = Context();
        NativeBattleActionMatrix regular = NativeBattleDuel215Hierarchy.Matrix(node, context, false);
        NativeBattleActionMatrix anchored = NativeBattleDuel215Hierarchy.Matrix(node, context, true);
        Assert.Equal(new short[] { 2048, 0, 0, 0, 4096, 0, 0, 0, 8192 }, regular.Rotation);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 2048, 0, 0, 0, 8192 }, anchored.Rotation);
        Assert.Equal((80, 210, 330), (regular.X, regular.Y, regular.Z));
        Assert.Equal((75, 214, 336), (anchored.X, anchored.Y, anchored.Z));
    }

    [Fact]
    public void DirectNodeStillRotatesAnchorButBypassesBasisForItsOwnPositionAndRotation()
    {
        NativeBattleGfAnimationNode node = Node(0x200);
        NativeBattleActionMatrix anchored = NativeBattleDuel215Hierarchy.Matrix(node, Context(), true);
        Assert.Equal(new short[] { 0, 2048, 0, -4096, 0, 0, 0, 0, 8192 }, anchored.Rotation);
        Assert.Equal((105, 224, 336), (anchored.X, anchored.Y, anchored.Z));
    }

    private static NativeBattleDuel215Program Context() => new(new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096), new(4, 5, 6), "0", -128, 0, 0, 0x20DFAB8 + 63768);

    private static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 30);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 8192);
        return new(0, 0, bytes.ToImmutableArray());
    }
}
