using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect209Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect209ControllerTests
{
    [Fact]
    public void Pause_bit_one_stops_before_current_IO_anchor_and_clock()
    {
        Harness harness = new() { Flags = 0x201, IoResult = -1 };
        NativeBattleEffect209Frame frame = Frame(0);
        NativeBattleEffect209Step step = AdvanceMain6375D0(frame, ChildBank, null!, harness.Inputs());
        Assert.Same(frame, step.Frame);
        Assert.Equal(0, harness.IoCalls);
        Assert.Empty(harness.Operations);
        Assert.Empty(harness.Points);
    }

    [Theory]
    [InlineData(-1, 17)]
    [InlineData(0, 18)]
    public void Pause_bit200_uses_actual_IO_result_instead_of_freezing_every_call(int result, int expectedAge)
    {
        Harness harness = new() { Flags = 0x200, IoResult = result };
        NativeBattleEffect209Step step = AdvanceMain6375D0(Frame(17), ChildBank, null!, harness.Inputs());
        Assert.Equal(expectedAge, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(1, harness.IoCalls);
    }

    [Fact]
    public void Age52_waits_before_variant_rebuild_color_capture_or_clock_then_retries_current_service()
    {
        Harness harness = new() { IoResult = -1 };
        NativeBattleEffect209Frame frame = Frame(52);
        NativeBattleEffect209Step waiting = AdvanceMain6375D0(frame, ChildBank, null!, harness.Inputs());
        Assert.Same(frame, waiting.Frame);
        Assert.Empty(harness.Operations);
        harness.IoResult = 0;
        NativeBattleEffect209Step resumed = AdvanceMain6375D0(waiting.Frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect209RebuildVariant50E3C0(0x209FAB8), Assert.Single(harness.Operations));
        Assert.Equal(0x80402010u, resumed.Frame.State.ReadDword(StageAfterColor40Global));
        Assert.Equal(0x80806040u, resumed.Frame.State.ReadDword(StageAfterColor44Global));
        Assert.Equal(53, Read(resumed.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Initial_anchor_preserves_W_and_saved_colors_before_exact_byte_flag_write()
    {
        Harness harness = new();
        NativeBattleEffect209Step step = AdvanceMain6375D0(Frame(0), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleVector(10, -1780, 30), Vector(TaskView(Read(step.Frame, ChildBank), 16, 8)));
        Assert.Equal(0xBEEF, Read(step.Frame, ChildBank).ReadWord(22));
        Assert.Equal(0x80A09080u, step.Frame.State.ReadDword(SavedColor40Global));
        Assert.Equal(0x80706050u, step.Frame.State.ReadDword(SavedColor44Global));
        Assert.Equal(new NativeBattleEffect209ModelFlags(3, 0xFF, 8, false), harness.Operations[0]);
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 65)]
    public void Native_visibility_release_only_clears_flag8_with_word_width(bool alternate, short age)
    {
        Harness harness = new();
        NativeBattleEffect209Step step = alternate ? AdvanceAlternate638BD0(Frame(age), ChildBank, null!, harness.Inputs()) : AdvanceMain6375D0(Frame(age), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect209ModelFlags(3, 0xFFF7, 0, true), Assert.Single(harness.Operations));
        Assert.Equal(age + 1, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 14)]
    public void Opening_uses_one_shared_angle_then_twelve_scale_draws_and_overwrites_point_W_with_mode(bool alternate, int active)
    {
        Harness harness = new();
        NativeBattleEffect209Frame frame = Frame(1);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteBytes(16, Point().Read(0, 8)));
        NativeCrtRandomState expected = frame.Random.Draw(out ushort seedAngle);
        for (int index = 0; index < 12; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect209Step step = alternate ? AdvanceAlternate638BD0(frame, ChildBank, null!, harness.Inputs()) : AdvanceMain6375D0(frame, ChildBank, null!, harness.Inputs());
        NativeBattleModulePoolTask[] stars = step.Frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x6385E0).ToArray();
        Assert.Equal(12, stars.Length);
        Assert.All(stars, task =>
        {
            Assert.Equal(seedAngle % 4096, task.ReadWord(24));
            Assert.Equal(alternate ? 1 : 0, task.ReadWord(22));
            Assert.Equal(0, task.ReadWord(14));
            Assert.InRange(task.ReadWord(32), (ushort)512, (ushort)682);
        });
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(active, step.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(new NativeBattleEffect209Sound501330(alternate ? AlternateSoundAddress : SoundAddress, 0, 128), harness.Operations.OfType<NativeBattleEffect209Sound501330>().Single());
    }

    [Theory]
    [InlineData(false, 31, true)]
    [InlineData(false, 30, false)]
    [InlineData(false, 91, true)]
    [InlineData(false, 101, false)]
    [InlineData(true, 73, true)]
    [InlineData(true, 71, false)]
    [InlineData(true, 85, true)]
    [InlineData(true, 91, false)]
    public void Periodic_sprite_birth_uses_exact_variant_window_and_five_CRT_draws(bool alternate, short age, bool expectedBirth)
    {
        Harness harness = new();
        NativeBattleEffect209Frame frame = Frame(age);
        NativeBattleEffect209Step step = alternate ? AdvanceAlternate638BD0(frame, ChildBank, null!, harness.Inputs()) : AdvanceMain6375D0(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(expectedBirth, step.Frame.Effect.Pools[1].Slots.Any(task => task.Callback == 0x638900));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < (expectedBirth ? 5 : 0); index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Frame.Random);
        if (expectedBirth)
        {
            Assert.Equal((3, 0xF1, 0), Assert.Single(harness.Points));
            Assert.Equal(0xBEEF, step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == 0x638900).ReadWord(22));
        }
    }

    [Fact]
    public void Contact124_reads_actual_first_contact_and_does_not_emit_all_contact_service()
    {
        Harness harness = new();
        NativeBattleEffect209Step step = AdvanceMain6375D0(Frame(124), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect209Contact506690(0), harness.Operations[1]);
        Assert.Equal(125, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Root_drain_appends_cleanup_and_executes_its_age0_in_the_same_scan_without_pause()
    {
        Harness harness = new() { Flags = 0 };
        NativeBattleEffect209Frame frame = Frame(130);
        NativeBattleEffect209Frame advanced = Advance(frame, null!, harness.Inputs());
        NativeBattleModulePoolTask cleanup = advanced.Effect.Pools[0].Slots.Single(task => task.Callback == CleanupCallback);
        Assert.Equal(1, cleanup.ReadWord(12));
        Assert.Equal(0, cleanup.ReadWord(14));
        Assert.Equal(1, advanced.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(0u, advanced.State.ReadDword(CursorGlobal) - 0x20DFAB8u);
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect209CommonTextureLoad508630);
        Assert.Equal(2, harness.Operations.OfType<NativeBattleEffect209Stage5713E0>().Count());
    }

    [Fact]
    public void Cleanup_polls_full_word_and_real_acknowledgement_after_json_without_synthetic_timeout()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect209Frame frame = Frame(130);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 9));
        NativeBattleEffect209Step first = AdvanceRoot639100(frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, frame.Effect.Pools[1] with { Head = null, Tail = null }) } }, RootBank, null!, harness.Inputs());
        uint cleanup = RootBank + 16;
        frame = first.Frame;
        frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 1));
        NativeBattleEffect209Step waiting = AdvanceCleanup639170(frame, cleanup, harness.Inputs());
        Assert.Equal(new NativeBattleEffect209CommonTextureLoad508630(0x20DFAB8, cleanup + 14), harness.Operations.Last());
        Assert.Equal(0, waiting.Result);
        frame = WriteCompletionByte(waiting.Frame, cleanup + 15, 1);
        frame = JsonSerializer.Deserialize<NativeBattleEffect209Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffect209Step completed = AdvanceCleanup639170(frame, cleanup, harness.Inputs());
        Assert.Equal(2, completed.Result);
        Assert.Equal(3, Read(completed.Frame, cleanup).ReadWord(12));
    }

    [Fact]
    public void Guardian_templates_and_COMMON_packet_bytes_have_distinct_current_owners()
    {
        NativeBattleEffect209Frame frame = Frame(17);
        frame = WriteBytes(frame, 0x209FAB8 + 0x19500, NativeBattleEffectArena.Clear().Write(0, [0xAB]), 1);
        frame = WriteBytes(frame, 0x20DFAB8 + 0x19500, NativeBattleEffectArena.Clear().Write(0, [0xCD]), 1);
        Assert.Equal(0xAB, ReadBytes(frame, 0x209FAB8 + 0x19500, 1).Read(0, 1)[0]);
        Assert.Equal(0xCD, ReadBytes(frame, 0x20DFAB8 + 0x19500, 1).Read(0, 1)[0]);
    }

    private static NativeBattleEffect209Frame Frame(short age)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (child, _) = child.Append(MainCallback);
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, unchecked((ushort)age))) };
        NativeBattleOriginalEffectState effect = new(209, 208, new(209, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, effect, new(42), NativeBattleEffect209State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(GuardianGlobal, 0x209FAB8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffectArena Point() => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)10)).Write(2, BitConverter.GetBytes((short)20)).Write(4, BitConverter.GetBytes((short)30)).Write(6, BitConverter.GetBytes((ushort)0xBEEF));

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public int IoResult { get; set; }
        public int IoCalls { get; private set; }
        public List<NativeBattleEffect209Operation> Operations { get; } = [];
        public List<(int Actor, int Selector, int Fraction)> Points { get; } = [];
        public bool Rebuilt { get; private set; }
        public NativeBattleEffect209Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, frame =>
        {
            IoCalls++;
            return new(frame, IoResult);
        }, _ => Packets(), (_, actor) => new(actor, 2) { ModelId4 = 131 }, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, _ => 512, _ => 0, (_, address) => address == 0xB8B7D8 ? Rebuilt ? 0x80402010u : 0x80A09080u : Rebuilt ? 0x80806040u : 0x80706050u, (frame, actor, retained) => new(frame, Point()), (frame, actor, selector, fraction, retained) =>
        {
            Points.Add((actor, selector, fraction));
            return new(frame, Point());
        }, (_, _) => true, (frame, operation) =>
        {
            Operations.Add(operation);
            Rebuilt |= operation is NativeBattleEffect209RebuildVariant50E3C0;
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 13, -1, 319, 209, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
