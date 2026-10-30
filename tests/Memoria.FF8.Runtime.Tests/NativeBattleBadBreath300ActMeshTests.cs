using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath300ActMeshTests
{
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1024)]
    [InlineData((short)4096)]
    public void Group19_capture_preserves_selected_and_morphed_native_state_and_actual_owner(short ratio)
    {
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        byte[] nodeBytes = Node(0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(42), 1);
        var node = Node(0) with { Data = nodeBytes.ToImmutableArray() };
        var native = Draw(node, Context(0), projection: projection);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var identity = new NativeBattlePilotVisualIdentity(300, 0x2500000, 2, 123, 44) { ResourceGeneration = 0x100000001 };
        var captured = Draw(node, Context(0), pilotIdentity: identity, projection: projection);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        var geometry = Assert.Single(captured.Batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!.PilotGeometry!;
        Assert.Equal(identity.Effect, geometry.Identity.Effect);
        Assert.Equal(identity.Bank, geometry.Identity.Bank);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(4, geometry.CornerCount);
        Assert.NotEqual(default(System.Numerics.Vector3), geometry.A);
    }

    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Zero_stack_gate_does_not_read_unknown_scale_or_unused_morph_pointer()
    {
        NativeBattleEffectArena context = Context(0).MarkUnknown(8, 12).MarkUnknown(0x18, 4);
        NativeBattleBadBreath300ActMeshStep result = Draw(Node(0), context);
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Registers.Geometry.Translation);
        Assert.Equal(0x2030u, U32(result.Temporary88, 0x1C));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(8, 4));
    }

    [Fact]
    public void Unknown_reached_stack_gate_fails_instead_of_inventing_a_scaling_policy()
    {
        Assert.Throws<NotSupportedException>(() => Draw(Node(0), Context(0).MarkUnknown(0x14, 4)));
    }

    [Fact]
    public void Nonzero_gate_scales_point_and_base_matrix_before_WORD_anchor_addition()
    {
        NativeBattleEffectArena context = Context(-3).Write(0, BitConverter.GetBytes((short)10)).Write(8, BitConverter.GetBytes(8192));
        NativeBattleBadBreath300ActMeshStep result = Draw(Node(100), context);
        Assert.Equal(210, result.Registers.Geometry.Translation[0]);
        Assert.Equal((byte?)0, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)32, result.Registers.Geometry.Rotation20[1]);
        NativeBattleBadBreath300ActMeshStep wrapped = Draw(Node(20000), context);
        Assert.Equal(unchecked((short)40010), wrapped.Registers.Geometry.Translation[0]);
    }

    [Fact]
    public void Camera_R_transform_retains_MAC32_and_adds_camera_translation_afterward()
    {
        NativeBattleActionMatrix camera = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleBadBreath300ActMeshStep result = Draw(Node(10000), Context(0), camera);
        Assert.Equal(129998, result.Registers.Geometry.Translation[0]);
        Assert.Equal(-3, result.Registers.Geometry.Translation[1]);
        Assert.Equal((byte?)255, result.Registers.Geometry.Rotation20[0]);
        Assert.Equal((byte?)127, result.Registers.Geometry.Rotation20[1]);
    }

    [Fact]
    public void Header_bits_do_not_select_alternate_Euler_position_scaling_or_UV_flags()
    {
        NativeBattleGfAnimationNode node = Node(100);
        NativeBattleBadBreath300ActMeshStep expected = Draw(node, Context(0));
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xC7300);
        NativeBattleBadBreath300ActMeshStep actual = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    [Fact]
    public void Fade_uses_fixed_20F0_and_native_node_color_without_reading_header_UV_options()
    {
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0xC08040);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 2048);
        NativeBattleBadBreath300ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0));
        Assert.Equal(0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(0xC08040u, U32(result.Temporary88, 8));
        Assert.Equal(2048u, U32(result.Temporary88, 12));
        Assert.Equal(0x400u, result.FarControls.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(0x10, 4));
    }

    [Fact]
    public void Interpolated_shared_scratch_preserves_W_and_the_adjacent_packet_boundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(NativeBattleBadBreath300Profile.MorphOffset, NativeBattleBadBreath300Profile.MorphBytes).Write(NativeBattleBadBreath300Profile.MorphOffset + NativeBattleBadBreath300Profile.MorphBytes, [1, 2, 3, 4]);
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleBadBreath300ActMeshStep result = Draw(node with { Data = bytes.ToImmutableArray() }, Context(0), arena: arena, count: 165);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(NativeBattleBadBreath300Profile.MorphOffset + 164 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(NativeBattleBadBreath300Profile.MorphOffset + 164 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Arena.Read(NativeBattleBadBreath300Profile.MorphOffset + NativeBattleBadBreath300Profile.MorphBytes, 4));
        Assert.Throws<InvalidDataException>(() => Draw(node, Context(0), count: 166));
    }

    [Fact]
    public void Physical_wrapper_publishes_GTE_and_88_bytes_before_delivery_and_restores_next_draw()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleBadBreath300Frame frame = new(memory, new(300, 299, new(300, NativeBattleBadBreath300Profile.Entry, NativeBattleBadBreath300Profile.Initializer), null, null), new(1), NativeBattleBadBreath300State.FromColdProcess())
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown())
        };
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        int calls = 0;
        NativeBattleBadBreath300Frame Deliver(NativeBattleBadBreath300Frame current, NativeBattleGfGeometryBatch batch)
        {
            calls++;
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.Equal(0x1042304u + 16, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
            Assert.Equal(2000, current.Memory.GeometryRegisters!.Translation[2]);
            Assert.Equal(52, batch.AllocationBytes);
            return current;
        }
        NativeBattleBadBreath300Frame result = NativeBattleBadBreath300Drawing.EmitNode(frame, Source(4), Context(0), Node(0), camera, new(null, 100, 160, 108), Constants, Deliver);
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleBadBreath300Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBadBreath300Frame>(NativeBattleTestSnapshots.Json(result))!;
        NativeBattleBadBreath300Frame Next(NativeBattleBadBreath300Frame current) => NativeBattleBadBreath300Drawing.EmitNode(current, Source(4), Context(0), Node(0), camera, new(null, 100, 160, 108), Constants, (next, _) => next);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(result)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleBadBreath300ActMeshStep Draw(NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleActionMatrix? camera = null, NativeBattleEffectArena? arena = null, int count = 4, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleCameraProjection? projection = null)
    {
        return NativeBattleBadBreath300ActMesh.Draw(Source(count), node, context, NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), arena ?? NativeBattleEffectArena.Clear(), new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(1, 2, 3, 4, 5), projection ?? new(null, 100, 160, 108), 0, 0, 0x155, 0x100, Constants, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleEffectArena Context(int gate)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 28).Write(0, new byte[6]);
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(8 + axis * 4, BitConverter.GetBytes(4096));
        }
        return context.Write(0x14, BitConverter.GetBytes(gate)).Write(0x18, BitConverter.GetBytes(NativeBattleBadBreath300Profile.ArenaAddress + NativeBattleBadBreath300Profile.MorphOffset));
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
        return new(0x1042304, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
