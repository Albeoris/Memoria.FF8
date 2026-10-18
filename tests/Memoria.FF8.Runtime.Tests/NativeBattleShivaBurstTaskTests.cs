using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaBurstTaskTests
{
    [Fact]
    public void Positive_delay_obeys_pause_and_never_reads_draw_owners_or_advances_other_fields()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(1);
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 0x200, null!, null!, null!);
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(paused.Frame));
        Assert.Equal(0, paused.Result);
        NativeBattleShivaEarlyTaskStep delayed = NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 0, null!, null!, null!);
        Assert.Equal((ushort)0, delayed.Frame.Pool.Slots[0].ReadWord(0x0E));
        Assert.Equal((ushort)0, delayed.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal((ushort)0xFFFF, delayed.Frame.Pool.Slots[0].ReadWord(0x1A));
        Assert.Equal(frame.Random, delayed.Frame.Random);
    }

    [Fact]
    public void Active_draw_precedes_pause_and_publishes_signed_scaled_translated_world_view_and_fade()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(-1);
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, frame.Pool.Slots[0].WriteWord(0x0C, 9).WriteWord(0x18, 0).WriteWord(0x1C, 2048).WriteWord(0x20, 4096)) } };
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = -200, Z = 4096 };
        int draws = 0;
        NativeBattleShivaEarlyTaskStep step = NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 1, view, Mesh, (current, draw) =>
        {
            draws++;
            Assert.Equal(frame.Pool.Slots[0].Payload.ToArray(), current.Pool.Slots[0].Payload.ToArray());
            Assert.Equal(new short[] { 2048, 0, 0, 0, 4096, 0, 0, 0, 2048 }, draw.Matrix.Rotation.ToArray());
            Assert.Equal(107, draw.Matrix.X);
            Assert.Equal(-209, draw.Matrix.Y);
            Assert.Equal(4107, draw.Matrix.Z);
            Assert.Equal(0xF3u, draw.Context.Flags);
            Assert.Equal(682, draw.Context.Fade);
            Assert.Equal(0u, draw.Context.FarColor);
            Assert.Null(draw.Context.TexturePage);
            Assert.Null(draw.Context.Clut);
            Assert.Null(draw.Context.UvOffset);
            Assert.Equal(17, draw.Context.TableBaseBucket);
            Assert.Equal((byte)2, draw.Context.DepthShift);
            return current with { Memory = current.Memory with { Arena = current.Memory.Arena!.Write(99, new byte[] { 42 }) } };
        });
        Assert.Equal(1, draws);
        Assert.Equal(0, step.Result);
        Assert.Equal(frame.Pool.Slots[0].Payload.ToArray(), step.Frame.Pool.Slots[0].Payload.ToArray());
        Assert.Equal(new byte[] { 42 }, step.Frame.Memory.Arena!.Read(99, 1));
    }

    [Theory]
    [InlineData(0, -1, -1, 3, 1, 0, -1)]
    [InlineData(13, -9, -7, 3, 14, -7, -5)]
    public void Post_draw_updates_use_negative_arithmetic_shift_signed_division_and_signed_completion(short counter, short speed, short velocity, short divisor, short expectedCounter, short expectedSpeed, short expectedVelocity)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0);
        NativeBattleModulePoolTask task = frame.Pool.Slots[0].WriteWord(0x0C, unchecked((ushort)counter)).WriteWord(0x18, 3).WriteWord(0x1A, unchecked((ushort)speed)).WriteWord(0x1C, 100).WriteWord(0x1E, unchecked((ushort)velocity)).WriteWord(0x22, unchecked((ushort)divisor));
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, task) } };
        NativeBattleShivaEarlyTaskStep step = NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 0, NativeBattleCuragaMatrices.Identity8DD770(), Mesh, (current, draw) =>
        {
            Assert.Equal(counter < 8 ? 0x33u : 0xF3u, draw.Context.Flags);
            return current;
        });
        NativeBattleModulePoolTask changed = step.Frame.Pool.Slots[0];
        Assert.Equal(unchecked((ushort)(3 + speed)), changed.ReadWord(0x18));
        Assert.Equal(unchecked((ushort)expectedSpeed), changed.ReadWord(0x1A));
        Assert.Equal(unchecked((ushort)(100 + velocity)), changed.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)expectedVelocity), changed.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)expectedCounter), changed.ReadWord(0x0C));
        Assert.Equal(expectedCounter >= 14 ? 2 : 0, step.Result);
        Assert.Equal((ushort)0xA5A5, changed.ReadWord(0x16));
        NativeBattleShivaEarlyTaskFrame restored = JsonSerializer.Deserialize<NativeBattleShivaEarlyTaskFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleShivaEarlyTaskStep independent = NativeBattleShivaBurstTask.Advance5C4900(restored, 0, 0, NativeBattleCuragaMatrices.Identity8DD770(), Mesh, (current, _) => current);
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(independent));
    }

    [Fact]
    public void Zero_decay_divisor_is_consumed_only_after_an_unpaused_actual_draw()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0);
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, frame.Pool.Slots[0].WriteWord(0x22, 0)) } };
        int draws = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 0, NativeBattleCuragaMatrices.Identity8DD770(), Mesh, (current, _) =>
        {
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaBurstTask.Advance5C4900(frame, 0, 0x200, NativeBattleCuragaMatrices.Identity8DD770(), Mesh, (current, _) => current);
        Assert.Equal(0, paused.Result);
        Assert.Equal((ushort)0, paused.Frame.Pool.Slots[0].ReadWord(0x22));
    }

    private static NativeBattleStaticMeshResource Mesh => new(0, 0xD3138C, [0]);

    private static NativeBattleShivaEarlyTaskFrame Fixture(short delay)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(0x5C4900);
        NativeBattleModulePoolTask task = pool.Slots[slot] with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() };
        task = task.WriteWord(0x0C, 0).WriteWord(0x0E, unchecked((ushort)delay)).WriteWord(0x10, 7).WriteWord(0x12, unchecked((ushort)-9)).WriteWord(0x14, 11).WriteWord(0x18, 0).WriteWord(0x1A, ushort.MaxValue).WriteWord(0x1C, 4096).WriteWord(0x1E, ushort.MaxValue).WriteWord(0x20, 4096).WriteWord(0x22, 3);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }
}
