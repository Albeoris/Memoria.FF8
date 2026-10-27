using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x500;

    [Fact]
    public void Preinitialization_root_resets_intensity_and_publishes_stage_without_io_or_children()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = Frame(0, false);
        frame = frame with { State = frame.State.WriteDword(IntensityGlobal, 999) };
        NativeBattleEffect285Step result = AdvanceRoot67A900(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(result.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Single(result.Frame.Effect.Pools);
        Assert.Equal(new NativeBattleEffect285Stage5713E0(0, 0), Assert.Single(harness.Operations));
        Assert.Equal(0, harness.Polls);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public void Main_drain_polls_real_io_without_incrementing_root_age(bool complete, int expected)
    {
        Harness harness = new() { Complete = complete };
        NativeBattleEffect285Step result = AdvanceRoot67A900(Frame(17, true, false), RootBank, null!, harness.Inputs());
        Assert.Equal(expected, result.Result);
        Assert.Equal(17, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(1, harness.Polls);
        Assert.Equal(0, harness.LastPollIndex);
        Assert.Collection(harness.Operations, operation => Assert.Equal(new NativeBattleEffect285Stage5713E0(0, 0), operation), operation => Assert.IsType<NativeBattleEffect285IoAdvance534210>(operation), operation => Assert.Equal(new NativeBattleEffect285Stage5713E0(0, 0), operation));
        Assert.Equal(123u, result.Frame.State.ReadDword(InitializerGlobal));
    }

    [Fact]
    public void Live_main_updates_stage_and_increments_root_without_advancing_io()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = Frame(10);
        frame = Write(frame, Main, Read(frame, Main).WriteWord(12, 32));
        NativeBattleEffect285Step result = AdvanceRoot67A900(frame, RootBank, null!, harness.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(11, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(33, Read(result.Frame, Main).ReadWord(12));
        Assert.Contains(new NativeBattleEffect285Stage5713E0(2400, 0), harness.Operations);
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect285IoAdvance534210);
        Assert.Equal(0, harness.Polls);
    }

    [Fact]
    public void Retirement_preserves_orphan_particle_flags_and_payload_knownness_in_common()
    {
        Harness harness = new() { Complete = true };
        NativeBattleEffect285Frame frame = Frame(17, true, false);
        NativeBattleEffectTaskPool particles = frame.Effect.Pools[2];
        (particles, int slot) = particles.Append(ParticleCallback);
        particles = particles.WriteSlot(slot, particles.Slots[slot].WriteBytes(12, new byte[8]).MarkPayloadUnknown(14, 1)).UpdateLinks(null, null);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, particles) } };
        NativeBattleEffect285Frame result = Advance(frame, null!, harness.Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(0, 2)));
        Assert.False(result.Memory.Arena.ReadSnapshot(14, 1).Known[0]);
        NativeBattleEffect285Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect285Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.False(restored.Memory.Arena!.ReadSnapshot(14, 1).Known[0]);
    }

    [Theory]
    [InlineData(30000, 30000, -2768)]
    [InlineData(-30000, -30000, 2768)]
    [InlineData(-3, 0, -1)]
    public void Target_centroid_wraps_word_sum_before_signed_division(int firstZ, int secondZ, int expected)
    {
        Harness harness = new();
        harness.Z[0] = (short)firstZ;
        harness.Z[1] = (short)secondZ;
        harness.SetContacts(5, 0, 1);
        NativeBattleEffect285Frame result = CaptureTargetCentroid(Frame(0), harness.Inputs());
        Assert.Equal(expected, unchecked((short)result.State.ReadWord(CentroidGlobal)));
        Assert.Equal(new uint[] { ModelAddress(0), ModelAddress(1) }, harness.ModelReads);
    }

    [Fact]
    public void Empty_native_contact_group_does_not_invent_a_centroid()
    {
        Harness harness = new();
        harness.SetContacts(0);
        Assert.Throws<NotSupportedException>(() => CaptureTargetCentroid(Frame(0), harness.Inputs()));
        Assert.Empty(harness.ModelReads);
    }

    [Fact]
    public void Snapshot_reads_all_seven_flags_and_stores_zeroextended_dwords()
    {
        Harness harness = new();
        for (int actor = 0; actor < 7; actor++)
        {
            harness.Flags[actor] = actor == 4 ? (ushort)0 : (ushort)(0xAB00 + actor);
        }
        NativeBattleEffect285Frame result = CaptureModelFlags67ABD0(Frame(0), harness.Inputs());
        Assert.Equal(Enumerable.Range(0, 7).Select(ModelAddress), harness.ModelReads);
        for (int actor = 0; actor < 7; actor++)
        {
            Assert.Equal((uint)harness.Flags[actor], result.State.ReadDword(SavedFlagsAddress + (uint)(actor * 4)));
        }
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Normalized_view_reads_only_column_y_and_preserves_output_padding_and_translation_unknownness()
    {
        NativeBattleEffect285Frame frame = Frame(0);
        frame = frame with { State = frame.State.WriteView(ViewAddress, frame.State.ReadView(ViewAddress, 32).MarkUnknown(18, 2), 32) };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = -22, Z = 33 }, null);
        bool[] known = packed.Known.ToArray();
        known[0] = false;
        known[1] = false;
        known[27] = false;
        packed = packed with { Known = known.ToImmutableArray() };
        NativeBattleEffect285Frame result = NormalizeView67AAE0(frame, packed);
        NativeBattleEffectArena view = result.State.ReadView(ViewAddress, 32);
        Assert.All(view.ReadSnapshot(0, 18).Known, value => Assert.True(value));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(view.Read(0, 2)));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(view.Read(8, 2)));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(view.Read(16, 2)));
        Assert.Equal(new[] { false, false }, view.ReadSnapshot(18, 2).Known);
        Assert.Equal(packed.Known.AsSpan().Slice(20).ToArray(), view.ReadSnapshot(20, 12).Known);
        Assert.Equal(frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-256, 96)]
    public void Caller16_preserves_unknown_w_and_exact_signed_depth_phase_morph(int depth, int phase)
    {
        NativeBattleEffectArena caller = ProgrammeCaller(PointView(new(1, -10000, 3)), (short)depth, (ushort)phase, Arena + MorphOffset);
        Assert.Equal(new[] { false, false }, caller.ReadSnapshot(6, 2).Known);
        Assert.Equal((short)depth, BinaryPrimitives.ReadInt16LittleEndian(caller.Read(8, 2)));
        Assert.Equal((ushort)phase, BinaryPrimitives.ReadUInt16LittleEndian(caller.Read(10, 2)));
        Assert.Equal(Arena + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(caller.Read(12, 4)));
    }

    [Fact]
    public void Successful_early_births_use_two_points_without_crt_draws()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = Frame(0);
        NativeBattleEffect285Frame result = BirthSprites(frame, 15, Resources(), harness.Inputs());
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(2, harness.BoneQueries);
        Assert.Equal(new short[] { 100, -100 }, harness.LocalXs);
        Assert.Equal(777u, result.State.ReadDword(InitializerGlobal));
        NativeBattleEffectTaskPool particles = result.Effect.Pools[2];
        Assert.Equal(2, particles.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(0, particles.Slots[0].ReadWord(18));
        Assert.Equal(0, particles.Slots[1].ReadWord(18));
    }

    [Fact]
    public void Extra_births_consume_three_crt_values_each_in_point_order()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = Frame(0);
        NativeCrtRandomState expected = frame.Random;
        ushort[] values = new ushort[24];
        for (int index = 0; index < values.Length; index++)
        {
            expected = expected.Draw(out values[index]);
        }
        NativeBattleEffect285Frame result = BirthSprites(frame, 16, Resources(), harness.Inputs());
        Assert.Equal(expected, result.Random);
        Assert.Equal(2, harness.BoneQueries);
        NativeBattleEffectTaskPool pool = result.Effect.Pools[2];
        Assert.Equal(10, pool.Slots.Count(task => (task.Flags & 1) != 0));
        int random = 0;
        foreach (int first in new[] { 0, 5 })
        {
            for (int extra = first + 1; extra <= first + 4; extra++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int coordinate = Signed(pool.Slots[first], 12 + axis * 2) + (values[random++] & 511) - 256;
                    Assert.Equal(unchecked((ushort)coordinate), pool.Slots[extra].ReadWord(12 + axis * 2));
                }
                Assert.Equal(0, pool.Slots[extra].ReadWord(18));
            }
        }
    }

    [Fact]
    public void Failed_optional_births_do_not_read_points_or_consume_random()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = FillParticles(Frame(0), 64);
        NativeBattleEffect285Frame result = BirthSprites(frame, 16, null!, harness.Inputs());
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(0, harness.BoneQueries);
        Assert.Equal(frame.Effect.Pools, result.Effect.Pools);
    }

    [Fact]
    public void Last_available_base_slot_does_not_consume_failed_extra_random()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = FillParticles(Frame(0), 63);
        NativeBattleEffect285Frame result = BirthSprites(frame, 16, Resources(), harness.Inputs());
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(1, harness.BoneQueries);
        Assert.Equal(64, result.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void Early_camera_uses_current_caster_offsets_and_retains_both_destination_padding_words()
    {
        Harness harness = new();
        AdvanceCamera(Frame(0), 0, null!, harness.Inputs());
        Assert.Equal(new NativeBattleVector(-604, -1975, 2585), Vector(harness.Camera.Target8));
        Assert.Equal(new NativeBattleVector(-1170, -217, 5328), Vector(harness.Camera.Eye8));
        Assert.Equal((ushort)0x1111, BinaryPrimitives.ReadUInt16LittleEndian(harness.Camera.Eye8.Read(6, 2)));
        Assert.Equal((ushort)0x2222, BinaryPrimitives.ReadUInt16LittleEndian(harness.Camera.Target8.Read(6, 2)));
        Assert.Equal((short)288, harness.Camera.ScalarFocal);
        Assert.Equal((short)0, harness.Camera.Roll);
        Assert.IsType<NativeBattleEffect285CameraForce50A730>(harness.Operations[0]);
        Assert.Equal(new[] { ModelAddress(0), ModelAddress(1), ModelAddress(2) }, harness.Operations.OfType<NativeBattleEffect285ModelFlags>().Select(operation => operation.ModelAddress));
    }

    [Theory]
    [InlineData(87, 1, 128)]
    [InlineData(94, 0, 128)]
    [InlineData(96, 0, 96)]
    public void Late_flash_uses_the_actual_common_envelope_arguments(int age, int fall, int intensity)
    {
        Harness harness = new();
        AdvanceCamera(Frame(0), age, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect285Flash5712C0(0, 1, fall, (short)intensity), Assert.Single(harness.Operations.OfType<NativeBattleEffect285Flash5712C0>()));
    }

    [Theory]
    [InlineData(0, 0x108FAA8u)]
    [InlineData(48, 0x108FAACu)]
    public void Sounds_keep_original_descriptor_flags_and_pan(int age, uint source)
    {
        Harness harness = new();
        EmitMainEvents(Frame(0), age, harness.Inputs());
        Assert.Equal(new NativeBattleEffect285Sound501330(source, 0, 128), Assert.Single(harness.Operations));
    }

    [Fact]
    public void All_contacts_use_current_header_pointer_and_actual_byte_count()
    {
        Harness harness = new();
        harness.SetContacts(7, 0, 1, 2);
        EmitContacts(Frame(0), 110, harness.Inputs());
        Assert.Equal(new NativeBattleEffect285AllContacts506BA0(7, 3), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Terminal_main_restores_four_saved_dwords_with_unknown_padding_then_queues_actual_file()
    {
        Harness harness = new();
        NativeBattleEffect285Frame frame = Frame(17);
        frame = Write(frame, Main, Read(frame, Main).WriteWord(12, 134));
        NativeBattleEffect285Step result = AdvanceMain67AC00(frame, Main, null!, harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(135, Read(result.Frame, Main).ReadWord(12));
        Assert.Equal(300u, result.Frame.State.ReadDword(IntensityGlobal));
        Assert.Equal(new NativeBattleEffect285IoQueue5341D0(360, Arena + ThirdPacketOffset, 0), harness.Operations[^1]);
        Assert.Equal(Vector(harness.Camera.SavedEye8), Vector(harness.Camera.Eye8));
        Assert.Equal(new[] { false, false }, harness.Camera.Eye8.ReadSnapshot(6, 2).Known);
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(harness.Camera.Target8.Read(6, 2)));
        Assert.Equal((short)512, harness.Camera.ScalarFocal);
        Assert.Equal((short)99, harness.Camera.Roll);
        Assert.Equal(2, harness.Operations.OfType<NativeBattleEffect285CameraPoint>().Count(operation => operation.CopyPadding));
        Assert.DoesNotContain(harness.Operations, operation => operation is NativeBattleEffect285CameraRoll or NativeBattleEffect285CameraForce50A730);
    }

    [Fact]
    public void Unknown_saved_focal_is_not_replaced_with_current_scalar_or_projection()
    {
        Harness harness = new();
        harness.Camera = harness.Camera with { SavedScalarFocal = null };
        Assert.Throws<NotSupportedException>(() => RestoreSavedCamera(Frame(0), harness.Inputs()));
        Assert.Empty(harness.Operations);
    }

    private static NativeBattleEffect285Frame FillParticles(NativeBattleEffect285Frame frame, int count)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        for (int index = 0; index < count; index++)
        {
            (pool, _) = pool.Append(ParticleCallback);
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
    }

    private static NativeBattleEffect285Resources Resources() => new(null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x108FAB8, PointView(new(100, 20, 30)).Write(6, BitConverter.GetBytes((short)10)).Read(0, 8).ToImmutableArray()).Add(0x108FAB0, PointView(new(-100, -20, -30)).Write(6, BitConverter.GetBytes((short)10)).Read(0, 8).ToImmutableArray()));

    private static NativeBattleEffect285Frame Frame(int rootAge, bool initialized = true, bool main = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        (root, _) = root.Append(RootCallback);
        root = root.WriteSlot(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, (ushort)rootAge).WriteByte(15, initialized ? (byte)1 : (byte)0));
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (initialized)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity);
                if (shape.Descriptor == MainDescriptor && main)
                {
                    (pool, _) = pool.Append(MainCallback);
                    pool = pool.WriteSlot(0, pool.Slots[0].WriteBytes(12, new byte[shape.Stride - 12]));
                }
                pools = pools.Add(pool);
            }
        }
        NativeBattleEffect285State state = NativeBattleEffect285State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(285, 284, new(285, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public bool Complete { get; init; }
        public int Polls { get; private set; }
        public int LastPollIndex { get; private set; }
        public int BoneQueries { get; private set; }
        public List<short> LocalXs { get; } = [];
        public List<uint> ModelReads { get; } = [];
        public List<NativeBattleEffect285Operation> Operations { get; } = [];
        public ushort[] Flags { get; } = new ushort[7];
        public short[] Z { get; } = Enumerable.Repeat((short)300, 7).ToArray();
        public NativeBattleActionPacketsState Packets { get; private set; } = NativeBattleActionPacketsState.Fresh();
        public NativeBattleEffect285CameraSnapshot Camera { get; set; } = new(PointView(new(1, 2, 3)).Write(6, BitConverter.GetBytes((ushort)0x1111)), PointView(new(4, 5, 6)).Write(6, BitConverter.GetBytes((ushort)0x2222)), PointView(new(11, 22, 33)), PointView(new(44, 55, 66)).Write(6, BitConverter.GetBytes((ushort)0xCAFE)), 288, 512, 99);

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

        public NativeBattleEffect285Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets, (_, pointer) =>
        {
            ModelReads.Add(pointer);
            int actor = checked((int)((pointer - ModelAddress(0)) / 156));
            return NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(Flags[actor])).Write(28, BitConverter.GetBytes((short)100)).Write(30, BitConverter.GetBytes((short)-200)).Write(32, BitConverter.GetBytes(Z[actor]));
        }, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => Camera, (frame, _, local, retained) =>
        {
            BoneQueries++;
            LocalXs.Add(Vector(local).X);
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 777) }, PutPoint(retained, 0, Vector(local)));
        }, (frame, index) =>
        {
            Polls++;
            LastPollIndex = index;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 123) }, Complete);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect285CameraPoint point:
                    NativeBattleEffectArena prior = point.Eye ? Camera.Eye8 : Camera.Target8;
                    NativeBattleEffectArena updated = CopyBytes(point.Point8, 0, prior, 0, point.CopyPadding ? 8 : 6);
                    Camera = point.Eye ? Camera with { Eye8 = updated } : Camera with { Target8 = updated };
                    break;
                case NativeBattleEffect285CameraScalarFocal focal:
                    Camera = Camera with { ScalarFocal = focal.Value };
                    break;
                case NativeBattleEffect285CameraRoll roll:
                    Camera = Camera with { Roll = roll.Value };
                    break;
            }
            return frame;
        });
    }
}
