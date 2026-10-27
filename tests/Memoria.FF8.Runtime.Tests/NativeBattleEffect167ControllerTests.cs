using System.Collections.Immutable;
using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167ControllerTests
{
    [Fact]
    public void Pause_publishes_marker_before_return_without_clock_birth_or_random_changes()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect167Frame frame = Frame(97);
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(1u, step.Frame.State.ReadDword(MarkerGlobal));
        Assert.Equal(97, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Single(step.Frame.Effect.Pools[1].Slots.Where(task => task.Callback != 0));
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData(27, 3, 0x5E2F30u)]
    [InlineData(44, 10, 0x5E33E0u)]
    public void History_birth_clears_only_actual_marker_DWORDs(short age, int count, uint callback)
    {
        NativeBattleEffect167Frame frame = Frame(age);
        uint start = age == 27 ? FirstHistoryAddress : SecondHistoryAddress;
        NativeBattleEffectArena prior = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, count * 20).ToArray()).MarkUnknown(7, 1);
        frame = WriteBytes(frame, start, prior, count * 20);
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(frame, ChildBank, null!, new Harness().Inputs());
        Assert.Contains(step.Frame.Effect.Pools[1].Slots, task => task.Callback == callback && task.ReadWord(12) == 0);
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(0u, step.Frame.State.ReadDword(start + (uint)(index * 20)));
            Assert.Equal(0xA5, step.Frame.State.Read(start + (uint)(index * 20 + 4), 1)[0]);
        }
        Assert.False(ReadBytes(step.Frame, start + 7, 1).ReadSnapshot(0, 1).Known[0]);
    }

    [Fact]
    public void Age64_hides_caster_byte_and_only_enabled_party_words_after_four_stage_flags()
    {
        Harness harness = new();
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(Frame(64), ChildBank, null!, harness.Inputs());
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == 0x5E38D0);
        Assert.Equal(unchecked((ushort)-170), child.ReadWord(0x10));
        Assert.Equal(0x800, child.ReadWord(0x20));
        Assert.Equal(4, harness.Operations.OfType<NativeBattleEffect167StageGroupFlags>().Count());
        NativeBattleEffect167WriteModelBytes[] writes = harness.Operations.OfType<NativeBattleEffect167WriteModelBytes>().ToArray();
        Assert.Equal(new[] { 3, 0, 2 }, writes.Select(write => write.Actor));
        Assert.Equal(new[] { 1, 2, 2 }, writes.Select(write => write.Length));
        Assert.Equal((ushort)0xA506, harness.Models[0].Flags0);
        Assert.Equal((ushort)0xA500, harness.Models[1].Flags0);
        Assert.Equal((ushort)0xA506, harness.Models[3].Flags0);
    }

    [Fact]
    public void Age74_saves_full_point8_and_changes_only_wrapped_Y_Z_words()
    {
        Harness harness = new();
        NativeBattleEffect167Frame frame = Frame(74);
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal(harness.OriginalPoint.Read(0, 8), step.Frame.State.Read(SavedPositionAddress, 8));
        NativeBattleEffect167WriteModelBytes[] position = harness.Operations.OfType<NativeBattleEffect167WriteModelBytes>().Where(write => write.Offset != 0).ToArray();
        Assert.Equal(new[] { 0x1E, 0x20 }, position.Select(write => write.Offset));
        Assert.All(position, write => Assert.Equal(2, write.Length));
        Assert.Equal(32760 + 700, unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(position[0].Data.Read(0, 2))));
        Assert.Equal(unchecked((ushort)(-32760 + 4400)), BinaryPrimitives.ReadUInt16LittleEndian(position[1].Data.Read(0, 2)));
    }

    [Fact]
    public void Age97_reads_current_local_F1_point_and_consumes_six_CRT_in_native_order()
    {
        Harness harness = new() { Scale = 4096 };
        NativeBattleEffect167Frame frame = Frame(97);
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(frame, ChildBank, null!, harness.Inputs());
        Assert.Equal((0, 0xF1, 0), Assert.Single(harness.LocalPoints));
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == 0x5E3C20);
        NativeCrtRandomState random = frame.Random.Draw(out ushort radiusValue);
        int radius = (radiusValue % 256 + 128) * 4096 >> 12;
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort z);
        random = random.Draw(out ushort angle);
        random = random.Draw(out ushort speed);
        Assert.Equal(unchecked((ushort)(10 + x % (2 * radius) - radius)), child.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)(20 + y % (2 * radius) - radius)), child.ReadWord(0x12));
        Assert.Equal(unchecked((ushort)(30 + z % (2 * radius) - radius)), child.ReadWord(0x14));
        Assert.Equal(unchecked((ushort)(angle % 1280 - 640)), child.ReadWord(0x18));
        Assert.Equal((ushort)((speed % 48 + 48) * 2560 >> 12), child.ReadWord(0x1E));
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(0, child.ReadWord(0x1C));
    }

    [Fact]
    public void Age143_delivers_current_header_base_and_count_without_a_fixed_acknowledgement()
    {
        Harness harness = new();
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(Frame(143), ChildBank, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect167AllContacts506BA0(0, 1), Assert.Single(harness.Operations));
        Assert.Equal(144, Read(step.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(144, 145, 0)]
    [InlineData(145, 146, 2)]
    [InlineData(32767, -32768, 0)]
    public void Signed_retirement_changes_only_current_resource_flags(short age, short next, int result)
    {
        Harness harness = new();
        NativeBattleEffect167Step step = AdvanceMain5E2AD0(Frame(age), ChildBank, null!, harness.Inputs());
        Assert.Equal(unchecked((ushort)next), Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
        Assert.Equal(result == 2 ? 1 : 0, harness.Operations.Count);
        if (result == 2)
        {
            Assert.Equal(new NativeBattleEffect167ResourceFlags44(3, ushort.MaxValue, 0x40), harness.Operations[0]);
        }
    }

    [Fact]
    public void Paused_root_advances_clock_and_selects_8000_private_bank_while_main_retains_age()
    {
        Harness harness = new() { Flags = 1 };
        NativeBattleEffect167Frame frame = Frame(0);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect167Step step = AdvanceRoot5E4E20(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0x20E7AB8u, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(0, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(1u, step.Frame.State.ReadDword(MarkerGlobal));
    }

    internal static NativeBattleEffect167Frame Frame(short age, uint callback = MainCallback)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, 0));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        child = child.WriteSlot(0, child.Slots[0].WriteWord(12, unchecked((ushort)age)));
        NativeBattleOriginalEffectState effect = new(167, 166, new(167, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(42), NativeBattleEffect167State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, 0x20DFAB8));
    }

    internal sealed class Harness
    {
        public uint Flags { get; init; }
        public short Scale { get; init; } = 700;
        public List<NativeBattleEffect167Operation> Operations { get; } = [];
        public List<(int Actor, int Selector, int Fraction)> LocalPoints { get; } = [];
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(actor => new NativeBattleGraphicsModel(actor, (ushort)(actor == 1 ? 0xA500 : 0xA502))).ToArray();
        public NativeBattleEffectArena OriginalPoint { get; } = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)11)).Write(2, BitConverter.GetBytes((short)32760)).Write(4, BitConverter.GetBytes((short)-32760)).Write(6, BitConverter.GetBytes((ushort)0xBEEF));
        public NativeBattleEffect167Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, actor) => Models[actor] with { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, Scale) }, (_, actor) => CopyBytes(OriginalPoint, 0, NativeBattleEffectArena.Clear(), 0x1C, 8), null!, null!, null!, null!, _ => 512, _ => 0, null!, (frame, actor, selector, fraction, retained) =>
        {
            LocalPoints.Add((actor, selector, fraction));
            return new(frame, retained.Write(0, BitConverter.GetBytes((short)10)).Write(2, BitConverter.GetBytes((short)20)).Write(4, BitConverter.GetBytes((short)30)));
        }, (frame, operation) =>
        {
            Operations.Add(NativeBattleStorageSnapshots.Capture(operation));
            if (operation is NativeBattleEffect167WriteModelBytes write && write.Offset == 0)
            {
                ushort flags = Models[write.Actor].Flags0;
                byte[] bytes = write.Data.Read(0, write.Length);
                flags = write.Length == 1 ? (ushort)((flags & 0xFF00) | bytes[0]) : BinaryPrimitives.ReadUInt16LittleEndian(bytes);
                Models[write.Actor] = Models[write.Actor] with { Flags0 = flags };
            }
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 12, -1, 274, 167, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
