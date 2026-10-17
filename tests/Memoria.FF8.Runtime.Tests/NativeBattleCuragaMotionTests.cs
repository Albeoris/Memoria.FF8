using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaMotionTests
{
    private const int Emitter = 0xC000;
    private const int Node = Emitter + 0x25F8;

    [Fact]
    public void Integrated_motion_updates_scalar_velocity_acceleration_gravity_and_Q16_displacement_in_native_order()
    {
        var fixture = Fixture(0, 0, 0, 0);
        NativeBattleCuragaFrame frame = fixture.Frame;
        //The six-byte angle vector is packed as three WORDs; the fourth WORD is retained padding.
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0x194, [0xFA, 0x0F, 1, 0, 0xFF, 0x0F, 0, 0]) } };
        frame = Channels(frame, [10, -2, 1, 0, 0, 0]);
        frame = Write(frame, Node + 0x174, [65536]);
        frame = Write(frame, Node + 0x184, [512]);
        frame = Write(frame, Node + 0x12C, [1000, 2000, 3000, 0xAA]);
        frame = Write(frame, Node + 0xDC, [9, 10, 11, 0xDD]);
        frame = Write(frame, Node + 0x220, [10, 20, 30]);
        frame = Write(frame, Node + 0x260, [1, 2, 3]);
        frame = Write(frame, Node + 0x1E0, [100, 200, 300]);
        frame = Write(frame, Node + 0x1DC, [7]);
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceMotion8805A0(frame, fixture.Task, Node);
        Assert.Equal(new uint[] { 1111, unchecked((uint)-63819), 3333, 0xAA }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0xDC, 4));
        Assert.Equal(new uint[] { 9, 10, 11, 0xDD }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0x11C, 4));
        Assert.Equal(new uint[] { 11, 22, 33 }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0x220, 3));
        Assert.Equal(new uint[] { 111, 229, 333 }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0x1E0, 3));
        Assert.Equal(new uint[] { 66048 }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0x174, 1));
        Assert.Equal(new byte[] { 4, 0, 255, 15, 0, 0 }, result.Memory.Arena!.Read(Node + 0xCC, 6));
    }

    [Theory]
    [InlineData(false, 18)]
    [InlineData(true, 0)]
    public void Curve_positions_are_shifted_to_Q16_then_rotated_and_added_to_current_emitter_anchor_before_optional_ground_projection(bool ground, int expectedY)
    {
        var fixture = Fixture(1, 1, 1, 0);
        NativeBattleCuragaFrame frame = Channels(fixture.Frame, [0, 0, 0, 1, -2, 3]);
        frame = NativeBattleCuragaEmitterTests.Source(frame, source => source[0x1B] = ground ? (byte)1 : (byte)0);
        frame = Write(frame, Emitter + 0x4C, [10u << 16, 20u << 16, 30u << 16, 0xABCD]);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(Node + 0x174, 4).MarkUnknown(Node + 0x220, 12) } };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceMotion8805A0(frame, fixture.Task, Node);
        Assert.Equal(new uint[] { 11u << 16, unchecked((uint)(expectedY << 16)), 33u << 16, 0xABCD }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0xDC, 4));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(Node + 0x174, 4));
    }

    [Fact]
    public void Rotated_temporary_vector_copies_unknown_fourth_DWORD_without_requiring_its_value()
    {
        var fixture = Fixture(1, 0, 1, 0);
        NativeBattleCuragaFrame frame = Channels(fixture.Frame, [0, 0, 0, 1, 0, 0]);
        frame = Matrix(frame, Emitter + 0x2C, NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024));
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceMotion8805A0(frame, fixture.Task, Node);
        Assert.Equal(new uint[] { 0, 0, unchecked((uint)-65536) }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0xDC, 3));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(Node + 0xDC + 12, 4));
    }

    [Fact]
    public void Camera_order_uses_actual_copied_camera_transpose_and_unknown_camera_is_required_only_on_that_path()
    {
        var fixture = Fixture(1, 0, 1, 2);
        NativeBattleCuragaFrame frame = Channels(fixture.Frame, [0, 0, 0, 1, 0, 0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodeKinematics.AdvanceMotion8805A0(frame, fixture.Task, Node));
        frame = frame with { State = frame.State with { CopiedCamera = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) } };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceMotion8805A0(frame, fixture.Task, Node);
        Assert.Equal(new uint[] { 0, 0, 65536 }, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0xDC, 3));
    }

    [Theory]
    [InlineData(32768, 257, 129)]
    [InlineData(32768, -257, -129)]
    [InlineData(65536, 257, 1)]
    [InlineData(65536, -257, -1)]
    [InlineData(-1, 257, 257)]
    [InlineData(65537, 257, 1)]
    [InlineData(65536, 2147483647, -2147483393)]
    public void Damping_retains_two_signed_divisions_and_the_intermediate_DWORD_overflow(int ratio, int value, int expected)
    {
        Assert.Equal(expected, NativeBattleCuragaNodeKinematics.Damp880C00(ratio, value));
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture(byte mode, byte position, byte rotate, byte order)
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(0, 0, false)]);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x27] = mode;
            source[0x22] = position;
            source[0x26] = rotate;
            source[0x1D] = order;
        });
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 0).Frame;
        frame = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(frame, fixture.Task, Emitter, _ => throw new InvalidOperationException());
        frame = NativeBattleCuragaNodes.AllocateGeometry882080(frame, fixture.Task, Emitter, 0).Frame;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0x1D8, [1]) } };
        frame = Matrix(frame, Node + 0xC, NativeBattleCuragaMatrices.Identity8DD770());
        return (Channels(frame, [0, 0, 0, 0, 0, 0]), fixture.Task);
    }

    private static NativeBattleCuragaFrame Matrix(NativeBattleCuragaFrame frame, int offset, NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index]);
        }
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, bytes) } };
    }
    private static NativeBattleCuragaFrame Write(NativeBattleCuragaFrame frame, int offset, uint[] values) => NativeBattleCuragaEmitterTests.Dwords(frame, offset, values);
    private static NativeBattleCuragaFrame Channels(NativeBattleCuragaFrame frame, short[] values)
    {
        int[] fields = [0xD8, 0xDC, 0xE0, 0x13C, 0x140, 0x144];
        byte[] bytes = frame.Memory.CuragaExecutable!.Data!.Value.ToArray();
        for (int index = 0; index < fields.Length; index++)
        {
            int offset = 0x400 + index * 16;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x104 + fields[index]), NativeBattleCuragaExecutableState.Address + (uint)offset);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), values[index]);
        }
        return frame with { Memory = frame.Memory with { CuragaExecutable = new(1, bytes.ToImmutableArray()) } };
    }
}
