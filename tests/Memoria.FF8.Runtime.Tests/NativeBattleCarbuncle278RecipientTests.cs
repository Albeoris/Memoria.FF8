using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278RecipientTests
{
    [Fact]
    public void Recipients_reverse_contacts_clear_their_whole_payload_and_cache_anchor_before_zeroing_height()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame preserved = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame));
        NativeBattleCarbuncle278Frame current = fixture.Frame;
        for (int index = 0; index < 3; index++)
        {
            BattleModelOwner expected = OriginalOwner(2 - index);
            current = NativeBattleCarbuncle278Recipients.Spawn681B8C(current, fixture.Creature.WriteWord(0xC, (ushort)(145 + index * 4)), OriginalAliases, actor =>
            {
                Assert.Equal(expected, actor);
                return new(-7, 123, 45, 6);
            });
            NativeBattleModulePoolTask task = current.Effect.Pools[0].Slots[index];
            Assert.Equal(expected, task.ReadExecutionReference<BattleExecutionReference.Model>(0x10, 4).Owner);
            Assert.Equal(NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode((byte)(2 - index))), task.ReadDword(0x10));
            Assert.Equal(unchecked((uint)-7), task.ReadDword(0x28));
            Assert.Equal(123u, task.ReadDword(0x2C));
            Assert.Equal(45u, task.ReadDword(0x30));
            Assert.Equal((ushort)0, task.ReadWord(0x5E));
            Assert.Equal((ushort)6, task.ReadWord(0x62));
            Assert.Equal(0x10C1CD8u, task.ReadDword(0x64));
            Assert.Equal(0u, task.ReadDword(0x68));
            Assert.Equal(new byte[0x10C - 0x6C], NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(0x6C - 12).ToArray());
            Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload, current.Memory.Arena!.Read(0x1FA0 + index * 0x10C + 12, 0x100));
        }
        Assert.All(preserved.Effect.Pools[0].Slots, task => Assert.Equal((ushort)0, task.Flags));
        fixture = (preserved, fixture.Creature);
        NativeBattleCarbuncle278Frame restored = NativeBattleTestSnapshots.Independent(fixture.Frame);
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature, OriginalAliases, _ => new(-7, 123, 45, 6))), NativeBattleTestSnapshots.Json(NativeBattleCarbuncle278Recipients.Spawn681B8C(restored, fixture.Creature, OriginalAliases, _ => new(-7, 123, 45, 6))));
        Assert.Same(current, NativeBattleCarbuncle278Recipients.Spawn681B8C(current, fixture.Creature, OriginalAliases, _ => throw new Exception("Full bank must skip anchor reads.")));
        Assert.Same(fixture.Frame, NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature.WriteWord(0xC, 146), OriginalAliases, _ => throw new Exception()));
        Assert.Same(fixture.Frame, NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature.WriteContactCount(0xE, 0), OriginalAliases, _ => throw new Exception()));
    }

    [Theory]
    [InlineData(700, 2800)]
    [InlineData(2000, 4096)]
    [InlineData(-700, -2800)]
    [InlineData(-10000, 25536)]
    public void Matrix_uses_signed_scale_cap_native_yaw_and_current_view_preserving_both_padding_words(short input, short scale)
    {
        NativeBattleModulePoolTask source = new(1, 0, null, 0x6822F0, Enumerable.Repeat((byte)0xA5, 0x100).ToImmutableArray());
        source = source.WriteDword(0x28, 10).WriteDword(0x2C, 20).WriteDword(0x30, 30);
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleModulePoolTask preserved = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        NativeBattleModulePoolTask result = NativeBattleCarbuncle278Recipients.Cache6DBCC0(source, 1024, input, view);
        short[] world = [0, 0, scale, 0, scale, 0, unchecked((short)-scale), 0, 0];
        short[] composed = [0, unchecked((short)-scale), 0, 0, 0, scale, unchecked((short)-scale), 0, 0];
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(world[index], unchecked((short)result.ReadWord(0x14 + index * 2)));
            Assert.Equal(composed[index], unchecked((short)result.ReadWord(0x34 + index * 2)));
        }
        Assert.Equal((ushort)0xA5A5, result.ReadWord(0x26));
        Assert.Equal((ushort)0xA5A5, result.ReadWord(0x46));
        Assert.Equal((80u, 210u, 330u), (result.ReadDword(0x48), result.ReadDword(0x4C), result.ReadDword(0x50)));
        Assert.Equal((10u, 20u, 30u), (result.ReadDword(0x28), result.ReadDword(0x2C), result.ReadDword(0x30)));
        Assert.Equal((ushort)0xA5A5, preserved.ReadWord(0x14));
    }

    [Fact]
    public void Actor_sparkle_uses_one_shared_draw_and_keeps_age_zero_until_the_next_pool_pass()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508178, NativeBattleEffectNodeMemory.ArenaAddress + 0x23A4, 0x14, 64);
        NativeBattleCarbuncle278Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] } };
        NativeBattleSharedEffectRandomState random = frame.Memory.SharedEffectRandom!.Draw7059E0(out ushort value);
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Recipients.SpawnSparkle6822F0(frame, OriginalOwner(0), actor =>
        {
            Assert.Equal(OriginalOwner(0), actor);
            return 30;
        }, (actor, bone) =>
        {
            Assert.Equal(OriginalOwner(0), actor);
            Assert.Equal(value * 30 >> 15, bone);
            return new(1, -2, 3);
        });
        Assert.Equal(random, result.Memory.SharedEffectRandom);
        NativeBattleModulePoolTask task = result.Effect.Pools[0].Slots[0];
        Assert.Equal((ushort)0, task.ReadWord(0x12));
        Assert.Equal(unchecked((ushort)-2), task.ReadWord(0xE));
        for (int index = 1; index < 64; index++)
        {
            (pool, _) = pool.Append(0x683E80);
        }
        NativeBattleCarbuncle278Frame full = frame with { Memory = frame.Memory with { SharedEffectRandom = null }, Effect = frame.Effect with { Pools = [pool] } };
        Assert.Same(full, NativeBattleCarbuncle278Recipients.SpawnSparkle6822F0(full, OriginalOwner(0), _ => throw new Exception(), (_, _) => throw new Exception()));
    }

    private static (NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Creature) Fixture()
    {
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 21);
        NativeBattleCarbuncle278Resources resources = new(new(0, 0, [])) { RecipientProgramme = new(0x10C1CD8, source.ToImmutableArray()) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508158, NativeBattleEffectNodeMemory.ArenaAddress + 0x1FA0, 0x10C, 3);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60) { Carbuncle278Resources = resources }, new(OriginalOwner(0), 0, BattleModelTargets.From([OriginalOwner(0), OriginalOwner(1)]), new BattleActionHeaderReference(0), 0, 0), null) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleModulePoolTask creature = new(1, 0, null, 0x681630, new byte[0x5E0 - 12].ToImmutableArray());
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 0, 0, 0, 0, 278, null));
        for (int slot = 0; slot < 3; slot++)
        {
            (packets, _) = packets.AppendContact(new(new(OriginalOwner(slot), 0, 0, 0, 0, 0, 0), null));
        }
        (packets, _) = packets.Publish(3);
        creature = creature.WriteWord(0xC, 145).WriteContactCount(0xE, 3).WriteOrderedContactRecipients(0x14, 3, packets, packets.ReadHeader(header));
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), creature);
    }

    [Fact]
    public void Expanded_recipients_keep_reverse_births_and_finish_after_a_saturated_pause()
    {
        var fixture = Fixture();
        BattleModelOwner extra = new BattlePartyActorModel(new("extra"));
        BattleModelRoster roster = new(OriginalRoster.Owners.Append(extra));
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        BattleModelOwner[] recipients = [OriginalOwner(0), extra, OriginalOwner(1), extra, OriginalOwner(2)];
        var packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 0, 0, 0, 0, 278, null));
        foreach (BattleModelOwner owner in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(owner, 0, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(recipients.Length);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(NativeBattleCarbuncle278Entry.MainDescriptor, NativeBattleCarbuncle278Entry.MainBank, 20, 1);
        (root, _) = root.Append(NativeBattleCarbuncle278Entry.MainCallback);
        root.Slots[0].WriteWord(0xC, 280).WriteByte(0xF, 1).WriteDword(0x10, 0);
        var state = NativeBattleCarbuncle278State.FromColdProcess() with { ModelBuffer81F8 = NativeBattleEffectNodeMemory.ArenaAddress, Done81D8 = 255 };
        var effect = fixture.Frame.Effect with
        {
            Binding = fixture.Frame.Effect.Binding with { Carbuncle278Resources = fixture.Frame.Effect.Binding.Carbuncle278Resources! with { CreatureBoundsMesh = new(0, 0x10BD488, ImmutableArray.Create(new byte[144])) } },
            Parameters = fixture.Frame.Effect.Parameters! with { TargetActors = BattleModelTargets.From(recipients) },
            ReturnedDescriptor = NativeBattleCarbuncle278Entry.MainDescriptor,
            Pools = [root]
        };
        var frame = new NativeBattleCarbuncle278Frame(fixture.Frame.Memory with { Carbuncle278 = state, Arena = NativeBattleEffectArena.Clear() }, effect, fixture.Frame.Random) { ExpandedStorage = true };
        frame = NativeBattleCarbuncle278Banks.Initialize(frame, roster, recipients.Length);
        frame.Memory.Arena!.Write(0x2E84, [0x81]);
        NativeBattleModulePoolTask creature = Bank(frame, NativeBattleCarbuncle278Banks.Creature).Slots[0];
        creature.WriteContactCount(0xE, recipients.Length).WriteOrderedContactRecipients(0x14, recipients.Length, packets, packets.ReadHeader(header));
        List<(int Age, BattleModelOwner Owner)> births = [];
        foreach (int age in new[] { 145, 146, 149, 153 })
        {
            frame = NativeBattleCarbuncle278Recipients.Spawn681B8C(frame, creature.WriteWord(0xC, (ushort)age), aliases, owner =>
            {
                births.Add((age, owner));
                return new(-7, 123, 45, 6);
            });
        }
        Assert.Equal(new[] { 145, 149, 153, 153, 153 }, births.Select(birth => birth.Age));
        Assert.Equal(recipients.Reverse(), births.Select(birth => birth.Owner));
        NativeBattleEffectTaskPool targetBank = Bank(frame, NativeBattleCarbuncle278Banks.Recipients);
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(targetBank.BankAddress, 0x10C, 5));
        Assert.Equal(640, Bank(frame, NativeBattleCarbuncle278Banks.Debris).Slots.Length);
        Assert.Equal(40, Bank(frame, NativeBattleCarbuncle278Banks.Rays).Slots.Length);
        Assert.Equal(15, Bank(frame, NativeBattleCarbuncle278Banks.Rings).Slots.Length);
        Assert.Equal(99, Bank(frame, NativeBattleCarbuncle278Banks.Sparkles).Slots.Length);
        Assert.All(targetBank.Slots, task => task.WriteWord(0xC, 15));
        var original = frame;
        frame = NativeBattleTestSnapshots.Independent(frame);
        Assert.NotSame(targetBank.RequireOwner(), Bank(frame, NativeBattleCarbuncle278Banks.Recipients).RequireOwner());
        frame = frame with { Memory = frame.Memory with { Carbuncle278 = state with { Pause8114 = 1 } } };
        List<BattleContactOccurrence> applied = [];
        int restored = 0;
        var inputs = new NativeBattleCarbuncle278Inputs(packets, (current, _, _) => current, current => new(current, 0))
        {
            AdvanceRecipient = (current, task) => new(current, NativeBattleCarbuncle278Recipients.Cache6DBCC0(task, 0, 4096, NativeBattleCuragaMatrices.Identity8DD770())),
            DrawRecipientRange = (current, task, address) =>
            {
                Assert.True(NativeBattleCarbuncle278Banks.IsHostedRecipient(current, address));
                Assert.Equal(-7, NativeBattleCarbuncle278ChildDrawing.ReadCached(task).X);
                var phase = NativeBattleCarbuncle278RangeSetup.Read6825A5(current.Memory, unchecked((short)task.ReadWord(0xC)));
                return new(NativeBattleCarbuncle278RangeSetup.Spawn6826B7(current, address, phase, 4096), task);
            },
            DrawRecipientChild = (current, task) =>
            {
                uint pointer = task.ReadDword(0x18);
                Assert.True(NativeBattleCarbuncle278Banks.IsHostedRecipient(current, pointer));
                NativeBattleModulePoolTask parent = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(pointer);
                Assert.Equal(45, NativeBattleCarbuncle278ChildDrawing.ReadCached(parent).Z);
                return new(current, task);
            },
            ApplyRecipientContact = (current, task) =>
            {
                applied.Add(NativeBattleCarbuncle278Recipients.ReadOccurrence(task).Occurrence);
                return current;
            },
            RestoreActors = current =>
            {
                restored++;
                return current;
            }
        };
        frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (_, _) => { }, inputs);
        NativeBattleEffectTaskPool rays = Bank(frame, NativeBattleCarbuncle278Banks.Rays);
        Assert.All(rays.Slots, task => Assert.Equal(1, task.Flags & 1));
        NativeBattleSharedEffectRandomState expected = frame.Memory.SharedEffectRandom!;
        for (int index = 0; index < 2 * recipients.Length; index++)
        {
            expected = expected.Draw7059E0(out _);
        }
        frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (_, _) => { }, inputs);
        Assert.Equal(expected, frame.Memory.SharedEffectRandom);
        Assert.Empty(applied);
        Assert.All(Bank(original, NativeBattleCarbuncle278Banks.Recipients).Slots, task => Assert.Equal((ushort)15, task.ReadWord(0xC)));
        frame = frame with { Memory = frame.Memory with { Carbuncle278 = state } };
        int maximumDebris = 0;
        for (int callback = 0; callback < 60 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (_, _) => { }, inputs);
            if (frame.Effect.ReturnedDescriptor != 0)
            {
                maximumDebris = Math.Max(maximumDebris, Bank(frame, NativeBattleCarbuncle278Banks.Debris).Slots.Count(task => (task.Flags & 1) != 0));
            }
        }
        Assert.Equal(600, maximumDebris);
        Assert.Equal(Enumerable.Range(0, recipients.Length).Reverse().Select(index => packets.ResolveContact(header, index)), applied);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(1, restored);
        Assert.DoesNotContain(frame.Effect.Pools, NativeBattleCarbuncle278Banks.IsHosted);
        Assert.Equal(new byte[] { 0x81 }, frame.Memory.Arena!.Read(0x2E84, 1));
        Assert.All(frame.Memory.Arena!.ReadSnapshot(0x1FA0 + 0x10C + 0x10, 4).Known, known => Assert.False(known));

        static NativeBattleEffectTaskPool Bank(NativeBattleCarbuncle278Frame current, uint descriptor) => current.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
    }
}
