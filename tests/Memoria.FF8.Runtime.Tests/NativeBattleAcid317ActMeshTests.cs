using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317ActMeshTests
{
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Header1000_rotates_anchor_by_camera_and_offset_by_normalized_view_separately()
    {
        NativeBattleGfAnimationNode node = Node(10000);
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x1000);
        NativeBattleActionMatrix camera = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleEffectArena context = Context(0).Write(0, BitConverter.GetBytes((short)10000));
        NativeBattleAcid317ActMeshStep result = Draw(node with { Data = data.ToImmutableArray() }, context, camera);
        Assert.Equal(139998, result.Registers.Geometry.Translation[0]);
        Assert.Equal(-3, result.Registers.Geometry.Translation[1]);
        Assert.Equal((byte?)0, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)16, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Camera_R_transform_retains_MAC32_and_adds_camera_translation_afterward()
    {
        NativeBattleActionMatrix camera = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleAcid317ActMeshStep result = Draw(Node(10000), Context(0), camera);
        Assert.Equal(129998, result.Registers.Geometry.Translation[0]);
        Assert.Equal(-3, result.Registers.Geometry.Translation[1]);
        Assert.Equal((byte?)255, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)127, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Header_bits_do_not_select_alternate_Euler_position_scaling_or_UV_flags()
    {
        NativeBattleGfAnimationNode node = Node(100);
        NativeBattleAcid317ActMeshStep expected = Draw(node, Context(0));
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xC6300);
        NativeBattleAcid317ActMeshStep actual = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Fade_uses_fixed_20F0_and_native_node_color_without_reading_header_UV_options()
    {
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 2048);
        NativeBattleAcid317ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(0xC08040u, U32(result.Temporary88, 8));
        Assert.Equal(2048u, U32(result.Temporary88, 12));
        Assert.Equal(0x400u, result.FarControls.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x10, 4));
    }

    [Fact]
    public void Interpolated_shared_scratch_preserves_W_and_the_adjacent_packet_boundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(NativeBattleAcid317Profile.MorphOffset, NativeBattleAcid317Profile.MorphBytes).Write(NativeBattleAcid317Profile.MorphOffset + NativeBattleAcid317Profile.MorphBytes, [1, 2, 3, 4]);
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleAcid317ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0), arena: arena, count: 64);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(NativeBattleAcid317Profile.MorphOffset + 63 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(NativeBattleAcid317Profile.MorphOffset + 63 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Arena.Read(NativeBattleAcid317Profile.MorphOffset + NativeBattleAcid317Profile.MorphBytes, 4));
        Assert.Throws<InvalidDataException>(() => Draw(node, Context(0), count: 65));
    }

    [Fact]
    public void Physical_wrapper_publishes_GTE_and_88_bytes_before_delivery_and_restores_next_draw()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleAcid317Frame frame = new(memory, new(300, 299, new(300, NativeBattleAcid317Profile.Entry, NativeBattleAcid317Profile.Initializer), null, null), new(1), NativeBattleAcid317State.FromColdProcess())
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown())
        };
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        int calls = 0;
        NativeBattleAcid317Frame Deliver(NativeBattleAcid317Frame current, NativeBattleGfGeometryBatch batch)
        {
            calls++;
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.Equal(0x1000224u + 16, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
            Assert.Equal(2000, current.Memory.GeometryRegisters!.Translation[2]);
            Assert.Equal(52, batch.AllocationBytes);
            return current;
        }
        NativeBattleAcid317Frame result = NativeBattleAcid317Drawing.EmitNode(frame, Source(4), Context(0), Node(0), camera, camera, new(null, 100, 160, 108), Constants, Deliver);
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleAcid317Frame restored = JsonSerializer.Deserialize<NativeBattleAcid317Frame>(JsonSerializer.Serialize(result))!;
        NativeBattleAcid317Frame Next(NativeBattleAcid317Frame current) => NativeBattleAcid317Drawing.EmitNode(current, Source(4), Context(0), Node(0), camera, camera, new(null, 100, 160, 108), Constants, (next, _) => next);
        Assert.Equal(JsonSerializer.Serialize(Next(result)), JsonSerializer.Serialize(Next(restored)));
    }

    private static NativeBattleAcid317ActMeshStep Draw(NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleActionMatrix? camera = null, NativeBattleEffectArena? arena = null, int count = 4)
    {
        return NativeBattleAcid317ActMesh.Draw(Source(count), node, context, NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), arena ?? NativeBattleEffectArena.Clear(), new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), new(null, 100, 160, 108), 0, 0, 0x155, 0x100, Constants);
    }

    private static NativeBattleEffectArena Context(int unused)
    {
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, new byte[8]).Write(8, BitConverter.GetBytes(NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.MorphOffset));
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
