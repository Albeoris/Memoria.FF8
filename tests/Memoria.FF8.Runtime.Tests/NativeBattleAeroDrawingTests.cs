using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAeroDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleAeroNodeConfig Config => new(new(0, 0, 0, 0), 4096, 4096, 4096, true, 0, 0x22A4);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Global_columns_scale_before_basis_and_node_rows_scale_after_basis()
    {
        NativeBattleActionMatrix rotated = View with { Rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096] };
        NativeBattleAeroNodeConfig config = Config with { ScaleX = 2048, ScaleY = 4096, ScaleZ = 8192 };
        NativeBattleGfAnimationNode node = Node(scale: new(8192, 4096, 2048));
        NativeBattleActionMatrix rows = NativeBattleAeroDrawing.ComposeNodeMatrix(node, config, rotated, View);
        Assert.Equal(new short[] { 0, -8192, 0, 2048, 0, 0, 0, 0, 4096 }, rows.Rotation.ToArray());
        NativeBattleActionMatrix columns = NativeBattleAeroDrawing.ComposeNodeMatrix(Node(0x100, scale: new(8192, 4096, 2048)), config, rotated, View);
        Assert.Equal(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }, columns.Rotation.ToArray());
    }

    [Fact]
    public void Billboard_position_uses_separate_full_MAC_vectors_while_ordinary_anchor_add_wraps_WORD()
    {
        NativeBattleActionMatrix view = View with { X = 123 };
        NativeBattleAeroNodeConfig config = Config with { Anchor = new(30000, 0, 0, 0) };
        NativeBattleActionMatrix facing = NativeBattleAeroDrawing.ComposeNodeMatrix(Node(0x1000, position: new(10000, 0, 0)), config, view, View);
        NativeBattleActionMatrix ordinary = NativeBattleAeroDrawing.ComposeNodeMatrix(Node(position: new(10000, 0, 0)), config, view, View);
        Assert.Equal(40123, facing.X);
        Assert.Equal(-25413, ordinary.X);
        NativeBattleActionMatrix doubled = view with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        Assert.Equal(70123, NativeBattleAeroDrawing.ComposeNodeMatrix(Node(0x1000, position: new(10000, 0, 0)), config, doubled, View).X);
    }

    [Fact]
    public void Header8000_skips_basis_rotation_but_keeps_position_transform_and_explicit_global_scale_gate()
    {
        NativeBattleActionMatrix rotated = View with { Rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096] };
        NativeBattleAeroNodeConfig config = Config with { ScaleX = 8192, GlobalScale = false };
        NativeBattleActionMatrix matrix = NativeBattleAeroDrawing.ComposeNodeMatrix(Node(0x8000, position: new(10, 20, 30)), config, rotated, View);
        Assert.Equal(View.Rotation.ToArray(), matrix.Rotation.ToArray());
        Assert.Equal(-20, matrix.X);
        Assert.Equal(10, matrix.Y);
        Assert.Equal(4126, matrix.Z);
    }

    [Fact]
    public void Morph_uses_twelve_byte_shape_header_and_preserves_fourth_WORDs_and_unused_shared_scratch()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleAeroNodeDraw draw = Draw(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(memory)), Node(ratio: 2048));
        byte[] scratch = draw.Memory.Arena!.Read(0x22A4, 0x2E0);
        Assert.Equal(-10, BinaryPrimitives.ReadInt16LittleEndian(scratch));
        Assert.Equal(-20, BinaryPrimitives.ReadInt16LittleEndian(scratch.AsSpan(2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(scratch.AsSpan(4)));
        Assert.Equal(0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(scratch.AsSpan(6)));
        Assert.All(scratch.AsSpan(24).ToArray(), value => Assert.Equal(0xA5, value));
        Assert.Equal(20, draw.Packets.AllocationBytes);
        Assert.Equal(0x20808080u, Assert.Single(draw.Packets.Attempts).Primitive!.Words[1]);
        Assert.All(memory.Arena!.Read(0x22A4, 0x2E0), value => Assert.Equal(0xA5, value));
    }

    [Theory]
    [InlineData(0, -20)]
    [InlineData(4096, 0)]
    public void Endpoint_shape_selection_leaves_shared_scratch_untouched(int ratio, short firstX)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleAeroNodeDraw draw = Draw(memory, Node(ratio: checked((short)ratio)));
        Assert.Same(memory, draw.Memory);
        uint expected = NativeBattleModuleProjection.Project45FE10(View, new(firstX, -20, 0), Projection).Screen;
        Assert.Equal(expected, Assert.Single(draw.Packets.Attempts).Primitive!.Words[2]);
    }

    [Fact]
    public void Skip_gates_do_not_consume_geometry_or_scratch_and_invalid_signed_geometry_is_rejected()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGfAnimationSource absent = new(0, []);
        NativeBattleAeroNodeDraw zero = NativeBattleAeroDrawing.Node(memory, absent, Node(scale: new(0, 0, 0)), Config, View, View, Projection, Colors);
        Assert.Same(memory, zero.Memory);
        Assert.Empty(zero.Packets.Attempts);
        Assert.Empty(NativeBattleAeroDrawing.Node(memory, absent, Node(cue: 4096), Config, View, View, Projection, Colors).Packets.Attempts);
        NativeBattleGfAnimationNode invalid = Node();
        byte[] bytes = invalid.Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => Draw(Memory(), invalid with { Data = bytes.ToImmutableArray() }));
    }

    [Fact]
    public void Morph_owner_restores_to_the_same_next_draw_and_particle_uses_zero_flags_source_colors()
    {
        NativeBattleEffectMemory memory = Draw(Memory(), Node(ratio: 1024)).Memory;
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleAeroNodeDraw first = Draw(memory, Node(ratio: 3072));
        NativeBattleAeroNodeDraw second = Draw(restored, Node(ratio: 3072));
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
        byte[] mesh = new byte[76];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 3);
        Source().Data.AsSpan().Slice(24, 24).CopyTo(mesh.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(32), 1);
        Face().CopyTo(mesh.AsSpan(36));
        NativeBattleAeroResources resources = new(new("animation", []), Source(), Source(), new(0, 0, mesh.ToImmutableArray()), 0);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x6C14B0, new byte[48].ToImmutableArray()).WriteWord(0x10, 4096);
        NativeBattleGfGeometryBatch packet = NativeBattleAeroDrawing.Particle(task, resources, Projection, View, Colors);
        Assert.Equal(0x20808080u, Assert.Single(packet.Attempts).Primitive!.Words[1]);
        Assert.Equal(20, packet.AllocationBytes);
    }

    private static NativeBattleAeroNodeDraw Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationNode node) => NativeBattleAeroDrawing.Node(memory, Source(), node, Config, View, View, Projection, Colors);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x22A4, Enumerable.Repeat((byte)0xA5, 0x2E0).ToArray()) };

    private static NativeBattleGfAnimationNode Node(uint header = 0, NativeBattleVector? position = null, NativeBattleVector? scale = null, short ratio = 0, short cue = 0)
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        Vector(8, position ?? new(0, 0, 0));
        Vector(24, scale ?? new(4096, 4096, 4096));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), cue);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        return new(0, 0, data.ToImmutableArray());

        void Vector(int offset, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), value.Z);
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
