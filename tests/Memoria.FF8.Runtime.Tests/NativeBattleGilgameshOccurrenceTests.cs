using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshOccurrenceTests
{
    [Theory]
    [InlineData(0, 0x0001, 0x0100)]
    [InlineData(7, 0x0080, 0x8000)]
    [InlineData(8, 0x0100, 0)]
    [InlineData(15, 0x8000, 0)]
    [InlineData(16, 0, 0)]
    [InlineData(31, 0, 0)]
    [InlineData(32, 0x0001, 0x0100)]
    public void Original_words_follow_x86_shifts_and_signed_DWORD_membership(int ordinal, int low, int sparks)
    {
        Assert.Equal((ushort)low, NativeBattleGilgameshOriginalParticleGroups.EncodeWord(ordinal, NativeBattleGilgameshParticleVariant.Masamune));
        Assert.Equal((ushort)low, NativeBattleGilgameshOriginalParticleGroups.EncodeWord(ordinal, NativeBattleGilgameshParticleVariant.Excalipoor));
        Assert.Equal((ushort)sparks, NativeBattleGilgameshOriginalParticleGroups.EncodeWord(ordinal, NativeBattleGilgameshParticleVariant.ExcalipoorSparks));
        Assert.Equal(0xFFFF8000u, NativeBattleGilgameshOriginalParticleGroups.EncodeRow(0x8000));
        Assert.True(NativeBattleGilgameshOriginalParticleGroups.Matches(0x10000, 0x8000));
        Assert.True(NativeBattleGilgameshOriginalParticleGroups.Matches(NativeBattleGilgameshOriginalParticleGroups.EncodeRow(0x8000), 0x8000));
        Assert.False(NativeBattleGilgameshOriginalParticleGroups.Matches(0xFFFF8000, 0));
    }

    [Fact]
    public void Original_zero_group_consumes_all_birth_RNG_and_reuses_its_apparently_free_row()
    {
        var arena = NativeBattleEffectArena.Clear().Write(0x2F820 + 6, [0x81, 0x81]);
        var task = new NativeBattleModulePoolTask(1, 0, null, 0x590B60, ImmutableArray.Create(new byte[24]));
        NativeBattleGilgameshParticleRows.WriteGroup(task, 16, NativeBattleGilgameshParticleVariant.Masamune, null);
        NativeCrtRandomState random = new(123);
        var expected = random;
        for (int draw = 0; draw < 64; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleMasamuneParticlePreparation actual = NativeBattleMasamuneParticles.Prepare(task, arena, random, false, false);
        Assert.Equal(expected, actual.Random);
        Assert.Equal(new byte[4], arena.Read(0x2F800, 4));
        Assert.NotEqual(0, NativeBattleMasamuneParticles.Word(arena, 0x2F806));
        Assert.Equal(new byte[] { 0x81, 0x81 }, arena.Read(0x2F826, 2));
    }

    [Theory]
    [InlineData(328, 17, 0x590510u)]
    [InlineData(329, 19, 0x591250u)]
    [InlineData(330, 19, 0x5920E0u)]
    public void Expanded_attack_preserves_full_header_delivery_and_releases_its_hosted_owners(int requested, int contactAge, uint callback)
    {
        const int count = 33;
        var (frame, packets, _) = Enter((ushort)requested, count);
        NativeBattleGilgameshAttackStorage storage = frame.Effect.GilgameshAttackStorage!;
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children.Slots[0].UpdateHeader(1, 0, null, callback).WriteWord(12, (ushort)contactAge);
        List<NativeBattleGilgameshContacts> delivered = [];
        var identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGilgameshInputs inputs = new(0, packets, new(identity, 288, 160, 108), 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), () => 0, () => throw new InvalidOperationException(), (current, operation) =>
        {
            if (operation is NativeBattleGilgameshContacts contacts)
            {
                delivered.Add(contacts);
            }
            return current;
        });
        NativeBattleGilgameshEffectResources resources = new(new([]) { Effect = (ushort)requested }, null!, new(new("fixture", []), null!, 0, 0, 0, 0) { Effect = (ushort)requested }, null!, null!, null!, null!, null!, null!, null!, null!, 0, null!, null!);
        frame = NativeBattleGilgameshController.Advance596B70(frame, resources, inputs with { GraphicsFlags = 1 });
        Assert.Empty(delivered);
        Assert.Equal((ushort)contactAge, children.Slots[0].ReadWord(12));
        for (int age = contactAge; age < 51 && frame.Effect.ReturnedDescriptor != 0; age++)
        {
            frame = NativeBattleGilgameshController.Advance596B70(frame, resources, inputs);
        }
        Assert.Equal(new NativeBattleGilgameshContacts(packets.ReadHeader(new(0)).FirstContact, count), Assert.Single(delivered));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.Effect.GilgameshAttackStorage);
        Assert.DoesNotContain(frame.Effect.Pools, pool => pool.BankAddress == storage.ChildAddress);
        var (next, _, _) = Enter((ushort)requested, 1, frame.Memory);
        Assert.True(next.Effect.GilgameshAttackStorage!.ChildAddress > storage.LongRows.Address);
    }

    [Theory]
    [InlineData(328)]
    [InlineData(329)]
    [InlineData(330)]
    public void Expanded_births_keep_duplicate_owners_distinct_and_copy_real_group_and_row_ownership(int requested)
    {
        const int count = 33;
        var (frame, packets, contacts) = Enter((ushort)requested, count);
        NativeBattleGilgameshAttackStorage storage = frame.Effect.GilgameshAttackStorage!;
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        Assert.Equal(265, children.Slots.Length);
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(children.BankAddress, 36, 265));
        Assert.Equal(requested == 328 ? 792 : 726, storage.ShortRows.Capacity);
        Assert.Equal(requested == 328 ? 792 : 396, storage.LongRows.Capacity);
        Assert.All(contacts, contact => Assert.Equal(contacts[0].Owner, contact.Owner));
        uint callback = NativeBattleGilgameshProfile.ForEffect((ushort)requested).AttackCallback;
        NativeBattleModulePoolTask attack = children.Slots[0].UpdateHeader(1, 0, null, callback);
        if (requested == 328)
        {
            frame = NativeBattleGilgameshMasamuneAttack.AdvanceOccurrences(frame, children.BankAddress, 0, contacts, Anchor).Frame;
            Assert.Equal(count, children.Slots.Count(task => task.Callback == 0x590790));
            attack.WriteWord(12, 16);
            frame = NativeBattleGilgameshMasamuneAttack.AdvanceOccurrences(frame, children.BankAddress, 0, contacts, Anchor).Frame;
        }
        else
        {
            attack.WriteWord(12, 18);
            NativeBattleGilgameshAttackStep produced = NativeBattleGilgameshExcalipoorAttack.AdvanceOccurrences(NativeBattleGilgameshAttackProfile.ForEffect(requested), attack, children, frame.Random, 0, contacts, _ => new(10, 20, 30, 40), storage);
            frame = frame with { Random = produced.Random };
        }
        Assert.Equal((requested == 328 ? 5 : 4) * count + 1, children.Slots.Count(task => (task.Flags & 1) != 0));
        uint shortCallback = requested == 328 ? 0x590F10u : 0x5915C0u;
        NativeBattleModulePoolTask[] programs = children.Slots.Where(task => task.Callback == shortCallback).ToArray();
        Assert.Equal(count, programs.Length);
        for (int index = 0; index < count; index++)
        {
            var group = programs[index].ReadExecutionReference<BattleExecutionReference.GilgameshGroup>(0x16, 2);
            Assert.Equal(packets.ResolveContact(new(0), index), group.Contact);
        }
        Assert.All(programs[32].BorrowPayloadArena(0x16, 2).ReadSnapshot(0, 2).Known, known => Assert.False(known));
        if (requested != 328)
        {
            Assert.All(programs[8].BorrowPayloadArena(0x16, 2).ReadSnapshot(0, 2).Known, known => Assert.False(known));
        }
        frame.Memory.Arena!.Write(0x2E000, BitConverter.GetBytes(0x8000u));
        var view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 3000 };
        NativeBattleGilgameshParticleRows rows = new(frame.Memory.Arena!, false, storage.ShortRows);
        if (requested == 328)
        {
            programs[0].WriteWord(14, 0);
            var spawned = NativeBattleMasamuneParticles.PrepareRows(programs[0], rows, frame.Random, true, false, null, false);
            frame = frame with { Random = spawned.Random };
        }
        else
        {
            var spawned = NativeBattleGilgameshParticles.StepRows(programs[0], frame.Memory.Arena!, frame.Random, NativeBattleGilgameshParticlesTests.Resources(), view, new(view, 288, 160, 108), 0, null, storage);
            frame = frame with { Random = spawned.Random };
            var high = children.Slots.Where(task => task.Callback == 0x591850).First();
            var highGroup = rows.ReadGroup(high)!;
            Assert.Equal(rows.ReadGroup(programs[0])!.Contact, highGroup.Contact);
            Assert.False(rows.Matches(0, 0, highGroup));
        }
        Assert.True(rows.Matches(0, 0, rows.ReadGroup(programs[0])));
        Assert.False(rows.Matches(0, 0, rows.ReadGroup(programs[32])));
        Assert.Equal(0x8000u, BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(0x2E000, 4)));
        var copy = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleGilgameshAttackStorage copied = copy.Effect.GilgameshAttackStorage!;
        NativeBattleGilgameshParticleRows copyRows = new(copy.Memory.Arena!, false, copied.ShortRows);
        Assert.True(copyRows.Matches(0, 0, rows.ReadGroup(programs[0])));
        copyRows.Arena.Write(1, [0x81]);
        Assert.True(copyRows.Free(0));
        Assert.False(rows.Free(0));
        copyRows.Occupy(0, 0, rows.ReadGroup(programs[32]));
        Assert.False(copyRows.Free(0));
        Assert.All(copyRows.Arena.ReadSnapshot(0, 4).Known, known => Assert.False(known));
        NativeBattleMasamuneParticles.AdvanceRecord(copyRows.Arena, 0, true, true);
        Assert.True(copyRows.Free(0));
        Assert.Equal(new byte[4], copyRows.Arena.Read(0, 4));

        NativeBattleGilgameshFrame Anchor(NativeBattleGilgameshFrame current, NativeBattleGilgameshOperation operation)
        {
            if (operation is NativeBattleGilgameshMasamuneAnchor anchor)
            {
                Assert.Equal(contacts[0].Owner, anchor.Actor);
                NativeBattleModulePoolTask target = children.Slots[(int)((anchor.Address - children.BankAddress) / children.Stride)];
                target.WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(22, 40);
            }
            return current;
        }
    }

    private static (NativeBattleGilgameshFrame Frame, NativeBattleActionPacketsState Packets, ImmutableArray<(BattleModelOwner Owner, BattleContactOccurrence? Occurrence)> Contacts) Enter(ushort requested, int count, NativeBattleEffectMemory? prior = null)
    {
        BattleModelOwner extra = new BattleEnemyActorModel(new("extra"));
        BattleModelRoster roster = new(OriginalRoster.Owners.Append(extra));
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        var packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 2, 4, 0, 2, requested, null));
        for (int index = 0; index < count; index++)
        {
            (packets, _) = packets.AppendContact(new(new(extra, 0, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(count);
        var profile = NativeBattleGilgameshProfile.ForEffect(requested);
        NativeBattleOriginalEffectState effect = new(requested, requested - 1, new(requested, profile.Entry, profile.Initializer), new(OriginalOwner(0), 0, BattleModelTargets.From([extra]), header, 0, 0), null) { ResourcePointers = [new(profile.TextureGlobal, 0x40000)] };
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        var graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(2) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(10, 20, 300), 0, 0, 4096), GeometryWorldMatrix = identity });
        byte[] tim = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(20), 12);
        NativeBattleEffectMemory memory = (prior ?? NativeBattleEffectMemory.FromColdProcess()) with { Arena = NativeBattleEffectArena.Clear().Write(0x40000, tim) };
        var entered = NativeBattleGilgameshController.Initialize58DCF0(new(memory, effect, new(123)), packets, graphics.ActorModels, BattleContactTaskStorageContract.ExpandableArena);
        var contacts = packets.ReadHeaderContacts(header).Select(packet => (packet.Primary.Recipient, packet.Occurrence)).ToImmutableArray();
        return (entered, packets, contacts);
    }
}
