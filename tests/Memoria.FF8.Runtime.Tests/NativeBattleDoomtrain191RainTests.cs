using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191RainTests
{
    [Fact]
    public void Empty_bank_draws_before_twenty_eight_spawns_and_consumes_exact_seven_draws_per_record()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(Start + 14, new byte[] { 0xA5, 0x5A }).MarkUnknown(Start + 14, 2).Write(Start + 22, new byte[] { 0x12, 0x34 });
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeCrtRandomState expected = frame.Random;
        int[] first = new int[7];
        for (int index = 0; index < 196; index++)
        {
            expected = expected.Draw(out ushort value);
            if (index < 7)
            {
                first[index] = value;
            }
        }
        int draws = 0;
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0, (current, batch) =>
        {
            draws++;
            Assert.Empty(batch.Attempts);
            Assert.Equal(0u, Dword(current, 0));
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal(1u, Dword(result.Frame, 0));
        Assert.Equal((short)(first[0] % 1536 + 1536), Word(result.Frame, 6));
        Assert.Equal((short)(2 * (first[1] % 20000) - 20000), Word(result.Frame, 8));
        Assert.Equal((short)(first[2] % 20000 - 10000), Word(result.Frame, 10));
        Assert.Equal((short)(2 * (first[3] % 20000) - 20000), Word(result.Frame, 12));
        Assert.Equal((short)(first[4] % 48 - 24), Word(result.Frame, 16));
        Assert.Equal((short)(first[5] % 48 - 24), Word(result.Frame, 18));
        Assert.Equal((short)(first[6] % 48 - 24), Word(result.Frame, 20));
        Assert.Equal(1u, Dword(result.Frame, 27 * 24));
        Assert.Equal(0u, Dword(result.Frame, 28 * 24));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, result.Frame.Memory.Arena!.ReadSnapshot(Start + 14, 2).Data);
        Assert.All(result.Frame.Memory.Arena.ReadSnapshot(Start + 14, 2).Known, Assert.False);
        Assert.Equal(new byte[] { 0x12, 0x34 }, result.Frame.Memory.Arena.Read(Start + 22, 2));
        Assert.Equal((ushort)1, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Advance(JsonSerializer.Deserialize<NativeBattleDoomtrain191Frame>(JsonSerializer.Serialize(frame))!, 0)));
    }

    [Fact]
    public void Paused_draw_keeps_age_position_random_and_task_counter_but_publishes_actual_back_color()
    {
        NativeBattleDoomtrain191Frame frame = Particle(Fixture(307), 5, 32760, 20, 3000, 12);
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 1, (_, batch) =>
        {
            Assert.Equal(40, batch.AllocationBytes);
            Assert.Equal(Colors[5], Assert.Single(batch.Attempts).Primitive!.Words[1]);
            return _;
        });
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(JsonSerializer.Serialize(frame.Memory.Arena), JsonSerializer.Serialize(result.Frame.Memory.Arena));
        Assert.Equal((short)5, Word(result.Frame, 4));
        Assert.Equal((ushort)307, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(new NativeBattleScriptedBackColor(17, 23, 29), result.Frame.Memory.ScriptedLighting!.BackColor);
        NativeBattleScriptedColorRegisters registers = result.Frame.Memory.ScriptedColorRegisters!;
        Assert.Equal(new int?[] { 32521, 299, 3029 }, registers.Mac);
        Assert.Equal(new int?[] { 32521, 299, 3029 }, registers.Ir);
        Assert.Equal(new byte[] { 0, 255, 0, 255, 0, 0, 0, 0 }, registers.NormalInput!.Value);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, registers.NormalInputKnown!.Value);
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Last_age_clears_record_but_draws_old_position_and_does_not_complete_until_no_accepted_draw()
    {
        NativeBattleDoomtrain191Frame frame = Particle(Fixture(308), 63, 0, 0, 3000, 100);
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0, (current, batch) =>
        {
            Assert.Equal(0u, Dword(current, 0));
            Assert.Equal(40, batch.AllocationBytes);
            Assert.Equal(Colors[63], batch.Attempts[0].Primitive!.Words[1]);
            return current;
        });
        Assert.Equal(0, result.Result);
        Assert.Equal((short)64, Word(result.Frame, 4));
        Assert.Equal((short)0, Word(result.Frame, 8));
        Assert.Equal(2, Advance(result.Frame, 0).Result);
    }

    [Fact]
    public void Rejected_active_record_allows_completion_after_clock_threshold_and_updates_wrapping_position()
    {
        NativeBattleDoomtrain191Frame frame = Particle(Fixture(307), 2, 32760, 0, -3000, 24);
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0, (current, batch) =>
        {
            Assert.Equal(0, batch.AllocationBytes);
            Assert.Null(Assert.Single(batch.Attempts).Primitive);
            return current;
        });
        Assert.Equal(2, result.Result);
        Assert.Equal(1u, Dword(result.Frame, 0));
        Assert.Equal(unchecked((short)(32760 + 24)), Word(result.Frame, 8));
        Assert.Equal((short)3, Word(result.Frame, 4));
    }

    [Fact]
    public void Spawn_search_requires_full_zero_DWORD_and_preserves_nonactive_occupied_record()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Start, BitConverter.GetBytes(2u)) } };
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0);
        Assert.Equal(2u, Dword(result.Frame, 0));
        Assert.Equal(1u, Dword(result.Frame, 24));
        Assert.Equal(1u, Dword(result.Frame, 28 * 24));
    }

    [Theory]
    [InlineData(4, 2064)]
    [InlineData(14, 18)]
    public void Billboard_uses_full_MAC_translation_SZ3_and_synthesized_fourth_screen_with_unknown_UV_padding(byte shift, int bucket)
    {
        NativeBattleActionMatrix camera = Identity with { X = 50000, Z = 32760 };
        NativeBattleGfGeometryAttempt result = NativeBattleDoomtrain191Rain.Emit(camera, new(1000, 0, 0), 4096, 0x2E123456, Projection, shift);
        NativeBattleGeometryPrimitive packet = Assert.IsType<NativeBattleGeometryPrimitive>(result.Primitive);
        Assert.Equal(bucket, packet.Bucket);
        Assert.Equal(0x2E123456u, packet.Words[1]);
        Assert.Equal((packet.Words[4] & 0xFFFF) | (packet.Words[6] & 0xFFFF0000), packet.Words[8]);
        Assert.Equal(0x3EE4C0A0u, packet.Words[3]);
        Assert.Equal(0x0036C0C0u, packet.Words[5]);
        Assert.Equal((ushort)0xE0A0, unchecked((ushort)packet.Words[7]));
        foreach (int index in new[] { 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, result.WriteMask[index]);
        }
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, new(1000, 0, 0));
        Assert.Equal(51000, mac.X);
        NativeBattleModuleProjectedVertex expected = NativeBattleModuleProjection.Project45FE10(Identity with { X = 51000, Z = 32760 }, new(-256, -256, 0), Projection);
        Assert.Equal(expected.Screen, packet.Words[2]);
    }

    [Fact]
    public void Near_rejection_keeps_preliminary_color_header_UV_stores_at_same_unallocated_cursor()
    {
        NativeBattleGfGeometryAttempt result = NativeBattleDoomtrain191Rain.Emit(Identity, new(0, 0, 1), 4096, 0x2E123456, Projection, 4, 80);
        Assert.Null(result.Primitive);
        Assert.Equal(80, result.ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Known, result.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, result.WriteMask[30]);
        Assert.Equal(0x09000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.Data.AsSpan()));
    }

    private const int Start = 0x33000;
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 104);
    private static ImmutableArray<uint> Colors => Enumerable.Range(0, 64).Select(index => 0x2E000000u | (uint)index).ToImmutableArray();
    private static NativeBattleDoomtrain191Frame Fixture(ushort age)
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Tests.Initialize(NativeBattleDoomtrain191Tests.Fixture());
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = (pool.Slots[0] with { Callback = 0x641AF0 }).WriteWord(0xC, age);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(0, task) }) } };
    }

    private static NativeBattleDoomtrain191Frame Particle(NativeBattleDoomtrain191Frame frame, short age, short x, short y, short z, short velocity)
    {
        byte[] data = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        for (int index = 0; index < 9; index++)
        {
            short value = index switch { 0 => age, 1 => 4096, 2 => x, 3 => y, 4 => z, 6 => velocity, _ => 0 };
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(4 + index * 2), value);
        }
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Start, data) } };
    }

    private static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, uint flags, Func<NativeBattleDoomtrain191Frame, NativeBattleGfGeometryBatch, NativeBattleDoomtrain191Frame>? draw = null)
    {
        NativeBattleActionMatrix camera = Identity with { X = 17, Y = 23, Z = 29 };
        NativeBattlePackedMatrix32 packed = NativeBattleDoomtrain191Controller.Pack(camera, frame.Memory.Doomtrain191!.BaseMatrix, true);
        return NativeBattleDoomtrain191Rain.Advance641AF0(frame, 0, Colors, Projection, new(() => flags, () => packed, draw ?? ((current, _) => current)));
    }
    private static short Word(NativeBattleDoomtrain191Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(Start + offset, 2));
    private static uint Dword(NativeBattleDoomtrain191Frame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(Start + offset, 4));
}
