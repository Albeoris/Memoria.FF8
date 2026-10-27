using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76ControllerTests
{
    [Fact]
    public void Startup_saves_actual_point8_then_requests_music_without_a_synthetic_completion()
    {
        Harness harness = new();
        NativeBattleEffect76Frame frame = Frame(MainCallback, 0);
        NativeBattleEffect76Step step = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(1, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(harness.Position.Read(0, 6), step.Frame.State.Read(SavedPointAddress, 6));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.Read(SavedPointAddress + 6, 2));
        Assert.Equal(0, step.Frame.State.Read(MusicCompletionGlobal, 1)[0]);
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect76Stage5713E0(4096, 0), operation),
            operation => Assert.Equal(new NativeBattleEffect76ModelPointY(3, 5000, false), operation),
            operation => Assert.Equal(new NativeBattleEffect76ModelFlags(3, 255, 4, false), operation),
            operation => Assert.Equal(new NativeBattleEffect76SetMode4A8480(0), operation),
            operation => Assert.Equal(new NativeBattleEffect76MusicLoad501A20(MusicAddress, MusicCompletionGlobal), operation));
    }

    [Fact]
    public void Startup_waits_on_the_current_music_completion_byte_at_its_actual_clock()
    {
        Harness harness = new();
        NativeBattleEffect76Frame frame = Frame(MainCallback, 10);
        NativeBattleEffect76Step waiting = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(10, Read(waiting.Frame, ChildBank).ReadWord(12));
        Assert.Empty(harness.Operations);
        frame = frame with { State = frame.State.Write(MusicCompletionGlobal, [1]) };
        NativeBattleEffect76Step ready = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect76QueueStage512AA0(1, 0x20DFAB8), Assert.Single(harness.Operations));
        Assert.Equal(11, Read(ready.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Startup_waits_at38_for_real_IO_and_preserves_the_returned_frame()
    {
        Harness harness = new() { IoResult = -1, MutateIoFrame = true };
        NativeBattleEffect76Frame frame = Frame(MainCallback, 38);
        frame = frame with { State = frame.State.Write(MusicCompletionGlobal, [1]) };
        NativeBattleEffect76Step step = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(38, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(77u, step.Frame.State.ReadDword(TargetGlobal));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Stage_reload_uses_index1_current_COMMON_then_current_Color44()
    {
        Harness harness = new();
        NativeBattleEffect76Frame frame = Frame(MainCallback, 38);
        frame = frame with { State = frame.State.Write(MusicCompletionGlobal, [1]) };
        NativeBattleEffect76Step step = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(39, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect76ReloadStage512AC0(1, 0x20DFAB8), operation),
            operation => Assert.Equal(new NativeBattleEffect76SyncModelColors61FC60(0x32ABCDEF), operation));
    }

    [Fact]
    public void Wave_request_has_only_three_consumed_arguments_and_appends_the_real_delay_task()
    {
        Harness harness = new();
        NativeBattleEffect76Frame frame = Frame(MainCallback, 39);
        frame = frame with { State = frame.State.Write(MusicCompletionGlobal, [1]) };
        NativeBattleEffect76Step step = AdvanceStartup61FA80(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect76WaveRequest46CBB0(0, "field2.fs", 127), Assert.Single(harness.Operations));
        Assert.Equal(2, step.Result);
        Assert.Equal(0x61FC90u, Read(step.Frame, ChildBank + ChildStride).Callback);
        Assert.Equal(0, Read(step.Frame, ChildBank + ChildStride).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, ChildBank + ChildStride).ReadWord(14));
        Assert.Equal(40, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Delay_advances_from49_to50_and_clears_only_the_next_task_clock_words()
    {
        Harness harness = new();
        NativeBattleEffect76Frame frame = Frame(0x61FC90, 49);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask retained = pool.Slots[1].WriteBytes(12, Enumerable.Repeat((byte)0xA7, ChildStride - 12).ToArray());
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(1, retained)) } };
        NativeBattleEffect76Step step = AdvanceDelay61FC90(frame, ChildBank, harness.Inputs());
        NativeBattleModulePoolTask next = Read(step.Frame, ChildBank + ChildStride);
        Assert.Equal(2, step.Result);
        Assert.Equal(DriverCallback, next.Callback);
        Assert.Equal(0, next.ReadWord(12));
        Assert.Equal(0, next.ReadWord(14));
        Assert.Equal(0xA7, next.ReadByte(16));
        Assert.Equal(1, Read(step.Frame, ChildBank).Next);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0x200)]
    public void Main_composes_before_pause_and_preserves_the_output_padding(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect76Frame frame = Frame(DriverCallback, 605);
        frame = frame with { State = frame.State.WriteWord(ViewAddress + 18, 0xA55A) };
        NativeBattleEffect76Step step = AdvanceMain61FCD0(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(605, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0xA55A, step.Frame.State.ReadWord(ViewAddress + 18));
        Assert.Equal(11, BitConverter.ToInt32(step.Frame.State.Read(ViewAddress + 20, 4)));
        Assert.Equal(22, BitConverter.ToInt32(step.Frame.State.Read(ViewAddress + 24, 4)));
        Assert.Equal(33, BitConverter.ToInt32(step.Frame.State.Read(ViewAddress + 28, 4)));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Sequence16_precedes_the_current_model_Y_decrement()
    {
        Harness harness = new();
        NativeBattleEffect76Step step = AdvanceMain61FCD0(Frame(DriverCallback, 160), ChildBank, null!, harness.Inputs());
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect76Sequence505C00(3, 16), operation),
            operation => Assert.Equal(new NativeBattleEffect76ModelPointY(3, -55, true), operation));
        Assert.Equal(161, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(270, 1)]
    [InlineData(350, 3)]
    [InlineData(430, 4)]
    public void Dialogue_uses_the_actual_original_actor_text_and_duration(short age, ushort text)
    {
        Harness harness = new();
        AdvanceMain61FCD0(Frame(DriverCallback, age), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect76Text4876F0(3, text, 60), Assert.Single(harness.Operations.OfType<NativeBattleEffect76Text4876F0>()));
        Assert.Equal(age == 270 ? 1 : 0, harness.Operations.OfType<NativeBattleEffect76MusicVolume501B10>().Count());
    }

    [Fact]
    public void Contact_is_consumed_at605_and_retirement_only_after_old665()
    {
        Harness harness = new();
        NativeBattleEffect76Step contact = AdvanceMain61FCD0(Frame(DriverCallback, 605), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect76Contact506690(0), Assert.Single(harness.Operations));
        Assert.Equal(0, contact.Result);
        harness.Operations.Clear();
        NativeBattleEffect76Step retired = AdvanceMain61FCD0(Frame(DriverCallback, 665), ChildBank, null!, harness.Inputs());
        Assert.Equal(666, Read(retired.Frame, ChildBank).ReadWord(12));
        Assert.Equal(2, retired.Result);
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect76Stage5713E0(0, 0), operation),
            operation => Assert.IsType<NativeBattleEffect76CaptureCameraRetirement>(operation),
            operation => Assert.Equal(new NativeBattleEffect76SetMode4A8480(1), operation),
            operation => Assert.Equal(new NativeBattleEffect76GraphicsFlags(0xFFFFFBFF, 0), operation));
    }

    [Theory]
    [InlineData(0x6200F0u, 0, 1)]
    [InlineData(0x620140u, 0, 0)]
    [InlineData(0x620140u, 1, 1)]
    public void Flash_draws_before_pause_with_its_native_first_age_policy(uint callback, short age, int draws)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect76Frame frame = Frame(callback, age);
        NativeBattleEffect76Step step = AdvanceWhite(frame, ChildBank, harness.Inputs());
        Assert.Equal(draws, harness.Operations.OfType<NativeBattleEffect76PcRender47CF50>().Count());
        Assert.Equal(unchecked((ushort)age), Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Root_selects_C000_private_bank_and_advances_while_the_child_is_paused()
    {
        Harness harness = new() { Flags = 1 };
        NativeBattleEffect76Frame frame = Frame(MainCallback, 0);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect76Step step = AdvanceRoot6225A0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0x20EBAB8u, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    private static NativeBattleEffect76Frame Frame(uint callback, short age)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(callback);
        children = children.WriteSlot(0, children.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteWord(14, 95));
        NativeBattleOriginalEffectState effect = new(76, 75, new(76, Entry, Initializer), new(3, 0, 8, 0, 0, 0), null) { Pools = [root, children], ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect76Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(42), NativeBattleEffect76State.FromColdProcess().DetachBank(0).DetachBank(1));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(CasterGlobal, 3) };
        return WriteBytes(frame, CapturedWorldAddress, PackedArena(NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 1, Y = 2, Z = 3 }, 0)), 32);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public int IoResult { get; init; }
        public bool MutateIoFrame { get; init; }
        public List<NativeBattleEffect76Operation> Operations { get; } = [];
        public NativeBattleEffectArena Position { get; } = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 11, 0, 22, 0, 33, 0 });
        private readonly NativeBattleActionPacketsState packets = Packets();
        public NativeBattleEffect76Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, frame => new(MutateIoFrame ? frame with { State = frame.State.WriteDword(TargetGlobal, 77) } : frame, IoResult), _ => packets, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 10, Y = 20, Z = 30 }, null), null!, (_, _) => NativeBattlePackedMatrix32.FromMatrix(Identity(), null), (_, _) => Position, null!, _ => new(new(1, 2, 3), new(4, 5, 6), 512, 0), _ => null, _ => null, (_, _) => false, (_, _) => 0x32ABCDEF, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 14, -1, 355, 76, null));
            (packets, _) = packets.AppendContact(new(new(3, 3, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
