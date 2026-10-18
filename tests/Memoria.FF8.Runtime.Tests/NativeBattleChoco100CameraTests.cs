using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100CameraTests
{
    [Fact]
    public void Phase9_publishes_old_advance_and_new_initialize_before_exact_sound_descriptor()
    {
        NativeBattleChoco100CameraResources resources = Resources();
        NativeBattleMoogleCameraState camera = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), resources.Sources[5], Context(9).Data.AsSpan().Slice(8, 8).ToArray().ToImmutableArray(), 0).State;
        NativeBattleChoco100CameraTaskStep step = Advance(Task(9), camera, Context(9));
        Assert.Equal(3, step.Operations.Length);
        NativeBattleChoco97CameraPublish first = Assert.IsType<NativeBattleChoco97CameraPublish>(step.Operations[0]);
        NativeBattleChoco97CameraPublish second = Assert.IsType<NativeBattleChoco97CameraPublish>(step.Operations[1]);
        Assert.Equal(resources.Sources[5].Address, U32(first.Step.State, 0xAC));
        Assert.Equal(resources.Sources[6].Address, U32(second.Step.State, 0xAC));
        Assert.Equal(2, Word(first.Step.State, 0xE2));
        Assert.Equal(1, Word(second.Step.State, 0xE2));
        Assert.Equal(new NativeBattleChoco97Sound(0x1529800, 0, 128), Assert.IsType<NativeBattleChoco97Sound>(step.Operations[2]));
        Assert.Equal((byte)10, step.Task.ReadByte(0x29));
        Assert.Equal(Context(9).Data.AsSpan().Slice(8, 8).ToArray(), second.Step.State.Data.AsSpan().Slice(0xA4, 8).ToArray());
    }

    [Fact]
    public void Phase6_initializes_before_nine_tick_hold_and_complete_does_not_shorten_it()
    {
        NativeBattleMoogleCameraState camera = NativeBattleMoogleCamera.Reset732AF0();
        NativeBattleChoco100CameraTaskStep step = Advance(Task(6), camera, Context(8));
        Assert.Equal(2, step.Operations.Length);
        Assert.Equal((ushort)9, step.Task.ReadWord(0x6C));
        Assert.Equal((byte)7, step.Task.ReadByte(0x29));
        for (int index = 1; index <= 9; index++)
        {
            step = Advance(step.Task, step.Camera, step.Context);
            Assert.IsType<NativeBattleChoco97CameraPublish>(Assert.Single(step.Operations));
            Assert.Equal((ushort)(9 - index), step.Task.ReadWord(0x6C));
            Assert.Equal(index == 9 ? (byte)8 : (byte)7, step.Task.ReadByte(0x29));
        }
    }

    [Theory]
    [InlineData(0, 65535, 8)]
    [InlineData(32768, 32767, 7)]
    public void Nine_tick_timer_wraps_before_signed_comparison(ushort initial, ushort expected, byte phase)
    {
        NativeBattleChoco100CameraTaskStep step = Advance(Task(7).WriteWord(0x6C, initial), NativeBattleMoogleCamera.Reset732AF0(), Context(8));
        Assert.Equal(expected, step.Task.ReadWord(0x6C));
        Assert.Equal(phase, step.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(8, 8, true)]
    [InlineData(8, 7, false)]
    [InlineData(7, 7, false)]
    public void Phase8_always_advances_then_requests_mode9_using_actual40_44_handshake_with_JSON_next(ushort current, ushort requested, bool accepted)
    {
        NativeBattleChoco97Context context = Context(8).WriteWord(0x40, current).WriteWord(0x44, requested);
        NativeBattleModulePoolTask task = Task(8);
        NativeBattleMoogleCameraState camera = NativeBattleMoogleCamera.Reset732AF0();
        NativeBattleChoco100CameraTaskStep step = Advance(task, camera, context);
        Assert.IsType<NativeBattleChoco97CameraPublish>(Assert.Single(step.Operations));
        Assert.Equal(1, Word(step.Camera, 0xE2));
        Assert.Equal(accepted ? (ushort)9 : requested, step.Context.Word(0x44));
        Assert.Equal(accepted ? (byte)9 : (byte)8, step.Task.ReadByte(0x29));
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(new Saved(task, camera, context)))!;
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(Advance(restored.Task, restored.Camera, restored.Context)));
    }

    [Fact]
    public void Waiting_phases_do_not_advance_terminal_has_no_publication_and_camera_gates_use42_not40()
    {
        NativeBattleMoogleCameraState camera = NativeBattleMoogleCamera.Reset732AF0();
        foreach (byte phase in new byte[] { 2, 5 })
        {
            NativeBattleChoco100CameraTaskStep held = Advance(Task(phase), camera, Context(1).WriteWord(0x40, 100));
            Assert.Empty(held.Operations);
            Assert.Same(camera, held.Camera);
            Assert.Equal(phase, held.Task.ReadByte(0x29));
        }
        NativeBattleChoco100CameraTaskStep terminal = Advance(Task(12).WriteWord(0x24, 65535).WriteByte(0x28, 1), camera, Context(10));
        Assert.Empty(terminal.Operations);
        Assert.Same(camera, terminal.Camera);
        Assert.Equal((ushort)0, terminal.Task.ReadWord(0x24));
        Assert.Equal((byte)13, terminal.Task.ReadByte(0x29));
        Assert.False(terminal.ReadyToComplete);
        Assert.True(Advance(terminal.Task.WriteByte(0x28, 0), terminal.Camera, terminal.Context).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => Advance(new(1, 0, null, NativeBattleChoco100CameraTask.Callback, new byte[0xB0 - 12].ToImmutableArray()), camera, Context(0)));
        Assert.Throws<NotSupportedException>(() => Advance(Task(14), camera, Context(0)));
    }

    private static NativeBattleChoco100CameraTaskStep Advance(NativeBattleModulePoolTask task, NativeBattleMoogleCameraState? camera, NativeBattleChoco97Context context) => NativeBattleChoco100CameraTask.Advance7148C0(task, camera, Resources(), context);
    private static NativeBattleModulePoolTask Task(byte phase) => new NativeBattleModulePoolTask(1, 0, null, NativeBattleChoco100CameraTask.Callback, new byte[0x70 - 12].ToImmutableArray()).WriteByte(0x29, phase);
    private static NativeBattleChoco97Context Context(ushort mode)
    {
        return NativeBattleChoco97Context.Clear72A350().WriteWord(0, 10).WriteWord(2, 20).WriteWord(4, 30).WriteWord(6, 40).WriteWord(8, 50).WriteWord(10, 60).WriteWord(12, 70).WriteWord(14, 80).WriteWord(0x40, mode).WriteWord(0x42, mode).WriteWord(0x44, mode);
    }
    private static NativeBattleChoco100CameraResources Resources() => new(Enumerable.Range(0, 8).Select(index =>
    {
        byte[] data = new byte[0x48];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x3A), 4096);
        return new NativeBattleMoogleCameraSource(0x1529CD8u + (uint)(index * 0x48), data.ToImmutableArray());
    }).ToImmutableArray());
    private static short Word(NativeBattleMoogleCameraState state, int offset) => BinaryPrimitives.ReadInt16LittleEndian(state.Data.AsSpan().Slice(offset));
    private static uint U32(NativeBattleMoogleCameraState state, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(state.Data.AsSpan().Slice(offset));
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleMoogleCameraState Camera, NativeBattleChoco97Context Context);
}
