using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect166ControllerTests
{
    [Fact]
    public void Pause_still_reads_current_bone37_and_publishes_anchor_without_clock_or_random_changes()
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect166Frame frame = Frame(54);
        frame = frame with { State = frame.State.WriteWord(AnchorAddress + 6, 0xBEEF) };
        NativeBattleEffect166Step step = AdvanceMain5E4F60(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal((3, 37), Assert.Single(harness.Bones));
        Assert.Equal(new NativeBattleVector(11, 22, 33), Vector(ReadBytes(step.Frame, AnchorAddress, 8)));
        Assert.Equal(0xBEEF, step.Frame.State.ReadWord(AnchorAddress + 6));
        Assert.Equal(54, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Age0_computes_orientation_before_target_center_service_and_preserves_returned_frame()
    {
        Harness harness = new();
        NativeBattleEffect166Frame frame = Frame(0);
        NativeBattleEffect166Step step = AdvanceMain5E4F60(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Contains(new NativeBattleEffect166TargetCenter5020A0(1, TargetPointAddress), harness.Operations);
        Assert.Equal(900, step.Frame.State.ReadWord(TargetPointAddress));
        Assert.Equal(77u, step.Frame.State.ReadDword(TargetGlobal));
        Assert.Equal(1, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0u, step.Frame.State.ReadDword(CueGlobal));
    }

    [Fact]
    public void Age8_orders_paired_meshes_pulse_then_six_path_indices_with_three_CRT_draws()
    {
        Harness harness = new();
        NativeBattleEffect166Frame frame = Frame(8);
        NativeBattleEffect166Step step = AdvanceMain5E4F60(frame, ChildBank, Resources(), harness.Inputs());
        NativeBattleModulePoolTask[] born = step.Frame.Effect.Pools[1].Slots.Where(task => task.Callback != 0 && task.Callback != MainCallback).ToArray();
        Assert.Equal(new uint[] { 0x5E5AB0, 0x5E5AB0, 0x5E5C30, 0x5E5D50, 0x5E5D50, 0x5E5D50, 0x5E5D50, 0x5E5D50, 0x5E5D50 }, born.Select(task => task.Callback));
        Assert.Equal(unchecked((ushort)-born[0].ReadWord(0x18)), born[1].ReadWord(0x1A));
        Assert.Equal(Enumerable.Range(0, 6).Select(index => (ushort)index), born.Skip(3).Select(task => task.ReadWord(14)));
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 3; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, step.Frame.Random);
    }

    [Fact]
    public void Age54_consumes96_particle_draws_then_two_per_enabled_party_actor()
    {
        Harness harness = new();
        NativeBattleEffect166Frame frame = Frame(54);
        NativeBattleEffect166Step step = AdvanceMain5E4F60(frame, ChildBank, Resources(), harness.Inputs());
        Assert.Equal(16, step.Frame.Effect.Pools[1].Slots.Count(task => task.Callback == 0x5E86D0));
        Assert.Equal(2, step.Frame.Effect.Pools[1].Slots.Count(task => task.Callback == 0x5E8EF0));
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 100; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, step.Frame.Random);
        NativeBattleModulePoolTask actor0 = step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == 0x5E8EF0 && task.ReadWord(14) == 0);
        NativeBattleModulePoolTask actor2 = step.Frame.Effect.Pools[1].Slots.Single(task => task.Callback == 0x5E8EF0 && task.ReadWord(14) == 2);
        Assert.Equal(0, actor0.ReadWord(0x1A));
        Assert.Equal(ushort.MaxValue, actor2.ReadWord(0x1A));
    }

    [Theory]
    [InlineData(97, 98, 0)]
    [InlineData(98, 99, 2)]
    [InlineData(32767, -32768, 0)]
    public void Main_retirement_uses_signed_incremented_WORD(short age, short expected, int result)
    {
        NativeBattleEffect166Step step = AdvanceMain5E4F60(Frame(age), ChildBank, Resources(), new Harness().Inputs());
        Assert.Equal(unchecked((ushort)expected), Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(result, step.Result);
    }

    [Fact]
    public void Empty_child_pool_appends_cleanup_before_root_retirement_even_when_paused()
    {
        NativeBattleEffect166Frame frame = Frame(0);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount)) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        NativeBattleEffect166Step step = AdvanceRoot5E93A0(frame, RootBank, null!, new Harness { Flags = 0x201 }.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(0x20EBAB8u, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(2, Read(step.Frame, RootBank).ReadWord(12));
        NativeBattleModulePoolTask cleanup = step.Frame.Effect.Pools[0].Slots.Single(task => task.Callback == CleanupCallback);
        Assert.Equal(0, cleanup.ReadWord(12));
        Assert.Equal(0, cleanup.ReadWord(14));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 256, 2)]
    public void Cleanup_queues_only_at_age1_and_tests_full_completion_WORD(short age, ushort completion, int expected)
    {
        Harness harness = new();
        NativeBattleEffect166Frame frame = Frame(0);
        frame = Write(frame, RootBank, Read(frame, RootBank).UpdateHeader(Read(frame, RootBank).Flags, Read(frame, RootBank).State, Read(frame, RootBank).Next, CleanupCallback));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, (ushort)age).WriteWord(14, completion));
        NativeBattleEffect166Step step = AdvanceCleanup5E9410(frame, RootBank, harness.Inputs());
        Assert.Equal(expected, step.Result);
        Assert.Equal(age == 1 ? 1 : 0, harness.Operations.Count);
        if (age == 1)
        {
            Assert.Equal(new NativeBattleEffect166CommonTextureLoad508630(0x20DFAB8, RootBank + 14), harness.Operations[0]);
        }
        Assert.Equal(age + 1, Read(step.Frame, RootBank).ReadWord(12));
    }

    private static NativeBattleEffect166Frame Frame(short age)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, 0));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(MainCallback);
        child = child.WriteSlot(0, child.Slots[0].WriteWord(12, unchecked((ushort)age)));
        NativeBattleOriginalEffectState effect = new(166, 165, new(166, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(42), NativeBattleEffect166State.FromColdProcess().DetachBank(0).DetachBank(1).WriteDword(CasterGlobal, 3).WriteDword(ArenaGlobal, 0x20DFAB8));
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleEffect166Resources Resources() => new(null!, null!, null!, null!, [], [], [], new Dictionary<uint, ImmutableArray<byte>>
    {
        [CenterOffsetAddress] = new byte[8].ToImmutableArray(),
        [AlignmentAddress] = BitConverter.GetBytes(0).Concat(BitConverter.GetBytes(0)).Concat(BitConverter.GetBytes(4096)).ToImmutableArray()
    }.ToImmutableDictionary());

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public List<(int Actor, int Bone)> Bones { get; } = [];
        public List<NativeBattleEffect166Operation> Operations { get; } = [];
        public NativeBattleEffect166Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets(), (_, actor) => new(actor, (ushort)(actor == 1 ? 0 : 2)), (_, actor, bone) =>
        {
            Bones.Add((actor, bone));
            return NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 1, Y = 2, Z = 3 }, null);
        }, (_, _) => NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 10, Y = 20, Z = 30 }, null), (_, _) => 6, _ => NativeBattlePackedMatrix32.FromMatrix(Identity(), null), null!, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleEffect166TargetCenter5020A0)
            {
                return frame with { State = frame.State.WriteWord(TargetPointAddress, 900).WriteDword(TargetGlobal, 77) };
            }
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 14, -1, 310, 166, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
