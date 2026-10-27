using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect68ControllerTests
{
    [Theory]
    [InlineData(0, 0x20E3AB8u)]
    [InlineData(1, 0x20DFAB8u)]
    public void Root_selects_the_previous_clock_parity_and_advances_while_paused(ushort age, uint expected)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, age).WriteWord(0x24, 65535));
        NativeBattleEffect68Step step = AdvanceRoot(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(expected, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(unchecked((ushort)(age + 1)), Read(step.Frame, RootBank).ReadWord(0x5C));
        Assert.Equal(0, Read(step.Frame, RootBank).ReadWord(0x24));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadByte(0x29));
        Assert.IsType<NativeBattleEffect68UpdateCaster8DC740>(Assert.Single(harness.Operations));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Root_tests_the_previous_survivor_count_before_scanning_children()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 3).WriteWord(0x5E, 1));
        NativeBattleEffect68Step first = AdvanceRoot(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, first.Result);
        Assert.Equal(3, Read(first.Frame, RootBank).ReadByte(0x29));
        Assert.Equal(0, Read(first.Frame, RootBank).ReadWord(0x5E));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect68SetMode4A8480>());
        NativeBattleEffect68Step second = AdvanceRoot(first.Frame, RootBank, null!, harness.Inputs());
        Assert.Equal(2, second.Result);
        Assert.Equal(new NativeBattleEffect68SetMode4A8480(1), Assert.Single(harness.Operations.OfType<NativeBattleEffect68SetMode4A8480>()));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 3)]
    [InlineData(255, 0, 1)]
    public void Root_uses_a_signed_header_offset_for_its_next_driver(byte group, short maximum, byte expectedPhase)
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 2).WriteByte(0x2A, group).WriteByte(0x63, 0).WriteWord(0x58, unchecked((ushort)maximum)));
        NativeBattleEffect68Step step = AdvanceRoot(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(expectedPhase, Read(step.Frame, RootBank).ReadByte(0x29));
        Assert.Equal(expectedPhase == 1 ? unchecked((byte)(group + 1)) : group, Read(step.Frame, RootBank).ReadByte(0x2A));
    }

    [Fact]
    public void Driver_delivers_the_current_header_contact_after_the_last_child()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        (frame, uint driver) = AppendHierarchy(frame, 1, DriverCallback, 0x58, RootBank, harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 1));
        frame = Write(frame, driver, Read(frame, driver).WriteByte(0x29, 1).WriteWord(0x24, 10));
        NativeBattleEffect68Step step = AdvanceDriver(frame, driver, null!, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(0, Read(step.Frame, RootBank).ReadByte(0x63));
        Assert.Equal(0, Read(step.Frame, RootBank).ReadByte(0x28));
        Assert.Equal(new NativeBattleEffect68Contact506690(0), Assert.Single(harness.Operations.OfType<NativeBattleEffect68Contact506690>()));
        Assert.Collection(harness.Operations, operation => Assert.IsType<NativeBattleEffect68UpdateTarget8DC610>(operation), operation => Assert.IsType<NativeBattleEffect68UpdateBounds8DC870>(operation), operation => Assert.IsType<NativeBattleEffect68Contact506690>(operation));
    }

    [Fact]
    public void Driver_waits_for_children_and_preserves_the_parent_completion_byte()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        (frame, uint driver) = AppendHierarchy(frame, 1, DriverCallback, 0x58, RootBank, harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 1));
        frame = Write(frame, driver, Read(frame, driver).WriteByte(0x29, 1).WriteByte(0x28, 1).WriteWord(0x24, 10));
        NativeBattleEffect68Step step = AdvanceDriver(frame, driver, null!, harness.Inputs());
        Assert.Equal(0, step.Result);
        Assert.Equal(1, Read(step.Frame, RootBank).ReadByte(0x63));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect68Contact506690>());
    }

    [Fact]
    public void Result_builder_preserves_the_distinct_scan40_header_and_updates_only_shared_strings()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        NativeBattleScanProcess process = frame.Memory.Scan!;
        NativeBattleScanResultMemory old = process.Results with { Count = 7, Empty = 0xA7, Element8 = process.Results.Element8.WriteByte(7, 0x93), Pages32 = process.Results.Pages32.WriteDword(28, 0x12345678) };
        frame = frame with { Memory = frame.Memory with { Scan = process with { Results = old } }, State = frame.State.WriteDword(ResultPagesAddress + 28, 0xAABBCCDD).Write(ResultElementAddress + 7, [0x77]) };
        ScanResultTestSource source = new();
        NativeBattleEffect68ResultStep step = NativeBattleEffect68Results.BuildB67EF0(frame, 0, source.Services);
        NativeBattleScanResultMemory actual = step.Frame.Memory.Scan!.Results;
        Assert.Equal(1, step.Count);
        Assert.Equal(1u, step.Frame.State.ReadDword(ResultCountAddress));
        Assert.Equal(old.Count, actual.Count);
        Assert.Equal(old.Empty, actual.Empty);
        Assert.Equal(old.Element8, actual.Element8);
        Assert.Equal(old.Pages32, actual.Pages32);
        Assert.NotEqual(old.Cursor, actual.Cursor);
        Assert.Equal(0xAABBCCDDu, step.Frame.State.ReadDword(ResultPagesAddress + 28));
        Assert.Equal(0x77, step.Frame.State.Read(ResultElementAddress + 7, 1)[0]);
        Assert.Equal(NativeBattleScanResults.RingAddress, NativeBattleEffect68Results.PageB68370(step.Frame, 0));
    }

    [Fact]
    public void Result_builder_overwrites_an_unknown_previous_count_and_round_trips_the_next_publication()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = frame with { State = frame.State.WriteView(ResultCountAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 4) };
        ScanResultTestSource source = new();
        frame = NativeBattleEffect68Results.BuildB67EF0(frame, 0, source.Services).Frame;
        NativeBattleEffect68Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect68Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        NativeBattleEffect68ResultStep direct = NativeBattleEffect68Results.BuildB67EF0(frame, 0, source.Services);
        NativeBattleEffect68ResultStep next = NativeBattleEffect68Results.BuildB67EF0(restored, 0, source.Services);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(direct)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next)));
    }

    [Fact]
    public void Retirement_returns_actual_residual_bank_bytes_to_the_inactive_owner()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 3).WriteByte(0x60, 0xA7));
        NativeBattleEffect68Frame retired = Advance(frame, null!, harness.Inputs());
        Assert.Empty(retired.Effect.Pools);
        Assert.Equal(0u, retired.Effect.ReturnedDescriptor);
        Assert.Equal(0xA7, retired.State.ReadBank(0).Read(0x60, 1)[0]);
        Assert.Equal(1, retired.State.ReadBank(0).Read(0x24, 1)[0]);
    }

    [Fact]
    public void XYZ_word_publication_preserves_the_retained_point_W_knownness()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, [0xA5]);
        NativeBattleEffectArena point = PutPoint(retained, 0, new(unchecked((short)0x12345), -2, unchecked((short)-32769)));
        Assert.Equal(new byte[] { 0x45, 0x23, 0xFE, 0xFF, 0xFF, 0x7F, 0xA5 }, point.Read(0, 7));
        Assert.Throws<NotSupportedException>(() => point.Read(7, 1));
    }

    [Fact]
    public void Invalid_signed_phase_fails_before_a_new_controller_phase_is_invented()
    {
        Harness harness = new();
        NativeBattleEffect68Frame frame = Frame(harness);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 0xFF));
        Assert.Throws<InvalidDataException>(() => AdvanceRoot(frame, RootBank, null!, harness.Inputs()));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect68SetMode4A8480>());
    }

    private static NativeBattleEffect68Frame Frame(Harness harness)
    {
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        NativeBattleOriginalEffectState effect = new(68, 67, new(68, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null) { Pools = pools, ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect68State state = NativeBattleEffect68State.FromColdProcess().WriteDword(FirstPacketGlobal, 0x20DFAB8).WriteDword(SecondPacketGlobal, 0x20E3AB8);
        for (int index = 0; index < Pools.Length; index++)
        {
            state = state.DetachBank(index);
        }
        NativeBattleEffect68Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Scan = NativeBattleScanProcess.FromColdProcess() }, effect, new(42), state);
        (frame, _) = AppendHierarchy(frame, 0, RootCallback, 0x64, 0, harness.Inputs());
        return Write(frame, RootBank, Read(frame, RootBank).WriteDword(12, 0x1D99A78));
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public List<NativeBattleEffect68Operation> Operations { get; } = [];
        private readonly NativeBattleActionPacketsState packets = Packets();
        public NativeBattleEffect68Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, _ => Flags, null!, null!, null!, null!, null!, null!, null!, _ => 0, null!, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 11, -1, 135, 68, null));
            (packets, _) = packets.AppendContact(new(new(3, 3, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
