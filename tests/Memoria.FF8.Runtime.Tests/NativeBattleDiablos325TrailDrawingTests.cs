using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325TrailDrawingTests
{
    [Fact]
    public void Degenerate_first_edge_is_rejected_while_the_later_native_segment_uses_fresh_edge_outputs()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = NativeBattleDiablos325Trail.Initialize657E70(frame, 5, 0, 0);
        byte[] tables = new byte[NativeBattleDiablos325LateResources.Length];
        tables[20] = 20;
        int widths = checked((int)(0xFDFF74 - NativeBattleDiablos325LateResources.Address));
        for (int index = 0; index < 2049; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(widths + index * 2), 4096);
        }
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, new byte[0x3890].ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0]))
        {
            LateResources = new(tables.ToImmutableArray())
        };
        uint model = frame.Memory.Diablos325!.ModelBuffer5208;
        byte[] points = new byte[72];
        PutPoint(0, 40, model + 0x4C5C + 24);
        PutPoint(24, 40, model + 0x4C5C + 48);
        PutPoint(48, 120, 0);
        byte[] block = new byte[48];
        BinaryPrimitives.WriteUInt32LittleEndian(block, model + 0x4C5C);
        BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(44), 3);
        BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(46), 4096);
        frame = frame with
        {
            Memory = frame.Memory with
            {
                Arena = frame.Memory.Arena!.Write(0x4C5C, points).Write(0x7C5C, block),
                PrimitiveProcess = frame.Memory.PrimitiveProcess! with { Temporary5082B0Word4 = 0x10000000 }
            },
            Effect = frame.Effect with { Binding = frame.Effect.Binding with { Diablos325Resources = resources } }
        };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 256, 0, 0);

        NativeBattleDiablos325BodyDraw drawn = NativeBattleDiablos325Trail.Draw657F00(frame, projection);

        Assert.Equal(52, drawn.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(drawn.Packets.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(64, primitive.Bucket);
        Assert.Equal(0x00900050u, primitive.Words[2]);
        Assert.Equal(0x00A000F0u, primitive.Words[5]);
        Assert.Equal(0x00B00050u, primitive.Words[8]);
        Assert.Equal(0x00A000F0u, primitive.Words[11]);
        Assert.Equal<uint?>(0x10000000u, drawn.Memory.PrimitiveProcess!.Temporary5082B0Word4);
        Assert.Equal(points, frame.Memory.Arena!.Read(0x4C5C, points.Length));
        Assert.Same(frame.Memory.Arena, drawn.Memory.Arena);
        Assert.Equal<uint?>(0x10000000u, frame.Memory.PrimitiveProcess!.Temporary5082B0Word4);

        void PutPoint(int offset, short x, uint next)
        {
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(offset + 2), 80);
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(offset + 4), 1024);
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(offset + 6), 384);
            BinaryPrimitives.WriteUInt32LittleEndian(points.AsSpan(offset + 20), next);
        }
    }
}
