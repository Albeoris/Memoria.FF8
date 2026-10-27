using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaFinalShatterTests
{
    [Fact]
    public void Initialization_uses800_draws_and_preserves_unwritten_record_and_task_words()
    {
        NativeBattleShivaFinalFrame original = Frame(0, false);
        NativeBattleEffectArena arena = original.Memory.Arena!;
        arena = Put(arena, 0x20016, 0x3456);
        arena = Put(arena, 0x24016, 0x789A);
        original = original with { Memory = original.Memory with { Arena = arena } };
        NativeBattleShivaFinalFrame initialized = NativeBattleShivaFinalShatter.Initialize5C7570(original, Mesh(0xD257CC), Mesh(0xD27E6C));
        NativeCrtRandomState expected = original.Random;
        expected = expected.Draw(out ushort first);
        for (int index = 1; index < 800; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((ushort)0x3456, Word(initialized, 0x20016));
        Assert.Equal((ushort)0x789A, Word(initialized, 0x24016));
        Assert.Equal(unchecked((ushort)(-10 - first % 190)), Word(initialized, 0x20012));
        Assert.Equal((ushort)0, Word(initialized, 0x20008));
        Assert.Equal((ushort)700, initialized.Pool.Slots[0].ReadWord(0x12));
        Assert.Equal(unchecked((ushort)-7000), initialized.Pool.Slots[0].ReadWord(0x14));
    }

    [Theory]
    [InlineData(16, -24, 3, 0)]
    [InlineData(17, -12, 3, 0)]
    [InlineData(18, 0, 195, 0)]
    [InlineData(26, 96, 195, 4096)]
    public void Middle_phase_uses_private_cursor_order_and_signed_scroll_even_when_paused(ushort counter, int scroll, uint flags, int cue)
    {
        NativeBattleShivaFinalFrame frame = Frame(counter);
        List<string> order = [];
        NativeBattleShivaFinalInputs inputs = Inputs() with
        {
            DrawPrivateMesh = (current, draw) =>
            {
                order.Add("mesh");
                Assert.Equal(0xD257CCu, draw.Resource.Address);
                Assert.Equal(2, draw.Context.DepthShift);
                return current with { PrivateDrawOffset = current.PrivateDrawOffset + 32 };
            },
            DrawPrivateScroll = (current, draw) =>
            {
                order.Add("scroll");
                Assert.Equal(0x10020, current.PrivateDrawOffset);
                Assert.Equal(scroll, draw.Context.VOffset);
                Assert.Equal(flags, draw.Context.Flags);
                Assert.Equal(counter < 18 ? null : (int?)cue, draw.Context.Fade);
                Assert.Equal(3, draw.Context.DepthShift);
                return current with { PrivateDrawOffset = current.PrivateDrawOffset + 40 };
            }
        };
        NativeBattleShivaFinalStep step = NativeBattleShivaFinalShatter.Advance5C77C0(frame, 0, 0x201, inputs);
        Assert.Equal(new[] { "mesh", "scroll" }, order);
        Assert.Equal(0x10048, step.Frame.PrivateDrawOffset);
        Assert.Equal(counter, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0, step.Result);
        Assert.Empty(step.CommonPackets.Attempts);
    }

    [Fact]
    public void Reveal_updates_brightness_before_rejected_projection_and_freezes_it_when_paused()
    {
        NativeBattleShivaFinalFrame frame = Frame(15);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 400; index++)
        {
            arena = Put(arena, 0x20000 + index * 32 + 6, 16);
            arena = Put(arena, 0x24000 + index * 32 + 6, 16);
        }
        arena = Put(arena, 0x20006, 0);
        arena = Put(arena, 0x20008, 48);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        List<NativeBattleGfGeometryBatch> submitted = [];
        NativeBattleShivaFinalInputs inputs = Inputs(true) with { SubmitPrivatePackets = (current, batch) =>
        {
            submitted.Add(batch);
            return current;
        } };
        NativeBattleShivaFinalFrame independent = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleShivaFinalStep step = NativeBattleShivaFinalShatter.Advance5C77C0(frame, 0, 0, inputs);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(submitted[0].Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0x26606060u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.Equal((ushort)96, Word(step.Frame, 0x20008));
        Assert.Empty(submitted[1].Attempts);
        Assert.Equal(0, step.Result);
        NativeBattleShivaFinalStep paused = NativeBattleShivaFinalShatter.Advance5C77C0(independent, 0, 1, inputs);
        Assert.Equal((ushort)48, Word(paused.Frame, 0x20008));
        Assert.Equal((ushort)15, paused.Frame.Pool.Slots[0].ReadWord(0x0C));
    }

    [Fact]
    public void Final_phase_publishes_even_paused_and_completes_only_on_unpaused_counter64()
    {
        NativeBattleShivaFinalFrame frame = DeadFragments(Frame(63));
        NativeBattleShivaFinalInputs inputs = Inputs();
        NativeBattleShivaFinalStep paused = NativeBattleShivaFinalShatter.Advance5C77C0(frame, 0, 0x200, inputs);
        Assert.Equal(3, paused.Frame.Phase22BD010);
        Assert.Equal((ushort)63, paused.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0, paused.Result);
        Assert.Empty(paused.CommonPackets.Attempts);
        NativeBattleShivaFinalFrame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaFinalFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleShivaFinalStep step = NativeBattleShivaFinalShatter.Advance5C77C0(clone, 0, 0, inputs);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)64, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(3, step.Frame.Phase22BD010);
    }

    [Fact]
    public void Active_fragment_keeps_brightness_alias_as_old_Xoffset_and_draws_after_becoming_dead()
    {
        NativeBattleShivaFinalFrame frame = DeadFragments(Frame(60));
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        arena = Put(arena, 0x20006, 0);
        arena = Put(arena, 0x20008, 144);
        arena = Put(arena, 0x2000E, 23);
        arena = Put(arena, 0x20010, -33);
        arena = Put(arena, 0x2001A, 0);
        arena = Put(arena, 0x2001E, 0);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleActionMatrix? observed = null;
        NativeBattleShivaFinalInputs inputs = Inputs() with { ObserveGte = operation => observed = operation.Matrix ?? observed };
        NativeBattleShivaFinalStep step = NativeBattleShivaFinalShatter.Advance5C77C0(frame, 0, 0, inputs);
        Assert.Single(step.CommonPackets.Attempts);
        Assert.NotNull(observed);
        Assert.Equal(-207, observed.X);
        Assert.Equal((ushort)111, Word(step.Frame, 0x20008));
        Assert.Equal(unchecked((ushort)-31), Word(step.Frame, 0x20010));
        Assert.Equal(ushort.MaxValue, Word(step.Frame, 0x2000E));
        Assert.Equal(0, step.Result);
    }

    private static NativeBattleShivaFinalFrame DeadFragments(NativeBattleShivaFinalFrame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 400; index++)
        {
            arena = Put(arena, 0x20000 + index * 32 + 14, -1);
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    private static NativeBattleShivaFinalFrame Frame(ushort counter, bool append = true)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        if (append)
        {
            (pool, int slot) = pool.Append(0x5C77C0);
            pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter)) });
        }
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), 1, 0x10000);
    }

    private static NativeBattleShivaFinalInputs Inputs(bool degenerate = false)
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 10000 };
        return new(view, Mesh(0xD257CC, degenerate), Mesh(0xD27E6C, degenerate), new(view, 216, 160, 108), 341, NativeBattleGteColorInterpolation.CodeConstants45F270, (frame, batch) => frame with { PrivateDrawOffset = frame.PrivateDrawOffset + batch.AllocationBytes }, (_, _) => throw new InvalidOperationException("Unexpected standard draw."), (_, _) => throw new InvalidOperationException("Unexpected scroll draw."));
    }

    private static NativeBattleStaticMeshResource Mesh(uint address, bool degenerate = false)
    {
        byte[] bytes = new byte[0x26A0];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x740);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 231);
        short[][] vertices = degenerate ? [[0, 0, 0], [0, 0, 0], [0, 0, 0]] : [[-100, -100, 0], [100, -100, 0], [-100, 100, 0]];
        for (int index = 0; index < 3; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8 + axis * 2), vertices[index][axis]);
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x748), 400);
        for (int index = 0; index < 400; index++)
        {
            int face = 0x74C + index * 20;
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
