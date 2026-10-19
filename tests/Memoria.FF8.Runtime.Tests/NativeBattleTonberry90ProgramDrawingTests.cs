using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ProgramDrawingTests
{
    [Fact]
    public void TonberryAlwaysUsesEuler701310AndColumnScalingDespiteAlternativeHeaderBits()
    {
        NativeBattleGfAnimationNode node = Node(0x40200 | 0x100);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 317);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), -503);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1001);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 8192);
        node = node with { Data = bytes.ToImmutableArray() };
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.Identity8DD770() with { X = 7, Y = 11, Z = 13 };
        NativeBattleActionMatrix actual = NativeBattleTonberry90ProgramDrawing.Compose764260(node, basis);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler701310(node.Vector16), 2048, 4096, 8192);
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((7, 11, 13), (actual.X, actual.Y, actual.Z));
        Assert.NotEqual(NativeBattleGfMatrices.Euler7015B0(node.Vector16).Rotation.ToArray(), actual.Rotation.ToArray());
    }

    [Fact]
    public void MorphUsesSharedRoundedKernelAndReturnsOnlyActualXYZWrites()
    {
        byte[] source = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 28);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(24), 2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(28), 1);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(30), -1);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(32), 17);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(34), 0xBEEF);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(36), 2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(38), -2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(40), 33);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(42), 0xCAFE);
        byte[] nodeBytes = Node(0x200).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(42), 1);
        NativeBattleGfAnimationSource model = new(0x1000, source.ToImmutableArray());
        NativeBattleTonberry90PreparedNode prepared = NativeBattleTonberry90ProgramDrawing.Prepare764260(model, Node(0x200) with { Data = nodeBytes.ToImmutableArray() }, NativeBattleCuragaMatrices.Identity8DD770(), -55, new(null, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(NativeBattleGfMorphGeometry.MorphVertices701390(model.Data, 16, 0, 1, 2048).ToArray(), prepared.Morph.ToArray());
        Assert.True(prepared.Active);
        Assert.Null(prepared.FarColor);
        Assert.Empty(prepared.Packets.Attempts);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(model.Data.AsSpan().Slice(34)));
    }

    [Fact]
    public void SkippedNodeConsumesNoGeometryAndPackedViewIgnoresOnlyUnconsumedPadding()
    {
        byte[] bytes = Node(0).Data.ToArray();
        bytes.AsSpan(24, 6).Clear();
        NativeBattleTonberry90PreparedNode prepared = NativeBattleTonberry90ProgramDrawing.Prepare764260(new(0, []), Node(0) with { Data = bytes.ToImmutableArray() }, NativeBattleCuragaMatrices.Identity8DD770(), 0, new(null, null, null, null), null!);
        Assert.False(prepared.Active);
        bool[] known = Enumerable.Repeat(true, 32).ToArray();
        known[18] = false;
        known[19] = false;
        NativeBattlePackedMatrix32 matrix = new(new byte[32].ToImmutableArray(), known.ToImmutableArray());
        Assert.Equal(0, matrix.ReadMatrix().X);
        known[20] = false;
        Assert.Throws<NotSupportedException>(() => (matrix with { Known = known.ToImmutableArray() }).ReadMatrix());
    }

    private static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
