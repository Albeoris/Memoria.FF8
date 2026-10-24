using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect174ControllerTests
{
    [Fact]
    public void Main_pause_precedes_every_anchor_state_birth_clock_and_random_producer()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect174Frame frame = Frame(68);
        NativeBattleEffect174Step step = AdvanceMain5D4660(frame, ChildBank, null!, harness.Inputs());
        Assert.Same(frame, step.Frame);
        Assert.Equal(0, step.Result);
        Assert.Empty(harness.Operations);
        Assert.Empty(harness.Points);
    }

    [Fact]
    public void Age9_clears_five_history_markers_and_preserves_opaque_pose_bytes()
    {
        NativeBattleEffect174Frame frame = Frame(9);
        for (int index = 0; index < HistoryCount; index++)
        {
            uint row = HistoryAddress + (uint)(index * HistoryStride);
            frame = WriteBytes(frame, row, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, HistoryLength).ToArray()).MarkUnknown(10, 1), HistoryLength);
        }
        NativeBattleEffect174Step step = AdvanceMain5D4660(frame, ChildBank, null!, new Harness().Inputs());
        Assert.Contains(step.Frame.Effect.Pools[1].Slots, value => value.Callback == 0x5D4ED0 && value.ReadWord(12) == 0);
        for (int index = 0; index < HistoryCount; index++)
        {
            uint row = HistoryAddress + (uint)(index * HistoryStride);
            Assert.Equal(0u, step.Frame.State.ReadDword(row));
            Assert.Equal(0xA5, step.Frame.State.Read(row + 4, 1)[0]);
            Assert.False(ReadBytes(step.Frame, row + 10, 1).ReadSnapshot(0, 1).Known[0]);
        }
    }

    [Fact]
    public void Age10_sets_exact_resource_flags_after_current_child_birth()
    {
        Harness harness = new();
        NativeBattleEffect174Step step = AdvanceMain5D4660(Frame(10), ChildBank, null!, harness.Inputs());
        Assert.Contains(step.Frame.Effect.Pools[1].Slots, value => value.Callback == 0x5D4CB0);
        Assert.Equal(new NativeBattleEffect174SetResourceFlags44(3, 0x1F), harness.Operations[0]);
    }

    [Fact]
    public void Age68_preserves_point_W_and_uses_actual_bone25_fraction11264_before_point_births()
    {
        Harness harness = new();
        NativeBattleEffect174Frame frame = Frame(68);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(22, 0xBEEF));
        NativeBattleEffect174Step step = AdvanceMain5D4660(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal((3, 0x19, 0x2C00), Assert.Single(harness.Points));
        NativeBattleModulePoolTask first = step.Frame.Effect.Pools[1].Slots.Single(value => value.Callback == 0x5D51C0);
        Assert.Equal(new NativeBattleVector(10, -60, 30), Vector(TaskView(first, 16, 8)));
        Assert.Equal(0xBEEF, first.ReadWord(22));
        Assert.Equal(0x1234, first.ReadWord(24));
        Assert.Equal(0x700, first.ReadWord(28));
        Assert.Contains(step.Frame.Effect.Pools[1].Slots, value => value.Callback == 0x5D5430);
        Assert.DoesNotContain(step.Frame.Effect.Pools[1].Slots, value => value.Callback == 0x5D52F0);
        Assert.Equal(new NativeBattleEffect174Stage5713E0(2048, 0), Assert.Single(harness.Operations));
    }

    [Theory]
    [InlineData(70, true)]
    [InlineData(97, true)]
    [InlineData(98, false)]
    [InlineData(100, false)]
    public void Periodic_birth_obeys_signed_remainder_and_inclusive_window(short age, bool periodic)
    {
        NativeBattleEffect174Step step = AdvanceMain5D4660(Frame(age), ChildBank, null!, new Harness().Inputs());
        Assert.Equal(periodic, step.Frame.Effect.Pools[1].Slots.Any(value => value.Callback == 0x5D52F0));
    }

    [Fact]
    public void Age100_uses_party_F1_and_floor_half_scale_then_signed_division18_with_two_shared_CRT_draws()
    {
        Harness harness = new();
        harness.Models[0] = harness.Models[0].Write(38, BitConverter.GetBytes((short)-7));
        NativeBattleEffect174Frame frame = Frame(100);
        NativeBattleEffect174Step step = AdvanceMain5D4660(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal((0, 0xF1, 0), Assert.Single(harness.Points));
        NativeBattleModulePoolTask[] children = step.Frame.Effect.Pools[1].Slots.Where(value => value.Callback == 0x5D7090).ToArray();
        Assert.Equal(2, children.Length);
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second);
        Assert.Equal(first % 4096, children[0].ReadWord(24));
        Assert.Equal(second % 4096, children[1].ReadWord(24));
        Assert.All(children, value => Assert.Equal(unchecked((ushort)-4), value.ReadWord(28)));
        Assert.All(children, value => Assert.Equal(0, value.ReadWord(30)));
        Assert.Equal(random, step.Frame.Random);
    }

    [Fact]
    public void Age120_wraps_double_scale_before_division40()
    {
        Harness harness = new();
        harness.Models[0] = harness.Models[0].Write(38, BitConverter.GetBytes((short)20000));
        NativeBattleEffect174Step step = AdvanceMain5D4660(Frame(120), ChildBank, null!, harness.Inputs());
        NativeBattleModulePoolTask[] children = step.Frame.Effect.Pools[1].Slots.Where(value => value.Callback == 0x5D5690).ToArray();
        Assert.Equal(2, children.Length);
        Assert.All(children, value => Assert.Equal(unchecked((ushort)40000), value.ReadWord(28)));
        Assert.All(children, value => Assert.Equal(unchecked((ushort)(-25536 / 40)), value.ReadWord(30)));
        Assert.Equal(new ushort[] { 0, 2048 }, children.Select(value => value.ReadWord(32)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, false)]
    public void Age175_conditionally_hides_current_target_but_always_delivers_actual_first_contact(byte hitFlags, bool hide)
    {
        Harness harness = new() { HitFlags = hitFlags };
        NativeBattleEffect174Step step = AdvanceMain5D4660(Frame(175), ChildBank, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect174FirstContact506690>());
        Assert.Equal(hide ? 1 : 0, harness.Operations.OfType<NativeBattleEffect174WriteModelBytes>().Count());
        Assert.Equal(176, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(179, 180, 0)]
    [InlineData(180, 181, 2)]
    [InlineData(32767, -32768, 0)]
    public void Signed_retirement_uses_incremented_WORD(short age, short next, int result)
    {
        Harness harness = new();
        NativeBattleEffect174Step step = AdvanceMain5D4660(Frame(age), ChildBank, null!, harness.Inputs());
        Assert.Equal(unchecked((ushort)next), Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
        if (result == 2)
        {
            Assert.Equal(new NativeBattleEffect174Stage5713E0(0, 0), harness.Operations[^1]);
        }
    }

    [Fact]
    public void Cleanup_birth_preserves_retained_completion_and_age1_explicitly_clears_it_before_real_load()
    {
        Harness harness = new();
        NativeBattleEffect174Frame frame = Frame(129);
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        root = root with { Slots = root.Slots.SetItem(1, root.Slots[1].WriteWord(14, 0xBEEF)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root) } };
        NativeBattleEffect174Step born = AdvanceMain5D4660(frame, ChildBank, null!, harness.Inputs());
        uint cleanup = RootBank + 16;
        Assert.Equal(0xBEEF, Read(born.Frame, cleanup).ReadWord(14));
        NativeBattleEffect174Step retained = AdvanceCleanup5D7400(born.Frame, cleanup, harness.Inputs());
        Assert.Equal(2, retained.Result);
        Assert.Empty(harness.Operations);
        frame = Write(born.Frame, cleanup, Read(born.Frame, cleanup).WriteWord(12, 1));
        NativeBattleEffect174Step started = AdvanceCleanup5D7400(frame, cleanup, harness.Inputs());
        Assert.Equal(0, started.Result);
        Assert.Equal(0, Read(started.Frame, cleanup).ReadWord(14));
        Assert.Equal(new NativeBattleEffect174AppendCommonTextureLoad508630(0x20FFAB8, cleanup + 14), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Paused_root_increments_native_clock_and_selects_C000_private_bank()
    {
        NativeBattleEffect174Frame frame = Frame(0);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect174Step step = AdvanceRoot5D7440(frame, RootBank, null!, new Harness { Flags = 1 }.Inputs());
        Assert.Equal(0x20EBAB8u, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(0x5D6DA0u, 4, 8)]
    [InlineData(0x5D6F50u, 16, 32)]
    public void Actor_fades_publish_both_DPCS_and_model_color_mode_before_pause(uint callback, short age, ushort duration)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect174Frame frame = Frame(age, callback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1).WriteWord(24, duration).WriteWord(26, 0x800));
        NativeBattleEffect174Step step = NativeBattleEffect174Lower.AdvanceActorServices(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(0x32604020u, Dword(harness.Models[1], 44));
        Assert.Equal(0x02302010u, Dword(harness.Models[1], 40));
        Assert.Equal(2048u, step.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0u, step.Frame.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(0x32604020u, step.Frame.Memory.ScriptedColorRegisters.Rgb[1]);
        Assert.Equal(0x00302010u, step.Frame.Memory.ScriptedColorRegisters.Rgb[2]);
        Assert.Equal(age, Signed(Read(step.Frame, ChildBank), 12));
        Assert.Equal(0, step.Result);
        Assert.Equal(0, Word(harness.Models[1], 0));
    }

    [Theory]
    [InlineData(0x5D6DA0u, 7, 8, false)]
    [InlineData(0x5D6F50u, 31, 32, true)]
    public void Actor_fade_retirement_restores_saved800_and_keeps_distinct_color_and_hide_contract(uint callback, short age, ushort duration, bool hide)
    {
        Harness harness = new();
        NativeBattleEffect174Frame frame = Frame(age, callback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1).WriteWord(24, duration).WriteWord(26, 0x800));
        NativeBattleEffect174Step step = NativeBattleEffect174Lower.AdvanceActorServices(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(hide ? (ushort)0x804 : (ushort)0x800, Word(harness.Models[1], 0));
        if (!hide)
        {
            Assert.Equal(0x80604020u, Dword(harness.Models[1], 40));
        }
        else
        {
            Assert.Equal(2, harness.Models[1].Read(43, 1)[0]);
            Assert.Equal(0, harness.Models[1].Read(7, 1)[0]);
        }
    }

    [Fact]
    public void Motion_recaptures_current_age0_before_pause_preserving_saved_padding_and_position_W()
    {
        Harness harness = new() { Flags = 1 };
        harness.Models[0] = harness.Models[0].Write(12, BitConverter.GetBytes((short)123)).Write(14, BitConverter.GetBytes((short)-456)).Write(16, BitConverter.GetBytes((short)0)).MarkUnknown(18, 2).Write(34, BitConverter.GetBytes((ushort)0xBEEF));
        NativeBattleEffect174Frame frame = Frame(0, 0x5D7220);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0).WriteWord(24, 4096).WriteBytes(16, Point(100, 200, 300, 0).Read(0, 8)));
        NativeBattleEffect174Step first = NativeBattleEffect174Lower.AdvanceActorMotion7220(frame, ChildBank, harness.Inputs());
        Assert.Equal(123, first.Frame.State.ReadWord(SavedActorsAddress + 4));
        Assert.False(ReadBytes(first.Frame, SavedActorsAddress + 10, 2).ReadSnapshot(0, 2).Known[0]);
        Assert.Equal(0xBEEF, Word(harness.Models[0], 34));
        NativeBattleEffect174Frame restored = JsonSerializer.Deserialize<NativeBattleEffect174Frame>(JsonSerializer.Serialize(first.Frame))!;
        NativeBattleEffect174Step second = NativeBattleEffect174Lower.AdvanceActorMotion7220(restored, ChildBank, harness.Inputs());
        Assert.Equal(0, second.Frame.State.ReadWord(SavedActorsAddress + 4));
        Assert.Equal(0, second.Frame.State.ReadWord(SavedActorsAddress + 6));
        Assert.Equal(0, Read(second.Frame, ChildBank).ReadWord(12));
        Assert.Equal(JsonSerializer.Serialize(frame.Memory.GeometryRegisters), JsonSerializer.Serialize(second.Frame.Memory.GeometryRegisters));
        Assert.Throws<InvalidDataException>(() => second.Frame.State.Read(SavedActorsAddress + 2, 1));
    }

    [Fact]
    public void Motion_retirement_restores_actual_saved_angles_point_W_and_only_saved_flag_bits()
    {
        Harness harness = new();
        harness.Models[0] = harness.Models[0].Write(0, BitConverter.GetBytes((ushort)0x4822));
        NativeBattleEffect174Frame frame = Frame(29, 0x5D7220);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0).WriteWord(24, 4096).WriteBytes(16, Point(100, 200, 300, 0).Read(0, 8)));
        NativeBattleEffectArena angles = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)100)).Write(2, BitConverter.GetBytes((short)-200)).Write(4, BitConverter.GetBytes((short)300)).MarkUnknown(6, 2);
        NativeBattleEffectArena savedPoint = Point(11, 22, 33, 0xBEEF);
        frame = WriteBytes(frame, SavedActorsAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x20)), 2);
        frame = WriteBytes(frame, SavedActorsAddress + 4, angles, 8);
        frame = WriteBytes(frame, SavedActorsAddress + 12, savedPoint, 8);
        NativeBattleEffect174Step step = NativeBattleEffect174Lower.AdvanceActorMotion7220(frame, ChildBank, harness.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(savedPoint.Read(0, 8), harness.Models[0].Read(28, 8));
        Assert.Equal(angles.Read(0, 6), harness.Models[0].Read(12, 6));
        Assert.False(harness.Models[0].ReadSnapshot(18, 2).Known[0]);
        Assert.Equal((ushort)0x4822, Word(harness.Models[0], 0));
    }

    private static NativeBattleEffect174Frame Frame(short age, uint callback = MainCallback)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteBytes(16, new byte[8])) };
        NativeBattleOriginalEffectState effect = new(174, 173, new(174, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return new(memory, effect, new(42), NativeBattleEffect174State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteDword(TargetGlobal, 0).WriteDword(ArenaGlobal, 0x20DFAB8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffectArena Point(short x, short y, short z, ushort w) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z)).Write(6, BitConverter.GetBytes(w));
    private static ushort Word(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(view.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public byte HitFlags { get; init; }
        public List<NativeBattleEffect174Operation> Operations { get; } = [];
        public List<(int Actor, int Selector, int Fraction)> Points { get; } = [];
        public NativeBattleEffectArena[] Models { get; } = Enumerable.Range(0, 7).Select(actor => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)(actor is 0 or 3 ? 2 : 0))).Write(14, BitConverter.GetBytes((short)0x1234)).Write(38, BitConverter.GetBytes((short)700))).ToArray();
        public NativeBattleEffect174Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, actor) => new(actor, Word(Models[actor], 0)) { Rotation14 = unchecked((short)Word(Models[actor], 14)), Transform = new(new(0, 0, 0), Vector(Models[actor], 28), unchecked((short)Word(Models[actor], 34)), 0, unchecked((short)Word(Models[actor], 38))) }, (_, actor) => Models[actor], (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, _ => 512, (_, address) => address == 0xB8B7D8 ? 0x80604020u : 0x80C08040u, (frame, actor, selector, fraction, retained) =>
        {
            Points.Add((actor, selector, fraction));
            return new(frame, CopyBytes(Point(10, 20, 30, 0), 0, retained, 0, 6));
        }, (frame, actor, retained) => new(frame, CopyBytes(Point(10, 20, 30, 0), 0, retained, 0, 6)), null!, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect174WriteModelBytes write)
            {
                Models[write.Actor] = CopyBytes(write.Data, 0, Models[write.Actor], write.Offset, write.Length);
            }
            return frame;
        });

        private NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 13, -1, 72, 174, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, HitFlags, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
