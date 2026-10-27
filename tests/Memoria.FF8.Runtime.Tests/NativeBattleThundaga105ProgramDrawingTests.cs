using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105ProgramDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static ImmutableArray<byte> Anchor => new byte[8].ToImmutableArray();
    private static NativeBattleEffectMemory Memory => NativeBattleEffectMemory.FromColdProcess() with { Arena = new NativeBattleEffectArena([]) };

    [Fact]
    public void CustomListsForceThreeLinksAtZeroPhaseAndKeepSourceCommandWithoutBackfaceCull()
    {
        NativeBattleThundaga105NodeDraw draw = Draw(0);
        Assert.Equal(140, draw.Packets.AllocationBytes);
        NativeBattleGeometryPrimitive[] linked = draw.Packets.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(6, linked.Length);
        Assert.Equal(new uint[] { 0xE2000000, 0x2E808080, NativeBattleEffectTextureWindows.Encode(new(0, 128, 0, 128)), 0xE2000000, 0x3E808080, NativeBattleEffectTextureWindows.Encode(new(0, 128, 0, 128)) }, linked.Select(primitive => primitive.Words[1]).ToArray());
        Assert.Equal(240u, (linked[1].Words[3] >> 8) & 255);
        Assert.Equal(240u, (linked[4].Words[3] >> 8) & 255);
    }

    [Fact]
    public void FlatAndGouraudReadDifferentWrapDimensionsButEmitTheSameAfterWindow()
    {
        NativeBattleThundaga105NodeDraw draw = Draw(1);
        NativeBattleGeometryPrimitive[] polygons = draw.Packets.Attempts.Where(attempt => attempt.Primitive is not null && attempt.Primitive.Words[1] >> 24 is 0x2E or 0x3E).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(2, polygons.Length);
        Assert.Equal(144u, (polygons[0].Words[3] >> 8) & 255);
        Assert.Equal(16u, (polygons[1].Words[3] >> 8) & 255);
        Assert.Equal(0x1234u, polygons[0].Words[3] >> 16);
        Assert.Equal(0x1234u, polygons[1].Words[3] >> 16);
        Assert.Equal(240u, (Draw(4).Packets.Attempts.First(attempt => attempt.Primitive?.Words[1] == 0x2E808080).Primitive!.Words[3] >> 8) & 255);
    }

    [Fact]
    public void RejectedQuadKeepsOriginalPartialUvStoresWithoutWindowLinksOrAllocation()
    {
        NativeBattleThundaga105NodeDraw draw = Draw(1, View with { Z = -4096 });
        Assert.Equal(0, draw.Packets.AllocationBytes);
        Assert.Equal(2, draw.Packets.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in draw.Packets.Attempts)
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(240u, (BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(12)) >> 8) & 255);
        }
    }

    [Fact]
    public void BillboardChangesRotationOnlyAndCallerScaleIsASeparateColumnScale()
    {
        NativeBattleGfAnimationNode node = Node().WithDataWord(8, 30000).WithDataWord(24, 2048).WithDataWord(26, 4096).WithDataWord(28, 8192);
        byte[] anchor = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(anchor, 30000);
        NativeBattleActionMatrix billboard = View with { Rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096], X = 900, Y = 900, Z = 900 };
        NativeBattleActionMatrix matrix = NativeBattleThundaga105ProgramDrawing.ComposeNodeMatrix(node, 2048, anchor.ToImmutableArray(), View, billboard);
        Assert.Equal(new short[] { 0, -2048, 0, 1024, 0, 0, 0, 0, 4096 }, matrix.Rotation.ToArray());
        Assert.Equal((60000, 0, 4096), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void MorphWritesOnlyXyzInsideTheActualScratchAndRestoresExactly()
    {
        NativeBattleGfAnimationSource source = Source();
        byte[] bytes = source.Data.ToArray();
        bytes = bytes[..56].Concat(bytes[24..56]).Concat(bytes[56..]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 76);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 2);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(56 + vertex * 8), 200);
        }
        source = source with { Data = bytes.ToImmutableArray() };
        NativeBattleEffectMemory memory = Memory with { Arena = Memory.Arena!.Write(0x2254, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        NativeBattleGfAnimationNode node = Node().WithDataWord(38, 2048).WithDataWord(42, 1);
        NativeBattleThundaga105NodeDraw draw = NativeBattleThundaga105ProgramDrawing.Node6D4910(memory, source, node, Selector(), 1, 4096, Anchor, View, View, Projection, Colors);
        byte[] result = draw.Memory.Arena!.Read(0x2254, 40);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(0xA5, result[vertex * 8 + 6]);
            Assert.Equal(0xA5, result[vertex * 8 + 7]);
        }
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8).ToArray(), result[32..]);
        string json = NativeBattleTestSnapshots.Json(draw.Memory);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(json)));
    }

    [Fact]
    public void HiddenNodeSkipsGeometryAndCustomSkippedGroupsCannotBeGuessed()
    {
        NativeBattleGfAnimationNode hidden = Node().WithDataWord(24, 0).WithDataWord(26, 0).WithDataWord(28, 0);
        Assert.Empty(NativeBattleThundaga105ProgramDrawing.Node6D4910(Memory, new(0, []), hidden, Selector(), 0, 4096, Anchor, View, View, Projection, Colors).Packets.Attempts);
        byte[] bytes = Source().Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaga105ProgramDrawing.Node6D4910(Memory, Source() with { Data = bytes.ToImmutableArray() }, Node(), Selector(), 0, 4096, Anchor, View, View, Projection, Colors));
    }

    private static NativeBattleThundaga105NodeDraw Draw(ushort phase, NativeBattleActionMatrix? view = null) => NativeBattleThundaga105ProgramDrawing.Node6D4910(Memory, Source(), Node(), Selector(), phase, 4096, Anchor, view ?? View, View, Projection, Colors);
    private static ImmutableArray<byte> Selector() => new byte[32].Select((value, index) => index == 20 ? (byte)1 : value).ToImmutableArray();

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x1000);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[148];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + vertex * 8), (short)((vertex & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26 + vertex * 8), (short)(vertex < 2 ? -100 : 100));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(68), 1);
        Face(72, 0x2E808080, false);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(108), 1);
        Face(112, 0x3E808080, true);
        return new(0x1000, bytes.ToImmutableArray());

        void Face(int offset, uint command, bool gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), command);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 12), 0x1234F000);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 16), 0x5678F000);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 20), 0xF000F000);
            if (gouraud)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 24 + corner * 4), 0x00808080);
                }
            }
        }
    }
}

internal static class NativeBattleThundaga105TestNodes
{
    internal static NativeBattleGfAnimationNode WithDataWord(this NativeBattleGfAnimationNode node, int offset, short value)
    {
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
        return node with { Data = bytes.ToImmutableArray() };
    }
}
