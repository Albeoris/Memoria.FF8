using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaSecondShatterTests
{
    [Fact]
    public void Spawner_pause_and_exact_signed_counters_own_children_and_completion()
    {
        NativeBattleShivaFinalFrame frame = Frame(0, 0x5C2DE0);
        Assert.Same(frame, NativeBattleShivaSecondShatter.Advance5C2DE0(frame, 0, 1).Frame);
        NativeBattleShivaFinalStep first = NativeBattleShivaSecondShatter.Advance5C2DE0(frame, 0, 0);
        Assert.Equal(0x5C2E90u, first.Frame.Pool.Slots[1].Callback);
        Assert.Equal((ushort)1828, first.Frame.Pool.Slots[1].ReadWord(0x12));
        Assert.Equal((ushort)365, first.Frame.Pool.Slots[1].ReadWord(0x22));
        NativeBattleShivaFinalStep halo = NativeBattleShivaSecondShatter.Advance5C2DE0(Frame(14, 0x5C2DE0), 0, 0);
        Assert.Equal(0x5C4140u, halo.Frame.Pool.Slots[1].Callback);
        Assert.Equal(2, NativeBattleShivaSecondShatter.Advance5C2DE0(Frame(88, 0x5C2DE0), 0, 0).Result);
        Assert.Equal(0, NativeBattleShivaSecondShatter.Advance5C2DE0(Frame(ushort.MaxValue, 0x5C2DE0), 0, 0).Result);
    }

    [Theory]
    [InlineData(3, 240, 1024)]
    [InlineData(4, 48, -1)]
    [InlineData(8, 240, 0)]
    [InlineData(12, 240, 4096)]
    public void Halo_uses_actual_zero_left_window_and_pause_still_draws(ushort counter, uint flags, int cue)
    {
        bool drawn = false;
        NativeBattleShivaFinalStep step = NativeBattleShivaSecondShatter.Advance5C4140(Frame(counter, 0x5C4140), 0, 0x201, Inputs().Geometry, (frame, draw) =>
        {
            drawn = true;
            Assert.Equal(new NativeBattleTextureWindow(0, 0, 64, 128), draw.Context.Window);
            Assert.Equal(counter * 8, draw.Context.VOffset);
            Assert.Equal(flags, draw.Context.Flags);
            Assert.Equal(cue < 0 ? null : (int?)cue, draw.Context.Fade);
            return frame;
        });
        Assert.True(drawn);
        Assert.Equal(counter, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Static_plane_clip_does_not_apply_backface_or_zero_area_gate()
    {
        NativeBattleStaticMeshResource mesh = Mesh(0xD2BA3C, true);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1000 };
        NativeBattleGfGeometryBatch batch = NativeBattleShivaSecondShatter.EmitStatic3170(mesh, matrix, new(matrix, 216, 160, 108), new(0, -4096, 0, 0), [], 341, 256, 0, 0);
        Assert.Equal(287, batch.Attempts.Length);
        Assert.Equal(287 * 32, batch.AllocationBytes);
        Assert.All(batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
    }

    [Fact]
    public void Repeated_paused64_reinitializes287_randoms_but_retains_old_X_Z_and_word16()
    {
        NativeBattleShivaFinalFrame frame = Frame(64);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        arena = Put(arena, 0x2E008, 111);
        arena = Put(arena, 0x2E00C, -222);
        arena = Put(arena, 0x2E016, 0x3456);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaFinalStep first = NativeBattleShivaSecondShatter.Advance5C2E90(frame, 0, 1, Inputs());
        NativeBattleShivaFinalStep second = NativeBattleShivaSecondShatter.Advance5C2E90(first.Frame, 0, 1, Inputs());
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 574; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, second.Frame.Random);
        Assert.Equal((ushort)111, Word(second.Frame, 0x2E008));
        Assert.Equal(unchecked((ushort)-222), Word(second.Frame, 0x2E00C));
        Assert.Equal((ushort)0x3456, Word(second.Frame, 0x2E016));
        Assert.Equal((ushort)0, Word(second.Frame, 0x2E00E));
        Assert.Equal((ushort)64, second.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0, second.Result);
    }

    [Fact]
    public void Secondary_completion_waits_for_actual_allocation_and_shared_parameters_die_at51()
    {
        NativeBattleShivaFinalFrame frame = Frame(88);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 287; index++)
        {
            arena = Put(arena, 0x2E000 + index * 32 + 14, -1);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        Assert.Equal(2, NativeBattleShivaSecondShatter.Advance5C2E90(frame, 0, 0, Inputs()).Result);
        arena = Put(arena, 0x2E00E, 50);
        arena = Put(arena, 0x2E006, 0);
        arena = Put(arena, 0x2E010, -9);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaFinalStep step = NativeBattleShivaSecondShatter.Advance5C2E90(frame, 0, 0, Inputs());
        Assert.Single(step.CommonPackets.Attempts);
        Assert.True(step.CommonPackets.AllocationBytes > 0);
        Assert.Equal(0, step.Result);
        Assert.Equal(ushort.MaxValue, Word(step.Frame, 0x2E00E));
        Assert.Equal(unchecked((ushort)-7), Word(step.Frame, 0x2E010));
        Assert.Equal((ushort)0, step.Frame.Pool.Slots[0].ReadWord(0x12));
    }

    [Theory]
    [InlineData(51, 0)]
    [InlineData(52, 70)]
    [InlineData(61, 70)]
    [InlineData(62, 0)]
    public void Temporary_depth_offset_is_draw_only(ushort counter, int offset)
    {
        NativeBattleShivaSecondInputs inputs = Inputs();
        NativeBattleActionMatrix? drawn = null;
        inputs = inputs with { DrawCommonMesh = (frame, draw) =>
        {
            drawn = draw.Matrix;
            return frame;
        } };
        NativeBattleShivaFinalFrame frame = Frame(counter);
        NativeBattleShivaSecondShatter.Advance5C2E90(frame, 0, 1, inputs);
        Assert.Equal(10000 + offset, drawn!.Z);
        Assert.Equal((ushort)0, frame.Pool.Slots[0].ReadWord(0x14));
    }

    private static NativeBattleShivaSecondInputs Inputs()
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 10000 };
        NativeBattleShivaFinalInputs geometry = new(matrix, Mesh(0xD2BA3C), Mesh(0xD2D600), new(matrix, 216, 160, 108), 341, NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        return new(geometry, [], 256, 0, 0, (frame, _) => frame);
    }

    private static NativeBattleShivaFinalFrame Frame(ushort counter, uint callback = 0x5C2E90)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(callback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter).WriteWord(0x1C, 4096).WriteWord(0x20, 4096)) });
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), 0, 0x10000);
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, bool degenerate = false)
    {
        byte[] bytes = new byte[0x1BC4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x538);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 166);
        short[][] vertices = degenerate ? [[0, -100, 0], [0, -100, 0], [0, -100, 0]] : [[-100, -100, 0], [100, -100, 0], [-100, 100, 0]];
        for (int index = 0; index < 3; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8 + axis * 2), vertices[index][axis]);
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x540), 287);
        for (int index = 0; index < 287; index++)
        {
            int face = 0x544 + index * 20;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x24808080);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        }
        return new(0, address, bytes.ToImmutableArray());
    }

    private static ushort Word(NativeBattleShivaFinalFrame frame, int address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(address, 2));

    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }
}
