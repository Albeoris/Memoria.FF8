using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Controller;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48ControllerTests
{
    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, false)]
    [InlineData((byte)2, true)]
    [InlineData((byte)3, false)]
    public void Entry_uses_mask_one_and_full_resets_six_exclusive_banks(byte flags, bool texture)
    {
        var frame = Inactive(flags);
        frame = frame with { State = frame.State.WriteDword(0x26495E0, 77).WriteDword(0x26489AC, 99) with { InactiveBanks = Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().Write(pool.Stride * pool.Capacity - 2, new byte[] { 0xAA, 0xBB })).ToImmutableArray() } };
        var actual = Initialize(frame, Resources(), Inputs([]));
        Assert.Equal(6, actual.Effect.Pools.Length);
        Assert.All(actual.State.InactiveBanks, bank => Assert.Null(bank));
        Assert.Equal(RootDescriptor, actual.Effect.ReturnedDescriptor);
        Assert.Equal((byte)3, Read(actual, RootBank).ReadByte(0x2C));
        Assert.Equal((byte)0, Read(actual, RootBank).ReadByte(0x2D));
        Assert.Equal((ushort)2, Read(actual, RootBank).ReadWord(0x5A));
        Assert.Equal((byte)1, Read(actual, RootBank).ReadByte(0x2F));
        foreach (var pool in actual.Effect.Pools)
        {
            Assert.Equal((ushort)0, pool.Slots[^1].ReadWord(pool.Stride - 2));
        }
        Assert.Equal(0u, actual.State.ReadDword(0x26495E0));
        Assert.Equal(0u, actual.State.ReadDword(0x26489AC));
        uint source = NativeBattleEffectNodeMemory.ArenaAddress + 0x30000u;
        Assert.Equal(source, actual.State.ReadDword(InitializerGlobal));
        Assert.Equal(source + 0x6000, actual.State.ReadDword(0x26495B8));
        Assert.Equal(source + 0xC000, actual.State.ReadDword(0x26495E4));
        NativeBattleEffectTextureState textures = actual.Memory.Textures ?? throw new InvalidDataException();
        Assert.Equal(texture ? 1 : 0, textures.Count);
        Assert.Equal(texture, textures.Animation is not null);
        Assert.Same(frame.Memory.Arena, actual.Memory.Arena);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)0, 0x36000u, 0x3C000u)]
    [InlineData((ushort)1, 0x30000u, 0x36000u)]
    [InlineData((ushort)65535, 0x30000u, 0x36000u)]
    public void Root_uses_old_WORD_parity_and_actual_packed_camera_without_pause(ushort age, uint primary, uint secondary)
    {
        var frame = Frame(0, RootCallback, 10);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, age).WriteWord(0x24, 65535));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1234 }, null);
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs(operations) with { GraphicsFlags = 0x201, ReadView = _ => view });
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + primary, actual.Frame.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + secondary, actual.Frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual.Frame, RootBank).ReadWord(0x5C));
        Assert.Equal((ushort)0, Read(actual.Frame, RootBank).ReadWord(0x24));
        Assert.Equal(view, actual.Frame.Memory.SharedEffectView2793E58);
        Assert.False(actual.Frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.Equal(new NativeBattleMagmaBreath48UpdateCaster8DC740(RootBank), Assert.Single(operations));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Root_allocates_exact_COMMON_node_regions_and_stage_runs_in_same_scan()
    {
        var frame = Frame(0, RootCallback, 2);
        uint origin = NativeBattleEffectNodeMemory.ArenaAddress + 0x50000;
        frame = frame with { State = frame.State.WriteDword(0x26495E4, origin), Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x50000, new byte[] { 9 }).Write(0x50000 + 0x10EF0, new byte[] { 88 }) } };
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = Advance(frame, Resources(), Inputs(operations));
        Assert.Equal(origin, actual.State.ReadDword(EmitterPointerGlobal));
        Assert.Equal(origin + 0x870, actual.State.ReadDword(GeometryPointerGlobal));
        Assert.Equal(origin + 0x10EF0, actual.State.ReadDword(0x26495E4));
        Assert.Equal(new byte[] { 0 }, actual.Memory.Arena!.Read(0x50000, 1));
        Assert.Equal(new byte[] { 88 }, actual.Memory.Arena.Read(0x50000 + 0x10EF0, 1));
        Assert.Equal((byte)1, Read(actual, Pools[5].Bank).ReadByte(0x29));
        Assert.Equal((ushort)1, Read(actual, RootBank).ReadWord(0x5E));
        Assert.Equal(4, operations.OfType<NativeBattleMagmaBreath48StageRgb>().Count());
        Assert.Equal(4, operations.OfType<NativeBattleMagmaBreath48StageColorDepth>().Count());
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Target_full_append_clear_precedes_descriptor_fields_and_real_parent_ownership()
    {
        var frame = Frame(1, 0x825170, 0);
        frame = Write(frame, Pools[3].Bank, Read(frame, Pools[3].Bank).WriteWord(0x29A, 0xBEEF).MarkPayloadUnknown(0x29B, 1));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs(operations));
        var engine = Read(actual.Frame, Pools[3].Bank);
        Assert.Equal(0x15B7C88u, engine.ReadDword(0x30));
        Assert.Equal((ushort)15, engine.ReadWord(0x298));
        Assert.Equal((ushort)0, engine.ReadWord(0x29C));
        Assert.Equal((ushort)40, engine.ReadWord(0x29E));
        Assert.Equal((ushort)0, engine.ReadWord(0x29A));
        Assert.Equal(RootBank, engine.ReadDword(0x10));
        Assert.Equal(Pools[1].Bank, engine.ReadDword(0x18));
        Assert.Equal((byte)2, Read(actual.Frame, Pools[1].Bank).ReadByte(0x28));
        Assert.Equal(0x82A540u, Read(actual.Frame, Pools[4].Bank).Callback);
        Assert.Equal(new NativeBattleMagmaBreath48UpdateTargetBounds8DC610_8DC870(Pools[1].Bank), operations[0]);
        Assert.Equal(new NativeBattleMagmaBreath48Sound501330(0x641B8, 0, 128), operations[1]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData((ushort)27, false)]
    [InlineData((ushort)28, true)]
    [InlineData((ushort)29, false)]
    [InlineData((ushort)65535, false)]
    public void Target_releases_next_group_only_at_exact_old28_before_contact(ushort age, bool release)
    {
        var frame = Frame(1, 0x825170, 1);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 1));
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteWord(0x24, age));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[1].Bank, Resources(), Inputs(operations));
        Assert.Equal((byte)(release ? 0 : 1), Read(actual.Frame, RootBank).ReadByte(0x63));
        Assert.Equal((byte)(release ? 2 : 1), Read(actual.Frame, Pools[1].Bank).ReadByte(0x29));
        Assert.Empty(operations.OfType<NativeBattleMagmaBreath48Contact506690>());
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual.Frame, Pools[1].Bank).ReadWord(0x24));
    }

    [Theory]
    [InlineData((ushort)54, false)]
    [InlineData((ushort)55, true)]
    [InlineData((ushort)32767, true)]
    [InlineData((ushort)32768, false)]
    public void Target_contact_is_signed_old55_single_physical_read_without_count(ushort age, bool contact)
    {
        var frame = Frame(1, 0x825170, 2);
        uint address = Pools[1].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, age).WriteByte(0x2B, 1));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var packets = Packets();
        packets = packets with { ContactCount = 0, Headers = packets.Headers.SetItem(1, packets.Headers[1] with { UnknownByteMask = 1u << 16 }) };
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations) with { Packets = packets });
        Assert.Equal(contact ? 2 : 0, actual.Result);
        if (contact)
        {
            var selected = Assert.Single(operations.OfType<NativeBattleMagmaBreath48Contact506690>());
            Assert.Equal(2, selected.ContactIndex);
            Assert.Equal((byte)1, packets.ReadContact(selected.ContactIndex).Primary.ActorIndex);
            Assert.Equal((byte)4, packets.ReadContact(selected.ContactIndex).Secondary.ActorIndex);
        }
        else
        {
            Assert.Empty(operations.OfType<NativeBattleMagmaBreath48Contact506690>());
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0x825050u, (ushort)768, (short)1024)]
    [InlineData(0x82A6A0u, (ushort)256, (short)0)]
    public void Stage_terminal_update_publishes_exact_four_groups_and_preserves_done_neighbor(uint callback, ushort prior, short expected)
    {
        var frame = Frame(5, callback, 1);
        uint address = Pools[5].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, prior).MarkPayloadUnknown(0x27, 1));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations));
        Assert.Equal(2, actual.Result);
        Assert.Equal((byte)2, Read(actual.Frame, address).ReadByte(0x29));
        Assert.Throws<NotSupportedException>(() => Read(actual.Frame, address).ReadByte(0x27));
        Assert.Equal(Enumerable.Range(0, 4).Select(group => new NativeBattleMagmaBreath48StageColorDepth(group, expected)), operations.Cast<NativeBattleMagmaBreath48StageColorDepth>());
    }

    [Theory]
    [InlineData((ushort)49, 7, 50u)]
    [InlineData((ushort)50, 8, 50u)]
    public void Color_FE_wait_uses_signed_old_age_then_writes_exact_mixed_packets(ushort age, int index, uint color)
    {
        var frame = Frame(2, 0x825220, 0);
        uint address = Pools[2].Bank;
        frame = Write(frame, address, Read(frame, address).WriteDword(0x30, 0x15ADC28).WriteWord(0x38, 7).WriteDword(0x34, color).WriteWord(0x24, age));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations));
        Assert.Equal((ushort)index, Read(actual.Frame, address).ReadWord(0x38));
        var batch = Assert.IsType<NativeBattleMagmaBreath48SubmitPackets>(Assert.Single(operations)).Batch;
        Assert.Equal(36, batch.AllocationBytes);
        Assert.Equal(new ushort[] { 1, 0 }, batch.Primitives.Select(item => item.Flags20));
        Assert.Equal(new uint[] { 0x05000000, 0x2A000032, 0, 320, 0xD80000, 0xD80140 }, batch.Primitives[0].Primitive.Words);
        Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[1].Primitive.Words);
        Assert.All(batch.Primitives, item => Assert.Equal(7, item.Primitive.Bucket));
        var captured = batch.CapturedWrites!;
        Assert.All(captured.Attempts[0].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(captured.Attempts[1].WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(captured.Attempts[1].WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Color_FF_completes_without_packet_or_requiring_retained_color()
    {
        var frame = Frame(2, 0x825220, 0);
        uint address = Pools[2].Bank;
        frame = Write(frame, address, Read(frame, address).WriteDword(0x30, 0x15ADC28).WriteWord(0x38, 15).MarkPayloadUnknown(0x34, 4));
        List<NativeBattleMagmaBreath48Operation> operations = [];
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations));
        Assert.Equal(2, actual.Result);
        Assert.Empty(operations);
        Assert.Throws<NotSupportedException>(() => Read(actual.Frame, address).ReadDword(0x34));
    }

    [Fact]
    public void Cold_drain_detaches_exact_six_banks_and_survives_json_without_extra_completion_handshake()
    {
        var frame = Frame(0, RootCallback, 9);
        frame = Write(frame, RootBank, Read(frame, RootBank).MarkPayloadUnknown(0x27, 1));
        var actual = Advance(frame, Resources(), Inputs([]));
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.All(actual.State.InactiveBanks, bank => Assert.NotNull(bank));
        var restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleMagmaBreath48Frame>(SnapshotJson(actual))!);
        Assert.Equal(actual.Random, restored.Random);
        Assert.Equal(actual.State.InactiveBanks[0]!.Read(0x29, 1), restored.State.InactiveBanks[0]!.Read(0x29, 1));
        Assert.Equal(restored, Advance(restored, Resources(), Inputs([])));
    }

    [Theory]
    [InlineData(0, RootCallback)]
    [InlineData(1, 0x825170u)]
    [InlineData(2, 0x825220u)]
    [InlineData(3, 0x8253B0u)]
    [InlineData(4, 0x82A540u)]
    [InlineData(5, 0x825050u)]
    [InlineData(5, 0x82A6A0u)]
    public void Signed_negative_phase_rejects_each_actual_table(int pool, uint callback)
    {
        var frame = Frame(pool, callback, 255);
        if (pool == 2)
        {
            //The color callback is a stream, not a phase table; its signed stream index is the selector.
            frame = Write(frame, Pools[pool].Bank, Read(frame, Pools[pool].Bank).WriteDword(0x30, 0x15ADC28).WriteWord(0x38, 65535));
            Assert.Throws<InvalidDataException>(() => AdvanceTask(frame, Pools[pool].Bank, Resources(), Inputs([])));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, Pools[pool].Bank, Resources(), Inputs([])));
        }
    }

    [Theory]
    [InlineData((byte)0, (short)1, (byte)3, (byte)1)]
    [InlineData((byte)1, (short)1, (byte)5, (byte)1)]
    public void Root_repeat_compares_signed_selector_and_sets_real27_countdown(byte selector, short groups, byte phase, byte resultingSelector)
    {
        var frame = Frame(0, RootCallback, 4);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 0).WriteByte(0x2A, selector).WriteWord(0x58, unchecked((ushort)groups)));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]));
        Assert.Equal(phase, Read(actual.Frame, RootBank).ReadByte(0x29));
        Assert.Equal(resultingSelector, Read(actual.Frame, RootBank).ReadByte(0x2A));
        if (phase == 5)
        {
            Assert.Equal((ushort)27, Read(actual.Frame, RootBank).ReadWord(0x60));
        }
    }

    [Theory]
    [InlineData((ushort)2, false)]
    [InlineData((ushort)1, true)]
    public void Root_countdown_decrements_before_comparison_and_runs_new_exit_child(ushort remaining, bool append)
    {
        var frame = Frame(0, RootCallback, 5);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x60, remaining));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]));
        Assert.Equal((byte)(append ? 6 : 5), Read(actual.Frame, RootBank).ReadByte(0x29));
        Assert.Equal(append, actual.Frame.Effect.Pools[5].Head is not null);
        if (append)
        {
            Assert.Equal((ushort)1024, Read(actual.Frame, Pools[5].Bank).ReadWord(0x1C));
            Assert.Equal((byte)1, Read(actual.Frame, Pools[5].Bank).ReadByte(0x29));
        }
    }

    [Fact]
    public void Root_wait_uses_previous_pass_count_before_rescanning_children()
    {
        var frame = Frame(4, 0x82A540, 1);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 6).WriteWord(0x5E, 0));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]));
        Assert.Equal((byte)7, Read(actual.Frame, RootBank).ReadByte(0x29));
        Assert.Equal((ushort)1, Read(actual.Frame, RootBank).ReadWord(0x5E));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)32768)]
    [InlineData((ushort)65535)]
    public void Engine_delay_is_signed_WORD_and_does_not_read_unreached_sources(ushort age)
    {
        var frame = Frame(3, 0x8253B0, 0);
        uint address = Pools[3].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, age).WriteWord(0x298, 15).MarkPayloadUnknown(0x30, 4));
        var actual = AdvanceTask(frame, address, Resources(), Inputs([]));
        Assert.Equal((byte)0, Read(actual.Frame, address).ReadByte(0x29));
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual.Frame, address).ReadWord(0x24));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    private static NativeBattleMagmaBreath48Frame Frame(int pool, uint callback, byte phase)
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

    private static NativeBattleMagmaBreath48Frame Inactive(byte flags)
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(48, 47, new(48, Entry, Initializer), new(3, flags, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleMagmaBreath48State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleMagmaBreath48Inputs Inputs(List<NativeBattleMagmaBreath48Operation> operations) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 2), (frame, actor, selector, distance) => new(frame, PointView(new((short)actor, (short)selector, (short)distance))), _ => 0, (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 120, 48, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 120, 48, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(1, 4, 0, 0, 0, 7, 0), new(4, 4, 0, 0, 0, 3, 0)));
        (packets, _) = packets.Publish(2);
        return packets.WritePlayerRepeatMode(1, 1);
    }

    private static NativeBattleMagmaBreath48Resources Resources() => NativeBattleMagmaBreath48StateTests.Resources();

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
