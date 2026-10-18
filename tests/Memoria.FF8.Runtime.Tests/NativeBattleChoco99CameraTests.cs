using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99CameraTests
{
    [Fact]
    public void Mode1_initialization_uses_distinct_eye_cache_and_immediately_publishes_two_offsets()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 0xEC).ToArray();
        Set(bytes, 0xE8, 0);
        byte[] source = new byte[0x48];
        Set(source, 0x24, 1);
        Set(source, 0x14, -3);
        Set(source, 0x1C, -4);
        Set(source, 0x26, 100);
        Set(source, 0x28, 100);
        Set(source, 0x3A, 2048);
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(new(bytes.ToImmutableArray(), true), new(0x152AD20, source.ToImmutableArray()), [10, 0, 20, 0, 30, 0, 0x34, 0x12], 0);
        Assert.Equal(1, Word(step.State, 0xE2));
        Assert.Equal(2048, Word(step.State, 0xD0));
        Assert.Equal(7, Word(step.State, 0x74));
        Assert.Equal(6, Word(step.State, 0x7C));
        Assert.Equal(7, Word(step.State, 0x54));
        Assert.Equal(0x1234, Word(step.State, 0x7A));
        Assert.Equal(0x1234, Word(step.State, 0x82));
        Assert.Equal(bytes.AsSpan(0x64, 16).ToArray(), step.State.Data.AsSpan().Slice(0x64, 16).ToArray());
        Assert.Equal((short)130, step.Pose.Eye.Z);
        Assert.Equal((short)230, step.Pose.Target.Z);
        Assert.Equal(30, Word(step.State, 0x58));
        Assert.Equal(unchecked((short)0xA5A5), Word(step.State, 0x5A));
        Assert.True(step.State.EyePaddingKnown);
    }

    [Theory]
    [InlineData(1, 32760, 100, -32676, -32576)]
    [InlineData(0, 32760, 100, -32676, 32760)]
    public void No_advance_publication_wraps_each_WORD_and_preserves_progress(short mode, short origin, short radius, short expectedEye, short expectedTarget)
    {
        NativeBattleMoogleCameraState state = State((0xCE, mode), (0x54, origin), (0x58, origin), (0x5C, origin), (0x60, origin), (0xDE, radius), (0xE2, 123), (0xD0, 2048), (0x5A, 678));
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.RecomputePublication(state);
        Assert.Equal(expectedEye, step.Pose.Eye.Z);
        Assert.Equal(expectedTarget, step.Pose.Target.Z);
        Assert.Equal(123, Word(step.State, 0xE2));
        Assert.Equal(2048, Word(step.State, 0xD0));
        Assert.Equal(mode == 1 ? origin : expectedEye, Word(step.State, 0x58));
        Assert.Equal(mode == 1 ? 678 : 0, Word(step.State, 0x5A));
        Assert.False(step.Complete);
        NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleMoogleCamera.RecomputePublication(restored)));
    }

    [Fact]
    public void Manual_phase6_updates_physics_without_Advance_then_initializes_after_its_publication()
    {
        NativeBattleMoogleCameraState camera = State((0x86, 120), (0x8C, 31), (0x5E, 32760), (0xD8, 4080), (0xE2, 55));
        NativeBattleChoco99CameraTaskStep step = Advance(Task(6), camera, 10);
        Assert.Equal(2, step.Publications.Length);
        Assert.Equal(55, Word(step.Publications[0].State, 0xE2));
        Assert.Equal(128, Word(step.Publications[0].State, 0x86));
        Assert.Equal(32, Word(step.Publications[0].State, 0x8C));
        Assert.Equal(unchecked((short)(32760 + 128)), Word(step.Publications[0].State, 0x5E));
        Assert.Equal(0, Word(step.Publications[0].State, 0xD8));
        Assert.Equal(0x152AE40u, BinaryPrimitives.ReadUInt32LittleEndian(step.Publications[1].State.Data.AsSpan().Slice(0xAC)));
        Assert.Equal((byte)7, step.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(64, 6, 6)]
    [InlineData(65, 6, 5)]
    [InlineData(65, 0, 0)]
    public void Phase8_advances_once_then_republishes_with_signed_threshold_and_actual_child_hold(short counter, short speed, short expected)
    {
        NativeBattleMoogleCameraState camera = State((0xE2, counter), (0x8E, speed), (0xB6, 2), (0xDA, 4094));
        NativeBattleModulePoolTask task = Task(8).WriteByte(0x28, 1).WriteWord(0x24, 65535);
        NativeBattleChoco99CameraTaskStep step = Advance(task, camera, 11);
        Assert.Equal(2, step.Publications.Length);
        Assert.Equal(counter + 1, Word(step.Publications[0].State, 0xE2));
        Assert.Equal(counter + 1, Word(step.Publications[1].State, 0xE2));
        Assert.Equal(speed, Word(step.Publications[0].State, 0x8E));
        Assert.Equal(expected, Word(step.Camera, 0x8E));
        Assert.Equal((4094 + expected) & 4095, Word(step.Camera, 0xDA));
        Assert.Equal((byte)9, step.Task.ReadByte(0x29));
        Assert.Equal((ushort)0, step.Task.ReadWord(0x24));
        Assert.False(step.ReadyToComplete);
        NativeBattleChoco99CameraTaskStep released = Advance(step.Task.WriteByte(0x28, 0), step.Camera, 11);
        Assert.True(released.ReadyToComplete);
        Assert.Empty(released.Publications);
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(new Saved(task, camera)))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored.Task, restored.Camera, 11)));
    }

    [Fact]
    public void Waiting_phase2_does_not_advance_and_invalid_modes_or_truncated_tasks_fail()
    {
        NativeBattleMoogleCameraState camera = State((0xE2, 10));
        NativeBattleChoco99CameraTaskStep step = Advance(Task(2), camera, -1);
        Assert.Empty(step.Publications);
        Assert.Same(camera, step.Camera);
        Assert.Equal((byte)2, step.Task.ReadByte(0x29));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogleCamera.RecomputePublication(State((0xCE, 2))));
        Assert.Throws<InvalidDataException>(() => Advance(new(1, 0, null, NativeBattleChoco99CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray()), camera, 0));
    }

    private static NativeBattleChoco99CameraTaskStep Advance(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, short mode) => NativeBattleChoco99CameraTask.Advance71DC40(task, camera, Resources(), mode, new byte[8].ToImmutableArray(), new byte[8].ToImmutableArray());
    private static NativeBattleModulePoolTask Task(byte phase) => new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco99CameraTask.Callback, new byte[0xB0 - 12].ToImmutableArray()).WriteByte(0x29, phase);
    private static NativeBattleChoco99CameraResources Resources()
    {
        uint[] addresses = [0x152AC48, 0x152AC90, 0x152ACD8, 0x152AD20, 0x152AD68, 0x152AE40, 0x152AE88];
        return new(addresses.Select((address, index) =>
        {
            byte[] data = new byte[0x48];
            Set(data, 0x24, index == 3 ? 1 : 0);
            Set(data, 0x3A, 4096);
            return new NativeBattleMoogleCameraSource(address, data.ToImmutableArray());
        }).ToImmutableArray());
    }
    private static NativeBattleMoogleCameraState State(params (int Offset, int Value)[] values)
    {
        byte[] data = new byte[0xEC];
        foreach ((int offset, int value) in values)
        {
            Set(data, offset, value);
        }
        return new(data.ToImmutableArray(), true);
    }
    private static short Word(NativeBattleMoogleCameraState state, int offset) => BinaryPrimitives.ReadInt16LittleEndian(state.Data.AsSpan().Slice(offset));
    private static void Set(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), unchecked((ushort)value));
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera);
}
