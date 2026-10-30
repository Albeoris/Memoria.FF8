using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85DrawingTests
{
    private static readonly NativeBattleSummonColors Colors = NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Title_strip_uses_screen_link_flags_and_preserves_unwritten_GT4_padding()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(88, 64).WriteByte(90, 37);
        NativeBattleTheEnd85DrawingStep result = NativeBattleTheEnd85Drawing.Draw78D2E0(task, NativeBattleFileStaging.Unknown());
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal((ushort)1, result.LinkFlags);
        Assert.False(result.PublishedFarColor);
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.Equal(5, packet.Primitive!.Bucket);
        Assert.Equal(0x3E808080u, packet.Primitive.Words[1]);
        Assert.Equal(0x00A000BAu, packet.Primitive.Words[2]);
        Assert.Equal(0x00A000FAu, packet.Primitive.Words[5]);
        Assert.Equal((byte)37, packet.Data[16]);
        Assert.Equal((byte)64, packet.Data[48]);
        foreach (int offset in new[] { 19, 31, 38, 39, 43, 50, 51 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[offset]);
        }
        Assert.Equal(new[] { 0, 0, 0, 0 }, packet.Primitive.VertexPayload4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(-1)]
    public void Black_bars_keep_signed_height_WORDs_and_exact_separate_packet_offsets(short height)
    {
        NativeBattleTheEnd85DrawingStep result = NativeBattleTheEnd85Drawing.Draw78D4D0(Task(), height, NativeBattleFileStaging.Unknown());
        Assert.Equal((ushort)1, result.LinkFlags);
        Assert.Equal(48, result.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 24 }, result.Packets.Attempts.Select(packet => packet.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt first = result.Packets.Attempts[0];
        NativeBattleGfGeometryAttempt last = result.Packets.Attempts[1];
        Assert.Equal(height, BinaryPrimitives.ReadInt16LittleEndian(first.Data.AsSpan().Slice(18)));
        Assert.Equal(unchecked((short)(216 - height)), BinaryPrimitives.ReadInt16LittleEndian(last.Data.AsSpan().Slice(10)));
        Assert.All(result.Packets.Attempts, packet =>
        {
            Assert.Equal(0x29000000u, packet.Primitive!.Words[1]);
            Assert.Equal(5, packet.Primitive.Bucket);
            Assert.All(packet.WriteMask, mask => Assert.Equal(NativeBattleGfPacketByteState.Known, mask));
        });
    }

    [Fact]
    public void Hidden_direct_draw_does_not_allocate_or_touch_source_or_far_color()
    {
        NativeBattleModulePoolTask task = Task().WriteByte(38, 4);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleTheEnd85DrawingStep result = NativeBattleTheEnd85Drawing.Draw788700(task, Projection(), Colors, staging, new((_, _) => throw new InvalidOperationException("Unexpected read."), (_, _) => throw new InvalidOperationException("Unexpected write.")));
        Assert.Same(staging, result.Staging);
        Assert.False(result.PublishedFarColor);
        Assert.Empty(result.Packets.Attempts);
    }

    [Fact]
    public void Node_Euler_priority_and_direct_position_reuse_original_matrix_kernels()
    {
        NativeBattleGfAnimationNode node = Node(0x40600, 0, 0, 0);
        byte[] bytes = node.Data.ToArray();
        StoreVector(bytes, 16, new(300, 400, 500));
        StoreVector(bytes, 8, new(-100, 200, -300));
        node = node with { Data = bytes.ToImmutableArray() };
        NativeBattleActionMatrix context = NativeBattleGfMatrices.Euler701310(new(100, 200, 300)) with { X = 13, Y = 17, Z = 4000 };
        NativeBattleActionMatrix result = NativeBattleTheEnd85Drawing.ComposeNode785790(node, context);
        Assert.Equal(NativeBattleGfMatrices.Euler701430(new(300, 400, 500)).Rotation.ToArray(), result.Rotation.ToArray());
        Assert.Equal(-87, result.X);
        Assert.Equal(217, result.Y);
        Assert.Equal(3700, result.Z);
    }

    [Fact]
    public void Morph_updates_only_XYZ_and_retains_released_context_unwritten_bytes_across_JSON_restore()
    {
        NativeBattleGfAnimationSource source = Source();
        Dictionary<uint, byte> scratch = [];
        for (uint index = 0; index < 4; index++)
        {
            scratch[0x25C1D28 + index * 8 + 6] = 0xAB;
            scratch[0x25C1D28 + index * 8 + 7] = 0xCD;
        }
        List<(uint Address, int Length)> writes = [];
        NativeBattleTheEnd85DrawServices services = new((address, length) =>
        {
            if (address >= source.Address && address - source.Address <= source.Data.Length - length)
            {
                return source.Data.AsSpan().Slice(checked((int)(address - source.Address)), length).ToArray();
            }
            return Enumerable.Range(0, length).Select(offset => scratch[address + (uint)offset]).ToArray();
        }, (address, bytes) =>
        {
            writes.Add((address, bytes.Length));
            for (int offset = 0; offset < bytes.Length; offset++)
            {
                scratch[address + (uint)offset] = bytes[offset];
            }
        });
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address + 100, new byte[] { 0x12, 0x34, 0x56, 0x78 });
        NativeBattleModulePoolTask task = Task().WriteWord(128, 0).WriteWord(136, unchecked((ushort)-32760));
        NativeBattleTheEnd85DrawingStep result = NativeBattleTheEnd85Drawing.DrawNode785790(task, source, Node(0x200, 0, 1, 2048), View(), Projection(), Colors, staging, services);
        Assert.True(result.PublishedFarColor);
        Assert.Null(result.FarRed);
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.Equal(4, writes.Count);
        Assert.All(writes, write => Assert.Equal(6, write.Length));
        for (uint index = 0; index < 4; index++)
        {
            Assert.Equal((byte)0xAB, scratch[0x25C1D28 + index * 8 + 6]);
            Assert.Equal((byte)0xCD, scratch[0x25C1D28 + index * 8 + 7]);
        }
        Assert.Equal(-33016, BinaryPrimitives.ReadInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 16, 4)));
        Assert.Equal(0x25C1D28u, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 4, 4)));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 100, 4));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(NativeBattleTemporaryMemory.Address + 8, 3));
        NativeBattleTheEnd85DrawingStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleTheEnd85DrawingStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_screen_paths_keep_authored_copies_flags_and_exact_zero_depths(bool title)
    {
        NativeBattleTheEnd85DrawingStep Draw() => title ? NativeBattleTheEnd85Drawing.Draw78D2E0(Task().WriteWord(88, 64).WriteByte(90, 37), NativeBattleFileStaging.Unknown()) : NativeBattleTheEnd85Drawing.Draw78D4D0(Task(), 40, NativeBattleFileStaging.Unknown());
        var native = Draw();
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw();
        var batch = NativeBattleTheEnd85Presentation.Depths(NativeBattleTheEnd85Presentation.Screen(captured.Packets, new(85, 1, 2, 3, 4)));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured with { Packets = batch }));
        Assert.Equal((ushort)1, captured.LinkFlags);
        Assert.Equal(batch.Attempts.Length, batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        foreach (var attempt in batch.Attempts)
        {
            var primitive = attempt.Primitive!;
            var geometry = primitive.PilotGeometry!;
            Assert.True(geometry.ScreenSpace);
            Assert.False(geometry.Continuous);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
            for (int corner = 0; corner < 4; corner++)
            {
                uint word = primitive.Words[2 + corner * (title ? 3 : 1)];
                Assert.Equal(unchecked((short)word), geometry.Corner(corner).X * geometry.ScreenScale * 0.125f);
                Assert.Equal(unchecked((short)(word >> 16)), geometry.Corner(corner).Y * geometry.ScreenScale * 0.125f);
            }
        }
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x7855C0, new byte[732 - 12].ToImmutableArray());
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4000, 4096);
    private static NativeBattleCameraProjection Projection() => new(View(), 160, 160, 108);

    private static NativeBattleGfAnimationNode Node(uint header, short first, short second, short ratio)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        StoreVector(bytes, 24, new(4096, 4096, 4096));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), first);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        const int geometry = 32;
        const int faces = 12 + 64;
        byte[] bytes = new byte[geometry + faces + 32 + 36];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + index * 4), geometry);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 4), 4);
        NativeBattleVector[] points = [new(-100, -100, 100), new(100, -100, 100), new(-100, 100, 100), new(100, 100, 100)];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                StoreVector(bytes, geometry + 12 + shape * 32 + corner * 8, points[corner] with { Y = unchecked((short)(points[corner].Y + shape * 100)) });
            }
        }
        int face = geometry + faces + 32;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x3C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        for (int corner = 1; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20 + corner * 4), 0x3C808080);
        }
        return new(0x17151EC, bytes.ToImmutableArray());
    }

    private static void StoreVector(byte[] bytes, int offset, NativeBattleVector point)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), point.Z);
    }
}
