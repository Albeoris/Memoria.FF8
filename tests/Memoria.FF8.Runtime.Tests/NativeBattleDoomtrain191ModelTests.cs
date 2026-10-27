using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191ModelTests
{
    [Fact]
    public void Sparse_model_initializer_preserves_unknown_bytes_and_uses_current_source_pointers()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(false);
        frame = NativeBattleDoomtrain191Model.Initialize642400(frame, 15);
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        Assert.Equal((ushort)2, U16(memory.Model, 0));
        Assert.Equal(0x808080u, U32(memory.Model, 0x28));
        Assert.Equal(NativeBattleDoomtrain191ModelMemory.DescriptorAddress + 12, U32(memory.Model, 0x64));
        Assert.Equal((ushort)15, U16(memory.Descriptor, 2));
        Assert.Equal(Base + 24, U32(memory.Descriptor, 12));
        Assert.Equal(Base + Geometry, U32(memory.Descriptor, 16));
        Assert.Equal(Base + Animations, U32(memory.Descriptor, 20));
        Assert.Equal(Base + SourceLength, U32(memory.Descriptor, 0x30));
        Assert.Throws<NotSupportedException>(() => memory.Model.Read(0x6D, 1));
        Assert.Throws<NotSupportedException>(() => memory.Model.Read(0x52, 2));
        Assert.Throws<NotSupportedException>(() => memory.Descriptor.Read(0, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoomtrain191Model.Select642470(frame, 0, SourceLength));
    }

    [Fact]
    public void Minimal_known_sequence_flag_allows_real_initial_pose_and_writes_only_actual_skeleton_fields()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(false);
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = memory.Model.Write(0x6D, new byte[1]) });
        frame = NativeBattleDoomtrain191Model.Initialize642400(frame, 15);
        frame = NativeBattleDoomtrain191Model.Select642470(frame, 0, SourceLength);
        Assert.Equal(new byte[] { 0, 4, 3, 0, 6, 0, 1, 2 }, NativeBattleDoomtrain191Model.Require(frame).Model.Read(0x6C, 8));
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(frame.Memory.Arena!, 24, 2);
        Assert.Equal(new NativeBattleVector(0, 0, 0), pose.Translation);
        Assert.Equal(2, pose.Bones.Length);
        Assert.Equal(new byte[] { 0, 0 }, frame.Memory.Arena!.Read(24 + 32 + 48 + 18, 2));
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(NativeBattleDoomtrain191Model.Select642470(JsonSerializer.Deserialize<NativeBattleDoomtrain191Frame>(JsonSerializer.Serialize(frame))!, 0, SourceLength)));
    }

    [Fact]
    public void World_identity_publisher_preserves_unknown_translation_and_matrix_padding()
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Model.InitializeWorld642E10(Fixture(false));
        NativeBattleEffectArena model = NativeBattleDoomtrain191Model.Require(frame).Model;
        Assert.Equal((ushort)4096, U16(model, 0x40));
        Assert.Equal((ushort)4096, U16(model, 0x48));
        Assert.Equal((ushort)4096, U16(model, 0x50));
        Assert.Throws<NotSupportedException>(() => model.Read(0x52, 2));
        Assert.Throws<NotSupportedException>(() => model.Read(0x54, 12));
    }

    [Fact]
    public void Pose_completion_checks_counter_before_animation_reads_and_only_then_loops_if_model_flag_allows()
    {
        NativeBattleDoomtrain191Frame frame = Ready();
        frame = NativeBattleDoomtrain191Model.Advance642CA0(frame, SourceLength);
        Assert.Equal((byte)2, NativeBattleDoomtrain191Model.Require(frame).Model.Read(0x72, 1)[0]);
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = memory.Model.Write(0, new byte[] { 3 }) });
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(Animations, SourceLength - Animations) } };
        Assert.Same(frame, NativeBattleDoomtrain191Model.Advance642CA0(frame, SourceLength));
    }

    [Theory]
    [InlineData(-40000, -32000)]
    [InlineData(40000, 32000)]
    public void Bone_composition_clamps_world_Z_before_view_and_uses_actual_custom_mask(int z, int clamped)
    {
        NativeBattleDoomtrain191Frame frame = Ready();
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = NativeBattleArenaPose.WriteMatrix(memory.Model, 0x40, Identity with { Z = z }, false) });
        var result = NativeBattleDoomtrain191Model.Compose642D80(frame, Identity with { Z = 50 }, [0x40u, 0x200u]);
        Assert.Equal(0x240u, result.Clipped);
        Assert.Equal(clamped + 50, NativeBattleArenaPose.ReadMatrix(result.Frame.Memory.Arena!, 24 + 32).Z);
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(24 + 32 + 18, 2));
        result = (NativeBattleDoomtrain191Model.Restore642830(result.Frame), result.Clipped);
        Assert.Equal(0, NativeBattleArenaPose.ReadMatrix(result.Frame.Memory.Arena!, 24 + 32).Z);
    }

    [Fact]
    public void Unclipped_bones_do_not_read_the_custom_mask_table()
    {
        var result = NativeBattleDoomtrain191Model.Compose642D80(Ready(), Identity, default);
        Assert.Equal(0u, result.Clipped);
    }

    [Fact]
    public void Raw_body_draw_handles_all_objects_reuses_actual_scratch_and_preserves_unwritten_tail()
    {
        NativeBattleDoomtrain191Frame frame = Ready();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x37000, Enumerable.Repeat((byte)0xA5, 32).ToArray()).MarkUnknown(0x37018, 8) } };
        var composed = NativeBattleDoomtrain191Model.Compose642D80(frame, Identity, default);
        var draw = NativeBattleDoomtrain191Model.Render5099D0(composed.Frame, new(Identity, 256, 160, 104), composed.Clipped, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(2, draw.Packets.Attempts.Length);
        Assert.Equal(24, draw.ProjectedBytesWritten);
        Assert.Equal(Base + Geometry + 116u, draw.SourceCursor);
        Assert.All(draw.Frame.Memory.Arena!.ReadSnapshot(0x37000, 24).Known, Assert.True);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), draw.Frame.Memory.Arena.ReadSnapshot(0x37018, 8).Data);
        Assert.All(draw.Frame.Memory.Arena.ReadSnapshot(0x37018, 8).Known, Assert.False);
        Assert.Equal(0x808080u, draw.Color);
    }

    [Fact]
    public void Anchor_is_world_times_current_local_bone_one_after_view_matrices_are_restored()
    {
        NativeBattleDoomtrain191Frame frame = Ready();
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Model = NativeBattleArenaPose.WriteMatrix(memory.Model, 0x40, Identity with { X = 123, Z = 456 }, false) });
        var composed = NativeBattleDoomtrain191Model.Compose642D80(frame, Identity with { X = 10000 }, default);
        frame = NativeBattleDoomtrain191Model.PublishAnchor(NativeBattleDoomtrain191Model.Restore642830(composed.Frame));
        NativeBattleActionMatrix local = NativeBattleArenaPose.ReadMatrix(frame.Memory.Arena!, 24 + 0x50);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Compose56C2F0(Identity with { X = 123, Z = 456 }, local);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(NativeBattleDoomtrain191Model.Require(frame).Anchor.ReadMatrix()));
        Assert.False(NativeBattleDoomtrain191Model.Require(frame).Anchor.Known[18]);
    }

    internal const int SourceLength = 336;
    internal const int Geometry = 136;
    internal const int Animations = 252;
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    internal static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    internal static NativeBattleDoomtrain191Frame Fixture(bool cold = true)
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Tests.Initialize(NativeBattleDoomtrain191Tests.Fixture());
        byte[] bytes = new byte[SourceLength];
        int[] header = [4, 24, Geometry, Animations, SourceLength, SourceLength];
        for (int index = 0; index < header.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), header[index]);
        }
        bytes[24] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 713);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(88), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(90), 200);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Geometry), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Geometry + 4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Geometry + 8), 64);
        for (int obj = 0; obj < 2; obj++)
        {
            int at = Geometry + 12 + obj * 52;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 4), 3);
            short[] positions = [-10, -10, 1000, 10, -10, 1000, 0, 10, 1000];
            for (int index = 0; index < positions.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6 + 2 * index), positions[index]);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 24), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 36), 0x8000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 38), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 40), 2);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Animations), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Animations + 4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(Animations + 8), 48);
        bytes[Animations + 12] = 2;
        bytes[Animations + 48] = 2;
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191!;
        state = state with { Model = cold ? NativeBattleDoomtrain191ModelMemory.FromColdProcess() : NativeBattleDoomtrain191ModelMemory.Unknown(), BaseView = NativeBattleDoomtrain191Controller.Pack(Identity, state.BaseView, true) };
        return frame with { Memory = frame.Memory with { Doomtrain191 = state, Arena = frame.Memory.Arena!.Write(0, bytes) } };
    }
    internal static NativeBattleDoomtrain191Frame Ready()
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Model.Initialize642400(Fixture(), 15);
        frame = NativeBattleDoomtrain191Model.Select642470(frame, 0, SourceLength);
        NativeBattleDoomtrain191ModelMemory memory = NativeBattleDoomtrain191Model.Require(frame);
        return NativeBattleDoomtrain191Model.Put(frame, memory with { Model = NativeBattleArenaPose.WriteMatrix(memory.Model, 0x40, Identity, false), ScalePointer = Base + 26 });
    }
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
