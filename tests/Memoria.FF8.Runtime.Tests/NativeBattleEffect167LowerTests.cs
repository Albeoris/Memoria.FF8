using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167LowerTests
{
    [Fact]
    public void Trail_draws_thirty_paired_quads_and_reserves_the_native_extra_half_pair()
    {
        NativeBattleEffect167TrailStep step = Trail(2000);
        Assert.Equal(60, step.Batch.Attempts.Length);
        Assert.Equal(30 * 72 + 36, step.Batch.AllocationBytes);
        for (int segment = 0; segment < 30; segment++)
        {
            NativeBattleGfGeometryAttempt first = step.Batch.Attempts[segment * 2];
            NativeBattleGfGeometryAttempt second = step.Batch.Attempts[segment * 2 + 1];
            Assert.Equal(segment * 72, first.ByteOffsetRelative);
            Assert.Equal(segment * 72 + 36, second.ByteOffsetRelative);
            Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, first.Primitive!.VertexPayload4);
            Assert.Equal(first.Primitive.VertexPayload4, second.Primitive!.VertexPayload4);
            Assert.Equal(0x3A000000u, second.Primitive.Words[1]);
            Assert.Equal(0x3A000000u, second.Primitive.Words[3]);
            Assert.Equal(first.Primitive.Words[1], first.Primitive.Words[5]);
            Assert.Equal(first.Primitive.Words[3], first.Primitive.Words[7]);
            foreach (int word in new[] { 2, 4, 6, 8 })
            {
                Assert.Equal(first.Primitive.Words[word], second.Primitive.Words[word]);
            }
        }
        Assert.DoesNotContain(true, step.Temporary64.ReadSnapshot(0, 8).Known);
        Assert.Equal(0u, step.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.False(step.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Trail_first_projection_rejection_keeps_only_tag_stores_and_still_reserves_thirty_six_bytes()
    {
        NativeBattleEffect167TrailStep step = Trail(0);
        Assert.Equal(36, step.Batch.AllocationBytes);
        Assert.Equal(30, step.Batch.Attempts.Length);
        Assert.All(step.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.All(attempt.WriteMask.Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
            Assert.All(attempt.WriteMask.Skip(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        });
        Assert.DoesNotContain(true, step.Temporary64.ReadSnapshot(8, 4).Known);
        Assert.Equal(0x3A903030u, BitConverter.ToUInt32(step.Temporary64.Read(0x14, 4)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Trail_delivery_preserves_unlinked_reservation_and_advances_the_actual_cursor(bool accepted)
    {
        NativeBattleEffect167TrailStep step = Trail(accepted ? 2000 : 0);
        Assert.True(step.Batch.NativeUnlinkedAllocation);
        const int cursor = 64;
        int reservation = cursor + step.Batch.AllocationBytes - 36;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(cursor, step.Batch.AllocationBytes).Write(reservation + 8, [0xA5, 0x5A]);
        NativeBattleEffectMemory memory = step.Memory with { Arena = arena };
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(step.Batch, cursor, NativeBattleEffectArena.Length, offset =>
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
            return (bytes[0], known[0]);
        });
        Assert.True(resolved.NativeUnlinkedAllocation);
        NativeBattleEffectPacketWrite delivered = NativeBattleEffectPacketArena.Append(memory, cursor, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(cursor + step.Batch.AllocationBytes, delivered.Cursor);
        Assert.Equal(accepted ? 60 : 0, delivered.Ordering.Proxies.Length);
        int preservedStart = accepted ? reservation : reservation + 4;
        int preservedLength = accepted ? 36 : 32;
        Assert.Equal(arena.ReadSnapshot(preservedStart, preservedLength).Data, delivered.Memory.Arena!.ReadSnapshot(preservedStart, preservedLength).Data);
        Assert.Equal(arena.ReadSnapshot(preservedStart, preservedLength).Known, delivered.Memory.Arena.ReadSnapshot(preservedStart, preservedLength).Known);
    }

    [Theory]
    [InlineData(0u, 2, 41)]
    [InlineData(0x201u, 0, 40)]
    public void Body_terminal_old_age_forty_skips_all_render_and_model_reads(uint flags, int result, int age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23BE130, 0x23BE140, 36, 100);
        (pool, _) = pool.Append(0x5E3C20);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 40).WriteWord(0x1C, 10).WriteWord(0x1E, 2));
        NativeBattleEffect167Frame frame = new(NativeBattleEffect167BodyDrawingTests.Memory(), new(167, 166, new(167, 0x5E2A10, 0x5E29F0), null, null) { Pools = [pool] }, new(77), NativeBattleEffect167State.FromColdProcess());
        NativeBattleEffect167Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, (_, _) => throw new InvalidOperationException("Terminal callback must not draw."));
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceBody(frame, 0x23BE140, inputs);
        Assert.Equal(result, step.Result);
        Assert.Equal(age, Read(step.Frame, 0x23BE140).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(0)]
    public void Actual167_trail_capture_preserves_paired_native_packets_reservations_and_rejections(int z)
    {
        NativeBattleEffect167TrailStep native = Trail(z);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleEffect167TrailStep captured = Trail(z, new(167, 1, 2, 3, 4) { ResourceGeneration = 5 });
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        if (z == 0)
        {
            Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        }
        else
        {
            Assert.Equal(60, captured.Batch.Attempts.Length);
            Assert.All(captured.Batch.Attempts, attempt =>
            {
                Assert.NotNull(attempt.Primitive!.PilotGeometry);
                Assert.Equal((ushort)167, attempt.Primitive.PilotGeometry!.Identity.Effect);
            });
        }
    }

    private static NativeBattleEffect167TrailStep Trail(int z, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
        NativeBattleEffectArena second = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
        for (int row = 0; row < 32; row++)
        {
            first = Store(first, row * 8, (short)(row * 4 - 60), -100, 0);
            second = Store(second, row * 8, (short)(row * 4 - 60), 100, 0);
        }
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 64);
        return NativeBattleEffect167Lower.DrawTrail(NativeBattleEffect167BodyDrawingTests.Memory(z), first, second, context, new(pilotIdentity.HasValue ? NativeBattleCuragaMatrices.Identity8DD770() : null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity);
    }

    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, short x, short y, short z) => arena.Write(offset, BitConverter.GetBytes(x)).Write(offset + 2, BitConverter.GetBytes(y)).Write(offset + 4, BitConverter.GetBytes(z));
}
