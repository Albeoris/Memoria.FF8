using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleJunk44Controller;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleJunk44ControllerTests
{
    [Fact]
    public void Entry_resets_only_BSS_root_and_retains_inactive_COMMON_until_old_one()
    {
        var inactive = Inactive();
        inactive = inactive with { Memory = inactive.Memory with { Arena = inactive.Memory.Arena!.Write(0x1240, new byte[] { 0xAB, 0xCD }) }, State = inactive.State with { RootBankStorage = NativeBattleEffectArena.Clear().Write(18, new byte[] { 0xFE, 0xDC }) } };
        var frame = Initialize(inactive, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(0x6E7B90u, Read(frame, RootBank).Callback);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x1240, 2));
        Assert.Equal(0x1D99A78u, frame.State.ReadDword(ParametersGlobal));
        Assert.Equal(ModelAddressFor(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal(ArenaAddress, frame.State.ReadDword(ArenaGlobal));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(0x0C));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0x1240, 2));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Equal(5, frame.Effect.Pools.Length);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(0, 1));
        Assert.Equal(0x6E7E00u, Read(frame, Pools[4].Bank).Callback);
        Assert.Equal((ushort)1, Read(frame, Pools[4].Bank).ReadWord(0x0C));
    }

    [Theory]
    [InlineData(0u, 0x112D4u, 1u)]
    [InlineData(1u, 0x12D4u, 0u)]
    [InlineData(0x80000000u, 0x12D4u, 0u)]
    public void Root_parity_uses_full_DWORD_and_advances_without_graphics_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(0x10, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(0x10));
        Assert.Equal((ushort)1, Read(actual, RootBank).ReadWord(0x0C));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Setup_uses_wrapping_WORD_mean_real_recipient_pointers_and_one_CRT_draw()
    {
        var packets = Packets(2);
        var frame = Initialize(Inactive(), Resources(), Inputs([], packets));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 1));
        List<uint> anchors = [];
        var inputs = Inputs([], packets) with
        {
            ReadAnchor571400 = (current, model) =>
            {
                anchors.Add(model);
                return new(current, PointView(new(30000, -30000, 13)));
            }
        };
        var expected = frame.Random.Draw(out ushort seed);
        frame = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        uint main = Pools[4].Bank;
        Assert.Equal(new[] { ModelAddressFor(0), ModelAddressFor(1) }, anchors);
        Assert.Equal(unchecked((ushort)-2768), Read(frame, main).ReadWord(0x44));
        Assert.Equal((ushort)2768, Read(frame, main).ReadWord(0x46));
        Assert.Equal((ushort)13, Read(frame, main).ReadWord(0x48));
        Assert.Equal(seed, Read(frame, main).ReadWord(0x10));
        Assert.Equal(expected, frame.Random);
        Assert.Equal(ModelAddressFor(0), Read(frame, main).ReadDword(0x14));
        Assert.Equal(ModelAddressFor(1), Read(frame, main).ReadDword(0x24));
        Assert.Equal(PointView(new(100, 200, 300), 444).Read(0, 8), TaskView(Read(frame, main), 0x18, 8).Read(0, 8));
        Assert.Equal(0x128C, frame.Memory.EffectIo!.BaseOffset);
    }

    [Fact]
    public void Setup_rejects_native_zero_divisor_instead_of_inventing_mean_anchor()
    {
        var packets = Packets(1);
        packets = SetCount(packets, 0);
        var frame = Initialize(Inactive(), Resources(), Inputs([], packets));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 1));
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, RootBank, Resources(), Inputs([], packets)));
    }

    [Theory]
    [InlineData((ushort)113, true)]
    [InlineData((ushort)114, false)]
    [InlineData((ushort)115, false)]
    public void Root_stops_child_scans_at_signed_old_one_fourteen(ushort age, bool scanned)
    {
        var frame = Active();
        uint main = Pools[4].Bank;
        frame = Write(frame, main, Read(frame, main).WriteWord(0x0C, 98));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, age));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        Assert.Equal(scanned ? (ushort)99 : (ushort)98, Read(actual, main).ReadWord(0x0C));
    }

    [Fact]
    public void Root_queues_actual_two_file_descriptors_at_old115_and_polls_without_age_advance()
    {
        var frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 115));
        List<NativeBattleJunk44Operation> operations = [];
        var inputs = Inputs(operations) with { ReloadComplete = current => new(current, false) };
        frame = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal((ushort)116, Read(frame, RootBank).ReadWord(0x0C));
        Assert.Equal(2, frame.Memory.Arena!.Read(0x128F, 1)[0]);
        Assert.Equal(BitConverter.GetBytes((short)1), frame.Memory.Arena!.Read(0x1294, 2));
        Assert.Equal(BitConverter.GetBytes((short)360), frame.Memory.Arena!.Read(0x1296, 2));
        Assert.Equal(BitConverter.GetBytes((short)0), frame.Memory.Arena!.Read(0x129C, 2));
        Assert.Equal(BitConverter.GetBytes((short)361), frame.Memory.Arena!.Read(0x129E, 2));
        Assert.Equal(BitConverter.GetBytes(ArenaAddress + 0x12D4), frame.Memory.Arena!.Read(0x1298, 4));
        Assert.Equal(BitConverter.GetBytes(ArenaAddress + 0x12D4), frame.Memory.Arena!.Read(0x12A0, 4));
        Assert.IsType<NativeBattleJunk44AdvanceIo534210>(Assert.Single(operations));
        var waiting = AdvanceTask(frame, RootBank, Resources(), inputs);
        Assert.Equal(0, waiting.Result);
        Assert.Equal((ushort)116, Read(waiting.Frame, RootBank).ReadWord(0x0C));
        var complete = AdvanceTask(waiting.Frame, RootBank, Resources(), inputs with { ReloadComplete = current => new(current, true) });
        Assert.Equal(2, complete.Result);
        Assert.Equal((ushort)116, Read(complete.Frame, RootBank).ReadWord(0x0C));
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 2)]
    public void Debris_birth_consumes_two_attempt_draws_and_four_success_only_draws(bool available, int draws)
    {
        var frame = MainAt(18);
        if (!available)
        {
            for (int index = 0; index < Pools[2].Capacity; index++)
            {
                (frame, _) = Append(frame, Pools[2].Descriptor, 0x6E84D0);
            }
        }
        NativeCrtRandomState expected = frame.Random;
        ushort[] values = new ushort[draws];
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out values[index]);
        }
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(expected, actual.Random);
        if (available)
        {
            var child = Read(actual, Pools[2].Bank);
            Assert.Equal(unchecked((ushort)((values[2] * 800 >> 15) - 400)), child.ReadWord(0x1C));
            Assert.Equal(values[3], child.ReadWord(0x1E));
            Assert.Equal(unchecked((ushort)((values[4] & 255) - 128)), child.ReadWord(0x24));
            Assert.Equal(unchecked((ushort)((values[5] & 255) - 128)), child.ReadWord(0x26));
        }
    }

    [Theory]
    [InlineData((ushort)3, true)]
    [InlineData((ushort)4, false)]
    [InlineData((ushort)10, true)]
    [InlineData((ushort)34, false)]
    [InlineData((ushort)38, false)]
    public void Lift_amplitude_draw_uses_selected_seven_tick_remainders(ushort age, bool draw)
    {
        var frame = MainAt(age);
        var expected = draw ? frame.Random.Draw(out _) : frame.Random;
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(expected, actual.Random);
    }

    [Fact]
    public void Lift_writes_only_recipient_Y_then_restores_saved_full_packed_position_at_new100()
    {
        var frame = MainAt(3);
        List<NativeBattleJunk44Operation> operations = [];
        frame = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs(operations)).Frame;
        var write = Assert.Single(operations.OfType<NativeBattleJunk44ModelPositionY>());
        Assert.Equal(ModelAddressFor(0), write.ModelAddress);
        Assert.Empty(operations.OfType<NativeBattleJunk44RestoreModelPosition>());
        frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(0x0C, 99));
        operations.Clear();
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs(operations));
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)100, Read(actual.Frame, Pools[4].Bank).ReadWord(0x0C));
        var restore = Assert.Single(operations.OfType<NativeBattleJunk44RestoreModelPosition>());
        Assert.Equal(ModelAddressFor(0), restore.ModelAddress);
        Assert.Equal(PointView(new(100, 200, 300), 444).Read(0, 8), restore.Point8.Read(0, 8));
    }

    [Theory]
    [InlineData((ushort)3)]
    [InlineData((ushort)4)]
    public void Lift_uses_native_56D130_sine_with_full_angle_and_signed_truncation(ushort age)
    {
        var frame = MainAt(age);
        int amplitude = 500;
        if (age == 3)
        {
            _ = frame.Random.Draw(out ushort random);
            amplitude = (random & 0x1FF) + 0x100;
        }
        else
        {
            frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(0x20, unchecked((ushort)amplitude)));
        }
        int angle = (age - 3) * 2048 / 7 + 0x124;
        short expected = unchecked((short)(200 - unchecked(amplitude * NativeBattleCameraMath.Sine(angle)) / 4096));
        short swapped = unchecked((short)(200 - unchecked(amplitude * NativeBattleCameraMath.Cosine(angle)) / 4096));
        Assert.NotEqual(swapped, expected);
        List<NativeBattleJunk44Operation> operations = [];
        _ = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs(operations));
        Assert.Equal(expected, Assert.Single(operations.OfType<NativeBattleJunk44ModelPositionY>()).Y);
    }

    [Fact]
    public void Old62_anchor_keeps_frame_publications_and_births_actual_target_debris_row_six()
    {
        var frame = MainAt(62);
        int anchors = 0;
        var view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 99 }, null);
        var inputs = Inputs([]) with
        {
            ReadAnchor571400 = (current, model) =>
            {
                Assert.Equal(ModelAddressFor(0), model);
                anchors++;
                return new(current with { Memory = current.Memory with { SharedEffectView2793E58 = view } }, PointView(new(101, -202, 303)));
            }
        };
        var expected = frame.Random.Draw(out ushort velocity).Draw(out ushort angle);
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), inputs).Frame;
        Assert.Equal(1, anchors);
        Assert.Equal(view, actual.Memory.SharedEffectView2793E58);
        Assert.Equal(expected, actual.Random);
        var child = Read(actual, Pools[2].Bank);
        Assert.Equal(ModelAddressFor(0), child.ReadDword(0x30));
        Assert.Equal((ushort)24, child.ReadWord(0x12));
        Assert.Equal(unchecked((ushort)((velocity & 0x1FF) - 0x500)), child.ReadWord(0x1C));
        Assert.Equal(angle, child.ReadWord(0x1E));
        Assert.Equal((ushort)0, child.ReadWord(0x24));
    }

    [Fact]
    public void IO_completion_restores_COMMON_owners_with_still_active_children_and_warm_reset_is_deferred()
    {
        var frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 116));
        frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(0x40, 0xA55A));
        var actual = Advance(frame, Resources(), Inputs([]) with { ReloadComplete = current => new(current, true) });
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Equal(BitConverter.GetBytes((ushort)0xA55A), actual.Memory.Arena!.Read(0x1280, 2));
        Assert.Equal(1, actual.Memory.Arena!.Read(0x1240, 1)[0] & 1);
        actual = JsonSerializer.Deserialize<NativeBattleJunk44Frame>(JsonSerializer.Serialize(actual))!;
        actual = Initialize(actual, Resources(), Inputs([]));
        Assert.Single(actual.Effect.Pools);
        Assert.Equal(BitConverter.GetBytes((ushort)0xA55A), actual.Memory.Arena!.Read(0x1280, 2));
        actual = Advance(actual, Resources(), Inputs([]));
        actual = Advance(actual, Resources(), Inputs([]));
        Assert.Equal((ushort)0, Read(actual, Pools[4].Bank).ReadWord(0x40));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0x1240, 1));
    }

    private static NativeBattleJunk44Frame Active()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x0C, 1));
        return AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
    }

    private static NativeBattleJunk44Frame MainAt(ushort age)
    {
        var frame = Active();
        return Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(0x0C, age));
    }

    private static NativeBattleJunk44Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(44, 43, new(44, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleJunk44State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleJunk44Inputs Inputs(List<NativeBattleJunk44Operation> operations, NativeBattleActionPacketsState? packets = null)
    {
        packets ??= Packets(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, address) => Model(address), (current, address) => new(current, PointView(Model(address).Transform!.Position28)), (current, _, selector, distance) => new(current, PointView(new((short)selector, (short)distance, 1000))), _ => packets, current => new(current, false), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new((short)(100 + actor), (short)(200 + actor), (short)(300 + actor)), (short)(444 + actor), 777, 4096) };
    }

    private static uint ModelAddressFor(int actor) => 0x1D972C0u + (uint)(actor * 156);

    private static NativeBattleActionPacketsState Packets(int count)
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 226, 44, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 226, 44, null));
        for (int index = 0; index < count; index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)index, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)count));
        return packets.WritePlayerRepeatMode(1, 1);
    }

    private static NativeBattleActionPacketsState SetCount(NativeBattleActionPacketsState packets, byte count)
    {
        NativeBattleActionHeaderSlot header = packets.Headers[1];
        return packets with { Headers = packets.Headers.SetItem(1, header with { Payload = header.Payload.SetItem(16, count), UnknownByteMask = header.UnknownByteMask & ~(1u << 16) }) };
    }

    private static NativeBattleJunk44Resources Resources() => NativeBattleJunk44StateTests.Resources();
}
