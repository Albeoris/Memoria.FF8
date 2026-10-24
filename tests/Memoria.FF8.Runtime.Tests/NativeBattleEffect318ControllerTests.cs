using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect318ControllerTests
{
    private const uint Arena = 0x20DFAB8;

    [Fact]
    public void Initial_root_visit_selects_second_bank_without_consuming_children()
    {
        Harness harness = new();
        NativeBattleEffect318Step result = AdvanceRoot65E5C0(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Root_uses_byte_e_and_holds_age_two_while_current_main_survives()
    {
        Harness harness = new();
        NativeBattleEffect318Frame frame = AddMain(Frame(40), 0);
        NativeBattleEffect318Step result = AdvanceRoot65E5C0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(2, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadByte(14));
        Assert.Equal(0, Read(result.Frame, RootBank).ReadByte(15));
        Assert.Equal(1, Read(result.Frame, Arena).ReadWord(12));
        Assert.IsType<NativeBattleEffect318Advance534210>(Assert.Single(harness.Operations));
    }

    [Fact]
    public void Delayed_bone_survivor_prevents_root_retirement_without_a_main()
    {
        Harness harness = new();
        NativeBattleEffect318Frame frame = Frame(2);
        frame = NativeBattleEffect318Lower.Birth65F250(frame, 22, 8, 1024, 512, harness.Inputs());
        NativeBattleEffect318Step result = AdvanceRoot65E5C0(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(2, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(new NativeBattleEffect318ScaleBone56BEF0(0x1930000 + 22 * 48, 0, 0), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Main_allocation_is_mandatory_even_on_initialized_age_one_reentry()
    {
        NativeBattleEffect318Frame frame = AddMain(Frame(1), 0);
        Assert.Throws<InvalidDataException>(() => AdvanceRoot65E5C0(frame, RootBank, null!, new Harness().Inputs()));
    }

    [Fact]
    public void Empty_first_contact_source_is_not_replaced_with_a_default_actor()
    {
        Harness harness = new();
        harness.SetContacts(0);
        Assert.Throws<NotSupportedException>(() => InitializeMain(AddMain(Frame(0), 0), Arena, null!, harness.Inputs()));
    }

    [Fact]
    public void Retirement_restores_current_common_knownness_and_exclusive_root()
    {
        NativeBattleEffect318Frame frame = Frame(2);
        frame = WriteBytes(frame, Arena + 0xB04, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4 }).MarkUnknown(2, 1), 4);
        NativeBattleEffect318Frame result = Advance(frame, null!, new Harness().Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        (byte[] bytes, bool[] known) = result.Memory.Arena!.ReadSnapshot(0xB04, 4);
        Assert.Equal(new[] { true, true, false, true }, known);
        Assert.Equal(new byte[] { 1, 2, 4 }, bytes.Where((_, index) => known[index]).ToArray());
        NativeBattleEffect318Frame restored = JsonSerializer.Deserialize<NativeBattleEffect318Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(known, restored.Memory.Arena!.ReadSnapshot(0xB04, 4).Known);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-64, 0)]
    [InlineData(-192, 0)]
    [InlineData(2048, 256)]
    [InlineData(-2048, 256)]
    [InlineData(3840, 3164)]
    [InlineData(4096, 4096)]
    public void Quartic_keeps_both_integer_rounding_steps_and_negative_inner_fraction(int fraction, int expected)
    {
        Assert.Equal(expected, Quartic(fraction));
    }

    [Fact]
    public void Sound_precedes_per_callback_io_advance_at_old_age_eight()
    {
        Harness harness = new();
        NativeBattleEffect318Step result = FinishMain(AddMain(Frame(2), 8), Arena, 8, harness.Inputs());
        Assert.Equal(new NativeBattleEffect318Sound501330(SoundAddress, 0, 128), harness.Operations[0]);
        Assert.IsType<NativeBattleEffect318Advance534210>(harness.Operations[1]);
        Assert.Equal(9, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(0, harness.Polls);
    }

    [Fact]
    public void Contact_uses_current_first_absolute_index_after_io_advance()
    {
        Harness harness = new();
        harness.SetContacts(9, 0, 1);
        FinishMain(AddMain(Frame(2), 24), Arena, 24, harness.Inputs());
        Assert.IsType<NativeBattleEffect318Advance534210>(harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect318Contact506690(9), harness.Operations[1]);
        Assert.Equal(2, harness.Operations.Count);
    }

    [Fact]
    public void File_queue_uses_actual_third_packet_destination_before_advance()
    {
        Harness harness = new();
        FinishMain(AddMain(Frame(2), 41), Arena, 41, harness.Inputs());
        Assert.Equal(new NativeBattleEffect318Queue5341D0(360, Arena + ThirdPacketOffset, 0), harness.Operations[0]);
        Assert.IsType<NativeBattleEffect318Advance534210>(harness.Operations[1]);
        Assert.Equal(0, harness.Polls);
    }

    [Fact]
    public void Age_39_restores_only_mask_eight_and_binds_five_current_bone_records()
    {
        Harness harness = new();
        NativeBattleEffect318Step result = FinishMain(AddMain(Frame(2), 39), Arena, 39, harness.Inputs());
        Assert.IsType<NativeBattleEffect318Advance534210>(harness.Operations[0]);
        Assert.Equal(new NativeBattleEffect318MeshMask(ModelAddress(3), uint.MaxValue, 8), harness.Operations[1]);
        Assert.Equal(new[] { 22, 29, 33, 34, 35 }, harness.Bones);
        NativeBattleEffectTaskPool pool = result.Frame.Effect.Pools.Single(value => value.DescriptorAddress == BoneDescriptor);
        Assert.Equal(new ushort[] { 0, 8, 16, 16, 16 }, pool.Slots.Select(value => value.ReadWord(16)).ToArray());
        Assert.All(pool.Slots, value =>
        {
            Assert.Equal(1024, value.ReadWord(20));
            Assert.Equal(512, value.ReadWord(24));
            Assert.Equal(0, value.ReadWord(18));
            Assert.Equal(0, value.ReadWord(22));
        });
    }

    [Theory]
    [InlineData(50, false, 0)]
    [InlineData(50, true, 2)]
    [InlineData(51, false, 0)]
    [InlineData(51, true, 2)]
    public void Main_first_polls_after_increment_to_51_and_holds_while_busy(int age, bool complete, int expected)
    {
        Harness harness = new() { Complete = complete };
        NativeBattleEffect318Step result = FinishMain(AddMain(Frame(2), age), Arena, age, harness.Inputs());
        Assert.Equal(expected, result.Result);
        Assert.Equal(complete ? age + 1 : 51, Read(result.Frame, Arena).ReadWord(12));
        Assert.Equal(1, harness.Polls);
        Assert.Equal(123u, result.Frame.State.ReadDword(InitializerGlobal));
        Assert.IsType<NativeBattleEffect318Advance534210>(Assert.Single(harness.Operations));
    }

    [Fact]
    public void Full_mesh_pool_consumes_only_the_callback_angle()
    {
        NativeBattleEffect318Frame frame = AddMain(Frame(2), 19);
        for (int index = 0; index < 32; index++)
        {
            (frame, _) = Append(frame, MeshDescriptor, 0x65F100);
        }
        NativeBattleEffect318Frame result = BirthMeshes(frame, Arena, new Harness().Inputs());
        Assert.Equal(frame.Random.Draw(out _), result.Random);
    }

    [Fact]
    public void One_free_mesh_uses_two_more_draws_and_preserves_velocity_padding()
    {
        NativeBattleEffect318Frame frame = AddMain(Frame(2), 19);
        for (int index = 0; index < 31; index++)
        {
            (frame, _) = Append(frame, MeshDescriptor, 0x65F100);
        }
        uint final = Arena + 0x1390 + 31 * 0x30;
        frame = WriteBytes(frame, final + 0x1E, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x34, 0x12 }).MarkUnknown(1, 1), 2);
        NativeBattleEffect318Frame result = BirthMeshes(frame, Arena, new Harness().Inputs());
        Assert.Equal(frame.Random.Draw(out _).Draw(out _).Draw(out _), result.Random);
        Assert.Equal(new[] { true, false }, TaskView(Read(result, final), 0x1E, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(new[] { true, false }, TaskView(Read(result, final), 0x2E, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal((byte)0x34, TaskView(Read(result, final), 0x2E, 2).ReadSnapshot(0, 2).Data[0]);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 8)]
    public void Full_ribbon_records_still_consume_each_upper_pair_angle(int time, int draws)
    {
        NativeBattleEffect318Frame frame = AddMain(Frame(2), 4 + time);
        for (int index = 0; index < RecordCount; index++)
        {
            frame = WriteBytes(frame, Arena + RecordOffset + (uint)(index * RecordStride + 6), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)1)), 2);
        }
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect318Frame result = BirthRibbonRecords(frame, Arena, time, null!, new Harness().Inputs());
        Assert.Equal(expected, result.Random);
        Assert.Equal(Arena + RecordOffset, Read(result, Arena).ReadDword(RecordCursorOffset));
    }

    private static NativeBattleEffect318Frame AddMain(NativeBattleEffect318Frame frame, int age)
    {
        (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords701200(frame, main + 12, 0x4BE);
        return Write(frame, main, Read(frame, main).WriteWord(12, (ushort)age).WriteDword(RecordCursorOffset, main + RecordOffset));
    }

    private static NativeBattleEffect318Frame Frame(int age, bool initialized = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)age).WriteByte(14, initialized ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                pools = pools.Add(NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity));
            }
        }
        NativeBattleEffect318State state = NativeBattleEffect318State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(318, 317, new(318, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, pools), effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public bool Complete { get; init; }
        public int Polls { get; private set; }
        public List<int> Bones { get; } = [];
        public List<NativeBattleEffect318Operation> Operations { get; } = [];
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

        public NativeBattleEffect318Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, _, retained) => new(frame, retained), (frame, _, retained) => new(frame, retained), (_, model, bone) =>
        {
            Assert.Equal(ModelAddress(3), model);
            Bones.Add(bone);
            return checked(0x1930000u + (uint)(bone * 48));
        }, (frame, index) =>
        {
            Assert.Equal(0, index);
            Polls++;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 123) }, Complete);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
