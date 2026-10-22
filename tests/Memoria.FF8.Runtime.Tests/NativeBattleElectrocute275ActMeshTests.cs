using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275ActMeshTests
{
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Header200_rotates_anchor_by_camera_but_keeps_node_offset_and_Euler_in_camera_space()
    {
        NativeBattleGfAnimationNode node = Node(10000);
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x200);
        NativeBattleActionMatrix camera = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleEffectArena context = Context(0).Write(0, BitConverter.GetBytes((short)10000));
        NativeBattleElectrocute275ActMeshStep result = Draw(node with { Data = data.ToImmutableArray() }, context, camera);
        Assert.Equal(139998, result.Registers.Geometry.Translation[0]);
        Assert.Equal(-3, result.Registers.Geometry.Translation[1]);
        Assert.Equal((byte?)0, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)16, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Camera_R_transform_retains_MAC32_and_adds_camera_translation_afterward()
    {
        NativeBattleActionMatrix camera = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleElectrocute275ActMeshStep result = Draw(Node(10000), Context(0), camera);
        Assert.Equal(129998, result.Registers.Geometry.Translation[0]);
        Assert.Equal(-3, result.Registers.Geometry.Translation[1]);
        Assert.Equal((byte?)255, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)127, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Header40000_selects_actual_alternate_Euler_and_nonunit_scale_keeps_row_policy()
    {
        NativeBattleGfAnimationNode node = Node(100);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x40000);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 300);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 500);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 700);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 2048);
        node = node with { Data = bytes.ToImmutableArray() };
        NativeBattleElectrocute275ActMeshStep result = Draw(node, Context(0));
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler7015B0(new(300, 500, 700)), 2048, 4096, 4096);
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), expected.Rotation[index]);
        }
        Assert.Equal(rotation.Select(value => (byte?)value).ToArray(), result.Registers.Geometry.Rotation20.Take(18).ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x100);
        NativeBattleElectrocute275ActMeshStep rowScaled = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0);
        NativeBattleElectrocute275ActMeshStep ordinary = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(rowScaled));
    }

    [Fact]
    public void Fade_uses_fixed_20F0_and_native_node_color_without_reading_header_UV_options()
    {
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 2048);
        NativeBattleElectrocute275ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(0xC08040u, U32(result.Temporary88, 8));
        Assert.Equal(2048u, U32(result.Temporary88, 12));
        Assert.Equal(0x400u, result.FarControls.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x10, 4));
    }

    [Fact]
    public void Interpolated_shared_scratch_preserves_W_and_the_adjacent_packet_boundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(NativeBattleElectrocute275Profile.MorphOffset, NativeBattleElectrocute275Profile.MorphBytes).Write(NativeBattleElectrocute275Profile.MorphOffset + NativeBattleElectrocute275Profile.MorphBytes, [1, 2, 3, 4]);
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleElectrocute275ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0), arena: arena, count: 51);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(NativeBattleElectrocute275Profile.MorphOffset + 50 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(NativeBattleElectrocute275Profile.MorphOffset + 50 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Arena.Read(NativeBattleElectrocute275Profile.MorphOffset + NativeBattleElectrocute275Profile.MorphBytes, 4));
        Assert.Throws<InvalidDataException>(() => Draw(node, Context(0), count: 52));
    }

    [Fact]
    public void Physical_wrapper_publishes_GTE_and_88_bytes_before_delivery_and_restores_next_draw()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleElectrocute275Frame frame = new(memory, new(275, 274, new(275, NativeBattleElectrocute275Profile.Entry, NativeBattleElectrocute275Profile.Initializer), null, null), new(1), NativeBattleElectrocute275State.FromColdProcess().WriteDword(NativeBattleElectrocute275Profile.WorkingGlobal, NativeBattleElectrocute275Profile.ArenaAddress))
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown())
        };
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        int calls = 0;
        NativeBattleElectrocute275Frame Deliver(NativeBattleElectrocute275Frame current, NativeBattleGfGeometryBatch batch)
        {
            calls++;
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.Equal(0x1000224u + 16, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
            Assert.Equal(2000, current.Memory.GeometryRegisters!.Translation[2]);
            Assert.Equal(52, batch.AllocationBytes);
            return current;
        }
        NativeBattleElectrocute275Frame result = NativeBattleElectrocute275Drawing.EmitNode(frame, Source(4), Context(0), Node(0), camera, new(null, 100, 160, 108), Constants, Deliver);
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleElectrocute275Frame restored = JsonSerializer.Deserialize<NativeBattleElectrocute275Frame>(JsonSerializer.Serialize(result))!;
        NativeBattleElectrocute275Frame Next(NativeBattleElectrocute275Frame current) => NativeBattleElectrocute275Drawing.EmitNode(current, Source(4), Context(0), Node(0), camera, new(null, 100, 160, 108), Constants, (next, _) => next);
        Assert.Equal(JsonSerializer.Serialize(Next(result)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Direct_anchor_unknown_W_and_far_color_remain_unknown_while_dispatcher_clears_UV_offset()
    {
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
        NativeBattleGfAnimationSource source = Source(4);
        byte[] empty = source.Data.ToArray();
        int faces = BinaryPrimitives.ReadInt32LittleEndian(empty.AsSpan(16));
        BinaryPrimitives.WriteInt32LittleEndian(empty.AsSpan(16 + faces + 28), 0);
        NativeBattleElectrocute275ActMeshStep result = NativeBattleElectrocute275ActMesh.Draw(source with { Data = empty.ToImmutableArray() }, node with { Data = bytes.ToImmutableArray() }, Context(0).MarkUnknown(6, 2), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), NativeBattleEffectArena.Clear(), NativeBattleElectrocute275Profile.ArenaAddress + NativeBattleElectrocute275Profile.MorphOffset, NativeBattleElectrocute275Profile.MorphBytes, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), new(null, 100, 160, 108), 0, 0, 0x155, 0x100, Constants);
        Assert.Empty(result.Batch.Attempts);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(8, 4));
        Assert.Equal(0u, U32(result.Temporary88, 0x18));
    }

    private static NativeBattleElectrocute275ActMeshStep Draw(NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleActionMatrix? camera = null, NativeBattleEffectArena? arena = null, int count = 4)
    {
        return NativeBattleElectrocute275ActMesh.Draw(Source(count), node, context, NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), arena ?? NativeBattleEffectArena.Clear(), NativeBattleElectrocute275Profile.ArenaAddress + NativeBattleElectrocute275Profile.MorphOffset, NativeBattleElectrocute275Profile.MorphBytes, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), new(null, 100, 160, 108), 0, 0, 0x155, 0x100, Constants);
    }

    private static NativeBattleEffectArena Context(int unused)
    {
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, new byte[8]).Write(8, BitConverter.GetBytes(NativeBattleElectrocute275Profile.ArenaAddress + NativeBattleElectrocute275Profile.MorphOffset));
    }

    private static NativeBattleGfAnimationNode Node(short x)
    {
        byte[] node = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(8), x);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 68];
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
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
