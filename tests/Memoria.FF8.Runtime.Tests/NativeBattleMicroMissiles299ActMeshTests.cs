using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMicroMissiles299ActMeshTests
{
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Actual_64_vertex_morph_retains_W_and_adjacent_packet_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x4580, 512).Write(0x4780, [1, 2, 3, 4]);
        NativeBattleMicroMissiles299ActMeshStep result = Draw(Source(64), Node(0x200, 2048), arena);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Arena.Read(0x4780, 4));
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x4580 + 63 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4580 + 63 * 8 + 6, 2));
        Assert.Throws<InvalidDataException>(() => Draw(Source(65), Node(0x200, 2048), arena));
    }

    [Fact]
    public void Direct_selected_shape_does_not_read_unknown_morph_pointer_or_root_rotation()
    {
        NativeBattleEffectArena context = Context().MarkUnknown(0, 20).MarkUnknown(0x24, 4);
        NativeBattleMicroMissiles299ActMeshStep result = Draw(Source(4), Node(0x200, 4096), NativeBattleEffectArena.Clear(), context);
        Assert.Equal(0x10490A8u + 16u + 12u + 32u, U32(result.Temporary88, 4));
        Assert.Equal(0x2030u, U32(result.Temporary88, 0x1C));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
    }

    [Fact]
    public void Header8000_skips_CPU_matrix_product_but_keeps_full_MAC32_position()
    {
        NativeBattleActionMatrix matrix = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096);
        NativeBattleMicroMissiles299ActMeshStep result = Draw(Source(4), Node(0x8000, 0, 10000), NativeBattleEffectArena.Clear(), Context(matrix));
        Assert.Equal(79998, result.Registers.Geometry.Translation[0]);
        Assert.Equal((byte?)0, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)16, result.Registers.Geometry.Rotation20[1]);
        NativeBattleMicroMissiles299ActMeshStep extra = Draw(Source(4), Node(0x49000, 0, 10000), NativeBattleEffectArena.Clear(), Context(matrix));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(extra));
    }

    [Fact]
    public void Header100_does_not_select_column_scaling_and_header2000_alone_controls_force_opaque_flags()
    {
        NativeBattleGfAnimationNode node = Node(0x200, 0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 8192);
        node = node with { Data = bytes.ToImmutableArray() };
        NativeBattleMicroMissiles299ActMeshStep plain = Draw(Source(4), node, NativeBattleEffectArena.Clear());
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x300);
        NativeBattleMicroMissiles299ActMeshStep bit100 = Draw(Source(4), node with { Data = bytes.ToImmutableArray() }, NativeBattleEffectArena.Clear());
        Assert.Equal(plain.Registers.Geometry.Rotation20, bit100.Registers.Geometry.Rotation20);
        Assert.Equal(0x2030u, U32(bit100.Temporary88, 0x1C));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x6200);
        NativeBattleMicroMissiles299ActMeshStep force = Draw(Source(4), node with { Data = bytes.ToImmutableArray() }, NativeBattleEffectArena.Clear());
        Assert.Equal(0x203Cu, U32(force.Temporary88, 0x1C));
    }

    [Fact]
    public void Fade_publishes_C0_and_far_color_and_leaves_unwritten_temporary_lanes_unknown()
    {
        NativeBattleGfAnimationNode node = Node(0x2200, 0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 2048);
        NativeBattleMicroMissiles299ActMeshStep result = Draw(Source(4), node with { Data = bytes.ToImmutableArray() }, NativeBattleEffectArena.Clear());
        Assert.Equal(0x20FCu, U32(result.Temporary88, 0x1C));
        Assert.Equal(0xC08040u, U32(result.Temporary88, 8));
        Assert.Equal(2048u, U32(result.Temporary88, 12));
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x10, 4));
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x40, 24));
    }

    [Fact]
    public void Physical_wrapper_publishes_88_bytes_before_delivery_and_releases_the_same_allocation()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleMicroMissiles299Frame frame = new(memory, new(299, 298, new(299, NativeBattleMicroMissiles299Profile.Entry, NativeBattleMicroMissiles299Profile.Initializer), null, null), new(1), NativeBattleMicroMissiles299State.FromColdProcess())
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown())
        };
        int calls = 0;
        NativeBattleMicroMissiles299Frame Deliver(NativeBattleMicroMissiles299Frame current, NativeBattleGfGeometryBatch batch)
        {
            calls++;
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.Equal(0x10490A8u + 16, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
            Assert.True(batch.AllocationBytes > 0);
            return current;
        }
        NativeBattleMicroMissiles299Frame result = NativeBattleMicroMissiles299Drawing.EmitNode(frame, Source(4), Context(), Node(0x200, 0), new(null, 100, 160, 108), Constants, Deliver);
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleMicroMissiles299Frame restored = JsonSerializer.Deserialize<NativeBattleMicroMissiles299Frame>(JsonSerializer.Serialize(result))!;
        NativeBattleMicroMissiles299Frame Next(NativeBattleMicroMissiles299Frame current) => NativeBattleMicroMissiles299Drawing.EmitNode(current, Source(4), Context(), Node(0x200, 2048), new(null, 100, 160, 108), Constants, (next, _) => next);
        Assert.Equal(JsonSerializer.Serialize(Next(result)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Hidden_node_does_not_read_or_allocate_unreached_physical_owners()
    {
        NativeBattleGfAnimationNode node = Node(0, 0);
        byte[] bytes = node.Data.ToArray();
        bytes.AsSpan(24, 6).Clear();
        NativeBattleMicroMissiles299Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), null!, new(1), NativeBattleMicroMissiles299State.FromUnknown());
        Assert.Same(frame, NativeBattleMicroMissiles299Drawing.EmitNode(frame, null!, null!, node with { Data = bytes.ToImmutableArray() }, null!, null!, (_, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleMicroMissiles299ActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena arena, NativeBattleEffectArena? context = null)
    {
        return NativeBattleMicroMissiles299ActMesh.Draw(source, node, context ?? Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), arena, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), new(null, 100, 160, 108), 0, 0, 0x155, 0x100, Constants);
    }

    private static NativeBattleEffectArena Context(NativeBattleActionMatrix? matrix = null)
    {
        NativeBattleActionMatrix root = matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
        context = NativeBattleEffectOwnedModelRendering.StoreMatrix(context, 0, NativeBattlePackedMatrix32.FromMatrix(root, null));
        return context.Write(0x20, BitConverter.GetBytes(unchecked((uint)-128))).Write(0x24, BitConverter.GetBytes(NativeBattleMicroMissiles299Profile.ArenaAddress + NativeBattleMicroMissiles299Profile.MorphOffset));
    }

    private static NativeBattleGfAnimationNode Node(uint header, short ratio, short x = 0)
    {
        byte[] node = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(8), x);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 56];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (shape * count + vertex) * 8;
                short x = (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100);
                short y = (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), x);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 12;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x2C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x10490A8, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    }
}
