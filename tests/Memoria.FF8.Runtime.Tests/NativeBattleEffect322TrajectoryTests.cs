using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect322TrajectoryTests
{
    [Fact]
    public void Nonzero_tangents_produce_current_weighted_point_and_preserve_output_W_and_tail()
    {
        NativeBattleEffect322Frame frame = Frame();
        NativeBattleEffectArena controls = Controls(new(0, 0, 0), new(100, 200, 300), new(200, 400, 600), new(300, 600, 900));
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(6, new byte[] { 0xEF, 0xBE, 0x91, 0x92 });
        NativeBattleEffect322PointStep result = Sample(frame, controls, 0, 2048, retained);
        Assert.Equal(new short[] { 150, 300, 450 }, XYZ(result.Output8));
        Assert.Equal(new byte[] { 0xEF, 0xBE, 0x91, 0x92 }, result.Output8.Read(6, 4));
        Assert.Equal(2048u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 150, 300, 450 }, result.Frame.Memory.ScriptedColorRegisters.Ir);
        Assert.Same(frame.Memory.GeometryRegisters, result.Frame.Memory.GeometryRegisters);
        Assert.Same(frame.State, result.Frame.State);
        Assert.Same(frame.Effect, result.Frame.Effect);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Repeated_endpoints_skip_intermediate_blends_and_keep_actual_RGB_FIFO_history()
    {
        NativeBattleEffect322Frame frame = Frame();
        NativeBattleEffectArena controls = Controls(new(100, 200, 300), new(100, 200, 300), new(300, 400, 500), new(300, 400, 500));
        NativeBattleEffect322PointStep result = Sample(frame, controls, 0, 2048, NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
        Assert.Equal(new short[] { 200, 300, 400 }, XYZ(result.Output8));
        Assert.Equal(0xAA030303u, result.Frame.Memory.ScriptedColorRegisters!.Rgb[0]);
        Assert.Throws<NotSupportedException>(() => result.Output8.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => controls.Read(6, 2));
    }

    [Fact]
    public void Signed_square_sum_overflow_uses_native_indefinite_zero_length()
    {
        NativeBattleEffectArena controls = Controls(new(0, 0, 0), new(short.MaxValue, short.MaxValue, short.MaxValue), new(0, 0, 0), new(0, 0, 0));
        NativeBattleEffect322PointStep result = Sample(Frame(), controls, 0, 2048, NativeBattleEffectArena.Clear());
        Assert.Equal(new short[] { 16384, 16384, 16384 }, XYZ(result.Output8));
        Assert.Equal(0xAA030303u, result.Frame.Memory.ScriptedColorRegisters!.Rgb[0]);
    }

    [Fact]
    public void Final_produced_segment_survives_JSON_without_consuming_unknown_control_W_or_unused_point7()
    {
        NativeBattleEffect322Frame frame = Frame();
        NativeBattleEffectArena controls = NativeBattleEffectArena.Clear().MarkUnknown(0, 64);
        for (int index = 3; index <= 6; index++)
        {
            controls = WritePoint(controls, index * 8, new((short)(index * 100), 0, (short)(index * -100)));
        }
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(6, 2);
        NativeBattleEffect322Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect322Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        NativeBattleEffectArena restoredControls = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(controls)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Sample(frame, controls, 3, 1024, retained))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Sample(restored, restoredControls, 3, 1024, retained))));
        Assert.Throws<NotSupportedException>(() => Sample(frame, controls, 4, 0, retained));
        Assert.Throws<NotSupportedException>(() => Sample(frame, controls.MarkUnknown(4 * 8, 2), 3, 0, retained));
    }

    private static NativeBattleEffect322PointStep Sample(NativeBattleEffect322Frame frame, NativeBattleEffectArena controls, int segment, int parameter, NativeBattleEffectArena retained) => NativeBattleEffect322Trajectory.Sample(frame, controls, segment, parameter, retained, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static short[] XYZ(NativeBattleEffectArena point) => Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2))).ToArray();
    private static NativeBattleEffectArena Controls(params NativeBattleVector[] points)
    {
        NativeBattleEffectArena controls = NativeBattleEffectArena.Clear().MarkUnknown(0, 64);
        for (int index = 0; index < points.Length; index++)
        {
            controls = WritePoint(controls, index * 8, points[index]);
        }
        return controls;
    }
    private static NativeBattleEffectArena WritePoint(NativeBattleEffectArena arena, int offset, NativeBattleVector point) => arena.Write(offset, BitConverter.GetBytes(point.X)).Write(offset + 2, BitConverter.GetBytes(point.Y)).Write(offset + 4, BitConverter.GetBytes(point.Z));
    private static NativeBattleEffect322Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0xAA010101u, 0xAA020202u, 0xAA030303u], Material = 0xAA000000 } };
        NativeBattleOriginalEffectState effect = new(Effect, Effect - 1, new(Effect, Entry, Initializer), new(4, 0, 1, 0, 0, 0), null);
        return new(memory, effect, new(123), NativeBattleEffect322State.FromColdProcess());
    }
}
