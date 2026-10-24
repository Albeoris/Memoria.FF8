using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140ControllerTests
{
    private const uint Main = 0x20DFAB8;

    [Theory]
    [InlineData(1, 0, 3)]
    [InlineData(59, 58, 61)]
    [InlineData(61, 60, 82)]
    public void Early_clock_tail_updates_the_clock_before_the_remaining_timeline(short age, int relative, ushort finalAge)
    {
        Harness harness = new();
        NativeBattleEffect140Frame frame = AdvanceEarlyClock6A6A6E(Frame(age), Main, relative, 3690, harness.Inputs());
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(frame, Main, null!, harness.Inputs());
        Assert.Equal(finalAge, Read(step.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void Paused_early_clock_skips_the_inner_increment()
    {
        Harness harness = new();
        NativeBattleEffect140Frame frame = Frame(1);
        frame = frame with { State = frame.State.WriteDword(0x2517AAC, 1) };
        frame = AdvanceEarlyClock6A6A6E(frame, Main, 0, 3690, harness.Inputs());
        Assert.Equal((ushort)1, Read(frame, Main).ReadWord(12));
    }

    [Fact]
    public void First_timeline_pass_queues_the_original_files_before_advancing_and_sound()
    {
        Harness harness = new();
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(Frame(0), Main, null!, harness.Inputs());
        Assert.Equal(new short[] { 528, 529, 530, 531, 532 }, harness.Operations.OfType<NativeBattleEffect140QueueIo5341D0>().Select(operation => operation.FileId));
        Assert.Equal(new short[] { 1, 1, 16, 1, 3 }, harness.Operations.OfType<NativeBattleEffect140QueueIo5341D0>().Select(operation => operation.Type));
        Assert.All(harness.Operations.OfType<NativeBattleEffect140QueueIo5341D0>(), operation => Assert.Equal(Main + 0x1F7A0, operation.DestinationAddress));
        Assert.IsType<NativeBattleEffect140AdvanceIo534210>(harness.Operations[5]);
        Assert.Equal(new NativeBattleEffect140Sound501330(0x11D003C, 1, 128), harness.Operations[6]);
        Assert.Equal((ushort)1, Read(step.Frame, Main).ReadWord(12));
    }

    [Fact]
    public void At101_busy_IO_keeps_age_after_advance_and_retains_the_poll_frame()
    {
        Harness harness = new() { Busy = true };
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(Frame(101), Main, null!, harness.Inputs());
        Assert.IsType<NativeBattleEffect140AdvanceIo534210>(Assert.Single(harness.Operations));
        Assert.Equal((ushort)101, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(99u, step.Frame.State.ReadDword(0x2517B1C));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(194)]
    public void Reload_waits_return_before_the_unconditional_IO_advance(short age)
    {
        Harness harness = new() { Busy = true };
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(Frame(age), Main, null!, harness.Inputs());
        Assert.Empty(harness.Operations);
        Assert.Equal(unchecked((ushort)age), Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(99u, step.Frame.State.ReadDword(0x2517B1C));
    }

    [Fact]
    public void Ready150_queues_texture_and_raw_file_to_the_same_physical_destination()
    {
        Harness harness = new();
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(Frame(150), Main, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect140QueueIo5341D0(535, Main + 0x1F7A0, 1), harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect140QueueIo5341D0(536, Main + 0x1F7A0, 0), harness.Operations[1]);
        Assert.IsType<NativeBattleEffect140AdvanceIo534210>(harness.Operations[2]);
        Assert.Equal((ushort)151, Read(step.Frame, Main).ReadWord(12));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(161)]
    public void Stream_playback_uses_current_bank_at_both_native_clocks(short age)
    {
        Harness harness = new();
        AdvanceTimeline6A7119(Frame(age), Main, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect140Stream5018C0(128, 127), Assert.Single(harness.Operations.OfType<NativeBattleEffect140Stream5018C0>()));
    }

    [Fact]
    public void Auxiliary_completion_and_all_recipient_contact_are_distinct_clocks()
    {
        Harness harness = new();
        AdvanceTimeline6A7119(Frame(235), Main, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect140CommonTextureLoad508630(Main + 0x1F7A0, 0x2517B24), Assert.Single(harness.Operations.OfType<NativeBattleEffect140CommonTextureLoad508630>()));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect140ContactAll506BA0>());
        harness.Operations.Clear();
        AdvanceTimeline6A7119(Frame(245), Main, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect140ContactAll506BA0>());
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect140CommonTextureLoad508630>());
    }

    [Fact]
    public void Retirement_restores_saved_Color40_DWORD_and_clears_only0800()
    {
        Harness harness = new();
        NativeBattleEffect140Frame frame = Frame(254);
        frame = Write(frame, Main, Read(frame, Main).WriteWord(14, 1).WriteDword(0x14, ModelAddress(3)).WriteDword(0x3C, 0xEE123456));
        NativeBattleEffect140Step step = AdvanceTimeline6A7119(frame, Main, null!, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)255, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(new NativeBattleEffect140ModelFlags(3, 0xF7FF, 0), Assert.Single(harness.Operations.OfType<NativeBattleEffect140ModelFlags>()));
        Assert.Equal(new NativeBattleEffect140ModelColor40(3, 0xEE123456), Assert.Single(harness.Operations.OfType<NativeBattleEffect140ModelColor40>()));
        Assert.Equal(0u, step.Frame.State.ReadDword(0x2517B20));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public void Root_waits_on_actual_completion_after_main_and_particles_are_empty(bool completed, int result)
    {
        Harness harness = new();
        NativeBattleEffect140Frame frame = Frame(0);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2517AE8, 0x2517AF8, 20, 1);
        (root, _) = root.Append(0x6A6530);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 7).WriteByte(15, 1).WriteDword(16, 1)) };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x2517AD8, Main, 0x878, 1);
        frame = frame with { Effect = frame.Effect with { Pools = [root, main] }, State = frame.State.Write(0x2517B24, [(byte)(completed ? 1 : 0)]) };
        NativeBattleEffect140Step step = AdvanceRoot6A6530(frame, 0x2517AF8, null!, harness.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)7, Read(step.Frame, 0x2517AF8).ReadWord(12));
        Assert.Equal(Main + 0x3758, step.Frame.State.ReadDword(0x2517B0C));
        Assert.Equal(new NativeBattleEffect140Stage5713E0(0, 0), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Root_pause_retains_clock_but_switches_packet_bank()
    {
        Harness harness = new();
        NativeBattleEffect140Frame frame = Frame(0);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2517AE8, 0x2517AF8, 20, 1);
        (root, _) = root.Append(0x6A6530);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 1).WriteByte(15, 0).WriteDword(16, 0)) };
        frame = frame with { Effect = frame.Effect with { Pools = [root] }, State = frame.State.WriteDword(0x2517AAC, 1) };
        NativeBattleEffect140Step step = AdvanceRoot6A6530(frame, 0x2517AF8, null!, harness.Inputs());
        Assert.Equal((ushort)1, Read(step.Frame, 0x2517AF8).ReadWord(12));
        Assert.Equal(Main + 0x11758, step.Frame.State.ReadDword(0x2517B0C));
    }

    internal static NativeBattleEffect140Frame Frame(short age)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x2517AD8, Main, 0x878, 1);
        (main, _) = main.Append(0x6A6750);
        main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteBytes(12, new byte[0x878 - 12]).WriteWord(12, unchecked((ushort)age))) };
        NativeBattleOriginalEffectState effect = new(140, 139, new(140, 0x6A6300, 0x6A6310), new(0, 0, 240, 0, 0, 0), null) { Pools = [main], ReturnedDescriptor = 0x2517AE8 };
        NativeBattleEffect140State state = NativeBattleEffect140State.FromColdProcess().DetachBank().WriteDword(0x2517B28, Main).WriteDword(0x2517B18, 3690);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1), state) { Staging = NativeBattleFileStaging.Unknown().Rebase(0x1100000) };
    }

    private sealed class Harness
    {
        public bool Busy { get; init; }
        public List<NativeBattleEffect140Operation> Operations { get; } = [];
        public NativeBattleEffect140Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, null!, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattlePandemona291Restoration.Rotation701270(0), 0), null!, _ => new(new(1, 2, 5000), new(0, 0, 0), 512, 9), _ => 0x1234, _ => 0x5678, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        })
        {
            Poll534270 = frame => new(frame with { State = frame.State.WriteDword(0x2517B1C, 99) }, Busy)
        };
    }
}
