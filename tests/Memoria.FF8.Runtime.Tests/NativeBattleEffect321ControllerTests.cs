using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect321ControllerTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(512u)]
    [InlineData(513u)]
    public void Main_pause_precedes_anchor_births_random_and_age(uint flags)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect321Frame frame = Frame(0, 24);
        NativeBattleEffect321Step result = AdvanceMain597AB0(frame, ChildBank, null!, harness.Inputs());
        Assert.Same(frame, result.Frame);
        Assert.Equal(0, result.Result);
        Assert.Empty(harness.Operations);
        Assert.Empty(harness.Anchors);
    }

    [Theory]
    [InlineData(0, FirstPacketAddress)]
    [InlineData(1, SecondPacketAddress)]
    [InlineData(2, FirstPacketAddress)]
    public void Root_alternates_while_main_is_paused(int age, uint bank)
    {
        Harness harness = new() { Flags = 0x201 };
        NativeBattleEffect321Step result = AdvanceRoot598D00(Frame(age, 24), RootBank, null!, harness.Inputs());
        Assert.Equal(bank, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(age + 1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(24, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Main_zero_uses_signed_yaw_shift_and_retains_anchor_w_knownness()
    {
        Harness harness = new() { Yaw = 1, Anchor = new(100, 200, 300) };
        NativeBattleEffect321Frame frame = Frame(0, 0);
        frame = WriteBytes(frame, ChildBank + 22, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeBattleEffect321Frame result = AdvanceMain597AB0(frame, ChildBank, null!, harness.Inputs()).Frame;
        NativeBattleModulePoolTask task = Read(result, ChildBank);
        Assert.Equal(unchecked((short)(100 + (unchecked(-2800 * NativeBattleCameraMath.Sine(1)) >> 12))), Signed(task, 16));
        Assert.Equal(0, task.ReadWord(18));
        Assert.Equal(unchecked((short)(300 + (unchecked(-2800 * NativeBattleCameraMath.Cosine(1)) >> 12))), Signed(task, 20));
        Assert.Equal(new[] { true, false }, TaskView(task, 22, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(ModelAddress(3), Assert.Single(harness.Anchors));
        Assert.Equal(77u, result.State.ReadDword(VibrationGlobal));
        Assert.Equal(new NativeBattleEffect321VibrationStart4A29A0(VibrationAddress, 1, 128, VibrationGlobal), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Births_keep_duplicate_contacts_and_consume_five_ordered_random_draws_each()
    {
        Harness harness = new();
        harness.SetContacts(8, 0, 0);
        NativeBattleEffect321Frame frame = Frame(0, 24);
        NativeCrtRandomState expected = frame.Random;
        List<ushort[]> draws = [];
        for (int contact = 0; contact < 2; contact++)
        {
            ushort[] values = new ushort[5];
            for (int index = 0; index < 5; index++)
            {
                expected = expected.Draw(out values[index]);
            }
            draws.Add(values);
        }
        NativeBattleEffect321Frame result = BirthChildren(frame, ChildBank, harness.Inputs());
        Assert.Equal(expected, result.Random);
        Assert.Equal(11, result.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(new[] { ModelAddress(0), ModelAddress(0) }, harness.Anchors);
        NativeBattleModulePoolTask[] targets = result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x5985C0).ToArray();
        NativeBattleModulePoolTask[] particles = result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x598B90).ToArray();
        for (int index = 0; index < 2; index++)
        {
            Assert.Equal(index, targets[index].ReadWord(26));
            Assert.Equal(((draws[index][0] % 3) + 1) << 12, targets[index].ReadWord(24));
            Assert.Equal(-6000 - draws[index][1] % 2500, Signed(targets[index], 32));
            Assert.Equal(draws[index][2] & 4095, particles[index].ReadWord(24));
            Assert.Equal(draws[index][3] % 640 + 2304, particles[index].ReadWord(28));
            Assert.Equal(((draws[index][4] & 2047) + 6144) / 8, particles[index].ReadWord(32));
            Assert.Equal(particles[index].ReadWord(32), particles[index].ReadWord(34));
        }
        Assert.Equal(new ushort[] { 0, 3, 6 }, result.Effect.Pools[1].Slots.Where(task => task.Callback == 0x597E30).Select(task => task.ReadWord(14)).ToArray());
    }

    [Fact]
    public void Mandatory_birth_failure_does_not_consume_random_or_resize_the_bank()
    {
        NativeBattleEffect321Frame frame = Frame(0, 24);
        for (int index = 1; index < 100; index++)
        {
            (frame, _) = Append(frame, ChildDescriptor, 0x598570);
        }
        NativeCrtRandomState random = frame.Random;
        Assert.Throws<InvalidDataException>(() => BirthChildren(frame, ChildBank, new Harness().Inputs()));
        Assert.Equal(random, frame.Random);
        Assert.Equal(100, frame.Effect.Pools[1].Slots.Length);
    }

    [Fact]
    public void Positional_sound_uses_actual_target_mask_and_unknown_local_w()
    {
        Harness harness = new();
        NativeBattleEffect321Frame frame = Frame(0, 92);
        NativeBattleEffect321Frame result = AdvanceMain597AB0(frame, ChildBank, null!, harness.Inputs()).Frame;
        NativeBattleEffect321PositionalSound5013A0 sound = Assert.IsType<NativeBattleEffect321PositionalSound5013A0>(Assert.Single(harness.Operations));
        Assert.Equal(SecondSoundAddress, sound.SourceAddress);
        Assert.Equal(0x15, harness.TargetMask);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, sound.Point8.ReadSnapshot(0, 8).Known);
        Assert.Equal(456u, result.State.ReadDword(InitializerGlobal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_uses_current_vibration_handle_only_when_active(bool active)
    {
        Harness harness = new() { VibrationActive = active };
        NativeBattleEffect321Frame frame = Frame(0, 97);
        frame = frame with { State = frame.State.WriteDword(VibrationGlobal, 321) };
        NativeBattleEffect321Frame result = AdvanceMain597AB0(frame, ChildBank, null!, harness.Inputs()).Frame;
        Assert.Equal(321, harness.QueriedHandle);
        Assert.Equal(789u, result.State.ReadDword(InitializerGlobal));
        if (active)
        {
            Assert.Equal(new NativeBattleEffect321VibrationStop4A2940(321), Assert.Single(harness.Operations));
        }
        else
        {
            Assert.Empty(harness.Operations);
        }
    }

    [Theory]
    [InlineData(99, 0)]
    [InlineData(100, 2)]
    public void Main_retires_only_after_new_signed_age_exceeds_one_hundred(int age, int expected)
    {
        NativeBattleEffect321Step result = AdvanceMain597AB0(Frame(0, age), ChildBank, null!, new Harness().Inputs());
        Assert.Equal(age + 1, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(expected, result.Result);
    }

    [Fact]
    public void Empty_child_scan_runs_new_cleanup_in_same_root_pass_and_waits_real_completion()
    {
        Harness harness = new();
        NativeBattleEffect321Frame frame = Frame(0, null);
        frame = Advance(frame, null!, harness.Inputs());
        NativeBattleModulePoolTask cleanup = frame.Effect.Pools[0].Slots[1];
        Assert.Equal(CleanupCallback, cleanup.Callback);
        Assert.Equal(1, cleanup.ReadWord(12));
        Assert.Equal(0, cleanup.ReadWord(14));
        Assert.Equal(1, frame.Effect.Pools[0].Head);
        Assert.Empty(harness.Operations);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect321AppendTextureLoad508630(FirstPacketAddress, RootBank + 16 + 14), Assert.Single(harness.Operations));
        Assert.Equal(2, Read(frame, RootBank + 16).ReadWord(12));
        frame = WriteCompletionByte(frame, RootBank + 16 + 14, 1);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.NotNull(frame.State.FixedBanks[RootBank]);
        Assert.NotNull(frame.State.FixedBanks[ChildBank]);
        Assert.Single(harness.Operations);
    }

    [Fact]
    public void Retirement_keeps_packet_unknownness_and_both_exclusive_bank_bytes_across_json()
    {
        NativeBattleEffect321Frame frame = Frame(0, null);
        frame = WriteBytes(frame, FirstPacketAddress + 100, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        frame = Advance(frame, null!, new Harness().Inputs());
        frame = WriteCompletionByte(frame, RootBank + 30, 1);
        frame = Advance(frame, null!, new Harness().Inputs());
        NativeBattleEffect321Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect321Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(new[] { true, true, false, true }, restored.State.ReadView(FirstPacketAddress + 100, 4).ReadSnapshot(0, 4).Known);
        Assert.Equal(new byte[] { 1, 2, 4 }, restored.State.ReadView(FirstPacketAddress + 100, 4).ReadSnapshot(0, 4).Data.Where((_, index) => index != 2).ToArray());
        Assert.Empty(restored.Effect.Pools);
    }

    private static NativeBattleEffect321Frame Frame(int rootAge, int? mainAge)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteWord(12, (ushort)rootAge));
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        if (mainAge is int age)
        {
            (child, _) = child.Append(MainCallback);
            child = child.WriteSlot(0, child.Slots[0].WriteBytes(12, new byte[24]).WriteWord(12, (ushort)age));
        }
        NativeBattleEffect321State state = NativeBattleEffect321State.FromColdProcess().DetachBank(RootBank).DetachBank(ChildBank).WriteDword(CasterGlobal, 3);
        NativeBattleOriginalEffectState effect = new(321, 320, new(321, Entry, Initializer), new(3, 0, 0x15, 0, 0, 0), RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public short Yaw { get; init; }
        public NativeBattleVector Anchor { get; init; } = new(100, 200, 300);
        public bool VibrationActive { get; init; }
        public int QueriedHandle { get; private set; }
        public ushort TargetMask { get; private set; }
        public List<uint> Anchors { get; } = [];
        public List<NativeBattleEffect321Operation> Operations { get; } = [];
        public NativeBattleActionPacketsState Packets { get; private set; } = NativeBattleActionPacketsState.Fresh();

        public void SetContacts(int first, params int[] actors)
        {
            byte[] header = new byte[20];
            header[16] = (byte)actors.Length;
            ImmutableArray<NativeBattleContactPacket?> contacts = Packets.Contacts;
            for (int index = 0; index < actors.Length; index++)
            {
                contacts = contacts.SetItem(first + index, new(new((byte)actors[index], 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            Packets = Packets with { Headers = Packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, first, null)), Contacts = contacts };
        }

        public NativeBattleEffect321Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => Packets, (_, _) => NativeBattleEffectArena.Clear().Write(14, BitConverter.GetBytes(Yaw)), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, model, retained) =>
        {
            Anchors.Add(model);
            return new(frame, PutPoint(retained, 0, Anchor));
        }, (frame, mask, retained) =>
        {
            TargetMask = mask;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 456) }, PutPoint(retained, 0, new(10, 20, 30)));
        }, (frame, handle) =>
        {
            QueriedHandle = handle;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 789) }, VibrationActive);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return operation is NativeBattleEffect321VibrationStart4A29A0 start ? frame with { State = frame.State.WriteDword(start.DestinationAddress, 77) } : frame;
        });
    }
}
