using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleStare45Controller;
using static Memoria.FF8.Runtime.NativeBattleStare45Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStare45ControllerTests
{
    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, false)]
    [InlineData((byte)2, true)]
    [InlineData((byte)3, false)]
    public void Entry_uses_literal_flags_mask_and_full_six_bank_resets(byte flags, bool register)
    {
        var frame = Inactive(flags);
        frame = frame with { State = frame.State.WriteWord(0x26553C8, 0xABCD).WriteDword(0x2655750, 17).WriteDword(0x2654728, 23) with { InactiveBanks = Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().Write(pool.Stride * pool.Capacity - 2, new byte[] { 0xAA, 0xBB })).ToImmutableArray() } };
        var actual = Initialize(frame, Resources(), Inputs([]));
        Assert.Equal(6, actual.Effect.Pools.Length);
        Assert.All(actual.State.InactiveBanks, bank => Assert.Null(bank));
        Assert.All(actual.Effect.Pools, pool => Assert.Equal((ushort)0, pool.Slots[^1].ReadWord(pool.Stride - 2)));
        Assert.Equal(RootDescriptor, actual.Effect.ReturnedDescriptor);
        Assert.Equal(3, Read(actual, RootBank).ReadByte(0x2C));
        Assert.Equal(0, Read(actual, RootBank).ReadByte(0x2D));
        Assert.Equal((ushort)2, Read(actual, RootBank).ReadWord(0x5A));
        Assert.Equal(1, Read(actual, RootBank).ReadByte(0x2F));
        Assert.Equal((ushort)0xABCD, actual.State.ReadWord(0x26553C8));
        Assert.Equal(0u, actual.State.ReadDword(0x2655750));
        Assert.Equal(0u, actual.State.ReadDword(0x2654728));
        uint source = NativeBattleEffectNodeMemory.ArenaAddress + 0x30000;
        Assert.Equal(source, actual.State.ReadDword(InitializerGlobal));
        Assert.Equal(source, actual.State.ReadDword(0x26553C4));
        Assert.Equal(source + 0xC000, actual.State.ReadDword(0x26553C0));
        Assert.Equal(source + 0x18000, actual.State.ReadDword(0x2655754));
        NativeBattleEffectTextureState textures = actual.Memory.Textures ?? throw new InvalidDataException();
        Assert.Equal(register ? 1 : 0, textures.Count);
        Assert.Equal(register, textures.Animation is not null);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)0, 0x3C000u, 0x48000u)]
    [InlineData((ushort)1, 0x30000u, 0x3C000u)]
    [InlineData((ushort)65535, 0x30000u, 0x3C000u)]
    public void Root_uses_old_WORD_parity_and_current_view_without_pause(ushort old, uint primary, uint secondary)
    {
        var frame = Frame(0, RootCallback, 10);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, old).WriteWord(0x24, 0xFFFF));
        List<NativeBattleStare45Operation> operations = [];
        var camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 17, Z = 12345 }, null);
        var result = AdvanceTask(frame, RootBank, Resources(), Inputs(operations) with { GraphicsFlags = 0x201, ReadView = _ => camera });
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + primary, result.Frame.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + secondary, result.Frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(unchecked((ushort)(old + 1)), Read(result.Frame, RootBank).ReadWord(0x5C));
        Assert.Equal((ushort)0, Read(result.Frame, RootBank).ReadWord(0x24));
        Assert.Equal(camera, result.Frame.Memory.SharedEffectView2793E58);
        Assert.Equal(new NativeBattleStare45UpdateCaster8DC740(RootBank), Assert.Single(operations));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void CPU_basis_preserves_padding_knownness_and_current_GTE_owners()
    {
        var frame = Frame(0, RootCallback, 10);
        frame = frame with { State = frame.State.WriteView(0x2655758, NativeBattleEffectArena.Clear().Write(18, new byte[] { 0xA5, 0xB6 }).MarkUnknown(19, 1), 32) };
        var camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = int.MinValue, Y = 23, Z = -91 }, null);
        var actual = BuildBasis836840(frame, camera);
        var basis = actual.State.ReadView(0x2655758, 32);
        Assert.Equal(BitConverter.GetBytes((short)4096), basis.Read(0, 2));
        Assert.Equal(BitConverter.GetBytes((short)4096), basis.Read(8, 2));
        Assert.Equal(BitConverter.GetBytes((short)4096), basis.Read(16, 2));
        Assert.Equal(0xA5, basis.Read(18, 1)[0]);
        Assert.Throws<NotSupportedException>(() => basis.Read(19, 1));
        Assert.Equal(camera.Bytes.AsSpan().Slice(20, 12).ToArray(), basis.Read(20, 12));
        Assert.Same(frame.Memory.GeometryRegisters, actual.Memory.GeometryRegisters);
        Assert.Same(frame.Memory.ScriptedColorRegisters, actual.Memory.ScriptedColorRegisters);
        Assert.Same(frame.Staging, actual.Staging);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void Root_final_phases_advance_without_texture_reload_or_acknowledgement(byte phase)
    {
        var frame = Frame(0, RootCallback, phase);
        frame = Write(frame, RootBank, Read(frame, RootBank).MarkPayloadUnknown(0x62, 1));
        List<NativeBattleStare45Operation> operations = [];
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs(operations)).Frame;
        Assert.Equal(phase + 1, Read(actual, RootBank).ReadByte(0x29));
        Assert.Single(operations);
        Assert.Throws<NotSupportedException>(() => Read(actual, RootBank).ReadByte(0x62));
    }

    [Fact]
    public void Root_phase_six_consumes_previous_survivor_WORD_before_new_scans()
    {
        var frame = Frame(0, RootCallback, 6);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5E, 1));
        frame = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        Assert.Equal(6, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(0x5E));
        frame = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        Assert.Equal(7, Read(frame, RootBank).ReadByte(0x29));
    }

    [Fact]
    public void Director_uses_two_acknowledgements_before_three_distinct_programmes_and_stage_birth()
    {
        var frame = Frame(2, 0x836AC0, 0);
        uint address = Pools[2].Bank;
        List<NativeBattleStare45Operation> operations = [];
        var inputs = Inputs(operations);
        frame = AdvanceTask(frame, address, Resources(), inputs).Frame;
        Assert.Equal((ushort)1, frame.State.ReadWord(0x265540C));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x265540A));
        frame = AdvanceTask(frame, address, Resources(), inputs).Frame;
        Assert.Equal((ushort)1, frame.State.ReadWord(0x265540A));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x2655408));
        Assert.Null(frame.Effect.Pools[3].Head);
        frame = AdvanceTask(frame, address, Resources(), inputs).Frame;
        Assert.Equal((ushort)1, frame.State.ReadWord(0x2655408));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x265540E));
        Assert.Equal(2, Read(frame, address).ReadByte(0x29));
        Assert.Equal(new NativeBattleStare45Sound501330(Resources().Sound, 0, 128), Assert.Single(operations));
        var first = Read(frame, Pools[3].Bank);
        var second = Read(frame, Pools[3].Bank + 720);
        var third = Read(frame, Pools[3].Bank + 1440);
        Assert.Equal(0x836D60u, first.Callback);
        Assert.Equal(0x836D60u, second.Callback);
        Assert.Equal(0x839590u, third.Callback);
        Assert.Equal(0x17C4DA0u, first.ReadDword(0x74));
        Assert.Equal(0x17C4DA0u, second.ReadDword(0x74));
        Assert.Equal(0x17C6A14u, third.ReadDword(0x74));
        Assert.Equal(452u, first.ReadDword(0x78));
        Assert.Equal(572u, third.ReadDword(0x78));
        Assert.Equal((ushort)0, first.ReadWord(0x8A));
        Assert.Equal((ushort)1, second.ReadWord(0x8A));
        Assert.Equal((ushort)1, third.ReadWord(0x80));
        Assert.Equal(RootBank, third.ReadDword(0x10));
        Assert.Equal(address, third.ReadDword(0x18));
        Assert.Equal(0x839690u, Read(frame, Pools[4].Bank).Callback);
        Assert.Null(frame.Effect.Pools[5].Head);
        Assert.Equal(4, Read(frame, address).ReadByte(0x28));
    }

    [Theory]
    [InlineData((ushort)30, false)]
    [InlineData((ushort)31, true)]
    [InlineData((ushort)65535, false)]
    public void Director_contact_request_uses_wrapping_WORD_age_and_signed_threshold(ushort prior, bool ready)
    {
        var frame = Frame(2, 0x836AC0, 2);
        frame = frame with { State = frame.State.WriteWord(0x265540E, prior) };
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(ready ? (ushort)1 : (ushort)0, actual.State.ReadWord(0x2655410));
        Assert.Equal(ready ? 3 : 2, Read(actual, Pools[2].Bank).ReadByte(0x29));
    }

    [Theory]
    [InlineData((ushort)0, false)]
    [InlineData((ushort)1, true)]
    [InlineData((ushort)2, false)]
    public void Target_single_contact_requires_exact_WORD_one_and_uses_current_physical_pointer(ushort gate, bool contact)
    {
        var frame = Frame(1, 0x836990, 1);
        uint address = Pools[1].Bank;
        frame = frame with { State = frame.State.WriteWord(0x2655410, gate) };
        frame = Write(frame, address, Read(frame, address).WriteByte(0x2B, 1));
        var packets = Packets();
        packets = packets with { Headers = packets.Headers.SetItem(1, packets.Headers[1] with { UnknownByteMask = packets.Headers[1].UnknownByteMask | 1u << 16 }) };
        List<NativeBattleStare45Operation> operations = [];
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations) with { ReadPackets = _ => packets }).Frame;
        Assert.Equal(contact ? 2 : 1, Read(actual, address).ReadByte(0x29));
        Assert.Equal(contact ? 1 : 0, operations.OfType<NativeBattleStare45Contact506690>().Count());
        if (contact)
        {
            Assert.Equal(2, Assert.Single(operations.OfType<NativeBattleStare45Contact506690>()).ContactIndex);
        }
        Assert.IsType<NativeBattleStare45UpdateTargetBounds8DC610_8DC870>(operations[0]);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Target_initialization_clears_actual_object_and_copies_recipient_packed_W()
    {
        var frame = Frame(1, 0x836990, 0);
        frame = frame with { State = frame.State.WriteView(0x26553C8, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xAA, 84).ToArray()), 84) };
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(new byte[8], actual.State.ReadBytes(0x26553C8, 8));
        Assert.Equal(PointView(new(100, 200, 300), 444).Read(0, 8), actual.State.ReadBytes(0x26553D0, 8));
        Assert.Equal((ushort)0, actual.State.ReadWord(0x265540C));
        Assert.Equal(0x836AC0u, Read(actual, Pools[2].Bank).Callback);
        Assert.Equal(1, Read(actual, Pools[1].Bank).ReadByte(0x29));
    }

    [Fact]
    public void Recipient_programme_waits_through_old_fourteen_then_retains_one_stateful_F1_snapshot()
    {
        var frame = Frame(3, 0x839590, 0);
        uint address = Pools[3].Bank;
        frame = Write(frame, address, Read(frame, address).WriteDword(0x74, 0x17C6A14).WriteDword(0x78, 572).WriteWord(0x24, 14).WriteWord(0x80, 1).MarkPayloadUnknown(0x22, 2));
        int queries = 0;
        var inputs = Inputs([]) with
        {
            ReadBone502170 = (current, actor, selector, distance) =>
            {
                Assert.Equal(0, actor);
                Assert.Equal(0xF1, selector);
                Assert.Equal(0, distance);
                queries++;
                return new(current with { State = current.State.WriteDword(0x2655750, 0x12345678) }, PointView(new(71, -82, 93), 0x4567));
            }
        };
        frame = AdvanceTask(frame, address, Resources(), inputs).Frame;
        Assert.Equal(0, queries);
        Assert.Equal(0, Read(frame, address).ReadByte(0x29));
        frame = AdvanceTask(frame, address, Resources(), inputs).Frame;
        Assert.Equal(1, queries);
        Assert.Equal(1, Read(frame, address).ReadByte(0x29));
        Assert.Equal(0x12345678u, frame.State.ReadDword(0x2655750));
        Assert.Equal(PointView(new(71, -82, 93)).Read(0, 6), TaskView(Read(frame, address), 0x1C, 6).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => Read(frame, address).ReadWord(0x22));
        frame = AdvanceTask(frame, address, Resources(), inputs with { ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException("Recipient F1 must not refresh after its initialization callback.") }).Frame;
        Assert.Equal(1, queries);
        Assert.Equal(PointView(new(71, -82, 93)).Read(0, 6), TaskView(Read(frame, address), 0x1C, 6).Read(0, 6));
    }

    [Fact]
    public void Enabled_contact_requires_known_physical_pointer_even_when_declared_count_is_unused()
    {
        var frame = Frame(1, 0x836990, 1);
        frame = frame with { State = frame.State.WriteWord(0x2655410, 1) };
        var packets = Packets();
        packets = packets with { Headers = packets.Headers.SetItem(1, packets.Headers[1] with { FirstContactIndex = null, UnknownByteMask = packets.Headers[1].UnknownByteMask | 0xF00 }) };
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs([]) with { ReadPackets = _ => packets }));
    }

    [Fact]
    public void Stage_zeroes_four_group_RGB_and_depth_then_fades_with_signed_WORD_clamps()
    {
        var frame = Frame(4, 0x839690, 0);
        uint address = Pools[4].Bank;
        List<NativeBattleStare45Operation> operations = [];
        frame = AdvanceTask(frame, address, Resources(), Inputs(operations)).Frame;
        Assert.Equal(Enumerable.Range(0, 4), operations.OfType<NativeBattleStare45StageColorDepth>().Select(op => op.Group));
        Assert.Equal(Enumerable.Range(0, 4), operations.OfType<NativeBattleStare45StageRgb>().Select(op => op.Group));
        Assert.All(operations.OfType<NativeBattleStare45StageRgb>(), op => Assert.Equal((0, 0, 0), ((int)op.R, (int)op.G, (int)op.B)));
        frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 1400));
        operations.Clear();
        frame = AdvanceTask(frame, address, Resources(), Inputs(operations)).Frame;
        Assert.Equal((ushort)1536, Read(frame, address).ReadWord(0x1C));
        Assert.Equal(2, Read(frame, address).ReadByte(0x29));
        Assert.Equal(4, operations.Count);
        Assert.All(operations.Cast<NativeBattleStare45StageColorDepth>(), op => Assert.Equal((short)1536, op.Depth));
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 3).WriteWord(0x1C, 200).MarkPayloadUnknown(0x27, 1));
        var result = AdvanceTask(frame, address, Resources(), Inputs([]));
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)0, Read(result.Frame, address).ReadWord(0x1C));
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, address).ReadByte(0x27));
    }

    [Theory]
    [InlineData((ushort)40, false)]
    [InlineData((ushort)41, true)]
    [InlineData((ushort)65535, false)]
    public void Stage_wait_uses_old_signed_WORD_age(ushort age, bool next)
    {
        var frame = Frame(4, 0x839690, 2);
        uint address = Pools[4].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, age));
        var actual = AdvanceTask(frame, address, Resources(), Inputs([])).Frame;
        Assert.Equal(next ? 3 : 2, Read(actual, address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, 0x8366E0u)]
    [InlineData(1, 0x836990u)]
    [InlineData(2, 0x836AC0u)]
    [InlineData(3, 0x836D60u)]
    [InlineData(3, 0x839590u)]
    [InlineData(4, 0x839690u)]
    public void Six_tables_reject_negative_signed_phase(int pool, uint callback)
    {
        var frame = Frame(pool, callback, 255);
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, Pools[pool].Bank, Resources(), Inputs([])));
    }

    [Fact]
    public void Completion_detaches_all_six_physical_banks_and_survives_JSON_warm_reentry()
    {
        var frame = Frame(0, RootCallback, 9);
        frame = Write(frame, RootBank, Read(frame, RootBank).MarkPayloadUnknown(0x27, 1));
        var actual = Advance(frame, Resources(), Inputs([]));
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.All(actual.State.InactiveBanks, bank => Assert.NotNull(bank));
        actual = NativeBattleTestSnapshots.RestoreJson<NativeBattleStare45Frame>(NativeBattleTestSnapshots.Json(actual))!;
        var warm = Initialize(actual, Resources(), Inputs([]));
        Assert.Equal(RootDescriptor, warm.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)0, Read(warm, RootBank).ReadWord(0x26));
        Assert.Equal(frame.Random, warm.Random);
    }

    private static NativeBattleStare45Frame Frame(int pool, uint callback, byte phase)
    {
        var frame = Initialize(Inactive(1), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 10));
        uint address = RootBank;
        if (pool != 0)
        {
            (frame, address) = Append(frame, Pools[pool].Descriptor, callback, Pools[pool].Stride, RootBank, Packets());
        }
        return Write(frame, address, Read(frame, address).WriteByte(0x29, phase));
    }

    private static NativeBattleStare45Frame Inactive(byte flags)
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(45, 44, new(45, Entry, Initializer), new(3, flags, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleStare45State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleStare45Inputs Inputs(List<NativeBattleStare45Operation> operations) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 2) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(100, 200, 300), 444, 777, 4096) }, (current, actor, selector, distance) => new(current, PointView(new((short)actor, (short)selector, (short)distance))), _ => Packets(), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 84, 45, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 84, 45, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(1, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return packets.WritePlayerRepeatMode(1, 1);
    }

    private static NativeBattleStare45Resources Resources() => NativeBattleStare45StateTests.Resources();
}
