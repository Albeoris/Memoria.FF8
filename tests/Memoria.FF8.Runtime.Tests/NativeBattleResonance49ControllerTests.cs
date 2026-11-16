using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleResonance49Controller;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49ControllerTests
{
    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, false)]
    [InlineData((byte)2, true)]
    [InlineData((byte)3, false)]
    public void Entry_uses_literal_mask_one_and_six_full_reset_bank_owners(byte flags, bool register)
    {
        var frame = Inactive(flags);
        frame = frame with { State = frame.State.WriteWord(ScreenObjectAddress, 0xABCD).WriteDword(0x26480E8, 99).WriteDword(0x2646BE8, 77) with { InactiveBanks = Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().Write(pool.Stride * pool.Capacity - 2, new byte[] { 0xAA, 0xBB })).ToImmutableArray() } };
        var actual = Initialize(frame, Resources(), Inputs([]));
        Assert.Equal(6, actual.Effect.Pools.Length);
        Assert.All(actual.State.InactiveBanks, bank => Assert.Null(bank));
        Assert.Equal(RootDescriptor, actual.Effect.ReturnedDescriptor);
        Assert.Equal(3, Read(actual, RootBank).ReadByte(0x2C));
        Assert.Equal(0, Read(actual, RootBank).ReadByte(0x2D));
        Assert.Equal((ushort)2, Read(actual, RootBank).ReadWord(0x5A));
        Assert.Equal((ushort)0, Read(actual, RootBank).ReadWord(0x58));
        Assert.Equal(1, Read(actual, RootBank).ReadByte(0x2F));
        foreach (var pool in actual.Effect.Pools)
        {
            Assert.Equal((ushort)0, pool.Slots[^1].ReadWord(pool.Stride - 2));
        }
        Assert.Equal((ushort)0xABCD, actual.State.ReadWord(ScreenObjectAddress));
        Assert.Equal(0u, actual.State.ReadDword(0x26480E8));
        Assert.Equal(0u, actual.State.ReadDword(0x2646BE8));
        uint source = NativeBattleEffectNodeMemory.ArenaAddress + 0x30000u;
        Assert.Equal(source, actual.State.ReadDword(InitializerGlobal));
        Assert.Equal(source + 0xC000, actual.State.ReadDword(0x2647818));
        Assert.Equal(source + 0x18000, actual.State.ReadDword(0x26480EC));
        NativeBattleEffectTextureState textures = actual.Memory.Textures ?? throw new InvalidDataException();
        Assert.Equal(register ? 1 : 0, textures.Count);
        Assert.Equal(register, textures.Animation is not null);
        Assert.Same(frame.Memory.Arena, actual.Memory.Arena);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)0, 0x3C000u, 0x48000u)]
    [InlineData((ushort)1, 0x30000u, 0x3C000u)]
    [InlineData((ushort)65535, 0x30000u, 0x3C000u)]
    public void Root_uses_old_WORD_parity_and_preserves_packed_camera_knownness_without_pause(ushort old, uint primary, uint secondary)
    {
        var frame = Frame(0, RootCallback, 10);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, old).WriteWord(0x24, 0xFFFF));
        List<NativeBattleResonance49Operation> operations = [];
        var view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 12345 }, null);
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs(operations) with { GraphicsFlags = 0x201, ReadView = _ => view });
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + primary, actual.Frame.State.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + secondary, actual.Frame.State.ReadDword(SecondaryCursorGlobal));
        Assert.Equal(unchecked((ushort)(old + 1)), Read(actual.Frame, RootBank).ReadWord(0x5C));
        Assert.Equal((ushort)0, Read(actual.Frame, RootBank).ReadWord(0x24));
        Assert.Equal(view, actual.Frame.Memory.SharedEffectView2793E58);
        Assert.False(actual.Frame.Memory.SharedEffectView2793E58!.Known[18]);
        Assert.Equal(new NativeBattleResonance49UpdateCaster8DC740(RootBank), Assert.Single(operations));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Root_new_children_run_same_scan_and_effect_acknowledgement_requires_two_callbacks_before_mode_one_births()
    {
        var frame = Frame(0, RootCallback, 3);
        List<NativeBattleResonance49Operation> operations = [];
        var inputs = Inputs(operations);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(4, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal(1, Read(frame, Pools[1].Bank).ReadByte(0x29));
        Assert.Equal(1, Read(frame, Pools[2].Bank).ReadByte(0x29));
        Assert.Equal((ushort)0, frame.State.ReadWord(EffectObjectAddress + 0x40));
        Assert.Equal((ushort)0, frame.State.ReadWord(EffectObjectAddress + 0x42));
        Assert.Equal((ushort)1, frame.State.ReadWord(EffectObjectAddress + 0x44));
        Assert.Equal((ushort)2, Read(frame, RootBank).ReadWord(0x5E));
        Assert.Equal((ushort)1, frame.State.ReadWord(EffectObjectAddress + 0x46));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal((ushort)0, frame.State.ReadWord(EffectObjectAddress + 0x40));
        Assert.Equal((ushort)1, frame.State.ReadWord(EffectObjectAddress + 0x42));
        Assert.Null(frame.Effect.Pools[3].Head);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal((ushort)1, frame.State.ReadWord(EffectObjectAddress + 0x40));
        Assert.Equal((ushort)0, frame.State.ReadWord(EffectObjectAddress + 0x46));
        var a = Read(frame, Pools[3].Bank);
        var b = Read(frame, Pools[3].Bank + Pools[3].Stride);
        Assert.Equal(3, a.ReadByte(0x2C));
        Assert.Equal(4, b.ReadByte(0x2C));
        Assert.Equal(ProgrammeAAddress, a.ReadDword(0x74));
        Assert.Equal(ProgrammeBAddress, b.ReadDword(0x74));
        Assert.Equal(724u, a.ReadDword(0x78));
        Assert.Equal(712u, b.ReadDword(0x78));
        Assert.Equal((ushort)0, a.ReadWord(0x80));
        Assert.Equal((ushort)1, b.ReadWord(0x80));
        Assert.Equal((ushort)1, a.ReadWord(0x24));
        Assert.Equal((ushort)1, b.ReadWord(0x24));
        Assert.Equal(0x824260u, Read(frame, Pools[4].Bank).Callback);
        var reactions = frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x824970).ToArray();
        Assert.Equal(2, reactions.Length);
        Assert.Equal(new byte[] { 0, 1 }, reactions.Select(task => task.ReadByte(0x2B)));
        Assert.Equal(new byte[] { 0, 1 }, reactions.Select(task => task.ReadByte(0x2D)));
        Assert.Equal((ushort)7, Read(frame, RootBank).ReadWord(0x5E));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
        Assert.Equal(new Type[] { typeof(NativeBattleResonance49UpdateCaster8DC740), typeof(NativeBattleResonance49UpdateTargetBounds8DC610_8DC870) }, operations.Take(2).Select(operation => operation.GetType()));
    }

    [Fact]
    public void Root_drain_gate_reads_previous_sum_and_texture_reload_waits_real_completion_BYTE_before_detaching()
    {
        var frame = Frame(0, RootCallback, 6);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5E, 1));
        List<NativeBattleResonance49Operation> operations = [];
        var inputs = Inputs(operations);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(6, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal((ushort)0, Read(frame, RootBank).ReadWord(0x5E));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(7, Read(frame, RootBank).ReadByte(0x29));
        Assert.Empty(operations.OfType<NativeBattleResonance49CommonTextureLoad508630>());
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(8, Read(frame, RootBank).ReadByte(0x29));
        var load = Assert.Single(operations.OfType<NativeBattleResonance49CommonTextureLoad508630>());
        Assert.Equal(frame.State.ReadDword(InitializerGlobal), load.DestinationAddress);
        Assert.Equal(RootBank + 0x62, load.CompletionAddress);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(8, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal(RootDescriptor, frame.Effect.ReturnedDescriptor);
        frame = WriteCompletionByte(frame, load.CompletionAddress, 0xFF);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(9, Read(frame, RootBank).ReadByte(0x29));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.All(frame.State.InactiveBanks, bank => Assert.NotNull(bank));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleResonance49Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Null(restored.Effect.Parameters);
        //Action parameters belong to the invocation; a fresh entry supplies them after restoring process storage.
        restored = restored with { Effect = restored.Effect with { Parameters = frame.Effect.Parameters } };
        var warm = Initialize(restored, Resources(), inputs);
        Assert.Equal(0, Read(warm, RootBank).ReadByte(0x29));
        Assert.Equal(0, Read(warm, RootBank).ReadByte(0x62));
        Assert.Equal(frame.Random, warm.Random);
    }

    [Theory]
    [InlineData((ushort)27, 0, 0)]
    [InlineData((ushort)28, 1, 1)]
    [InlineData((ushort)0xFFFF, 0, 0)]
    public void Reaction_selection_uses_signed_effect_age_and_one_CRT_draw(ushort age, int draws, int phase)
    {
        var frame = Frame(4, 0x824970, 0);
        frame = frame with { State = frame.State.WriteWord(EffectObjectAddress + 0x46, age) };
        List<NativeBattleResonance49Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[4].Bank, Resources(), Inputs(operations));
        var random = frame.Random;
        ushort value = 0;
        if (draws != 0)
        {
            random = random.Draw(out value);
            Assert.Equal(new NativeBattleResonance49SelectSequence505CE0(OriginalOwner(0), 4 + (value & 1)), Assert.Single(operations));
        }
        else
        {
            Assert.Empty(operations);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(phase, Read(actual.Frame, Pools[4].Bank).ReadByte(0x29));
    }

    [Theory]
    [InlineData((byte)6, false)]
    [InlineData((byte)0, true)]
    public void Contact_waits_actual_resource_sequence_and_uses_single_physical_offset_despite_unknown_count(byte sequence, bool contact)
    {
        var frame = Frame(4, 0x824970, 4);
        uint address = Pools[4].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x78, 6).WriteTraversalPosition(0x2B, 1));
        List<NativeBattleResonance49Operation> operations = [];
        var packets = Packets(unknownCount: true).RestartContactPublication();
        var inputs = Inputs(operations) with { Packets = packets, ReadModel = (_, actor) => Model(actor, sequence) };
        var actual = AdvanceTask(frame, address, Resources(), inputs);
        Assert.Equal(contact ? 2 : 0, actual.Result);
        Assert.Equal(contact ? 5 : 4, Read(actual.Frame, address).ReadByte(0x29));
        Assert.Equal(contact ? 0 : 1, Read(actual.Frame, RootBank).ReadByte(0x28));
        if (contact)
        {
            var operation = Assert.IsType<NativeBattleResonance49Contact506690>(Assert.Single(operations));
            Assert.Equal(2, operation.Occurrence.Ordinal);
            Assert.Equal(OriginalOwner(1), packets.ReadContact(operation.Occurrence).Primary.Recipient);
            Assert.Equal(OriginalOwner(4), packets.ReadContact(operation.Occurrence).Secondary!.Recipient);
        }
        else
        {
            Assert.Empty(operations);
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData((byte)4, 1)]
    [InlineData((byte)0, 2)]
    public void Reaction_four_or_five_must_end_before_requesting_six(byte sequence, int phase)
    {
        var frame = Frame(4, 0x824970, 1);
        uint address = Pools[4].Bank;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x78, 4));
        List<NativeBattleResonance49Operation> operations = [];
        var actual = AdvanceTask(frame, address, Resources(), Inputs(operations) with { ReadModel = (_, actor) => Model(actor, sequence) });
        Assert.Equal(phase, Read(actual.Frame, address).ReadByte(0x29));
        Assert.Equal(phase == 2 ? (ushort)6 : (ushort)4, Read(actual.Frame, address).ReadWord(0x78));
        if (phase == 2)
        {
            Assert.Equal(new NativeBattleResonance49SelectSequence505CE0(OriginalOwner(0), 6), Assert.Single(operations));
        }
        else
        {
            Assert.Empty(operations);
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0, RootCallback)]
    [InlineData(1, 0x821840u)]
    [InlineData(2, 0x821970u)]
    [InlineData(3, 0x821C50u)]
    [InlineData(3, 0x824140u)]
    [InlineData(4, 0x824260u)]
    [InlineData(4, 0x824970u)]
    public void Seven_tables_reject_negative_signed_phase_instead_of_selecting_fallback(int pool, uint callback)
    {
        var frame = Frame(pool, callback, 255);
        Assert.Throws<NotSupportedException>(() => AdvanceTask(frame, Pools[pool].Bank, Resources(), Inputs([])));
    }

    [Fact]
    public void Root_BYTE_done_preserves_unknown_neighbor_but_effect_WORD_done_requires_it()
    {
        var root = Frame(0, RootCallback, 9);
        root = Write(root, RootBank, Read(root, RootBank).MarkPayloadUnknown(0x27, 1));
        var actual = AdvanceTask(root, RootBank, Resources(), Inputs([]));
        Assert.Equal(2, actual.Result);
        Assert.Throws<NotSupportedException>(() => Read(actual.Frame, RootBank).ReadByte(0x27));
        var effect = Frame(2, 0x821970, 3);
        effect = Write(effect, Pools[2].Bank, Read(effect, Pools[2].Bank).MarkPayloadUnknown(0x27, 1));
        Assert.Throws<NotSupportedException>(() => AdvanceTask(effect, Pools[2].Bank, Resources(), Inputs([])));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(11)]
    public void Expanded_single_header_reserves_only_its_screen_and_actual_reaction_count(int count)
    {
        List<NativeBattleResonance49Operation> operations = [];
        var (inactive, inputs, recipients) = ExpandedFixture(count, operations);
        NativeBattleResonance49Frame frame = Initialize(inactive, Resources(), inputs);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[4];
        Assert.Equal(Math.Max(8, count + 1), pool.Slots.Length);
        Assert.Equal(Pools[4].Descriptor, pool.DescriptorAddress);
        Assert.Equal(count <= 7, pool.BankAddress == Pools[4].Bank);
        if (count > 7)
        {
            Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, 128, count + 1));
        }
        else
        {
            Assert.Equal(inactive.Memory.PresentationTaskNextAddress, frame.Memory.PresentationTaskNextAddress);
        }
        foreach (int index in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(Pools[index].Bank, frame.Effect.Pools[index].BankAddress);
            Assert.Equal(Pools[index].Capacity, frame.Effect.Pools[index].Slots.Length);
        }
        frame = NativeBattleTestSnapshots.Independent(frame);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 3));
        for (int callback = 0; callback < 3; callback++)
        {
            frame = Advance(frame, Resources(), inputs);
        }
        pool = frame.Effect.Pools[4];
        Assert.Equal(0x824260u, pool.Slots[0].Callback);
        Assert.Equal(count + 1, pool.Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleModulePoolTask[] reactions = pool.Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x824970).ToArray();
        Assert.Equal(recipients, reactions.Select(task => task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner));
        Assert.Equal(Enumerable.Range(0, count), reactions.Select(task => task.ReadTraversalPosition(0x2B)));
        Assert.All(reactions, task => Assert.Equal(Pools[2].Bank, task.ReadDword(0x18)));
        Assert.All(reactions, task => Assert.Equal(RootBank, task.ReadDword(0x10)));
        Assert.Equal(count + 3, Read(frame, Pools[2].Bank).ReadByte(0x28));
        Assert.Equal(2, frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(3, frame.Effect.Pools[3].Slots[0].ReadByte(0x2C));
        Assert.Equal(4, frame.Effect.Pools[3].Slots[1].ReadByte(0x2C));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
        frame = NativeBattleTestSnapshots.Independent(frame);
        operations.Clear();
        for (int index = 0; index < count; index++)
        {
            uint address = checked(pool.BankAddress + (uint)((index + 1) * pool.Stride));
            frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 4).WriteWord(0x78, 6));
            var step = AdvanceTask(frame, address, Resources(), inputs);
            frame = step.Frame;
            Assert.Equal(2, step.Result);
        }
        var contacts = operations.OfType<NativeBattleResonance49Contact506690>().ToArray();
        Assert.Equal(count, contacts.Length);
        Assert.Equal(recipients, contacts.Select(contact => inputs.Packets.ReadContact(contact.Occurrence).Primary.Recipient));
        Assert.Equal(count, contacts.Select(contact => contact.Occurrence).Distinct().Count());
        Assert.Equal(3, Read(frame, Pools[2].Bank).ReadByte(0x28));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
    }

    [Fact]
    public void Original_reaction_bank_keeps_native_exhaustion_and_multi_header_growth_requires_its_lifetime_contract()
    {
        var (inactive, expanded, _) = ExpandedFixture(11, []);
        NativeBattleResonance49Inputs original = expanded with { StorageContract = BattleContactTaskStorageContract.OriginalArena };
        NativeBattleResonance49Frame frame = Initialize(inactive, Resources(), original);
        Assert.Equal(Pools[4].Bank, frame.Effect.Pools[4].BankAddress);
        Assert.Equal(8, frame.Effect.Pools[4].Slots.Length);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 3));
        frame = Advance(frame, Resources(), original);
        frame = Advance(frame, Resources(), original);
        Assert.Contains("exhausted native hierarchy", Assert.Throws<NotSupportedException>(() => Advance(frame, Resources(), original)).Message, StringComparison.Ordinal);
        NativeBattleResonance49Inputs repeated = expanded with { Packets = expanded.Packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 2) };
        Assert.Contains("single-header lifetime", Assert.Throws<NotSupportedException>(() => Initialize(inactive, Resources(), repeated)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Hosted_reaction_drain_preserves_original_inactive_prefix_knownness_and_allows_fresh_entry()
    {
        var (inactive, inputs, _) = ExpandedFixture(11, []);
        NativeBattleResonance49Frame frame = Initialize(inactive, Resources(), inputs);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[4];
        pool.Slots[7].WriteByte(126, 0xA5).MarkPayloadUnknown(127, 1);
        pool.Slots[8].WriteByte(127, 0xB6);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 9));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        frame.State.Validate();
        NativeBattleEffectArena prefix = frame.State.InactiveBanks[4]!;
        var (bytes, known) = prefix.ReadSnapshot(1022, 2);
        Assert.Equal((byte)0xA5, bytes[0]);
        Assert.True(known[0]);
        Assert.False(known[1]);
        prefix.ValidateExtent(1024);
        frame = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleResonance49Frame reentered = Initialize(frame, Resources(), inputs);
        Assert.True(reentered.Effect.Pools[4].BankAddress > pool.BankAddress);
        Assert.Equal((byte)0, reentered.Effect.Pools[4].Slots[7].ReadByte(126));
        Assert.Equal((byte)0, reentered.Effect.Pools[4].Slots[7].ReadByte(127));
        Assert.Equal((byte)0, reentered.Effect.Pools[4].Slots[8].ReadByte(127));
        Assert.Equal(frame.Random, reentered.Random);
    }

    private static (NativeBattleResonance49Frame Frame, NativeBattleResonance49Inputs Inputs, BattleModelOwner[] Recipients) ExpandedFixture(int count, List<NativeBattleResonance49Operation> operations)
    {
        BattleModelOwner[] extra = Enumerable.Range(0, Math.Max(0, count - 3)).Select(index => (BattleModelOwner)new BattlePartyActorModel(new($"expanded-resonance-{index}"))).ToArray();
        BattleModelOwner[] recipients = Enumerable.Range(0, Math.Min(3, count)).Select(OriginalOwner).Concat(extra).ToArray();
        BattleModelRoster roster = new(OriginalRoster.Owners.Concat(extra));
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(3), 8, 13, -1, 121, 49, null));
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 4, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(count);
        packets = packets.WritePlayerRepeatMode(header, 1);
        NativeBattleResonance49Frame frame = Inactive(1);
        frame = frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Header = header, TargetActors = BattleModelTargets.From(recipients) } } };
        return (frame, Inputs(operations) with { Packets = packets, StorageContract = BattleContactTaskStorageContract.ExpandableArena }, recipients);
    }

    private static NativeBattleResonance49Frame Frame(int pool, uint callback, byte phase)
    {
        var frame = Initialize(Inactive(1), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 10));
        uint address = RootBank;
        if (pool != 0)
        {
            (frame, address) = Append(frame, Pools[pool].Descriptor, callback, Pools[pool].Stride, RootBank, Packets());
        }
        return Write(frame, address, Read(frame, address).WriteByte(0x29, phase));
    }

    private static NativeBattleResonance49Frame Inactive(byte flags)
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
        NativeBattleOriginalEffectState effect = new(49, 48, new(49, Entry, Initializer), new(OriginalOwner(3), flags, BattleModelTargets.From([OriginalOwner(0), OriginalOwner(1), OriginalOwner(2), OriginalOwner(3), OriginalOwner(4), OriginalOwner(5), OriginalOwner(6)]), new BattleActionHeaderReference(1), 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleResonance49State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleResonance49Inputs Inputs(List<NativeBattleResonance49Operation> operations) => new(Packets(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => Model(actor, 0), (current, actor, selector, distance) => new(current, PointView(new((short)OriginalSlot(actor), (short)selector, (short)distance))), (_, _) => throw new InvalidOperationException(), _ => 1, (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleGraphicsModel Model(BattleModelOwner actor, byte sequence) => new(2) { Sequence = sequence, Rotation14 = 0, Transform = new(new(0, 0, 0), new(100, 200, 300), 444, 777, 4096) };

    private static NativeBattleActionPacketsState Packets(bool unknownCount = false)
    {
        var packets = OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(3), 8, 13, -1, 121, 49, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(6), 4, 0, 0, 0, 7, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        (packets, _) = packets.Prepare(new(OriginalOwner(3), 8, 13, -1, 121, 49, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(0), 4, 0, 0, 0, 7, 0), null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(1), 4, 0, 0, 0, 7, 0), new(OriginalOwner(4), 4, 0, 0, 0, 3, 0)));
        if (!unknownCount)
        {
            (packets, _) = packets.Publish(2);
        }
        return packets.WritePlayerRepeatMode(new BattleActionHeaderReference(1), 1);
    }

    private static NativeBattleResonance49Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BitConverter.GetBytes((ushort)2).CopyTo(camera, 0);
        BitConverter.GetBytes((ushort)8).CopyTo(camera, 2);
        BitConverter.GetBytes((ushort)32).CopyTo(camera, 4);
        BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
        return new(Source(ProgrammeAAddress, ProgrammeALength), Source(ProgrammeBAddress, ProgrammeBLength), new("FF8_EN:15ADAF8", camera.ToImmutableArray()), 0x61B5C);
    }

    private static NativeBattleGfAnimationSource Source(uint address, int length)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes(4).CopyTo(data, 0);
        BitConverter.GetBytes(0x400).CopyTo(data, 4);
        for (int index = 0; index < 4; index++)
        {
            int geometry = 24 + index * 52;
            BitConverter.GetBytes(geometry).CopyTo(data, 8 + index * 4);
            BitConverter.GetBytes(20).CopyTo(data, geometry);
            BitConverter.GetBytes(1).CopyTo(data, geometry + 4);
            BitConverter.GetBytes(1).CopyTo(data, geometry + 8);
        }
        BitConverter.GetBytes(8).CopyTo(data, 0x400);
        BitConverter.GetBytes(60).CopyTo(data, 0x404);
        for (int index = 0; index < 8; index++)
        {
            BitConverter.GetBytes(40 + index * 8).CopyTo(data, 0x408 + index * 4);
        }
        return new(address, data.ToImmutableArray());
    }
}
