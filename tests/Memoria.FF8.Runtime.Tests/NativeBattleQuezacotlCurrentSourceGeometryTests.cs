using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlCurrentSourceGeometryTests
{
    private const int ResourceOffset = 0x28E9C;
    private const uint ResourceAddress = 0x20DFAB8 + ResourceOffset;
    private const int ResourceLength = 136;
    private static NativeBattleOriginalEffectState Effect => new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096, 4503601774854144d);
    private static NativeBattleCameraProjection Projection => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);

    [Theory]
    [InlineData(0, 32767, 0, -64)]
    [InlineData(32767, 1, 4096, 64)]
    [InlineData(0, 0, 2048, -64)]
    public void Arena_source_selects_only_the_native_endpoint_shape_using_the_twelve_byte_header(short first, short second, short ratio, short expectedX)
    {
        NativeBattleEffectMemory memory = Memory();
        Assert.Null(memory.QuezacotlExecutableMemory);
        NativeBattleGfAnimationSource source = Source(memory);
        NativeBattleQuezacotlGeometryStep actual = NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(first, second, ratio), source, Projection, Colors, default);
        NativeBattleGeometryPrimitive primitive = Assert.Single(actual.Batch.Attempts).Primitive!;
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(Projection.View!, new(expectedX, -64, 0), Projection);
        Assert.Equal(projected.Screen, primitive.Words[2]);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
        Assert.Equal(272, primitive.Bucket);
        Assert.Equal(20, actual.Batch.AllocationBytes);
        Assert.False(actual.CapturedPackets);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory), NativeBattleTestSnapshots.Json(actual.Memory));
    }

    [Fact]
    public void Current_arena_color_changes_are_observed_after_independent_JSON_without_an_executable_fallback()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleGfAnimationNode node = Node(0, 0, 0);
        List<ushort> depths = [];
        NativeBattleQuezacotlGeometryStep before = NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, node, Source(memory), Projection, Colors, default, depthBias: -1024, observeDepth: depths.Add);
        Assert.Equal(new ushort[] { 4096, 4096, 4096 }, depths);
        Assert.Equal(17, Assert.Single(before.Batch.Attempts).Primitive!.Bucket);
        Assert.Equal(0x20ABCDEFu, before.Batch.Attempts[0].Primitive!.Words[1]);
        byte[] color = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(color, 0x20332211);
        memory = memory with { Arena = memory.Arena!.Write(ResourceOffset + 96, color) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleQuezacotlGeometryStep actual = NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, node, Source(memory), Projection, Colors, default, depthBias: -1024);
        NativeBattleQuezacotlGeometryStep clone = NativeBattleQuezacotlGeometry.Emit6C6060(restored, Effect, node, Source(restored), Projection, Colors, default, depthBias: -1024);
        Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(clone));
        Assert.Equal(0x20332211u, actual.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(before.Batch.Attempts[0].Primitive!.Words.Skip(2), actual.Batch.Attempts[0].Primitive!.Words.Skip(2));
        NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(actual.Memory, Effect, NativeBattleLogicalOrdering.Clear(), actual.Batch);
        Assert.Equal(148, written.Effect.DrawArenaOffset);
        Assert.Equal(ResourceAddress, Source(written.Memory).Address);
        Assert.Equal(Source(memory).Data.ToArray(), Source(written.Memory).Data.ToArray());
        Assert.Single(written.Ordering.Proxies);
    }

    [Fact]
    public void Interpolated_arena_shapes_reuse_the_same_XYZ_only_morph_scratch_and_preserve_unknown_padding()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena arena = memory.Arena!.Write(0x8D0, Enumerable.Repeat((byte)0xA5, 24).ToArray());
        for (int vertex = 0; vertex < 3; vertex++)
        {
            arena = arena.MarkUnknown(0x8D6 + vertex * 8, 2);
        }
        memory = memory with { Arena = arena };
        NativeBattleQuezacotlGeometryStep actual = NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(0, 1, 2048), Source(memory), Projection, Colors, default);
        short[] expectedX = [0, 128, 0];
        for (int vertex = 0; vertex < 3; vertex++)
        {
            Assert.Equal(expectedX[vertex], BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.Arena!.Read(0x8D0 + vertex * 8, 2)));
            Assert.Throws<NotSupportedException>(() => actual.Memory.Arena.Read(0x8D6 + vertex * 8, 2));
        }
        Assert.Equal(Source(memory).Data.ToArray(), Source(actual.Memory).Data.ToArray());
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(NativeBattleQuezacotlGeometry.Emit6C6060(restored, Effect, Node(0, 1, 2048), Source(restored), Projection, Colors, default)));
    }

    [Fact]
    public void A_bounded_current_source_rejects_shape_and_geometry_reads_outside_its_observed_extent()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleGfAnimationSource truncated = new(ResourceAddress, Source(memory).Data.AsSpan().Slice(0, 48).ToArray().ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(0, 1, 4096), truncated, Projection, Colors, default));
        NativeBattleGfAnimationSource invalid = new(ResourceAddress, default);
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(0, 0, 0), invalid, Projection, Colors, default));
        memory = memory with { Arena = memory.Arena!.MarkUnknown(ResourceOffset + 44, 2) };
        Assert.Throws<NotSupportedException>(() => Source(memory));
    }

    [Fact]
    public void Existing_executable_overload_keeps_full_bank_bounds_for_a_signed_shape_before_the_resource_pointer()
    {
        const uint sourceAddress = 0x12A9664;
        byte[] bytes = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        int root = checked((int)(sourceAddress - NativeBattleQuezacotlExecutableMemory.Address));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(root - 4), -64);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(root - 2), -64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(root + 8), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(root + 12), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(root + 16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(root + 20), 0x20ABCDEF);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(root + 24), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(root + 26), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(root + 28), 4);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlExecutableMemory = new(bytes.ToImmutableArray()) };
        NativeBattleQuezacotlGeometryStep actual = NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(-1, -1, 0), Projection, Colors, default, sourceAddress: sourceAddress);
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        NativeBattleGfAnimationSource narrowed = new(sourceAddress, bytes.AsSpan(root, 64).ToArray().ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlGeometry.Emit6C6060(memory, Effect, Node(-1, -1, 0), narrowed, Projection, Colors, default));
    }

    private static NativeBattleGfAnimationNode Node(short first, short second, short ratio)
    {
        byte[] data = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(40), first);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), second);
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(NativeBattleEffectMemory memory)
    {
        return new(ResourceAddress, memory.Arena!.Read(ResourceOffset, ResourceLength).ToImmutableArray());
    }

    private static NativeBattleEffectMemory Memory()
    {
        byte[] resource = new byte[ResourceLength];
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(8), 32);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(32), 60);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(36), 3);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(40), 2);
        NativeBattleVector[] vertices = [new(-64, -64, 0), new(64, -64, 0), new(-64, 64, 0)];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 44 + shape * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(offset), (short)(vertices[vertex].X + shape * 128));
                BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(offset + 2), vertices[vertex].Y);
                BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(offset + 4), vertices[vertex].Z);
                BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(offset + 6), 0xBEEF);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(92), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(96), 0x20ABCDEF);
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(100), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(102), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(104), 4);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        return memory with { Arena = memory.Arena!.Write(ResourceOffset, resource) };
    }
}
