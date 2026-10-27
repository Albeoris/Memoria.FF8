using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDevour62ControllerTests
{
    [Fact]
    public void Entry_retains_packet_bytes_and_current_cursors_but_publishes_actual_bank_bases()
    {
        Fixture fixture = new();
        NativeBattleDevour62State retained = fixture.Frame.State with { PrimaryDrawCursor = 123, SecondaryDrawCursor = 456, Counter2601918 = 99, Counter2601770 = 98 };
        NativeBattleEffectMemory memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.Write(256, new byte[] { 0xCD, 0xAB }) };
        NativeBattleDevour62Frame frame = NativeBattleDevour62Controller.Initialize(memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.Equal(new ushort[] { 100, 88, 84 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 10 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(123, frame.State.PrimaryDrawCursor);
        Assert.Equal(456, frame.State.SecondaryDrawCursor);
        Assert.Equal(256 + 0x6000, frame.State.PrimaryDrawBase);
        Assert.Equal(256, frame.State.AlternatePrimaryDrawBase);
        Assert.Equal(256 + 0xC000, frame.State.SecondaryDrawBase);
        Assert.Equal(256 + 0x6000, frame.State.AlternateSecondaryDrawBase);
        Assert.Equal(256 + 0xC000, frame.State.AuxiliaryCursor);
        Assert.Equal(0u, frame.State.Counter2601918);
        Assert.Equal(0u, frame.State.Counter2601770);
        Assert.Equal(new byte[] { 0xCD, 0xAB }, frame.Memory.Arena!.Read(256, 2));
        frame = fixture.Step(frame);
        Assert.Equal(256 + 0x6000, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + 0xC000, frame.State.SecondaryDrawCursor);
        Assert.Equal(fixture.Inputs.Camera, frame.Memory.SharedEffectView2793E58);
    }

    [Theory]
    [InlineData(0, 0x6000, 0xC000)]
    [InlineData(1, 0, 0x6000)]
    public void Root_selects_both_actual_primary_and_secondary_banks_from_its_preadvance_parity(ushort parity, int primary, int secondary)
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint address = hierarchy.Address(0, 0);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, 10).WriteWord(0x5C, parity)).Pools } };
        frame = fixture.Step(frame);
        Assert.Equal(256 + primary, frame.State.PrimaryDrawCursor);
        Assert.Equal(256 + secondary, frame.State.SecondaryDrawCursor);
        Assert.Equal((ushort)(parity + 1), new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadWord(0x5C));
        Assert.Empty(fixture.Operations);
        NativeBattleDevour62Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDevour62Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Step(frame)), NativeBattleTestSnapshots.Json(fixture.Step(restored)));
    }

    [Theory]
    [InlineData(74, false)]
    [InlineData(75, true)]
    [InlineData(76, false)]
    public void Target_parent_release_occurs_only_at_exact_age75(ushort age, bool released)
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.TargetCallback, 1, age, out uint address);
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint root = hierarchy.Read(address).ReadDword(0x10);
        Assert.Equal(released ? (byte)0 : (byte)1, hierarchy.Read(root).ReadByte(0x63));
        Assert.Equal(released ? (byte)2 : (byte)1, hierarchy.Read(address).ReadByte(0x29));
        Assert.Empty(fixture.Operations.OfType<NativeBattleDevour62Contact>());
    }

    [Theory]
    [InlineData(82, false)]
    [InlineData(83, true)]
    public void Selected_contact_threshold_is83_and_callback_state_survives(ushort age, bool contacts)
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.TargetCallback, 2, age, out _);
        NativeCrtRandomState before = frame.Random;
        frame = fixture.Step(frame);
        Assert.Equal(contacts ? 1 : 0, fixture.Operations.OfType<NativeBattleDevour62Contact>().Count());
        Assert.Equal(contacts ? before.Draw(out _) : before, frame.Random);
    }

    [Theory]
    [InlineData(0, 0x2601904u)]
    [InlineData(1, 0x26018F8u)]
    [InlineData(2, 0x26018FCu)]
    public void Upload_order_and_single_crt_draw_select_each_original_variant(int variant, uint global)
    {
        Fixture fixture = new();
        uint seed = Enumerable.Range(1, 1000).Select(value => (uint)value).First(value =>
        {
            new NativeCrtRandomState(value).Draw(out ushort draw);
            return draw % 3 == variant;
        });
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.AuxiliaryCallback, 0, 0, out uint address) with { Random = new(seed) };
        NativeCrtRandomState expected = frame.Random.Draw(out _);
        frame = fixture.Step(frame);
        Assert.Equal(new uint[] { 0x2601900, global }, fixture.Operations.OfType<NativeBattleDevour62UploadTim>().Select(operation => operation.GlobalAddress));
        Assert.Equal(expected, frame.Random);
        Assert.Equal((ushort)variant, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadWord(0x4E));
    }

    [Fact]
    public void Auxiliary_appends_fade_music_and_quad_in_native_order_and_children_tick_in_same_pass()
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.AuxiliaryCallback, 1, 1, out _);
        frame = fixture.Step(frame);
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[2].Slots.Where(task => task.Callback != 0).Skip(1).ToArray();
        Assert.Equal(new uint[] { NativeBattleDevour62Profile.ActorFadeCallback, NativeBattleDevour62Profile.MusicCallback, NativeBattleDevour62Profile.QuadCallback }, children.Select(task => task.Callback));
        Assert.All(children, task => Assert.Equal((ushort)1, task.ReadWord(0x24)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(7, 7)]
    [InlineData(9, 8)]
    public void Sound_clamps_signed_first_contact_outcome_and_uses_resolved_descriptor(short outcome, int index)
    {
        Fixture fixture = new(outcome);
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.AuxiliaryCallback, 2, 35, out _);
        fixture.Step(frame);
        NativeBattleModuleSound sound = Assert.Single(fixture.Operations.OfType<NativeBattleDevour62Sound>()).Sound;
        Assert.Equal(fixture.Resources.SoundSources[index].Value, sound.Sound);
        Assert.Equal(127u, sound.Volume);
        Assert.Equal(128u, sound.RawPan);
    }

    [Fact]
    public void Sound_uses_first_captured_header_outcome_instead_of_child_group_selector()
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = SelectSecondGroup(fixture.Child(NativeBattleDevour62Profile.AuxiliaryCallback, 2, 35, out uint address), address);
        NativeBattleDevour62Inputs inputs = fixture.Inputs with { Packets = TwoHeaders(fixture, 0, 8) };
        NativeBattleDevour62Controller.Advance(frame, fixture.Resources, inputs);
        NativeBattleModuleSound sound = Assert.Single(fixture.Operations.OfType<NativeBattleDevour62Sound>()).Sound;
        Assert.Equal(fixture.Resources.SoundSources[0].Value, sound.Sound);
        Assert.Empty(fixture.Operations.OfType<NativeBattleDevour62Contact>());
    }

    [Theory]
    [InlineData(8, 0, false)]
    [InlineData(0, 8, true)]
    public void Hide_target_uses_first_captured_header_outcome_independently_of_child_group(short first, short second, bool hidden)
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = SelectSecondGroup(fixture.Child(NativeBattleDevour62Profile.ActorFadeCallback, 1, 11, out uint address), address);
        NativeBattleDevour62Inputs inputs = fixture.Inputs with { Packets = TwoHeaders(fixture, first, second) };
        NativeBattleDevour62Controller.Advance(frame, fixture.Resources, inputs);
        NativeBattleDevour62WriteActorFlags[] writes = fixture.Operations.OfType<NativeBattleDevour62WriteActorFlags>().ToArray();
        Assert.Equal(hidden ? 1 : 0, writes.Length);
        if (hidden)
        {
            Assert.Equal(new NativeBattleDevour62WriteActorFlags(3, (ushort)(fixture.Models[3].Flags0 | 4)), writes[0]);
        }
        Assert.IsType<NativeBattleDevour62StageFade>(fixture.Operations[0]);
        Assert.IsType<NativeBattleDevour62FadeActors>(fixture.Operations[1]);
    }

    [Fact]
    public void Formula_contact_keeps_its_signed_group_selector_while_outcome_consumers_use_first_header()
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = SelectSecondGroup(fixture.Child(NativeBattleDevour62Profile.TargetCallback, 2, 83, out uint address), address);
        NativeBattleDevour62Inputs inputs = fixture.Inputs with { Packets = TwoHeaders(fixture, 8, 0) };
        NativeBattleDevour62Controller.Advance(frame, fixture.Resources, inputs);
        Assert.Equal(1, Assert.Single(fixture.Operations.OfType<NativeBattleDevour62Contact>()).Index);
        Assert.Empty(fixture.Operations.OfType<NativeBattleDevour62Sound>());
    }

    [Theory]
    [InlineData(16, 0, false)]
    [InlineData(32, 16, true)]
    public void Quad_fade_final_zero_is_not_drawn(short before, short after, bool draws)
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.QuadCallback, 2, 80, out uint address);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x50, unchecked((ushort)before))).Pools } };
        frame = fixture.Step(frame);
        Assert.Equal(draws ? 1 : 0, fixture.Operations.OfType<NativeBattleDevour62DrawQuads>().Count());
        Assert.Equal(unchecked((ushort)after), new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address).ReadWord(0x50));
    }

    [Fact]
    public void Fade_initialization_publishes_native_operation_order_and_captured_flags_stay_in_task()
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.ActorFadeCallback, 0, 10, out uint address);
        frame = fixture.Step(frame);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        for (int actor = 0; actor < 7; actor++)
        {
            Assert.Equal((uint)fixture.Models[actor].Flags0, hierarchy.Read(address).ReadDword(0x30 + 4 * actor));
        }
        Assert.Empty(fixture.Operations);
        frame = fixture.Step(frame);
        Assert.IsType<NativeBattleDevour62StageFade>(fixture.Operations[0]);
        Assert.Equal(new NativeBattleDevour62StageFade(4096, true), fixture.Operations[0]);
        Assert.Equal(new NativeBattleDevour62FadeActors(4096), fixture.Operations[1]);
        Assert.Equal(new NativeBattleDevour62WriteActorFlags(3, (ushort)(fixture.Models[3].Flags0 | 4)), fixture.Operations[2]);
    }

    [Fact]
    public void Snapshot_preserves_active_children_rng_and_current_packet_cursor()
    {
        Fixture fixture = new();
        NativeBattleDevour62Frame frame = fixture.Child(NativeBattleDevour62Profile.AuxiliaryCallback, 0, 0, out _);
        frame = fixture.Step(frame);
        NativeBattleDevour62Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDevour62Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Step(frame)), NativeBattleTestSnapshots.Json(fixture.Step(restored)));
        Assert.Null(NativeBattleDevour62State.FromColdProcess(0, 0).PrimaryDrawCursor);
    }

    private static NativeBattleDevour62Frame SelectSecondGroup(NativeBattleDevour62Frame frame, uint address)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x2A, 1).WriteByte(0x2B, 0)).Pools } };
    }

    private static NativeBattleActionPacketsState TwoHeaders(Fixture fixture, short first, short second)
    {
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        int header = fixture.Frame.Effect.Parameters!.HeaderIndex7C;
        NativeBattleActionHeaderSlot firstHeader = packets.Headers[header] with { FirstContactIndex = 0 };
        NativeBattleActionHeaderSlot secondHeader = firstHeader with { FirstContactIndex = 1 };
        NativeBattleContactPacket contact = packets.ReadContact(0);
        return packets with
        {
            ActionCount = 2,
            Headers = packets.Headers.SetItem(header, firstHeader).SetItem(header + 1, secondHeader),
            Contacts = packets.Contacts.SetItem(0, contact with { Primary = contact.Primary with { Amount6 = unchecked((ushort)first) } }).SetItem(1, contact with { Primary = contact.Primary with { Amount6 = unchecked((ushort)second) } })
        };
    }

    internal sealed class Fixture
    {
        public NativeBattleDevour62Frame Frame { get; }
        public NativeBattleDevour62Resources Resources { get; }
        public NativeBattleDevour62Inputs Inputs { get; }
        public NativeBattleGraphicsModel[] Models { get; }
        public List<NativeBattleDevour62Operation> Operations { get; } = [];
        public Fixture(short outcome = 0)
        {
            var prior = NativeBattleRaldo19ControllerTests.Fixture();
            byte[] camera = new byte[1968];
            BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 1968);
            uint[] values = [0x494CA, 0x494BD, 0x494B7, 0x494BF, 0x494B1, 0x494AE, 0x494B4, 0x494BA, 0x494DF];
            Resources = new(new("camera", camera.ToImmutableArray()), values.Select((value, index) => new NativeBattleDevour62SoundSource(0x1596290u + (uint)index * 4, value)).ToImmutableArray());
            NativeBattleActionPacketsState packets = prior.Inputs.Packets;
            NativeBattleContactPacket contact = packets.ReadContact(0);
            packets = packets with { Contacts = packets.Contacts.SetItem(0, contact with { Primary = contact.Primary with { Amount6 = unchecked((ushort)outcome) } }) };
            NativeBattleOriginalEffectState effect = new(62, 61, new(62, NativeBattleDevour62Profile.Entry, NativeBattleDevour62Profile.Initializer), prior.Frame.Effect.Parameters, null) { ResourcePointers = [new(NativeBattleDevour62Profile.InitializerGlobal, 256)] };
            Frame = NativeBattleDevour62Controller.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(61), effect, new(1), packets, Resources, NativeBattleDevour62State.FromColdProcess(123, 456));
            Models = Enumerable.Range(0, 7).Select(prior.Inputs.ReadModel).ToArray();
            Inputs = new(packets, NativeBattlePackedMatrix32.FromMatrix(prior.Inputs.Camera, null), index => Models[index], prior.Inputs.ReadPoseSource, (frame, operation) =>
            {
                Operations.Add(operation);
                return operation is NativeBattleDevour62Contact ? frame with { Random = frame.Random.Draw(out _) } : frame;
            });
        }

        public NativeBattleDevour62Frame Step(NativeBattleDevour62Frame frame) => NativeBattleDevour62Controller.Advance(frame, Resources, Inputs);

        public NativeBattleDevour62Frame Child(uint callback, byte phase, ushort age, out uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(Frame.Effect.Pools);
            uint root = hierarchy.Address(0, 0);
            hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 6).WriteByte(0x63, 1));
            int pool = callback == NativeBattleDevour62Profile.TargetCallback ? 1 : 2;
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(pool, callback, pool == 1 ? 88 : 84, root, (_, _, _) => new(0, 3));
            address = append.Address;
            hierarchy = append.Hierarchy.Write(address, append.Hierarchy.Read(address).WriteByte(0x29, phase).WriteWord(0x24, age));
            return Frame with { Effect = Frame.Effect with { Pools = hierarchy.Pools } };
        }
    }
}
