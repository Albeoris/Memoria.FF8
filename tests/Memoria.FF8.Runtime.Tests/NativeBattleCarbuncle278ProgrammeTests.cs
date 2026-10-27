using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278ProgrammeTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Programme_context_uses_native_positive_A00_rotation_and_posed_translation()
    {
        NativeBattleActionMatrix context = NativeBattleCarbuncle278Programme.PrepareContext681949(new(10, 20, 30), View with { Z = 0 });
        Assert.Equal(new short[] { 4096, 0, 0, 0, -2896, -2896, 0, 2896, -2896 }, context.Rotation);
        Assert.Equal((10, 20, 30), (context.X, context.Y, context.Z));
    }

    [Fact]
    public void Matrix_preserves_column_scale_rounding_and_direct_position_flags_without_billboard_inference()
    {
        NativeBattleActionMatrix context = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleGfAnimationNode node = Node(scale: new(1024, 2048, 4096), position: new(10, 20, 30));
        NativeBattleActionMatrix row = NativeBattleCarbuncle278Programme.Compose681FD0(node, context);
        Assert.Equal(new short[] { 0, -2048, 0, 1024, 0, 0, 0, 0, 4096 }, row.Rotation);
        Assert.Equal((80, 210, 330), (row.X, row.Y, row.Z));
        NativeBattleActionMatrix column = NativeBattleCarbuncle278Programme.Compose681FD0(Node(0x100, scale: new(1024, 2048, 4096), position: new(10, 20, 30)), context);
        Assert.Equal(new short[] { 0, -2048, 0, 1024, 0, 0, 0, 0, 4096 }, column.Rotation);
        NativeBattleActionMatrix direct = NativeBattleCarbuncle278Programme.Compose681FD0(Node(0x200, position: new(10, 20, 30)), context);
        Assert.Equal(View.Rotation.ToArray(), direct.Rotation.ToArray());
        Assert.Equal((110, 220, 330), (direct.X, direct.Y, direct.Z));
        NativeBattleActionMatrix ordinary = NativeBattleCarbuncle278Programme.Compose681FD0(Node(), context);
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary), NativeBattleTestSnapshots.Json(NativeBattleCarbuncle278Programme.Compose681FD0(Node(0x1000), context)));
        NativeBattleActionMatrix fractional = context with { Rotation = [3, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        Assert.Equal((short)1, NativeBattleCarbuncle278Programme.Compose681FD0(Node(scale: new(2048, 4096, 4096)), fractional).Rotation[0]);
        Assert.Equal((short)2, NativeBattleCarbuncle278Programme.Compose681FD0(Node(0x100, scale: new(2048, 4096, 4096)), fractional).Rotation[0]);
    }

    [Fact]
    public void Morph_reuses_shared_xyz_only_stores_and_packet_depth_bias_without_touching_padding()
    {
        NativeBattleEffectMemory source = Memory();
        NativeBattleCarbuncle278ProgrammeNode draw = Draw(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source)), Node(ratio: 2048));
        Assert.True(draw.Active);
        Assert.Null(draw.FarColor);
        Assert.Equal((short)-10, BinaryPrimitives.ReadInt16LittleEndian(draw.Memory.Arena!.Read(0x2E84, 2)));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, draw.Memory.Arena.Read(0x2E84 + 6, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, draw.Memory.Arena.Read(0x2E84 + 24, 2));
        Assert.Throws<NotSupportedException>(() => draw.Memory.Arena.Read(0x2E84 + 22, 2));
        Assert.Equal(20, draw.Packets.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(draw.Packets.Attempts).Primitive!;
        Assert.Equal(208, primitive.Bucket);
        Assert.All(source.Arena!.Read(0x2E84, 22), value => Assert.Equal((byte)0xA5, value));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(source))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(draw), NativeBattleTestSnapshots.Json(Draw(restored, Node(ratio: 2048))));
    }

    [Theory]
    [InlineData(0, -20)]
    [InlineData(4096, 0)]
    public void Endpoint_selection_reads_original_shape_and_preserves_unknown_scratch(int ratio, short x)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E84, 0x1000) };
        NativeBattleCarbuncle278ProgrammeNode draw = Draw(memory, Node(ratio: (short)ratio));
        Assert.Same(memory, draw.Memory);
        uint screen = NativeBattleModuleProjection.Project45FE10(View, new(x, -20, 0), Projection).Screen;
        Assert.Equal(screen, Assert.Single(draw.Packets.Attempts).Primitive!.Words[2]);
    }

    [Fact]
    public void Skip_gates_avoid_missing_source_and_cue_retains_explicit_far_color()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleCarbuncle278ProgrammeNode zero = NativeBattleCarbuncle278Programme.Node681FD0(memory, new(0, []), Node(scale: new(0, 0, 0)), View, Projection, Colors);
        Assert.False(zero.Active);
        Assert.Same(memory, zero.Memory);
        Assert.False(Draw(memory, Node(cue: 4096)).Active);
        NativeBattleCarbuncle278ProgrammeNode cue = Draw(Memory(), Node(cue: 2048, color: 0x123456));
        Assert.Equal(0x123456u, cue.FarColor);
        byte[] invalid = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(2), ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => Draw(Memory(), Node() with { Data = invalid.ToImmutableArray() }));
    }

    private static NativeBattleCarbuncle278ProgrammeNode Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationNode node) => NativeBattleCarbuncle278Programme.Node681FD0(memory, Source(), node, View, Projection, Colors);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x2E84, Enumerable.Repeat((byte)0xA5, 0x1000).ToArray()).MarkUnknown(0x2E84 + 22, 2) };

    private static NativeBattleGfAnimationNode Node(uint header = 0, short ratio = 0, short cue = 0, uint color = 0, NativeBattleVector? scale = null, NativeBattleVector? position = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Put(8, position ?? new(0, 0, 0));
        Put(24, scale ?? new(4096, 4096, 4096));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), color);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), cue);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        return new(0, 0, bytes.ToImmutableArray());
        void Put(int offset, NativeBattleVector vector)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vector.Z);
        }
    }
    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[116];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 60);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 24 + shape * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)((vertex == 0 ? -20 : vertex == 1 ? 20 : 0) + shape * 20));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(vertex == 2 ? 20 : -20));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 6), (ushort)(shape == 0 ? 0x1111 : 0x2222));
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 1);
        Face().CopyTo(bytes.AsSpan(76));
        return new(0x1000, bytes.ToImmutableArray());
    }

    private static byte[] Face()
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x20808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 4);
        return bytes;
    }
}
