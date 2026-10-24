using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect345ControllerTests
{
    public static IEnumerable<object[]> PhaseEntries()
    {
        foreach ((uint callback, int[] expected) in new (uint, int[])[] { (0x705C00, [1, 2, 3, 4, 4, 6, 7, 8, 9, 10, 10]), (0x705E50, [1, 2, 3, 4, 4]), (0x705FF0, [1, 2, 3, 3]), (0x706130, [1, 1, 2, 3, 5, 5]), (0x706A80, [0, 2, 2]), (0x70C720, [0, 1, 2]), (0x70C9E0, [1, 2, 2]), (0x70CD70, [1, 2, 2]), (0x70D0E0, [1, 2, 2]) })
        {
            for (int phase = 0; phase < expected.Length; phase++)
            {
                yield return [callback, phase, expected[phase]];
            }
        }
    }

    [Theory]
    [MemberData(nameof(PhaseEntries))]
    public void All_forty_one_literal_phase_entries_dispatch_with_native_guards(uint callback, int phase, int expected)
    {
        Fixture f = new();
        (NativeBattleEffect345Frame frame, uint address) = f.Task(callback);
        NativeBattleModulePoolTask task = Read(frame, address).WriteByte(0x29, (byte)phase).WriteByte(0x26, 4);
        if (callback == RootCallback)
        {
            task = task.WriteByte(0x62, 1).WriteByte(0x63, 1);
        }
        if (callback == 0x705FF0)
        {
            task = task.WriteWord(0x44, 1);
        }
        if (callback == 0x706A80)
        {
            task = task.WriteWord(0x298, 1);
        }
        if (callback == 0x706A80 && phase == 1)
        {
            task = task.WriteWord(0x54, 2);
        }
        if (callback == 0x70C9E0 && phase == 1)
        {
            task = task.WriteWord(0x24, 5);
        }
        if (callback == 0x70CD70 && phase == 1)
        {
            task = task.WriteWord(0x24, 14);
        }
        frame = Write(frame, address, task);
        NativeBattleEffect345Step step = AdvanceTask(frame, address, f.Resources, f.Inputs);
        Assert.Equal(unchecked((ushort)(task.ReadWord(0x24) + 1)), Read(step.Frame, address).ReadWord(0x24));
        Assert.Equal(expected, Read(step.Frame, address).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0x705C00u, 11)]
    [InlineData(0x705E50u, 5)]
    [InlineData(0x705FF0u, 4)]
    [InlineData(0x706130u, 6)]
    [InlineData(0x706A80u, 3)]
    [InlineData(0x70C720u, 3)]
    [InlineData(0x70C9E0u, 3)]
    [InlineData(0x70CD70u, 3)]
    [InlineData(0x70D0E0u, 3)]
    public void Each_signed_domain_rejects_overflow_and_negative_selector(uint callback, byte outside)
    {
        Fixture f = new();
        (NativeBattleEffect345Frame frame, uint address) = f.Task(callback);
        foreach (byte phase in new[] { outside, (byte)255, (byte)128 })
        {
            NativeBattleEffect345Frame malformed = Write(frame, address, Read(frame, address).WriteByte(0x29, phase));
            Assert.Throws<InvalidDataException>(() => AdvanceTask(malformed, address, f.Resources, f.Inputs));
        }
    }

    [Fact]
    public void Root_parity_copies_current_view_and_retains_the_actual_source_aliases()
    {
        Fixture f = new();
        NativeBattleEffect345Frame first = AdvanceTask(f.Frame, RootBank, f.Resources, f.Inputs).Frame;
        Assert.Equal(Fixture.Common + 0xE000, first.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(Fixture.Common + 0x1C000, first.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(first.State.ReadDword(AllocationCursorGlobal), first.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(f.View.Bytes, first.Memory.SharedEffectView2793E58!.Bytes);
        Assert.Equal(f.View.Known, first.Memory.SharedEffectView2793E58.Known);
        NativeBattleEffect345Frame second = AdvanceTask(first, RootBank, f.Resources, f.Inputs).Frame;
        Assert.Equal(Fixture.Common, second.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(Fixture.Common + 0xE000, second.State.ReadDword(SecondaryCursorGlobal));
    }

    [Fact]
    public void Root_reserves_once_clears_exact_spans_and_preserves_neighbors()
    {
        Fixture f = new();
        uint start = Fixture.Common + 0x1C000;
        NativeBattleEffect345Frame frame = WriteBytes(f.Frame, start - 1, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 0xA5F2).ToArray()), 0xA5F2);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 2));
        frame = AdvanceTask(frame, RootBank, f.Resources, f.Inputs).Frame;
        Assert.Equal(start, frame.State.ReadDword(0x255EA44));
        Assert.Equal(start + 0x870, frame.State.ReadDword(0x255EA88));
        Assert.Equal(start + 0xA5F0, frame.State.ReadDword(AllocationCursorGlobal));
        Assert.Equal(new byte[0xA5F0], ReadBytes(frame, start, 0xA5F0).Read(0, 0xA5F0));
        Assert.Equal(0xA5, ReadBytes(frame, start - 1, 1).Read(0, 1)[0]);
        Assert.Equal(0xA5, ReadBytes(frame, start + 0xA5F0, 1).Read(0, 1)[0]);
    }

    [Fact]
    public void Target_contact_uses_current_physical_packet_and_completes_its_actual_parent()
    {
        Fixture f = new();
        (NativeBattleEffect345Frame frame, uint target) = f.Task(0x705E50);
        frame = Write(frame, target, Read(frame, target).WriteByte(0x29, 1));
        frame = AdvanceTask(frame, target, f.Resources, f.Inputs).Frame;
        Assert.Single(f.Operations.OfType<NativeBattleEffect345Contact506690>());
        Assert.Equal(f.PacketContact, f.Operations.OfType<NativeBattleEffect345Contact506690>().Single().AbsoluteIndex);
        Assert.Equal(2, Read(frame, target).ReadByte(0x29));
        frame = Write(frame, target, Read(frame, target).WriteByte(0x29, 3));
        NativeBattleEffect345Step complete = AdvanceTask(frame, target, f.Resources, f.Inputs);
        Assert.Equal(2, complete.Result);
        Assert.Equal(0, Read(complete.Frame, RootBank).ReadByte(0x28));
        Assert.Equal(0, Read(complete.Frame, RootBank).ReadByte(0x63));
    }

    [Fact]
    public void Root_loop_changes_target_not_header_and_native_particle_wrapper_keeps_all_words()
    {
        Fixture f = new(2);
        NativeBattleEffect345Frame frame = Write(f.Frame, RootBank, Read(f.Frame, RootBank).WriteByte(0x29, 4).WriteByte(0x63, 0).WriteWord(0x5A, 2));
        frame = AdvanceTask(frame, RootBank, f.Resources, f.Inputs).Frame;
        Assert.Equal(0, Read(frame, RootBank).ReadByte(0x2A));
        Assert.Equal(1, Read(frame, RootBank).ReadByte(0x2B));
        Assert.Equal(1, Read(frame, RootBank).ReadByte(0x2E));
        Assert.Equal(3, Read(frame, RootBank).ReadByte(0x29));
        (frame, uint child) = AppendParticle706A30(frame, RootBank, 0x1643698, 0, 20, 2, f.Inputs);
        NativeBattleModulePoolTask task = Read(frame, child);
        Assert.Equal(0, task.ReadWord(0x298));
        Assert.Equal(2, task.ReadWord(0x29C));
        Assert.Equal(20, task.ReadWord(0x29E));
        Assert.Equal(0x1643698u, task.ReadDword(0x30));
        Assert.Equal(RootBank, task.ReadDword(0x18));
    }

    [Fact]
    public void Frame14_still_samples_and_updates_before_mote_completion()
    {
        Fixture f = new();
        (NativeBattleEffect345Frame frame, uint child) = f.Task(0x70CD70);
        frame = Write(frame, child, Read(frame, child).WriteByte(0x29, 1).WriteByte(0x26, 4).WriteWord(0x24, 14).WriteWord(0xF4, unchecked((ushort)-63)).WriteWord(0xD4, 100));
        NativeBattleEffect345Step result = AdvanceTask(frame, child, f.Resources, f.Inputs);
        NativeBattleModulePoolTask task = Read(result.Frame, child);
        Assert.Equal(2, result.Result);
        Assert.Equal(15, task.ReadWord(0x24));
        Assert.Equal(-48, Signed(task, 0xF4));
        Assert.Equal(97, task.ReadWord(0xD4));
        Assert.Equal(14u, task.ReadDword(0x30));
        Assert.Equal(2, task.ReadByte(0x29));
    }

    [Fact]
    public void Warm_bound_sources_and_retired_physical_suffixes_remain_current()
    {
        Fixture f = new();
        NativeBattleEffect345Frame frame = f.Frame with { State = f.Frame.State with { OwnedModelSourceBound = true } };
        NativeBattleFileStaging staging = frame.Staging!.Write(0x1524658, BitConverter.GetBytes(0x255E620u)).Write(0x1525754, [0xA5]);
        frame = frame with { Staging = staging };
        NativeBattleEffect345Frame bound = BindOriginalSources(frame, f.Resources);
        Assert.Same(staging, bound.Staging);
        Assert.Equal(0x255E620u, BitConverter.ToUInt32(bound.Staging!.Read(0x1524658, 4)));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 9).WriteByte(0x61, 0xD7));
        frame = Advance(frame, f.Resources, f.Inputs);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(0xD7, frame.State.ReadBank(0).Read(0x61, 1)[0]);
        Assert.All(frame.State.BankStorage, bank => Assert.NotNull(bank));
    }

    private sealed class Fixture
    {
        public const uint Common = 0x20DFAB8;
        public NativeBattleEffect345Frame Frame { get; }
        public NativeBattleEffect345Inputs Inputs { get; }
        public NativeBattleEffect345Resources Resources { get; }
        public NativeBattlePackedMatrix32 View { get; } = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        public List<NativeBattleEffect345Operation> Operations { get; } = [];
        public int PacketContact { get; }

        public Fixture(int targets = 1)
        {
            ImmutableDictionary<uint, ImmutableArray<byte>> spans = new uint[] { 0x1529720, 0x1529740, 0x1529760, 0x1529780 }.ToImmutableDictionary(address => address, address => Enumerable.Range(0, 15).SelectMany(index => BitConverter.GetBytes((short)index)).ToImmutableArray());
            Resources = new(new("scheduler", []), new("embedded0", []), new("embedded1", []), new("camera", []), new(NativeBattleEffect345Resources.ModelAddress, [], null!, []), [], new(0, NativeBattleEffect345Resources.MeshAddress, []), spans);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(3, 8, 11, 0, 124, 345, null));
            for (int target = 0; target < targets; target++)
            {
                (packets, _) = packets.AppendContact(new(new((byte)target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish(1);
            PacketContact = packets.Headers[0].FirstContactIndex!.Value;
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, (_, _) => NativeBattleEffectArena.Clear().Write(0x26, BitConverter.GetBytes((short)4096)), _ => View, _ => new(null, 100, 160, 108), _ => 0, _ => new(0x808080, 0x32808080), (_, _, length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length), (frame, _, _, _, retained) => new(frame, PutPoint(retained, 0, new(0, 0, 2000))), (frame, _, _, _) => new(frame, 0), (frame, _, _) => new(frame, 0), (frame, _) => new(frame, 0), _ => throw new NotSupportedException("Rendering is outside these scheduler checks."), (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            })
            {
                ReadModel = (_, actor) => new(actor, 0),
                ReadPoseSource = (_, _) => throw new NotSupportedException("Posed particle initialization is outside this branch fixture.")
            };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), SharedEffectView2793E58 = View };
            NativeBattleOriginalEffectState effect = new(345, 345, new(345, Entry, Initializer), new(3, 1, 1, 0, 0, 0), null)
            {
                Pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray(),
                ReturnedDescriptor = RootDescriptor
            };
            NativeBattleEffect345State state = NativeBattleEffect345State.FromColdProcess() with { NodeData = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x1643698, new byte[18932].ToImmutableArray()).Add(0x164808C, new byte[9708].ToImmutableArray()) };
            for (int index = 0; index < Pools.Length; index++)
            {
                state = state.DetachBank(index);
            }
            state = state.WriteDword(InitializerGlobal, Common).WriteDword(0x255DC0C, Common).WriteDword(0x255DC08, Common + 0xE000).WriteDword(0x255CD64, Common + 0xE000).WriteDword(0x255CD60, Common + 0x1C000).WriteDword(AllocationCursorGlobal, Common + 0x1C000).WriteWord(0x255E660, 1).WriteWord(0x255E664, 1);
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(0x1524658, BitConverter.GetBytes(0x255E618u)).Write(0x15297A0, BitConverter.GetBytes(0x255EA58u));
            NativeBattleEffect345Frame frame = new(memory, effect, new(1), state) { Staging = staged };
            (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 100, 0, Inputs);
            Frame = Write(frame, root, Read(frame, root).WriteDword(12, 0x1D99A78).WriteWord(0x5A, 1).WriteByte(0x2C, 3).WriteByte(0x2D, 0));
        }

        public (NativeBattleEffect345Frame Frame, uint Address) Task(uint callback)
        {
            if (callback == RootCallback)
            {
                return (Frame, RootBank);
            }
            int pool = callback switch { 0x705E50 => 1, 0x705FF0 => 2, 0x706130 => 7, 0x706A80 => 3, 0x70C720 or 0x70D0E0 => 4, 0x70C9E0 or 0x70CD70 => 5, _ => throw new ArgumentOutOfRangeException(nameof(callback)) };
            return AppendHierarchy(Frame, pool, callback, Pools[pool].Stride, RootBank, Inputs);
        }
    }
}
