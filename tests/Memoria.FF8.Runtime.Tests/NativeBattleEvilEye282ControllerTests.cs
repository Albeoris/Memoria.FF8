using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Controller;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEvilEye282ControllerTests
{
    [Fact]
    public void Entry_consumes_one_CRT_seed_and_defers_secondary_COMMON_reset()
    {
        var inactive = Inactive();
        inactive = inactive with { Memory = inactive.Memory with { Arena = inactive.Memory.Arena!.Write(0xEC8, new byte[] { 0xAB, 0xCD }) } };
        var expected = inactive.Random.Draw(out ushort seed);
        var frame = Initialize(inactive, Resources(), Inputs([]));
        Assert.Equal(expected, frame.Random);
        Assert.Equal((uint)seed, frame.State.ReadDword(SeedGlobal));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, frame.Memory.Arena!.Read(0xEC8, 2));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(12));
        Assert.Equal(expected, frame.Random);
    }

    [Theory]
    [InlineData(0u, 0x1162Cu, 1u)]
    [InlineData(1u, 0x162Cu, 0u)]
    [InlineData(0x80000000u, 0x162Cu, 0u)]
    public void Root_parity_uses_full_DWORD_and_stages_zero_before_activation(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        frame = frame with { State = frame.State.WriteDword(CueGlobal, 0xDEADBEEF) };
        List<NativeBattleEvilEye282Operation> operations = [];
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs(operations) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(new NativeBattleEvilEye282Stage5713E0(0, 0), Assert.Single(operations));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Setup_preserves_three_physical_recipients_and_normalized_destination_padding()
    {
        var packets = Packets(3);
        var frame = Initialize(Inactive(), Resources(), Inputs([], packets));
        frame = frame with { State = frame.State.WriteWord(NormalizedViewAddress + 18, 0xA55A) };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        List<NativeBattleEvilEye282Operation> operations = [];
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs(operations, packets)).Frame;
        Assert.Equal(4, actual.Effect.Pools.Length);
        Assert.Equal((ushort)3, Read(actual, Pools[3].Bank).ReadWord(14));
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(ModelAddressFor(index), Read(actual, Pools[3].Bank).ReadDword(16 + index * 4));
        }
        Assert.Equal((ushort)0xA55A, actual.State.ReadWord(NormalizedViewAddress + 18));
        Assert.Equal(new NativeBattleEvilEye282Stage5713E0(2200, 0), Assert.Single(operations));
        Assert.Equal(frame.Random, actual.Random);
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0, 1));
    }

    [Theory]
    [InlineData((ushort)41, 2200, 0)]
    [InlineData((ushort)42, 1925, 0)]
    [InlineData((ushort)47, 550, 0)]
    [InlineData((ushort)48, 0, 2)]
    public void Cue_fades_with_native_DWORD_arithmetic_then_contacts_clear_it(ushort age, int cue, int result)
    {
        var frame = MainAt(age);
        List<NativeBattleEvilEye282Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), Inputs(operations));
        Assert.Equal(result, actual.Result);
        Assert.Equal(unchecked((uint)cue), actual.Frame.State.ReadDword(CueGlobal));
        Assert.Equal((ushort)(age + 1), Read(actual.Frame, Pools[3].Bank).ReadWord(12));
        Assert.Equal(age == 48 ? 1 : 0, operations.OfType<NativeBattleEvilEye282ContactsAll506BA0>().Count());
    }

    [Theory]
    [InlineData((ushort)1, false)]
    [InlineData((ushort)2, true)]
    [InlineData((ushort)27, true)]
    [InlineData((ushort)28, false)]
    public void Muzzle_programme_window_reads_actual_current_bone_thirteen_only_when_reached(ushort age, bool reached)
    {
        var frame = MainAt(age);
        int reads = 0;
        var inputs = Inputs([]) with { ReadCurrentBoneMatrix = (_, model, bone) =>
        {
            Assert.Equal(OriginalOwner(3), model);
            Assert.Equal(13, bone);
            reads++;
            return Identity();
        } };
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), inputs).Frame;
        Assert.Equal(reached ? 1 : 0, reads);
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Old_two_sound_and_old_five_flash_are_separate_native_boundaries()
    {
        List<NativeBattleEvilEye282Operation> sound = [];
        _ = AdvanceTask(MainAt(2), Pools[3].Bank, Resources(), Inputs(sound));
        Assert.Equal(new NativeBattleEvilEye282Sound501330(Resources().Sound, 0, 128), Assert.Single(sound.OfType<NativeBattleEvilEye282Sound501330>()));
        Assert.Empty(sound.OfType<NativeBattleEvilEye282Flash5712C0>());
        List<NativeBattleEvilEye282Operation> flash = [];
        _ = AdvanceTask(MainAt(5), Pools[3].Bank, Resources(), Inputs(flash));
        Assert.Equal(new NativeBattleEvilEye282Flash5712C0(0, 1, 0, 96), Assert.Single(flash.OfType<NativeBattleEvilEye282Flash5712C0>()));
        Assert.Empty(flash.OfType<NativeBattleEvilEye282Sound501330>());
    }

    [Fact]
    public void Old_ten_target_bone_calls_preserve_frame_publications_and_write_only_XYZ_then_phase()
    {
        var frame = Active(Packets(2));
        frame = Write(frame, Pools[3].Bank, Read(frame, Pools[3].Bank).WriteWord(12, 10));
        List<BattleModelOwner> models = [];
        var published = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 76543 }, null);
        var expectedRandom = frame.Random;
        var inputs = Inputs([], Packets(2)) with { ReadBone502170 = (current, model, selector, distance) =>
        {
            Assert.Equal(0xF1, selector);
            Assert.Equal(0, distance);
            models.Add(model);
            var random = current.Random.Draw(out _);
            return new(current with { Random = random, Memory = current.Memory with { SharedEffectView2793E58 = published } }, PointView(new(111, -222, 333)), 0);
        } };
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), inputs).Frame;
        expectedRandom = expectedRandom.Draw(out _).Draw(out _);
        Assert.Equal(new[] { OriginalOwner(0), OriginalOwner(1) }, models);
        Assert.Equal(expectedRandom, actual.Random);
        Assert.Equal(published, actual.Memory.SharedEffectView2793E58);
        foreach (var task in actual.Effect.Pools[2].Slots.Take(2))
        {
            Assert.Equal((ushort)111, task.ReadWord(12));
            Assert.Equal(unchecked((ushort)-222), task.ReadWord(14));
            Assert.Equal((ushort)333, task.ReadWord(16));
            Assert.Equal((ushort)0, task.ReadWord(18));
            Assert.Equal(ProgrammeBAddress, new NativeBattleGfAnimationWorkspace(TaskView(task, 24, 384).Read(0, 384).ToImmutableArray()).ResourceAddress);
        }
    }

    [Fact]
    public void Full_target_pool_skips_all_anchor_queries_at_old_ten()
    {
        var frame = MainAt(10);
        for (int index = 0; index < 3; index++)
        {
            (frame, _) = Append(frame, Pools[2].Descriptor, 0x67DB20);
        }
        var inputs = Inputs([]) with { ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException("No free target slot must not query a bone.") };
        var actual = AdvanceTask(frame, Pools[3].Bank, Resources(), inputs).Frame;
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(33)]
    public void Expanded_programmes_preserve_ordered_births_and_retire_only_the_original_bank_prefixes(int count)
    {
        BattleModelOwner extra = new BattlePartyActorModel(new("extra"));
        BattleModelRoster roster = new(OriginalRoster.Owners.Append(extra));
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        BattleModelOwner[] recipients = Enumerable.Range(0, count).Select(index => index % 2 == 0 ? extra : OriginalOwner(0)).ToArray();
        NativeBattleActionPacketsState packets = Packets(NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases), recipients, count);
        List<BattleModelOwner> births = [];
        List<NativeBattleEvilEye282Operation> operations = [];
        var inputs = Inputs(operations, packets) with
        {
            StorageContract = BattleContactTaskStorageContract.ExpandableArena,
            ReadModelWorld = (_, _) => Identity(),
            ReadBone502170 = (current, owner, _, _) =>
            {
                births.Add(owner);
                return new(current, PointView(new(101, -202, 303)), 0);
            }
        };
        var resources = Resources();
        var inactive = Inactive();
        inactive = inactive with { Effect = inactive.Effect with { Parameters = inactive.Effect.Parameters! with { TargetActors = BattleModelTargets.From(recipients.Distinct()) } } };
        var frame = Initialize(inactive, resources, inputs);
        byte[] spare = [0x71, 0xB2];
        frame.Memory.Arena!.Write(ScratchOffset + ScratchLength - spare.Length, spare);
        frame = Advance(frame, resources, inputs);
        frame = Advance(frame, resources, inputs);
        NativeBattleEffectTaskPool sprites = frame.Effect.Pools[1];
        NativeBattleEffectTaskPool targets = frame.Effect.Pools[2];
        Assert.Equal(Pools[1].Descriptor, sprites.DescriptorAddress);
        Assert.Equal(Pools[2].Descriptor, targets.DescriptorAddress);
        Assert.Equal(Math.Max(128, count * 4), sprites.Slots.Length);
        Assert.Equal(count == 4, sprites.BankAddress == Pools[1].Bank);
        if (count > 32)
        {
            Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(sprites.BankAddress, 20, sprites.Slots.Length));
            Assert.True(sprites.BankAddress + (uint)(sprites.Stride * sprites.Slots.Length) <= targets.BankAddress);
        }
        Assert.Equal(count, targets.Slots.Length);
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(targets.BankAddress, 408, count));
        Assert.Equal(Pools[3].Bank, frame.Effect.Pools[3].BankAddress);
        for (int age = 1; age < 10; age++)
        {
            frame = Advance(frame, resources, inputs);
        }
        Assert.Empty(births);
        Assert.Null(targets.Head);
        frame = Advance(frame, resources, inputs);
        Assert.Equal(recipients, births);
        Assert.Equal(0, targets.Head);
        Assert.Equal(count - 1, targets.Tail);
        for (int index = 0; index < count; index++)
        {
            uint child = targets.BankAddress + (uint)(index * targets.Stride);
            Assert.Equal(recipients[index], Read(frame, child).ReadExecutionReference<BattleExecutionReference.Model>(20, 4).Owner);
            Assert.Equal(ProgrammeBAddress, Read(frame, child).ReadDword(24));
        }
        Assert.All(ReadBytes(frame, targets.BankAddress + 20, 4).ReadSnapshot(0, 4).Known, known => Assert.False(known));
        if (count == 33)
        {
            NativeBattleEvilEye282Frame original = frame;
            NativeBattleEffectTaskPool originalTargets = targets;
            uint originalAge = Read(original, targets.BankAddress).ReadDword(28);
            frame = Advance(NativeBattleTestSnapshots.Independent(original), resources, inputs);
            sprites = frame.Effect.Pools[1];
            targets = frame.Effect.Pools[2];
            Assert.NotSame(originalTargets.RequireOwner(), targets.RequireOwner());
            Assert.Equal(originalTargets.BankAddress, targets.BankAddress);
            Assert.Equal(originalAge + 1, Read(frame, targets.BankAddress).ReadDword(28));
            Assert.Equal(originalAge, Read(original, targets.BankAddress).ReadDword(28));
            Assert.Equal(extra, Read(frame, targets.BankAddress).ReadExecutionReference<BattleExecutionReference.Model>(20, 4).Owner);
            Assert.All(ReadBytes(frame, targets.BankAddress + 20, 4).ReadSnapshot(0, 4).Known, known => Assert.False(known));
            var captured = Read(frame, Pools[3].Bank).ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(16, 4);
            var expected = packets.ReadHeaderContacts(new BattleActionHeaderReference(1)).Select(packet => (packet.Primary.Recipient, packet.Occurrence!.Value));
            Assert.Equal(expected, captured.Recipients);
        }
        int peak = 0;
        for (int callback = 0; callback < 64 && !frame.Effect.Pools.IsEmpty; callback++)
        {
            frame = Advance(frame, resources, inputs);
            if (!frame.Effect.Pools.IsEmpty)
            {
                peak = Math.Max(peak, sprites.Slots.Count(task => (task.Flags & 1) != 0));
            }
        }
        Assert.Equal(count * 4, peak);
        Assert.Single(operations.OfType<NativeBattleEvilEye282ContactsAll506BA0>());
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        Assert.Equal(spare, arena.Read(ScratchOffset + ScratchLength - spare.Length, spare.Length));
        Assert.All(arena.ReadSnapshot(0xA00 + 20, 4).Known, known => Assert.False(known));
        Assert.Equal(BitConverter.GetBytes(ModelAddressFor(0)), arena.Read(0xA00 + 408 + 20, 4));
        Assert.Equal(BitConverter.GetBytes(ProgrammeAAddress), arena.Read(0xEC8 + 28, 4));
    }

    [Fact]
    public void Last_main_callback_applies_current_counted_header_before_root_stages_zero()
    {
        var frame = MainAt(48);
        List<NativeBattleEvilEye282Operation> operations = [];
        var actual = Advance(frame, Resources(), Inputs(operations));
        Assert.Empty(actual.Effect.Pools);
        Assert.Equal(new BattleActionHeaderReference(1), Assert.Single(operations.OfType<NativeBattleEvilEye282ContactsAll506BA0>()).Header);
        Assert.Equal(new NativeBattleEvilEye282Stage5713E0(0, 0), operations.Last());
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Root_waits_for_target_survivors_and_preserves_unknown_normalized_padding_through_JSON()
    {
        var frame = MainAt(48);
        frame = frame with { State = frame.State.WriteView(NormalizedViewAddress, frame.State.ReadView(NormalizedViewAddress, 32).MarkUnknown(18, 2), 32) };
        (frame, uint target) = Append(frame, Pools[2].Descriptor, 0x67DB20);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[384].ToImmutableArray()).Initialize7016B0(Resources().ProgrammeB, 384);
        frame = WriteBytes(frame, target + 24, NativeBattleEffectArena.Clear().Write(0, NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan()), 384);
        frame = Write(frame, target, Read(frame, target).WriteDword(20, ModelAddressFor(0)));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.NotEmpty(frame.Effect.Pools);
        Assert.Null(frame.Effect.Pools[3].Head);
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(NormalizedViewAddress + 18));
        var restored = NativeBattleTestSnapshots.Independent(frame);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Throws<NotSupportedException>(() => restored.State.ReadWord(NormalizedViewAddress + 18));
    }

    private static NativeBattleEvilEye282Frame Active(NativeBattleActionPacketsState? packets = null)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([], packets));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        return AdvanceTask(frame, RootBank, Resources(), Inputs([], packets)).Frame;
    }

    private static NativeBattleEvilEye282Frame MainAt(ushort age)
    {
        var frame = Active();
        return Write(frame, Pools[3].Bank, Read(frame, Pools[3].Bank).WriteWord(12, age));
    }

    private static NativeBattleEvilEye282Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(282, 281, new(282, Entry, Initializer), new(OriginalOwner(3), 0, BattleModelTargets.From([OriginalOwner(0), OriginalOwner(1), OriginalOwner(2), OriginalOwner(3), OriginalOwner(4), OriginalOwner(5), OriginalOwner(6)]), new BattleActionHeaderReference(1), 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleEvilEye282State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEvilEye282Inputs Inputs(List<NativeBattleEvilEye282Operation> operations, NativeBattleActionPacketsState? packets = null)
    {
        packets ??= Packets(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, address) => Model(address), (_, _) => 24, (_, _, bone) => bone == 0 ? (short)-1 : (short)0, (_, _, _) => (byte)1, (_, _, bone) => Identity() with { Bytes = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = bone * 10, Y = bone * 20, Z = 1000 + bone * 30 }, null).Bytes }, (_, address) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = Model(address).Transform!.Position28.X, Y = Model(address).Transform!.Position28.Y, Z = Model(address).Transform!.Position28.Z }, null), (current, _, _, _) => new(current, PointView(new(101, -202, 303)), 0), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);

    private static NativeBattleGraphicsModel Model(BattleModelOwner owner)
    {
        int actor = OriginalSlot(owner);
        return new(2) { Rotation14 = 0, MinimumY146 = -100, Transform = new(new(0, 0, 0), new((short)(100 + actor * 100), (short)(200 + actor), (short)(1000 - actor * 1000)), (short)(444 + actor), 777, 4096) };
    }

    private static uint ModelAddressFor(int actor) => 0x1D972C0u + (uint)(actor * 156);

    private static NativeBattleActionPacketsState Packets(int count, int? declaredCount = null)
    {
        return Packets(OriginalPackets(), Enumerable.Range(0, count).Select(OriginalOwner), declaredCount ?? count);
    }

    private static NativeBattleActionPacketsState Packets(NativeBattleActionPacketsState packets, IEnumerable<BattleModelOwner> recipients, int declaredCount)
    {
        (packets, _) = packets.Prepare(new(OriginalOwner(3), 8, 12, -1, 152, 282, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(6), 4, 0, 0, 0, 7, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        (packets, _) = packets.Prepare(new(OriginalOwner(3), 8, 12, -1, 152, 282, null));
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 4, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(declaredCount);
        return packets.WritePlayerRepeatMode(new BattleActionHeaderReference(1), 1);
    }


    private static NativeBattleEvilEye282Resources Resources() => NativeBattleEvilEye282StateTests.Resources();
}
