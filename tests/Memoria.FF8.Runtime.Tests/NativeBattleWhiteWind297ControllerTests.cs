using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWhiteWind297Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWhiteWind297ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0xFFFFFFFFu, FirstPacketOffset, 0u)]
    public void First_root_scan_selects_native_private_parity_without_initializing_children(uint parity, int offset, uint next)
    {
        NativeBattleWhiteWind297Frame original = Frame(parity);
        NativeBattleWhiteWind297Frame result = NativeBattleWhiteWind297Controller.Advance(original, Resources(), Inputs());
        Assert.Equal(ArenaAddress + (uint)offset, result.State.ReadDword(CursorGlobal));
        Assert.Equal(next, result.Effect.Pools[0].Slots[0].ReadDword(16));
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Single(result.Effect.Pools);
        Assert.Equal(RootDescriptor, result.Effect.ReturnedDescriptor);
        Assert.Null(result.Effect.DrawArenaOffset);
    }

    [Fact]
    public void Cold_restored_root_has_identical_next_parity_and_pool_bytes()
    {
        NativeBattleWhiteWind297Frame original = Frame(1);
        NativeBattleWhiteWind297Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWhiteWind297Frame>(NativeBattleTestSnapshots.Json(original))!;
        NativeBattleWhiteWind297Frame Next(NativeBattleWhiteWind297Frame frame) => NativeBattleWhiteWind297Controller.Advance(frame, Resources(), Inputs());
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(original)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Fact]
    public void Canonical_globals_reject_duplicate_descriptor_and_root_task_ownership()
    {
        NativeBattleWhiteWind297State cold = NativeBattleWhiteWind297State.FromColdProcess();
        cold.Validate();
        Assert.Throws<InvalidDataException>(() => cold.WriteDword(RootBank, 0));
        Assert.Throws<InvalidDataException>(() => cold.WriteDword(TargetDescriptor, 0));
        Assert.Throws<InvalidDataException>(() => (cold with { Globals = cold.Globals.Write((int)(RootBank - NativeBattleWhiteWind297State.BaseAddress), [1]) }).Validate());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Real_ACT_workspaces_finish_same_scan_children_and_apply_each_matching_contact(bool capturePresentation, bool foreignBounds)
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 2, 4, 0, 0, 297, null));
        for (int target = 3; target <= 5; target++)
        {
            (packets, _) = packets.AppendContact(new(new(OriginalOwner(target), 1, 0, 0, 0, 7, 0), null));
        }
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        (packets, _) = packets.Publish(3);
        List<int> contacts = [];
        List<int> anchors = [];
        List<int> minimumReads = [];
        List<NativeBattleWhiteWind297Operation> operations = [];
        int emits = 0;
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleWhiteWind297Inputs inputs = new(packets, actor => new(0)
        {
            MinimumY146 = foreignBounds ? null : (short)-7,
            Transform = new(new(0, 0, 0), new(0, 0, (short)(OriginalSlot(actor) * 100)), 0, 100, 8192)
        }, _ => NativeBattlePackedMatrix32.FromMatrix(camera, 0x2222), (frame, actor) =>
        {
            anchors.Add(OriginalSlot(actor));
            return new(frame, new((short)(OriginalSlot(actor) * 10), -123, (short)(OriginalSlot(actor) * 100), 100));
        }, (frame, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleWhiteWind297Contact contact)
            {
                contacts.Add(contact.Occurrence.Ordinal);
            }
            return frame;
        }, (frame, _, context, _) =>
        {
            emits++;
            return frame with { State = frame.State.WriteDword(0x2507914, (uint)emits) };
        });
        if (foreignBounds)
        {
            inputs = inputs with
            {
                ReadMinimumY = actor =>
                {
                    minimumReads.Add(OriginalSlot(actor));
                    return -7;
                }
            };
        }
        List<NativeBattlePilotVisualIdentity> identities = [];
        if (capturePresentation)
        {
            var nativeEmit = inputs.EmitNode;
            inputs = inputs with
            {
                EmitNodePresentation = (frame, source, context, node, identity) =>
                {
                    identities.Add(identity);
                    Assert.Equal(node.ResourceOffset, identity.Primitive);
                    return nativeEmit(frame, source, context, node);
                }
            };
        }
        NativeBattleWhiteWind297Frame current = Frame(0);
        current = current with { State = current.State.WriteModelOwner(CasterGlobal, OriginalOwner(0), OriginalAliases) };
        NativeBattleWhiteWind297Resources resources = new(Source(22), Source(26), Source(26), new("fixture", []), 0x5578A, new("fixture", []));
        int callbacks = 0;
        while (current.Effect.ReturnedDescriptor != 0 && callbacks++ < 64)
        {
            current = NativeBattleWhiteWind297Controller.Advance(current, resources, inputs);
        }
        Assert.True(callbacks < 64);
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        Assert.Equal(new[] { 0, 3, 4, 5 }, anchors);
        Assert.Equal(foreignBounds ? new[] { 0, 0, 3, 4, 5 } : [], minimumReads);
        Assert.Single(operations.OfType<NativeBattleWhiteWind297Sound>());
        Assert.True(emits > 0);
        if (capturePresentation)
        {
            Assert.NotEmpty(identities);
            Assert.All(identities, identity => Assert.Equal((ushort)297, identity.Effect));
            Assert.Contains(identities, identity => identity.Bank == SceneDescriptor);
            Assert.Contains(identities, identity => identity.Bank == TargetDescriptor);
            Assert.True(identities.Select(identity => identity.Generation).Distinct().Count() > 1);
        }
        else
        {
            Assert.Empty(identities);
        }
        Assert.Equal((uint)emits, current.State.ReadDword(0x2507914));
        Assert.All(current.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
    }

    [Fact]
    public void Expanded_target_bank_keeps_occurrence_order_through_copy_and_retirement()
    {
        BattleModelOwner extra = new BattlePartyActorModel(new("extra"));
        BattleModelRoster roster = new(OriginalRoster.Owners.Append(extra));
        NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
        BattleModelOwner[] recipients = [extra, OriginalOwner(3), extra, OriginalOwner(4), OriginalOwner(5)];
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 2, 4, 0, 0, 297, null));
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 1, 0, 0, 0, 7, 0), null));
        }
        (packets, _) = packets.Publish(recipients.Length);
        List<(int Age, BattleModelOwner Owner)> births = [];
        List<BattleContactOccurrence> contacts = [];
        NativeBattleWhiteWind297Inputs inputs = new(packets, _ => new(0) { MinimumY146 = -7, Transform = new(new(0, 0, 0), new(0, 0, 100), 0, 100, 8192) },
            _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (frame, owner) =>
            {
                if (owner != OriginalOwner(0))
                {
                    births.Add((frame.Effect.Pools[1].Slots[0].ReadWord(12), owner));
                }
                return new(frame, new(10, -123, 100, 100));
            }, (frame, operation) =>
            {
                if (operation is NativeBattleWhiteWind297Contact contact)
                {
                    contacts.Add(contact.Occurrence);
                }
                return frame;
            }, (frame, _, _, _) => frame) { StorageContract = BattleContactTaskStorageContract.ExpandableArena };
        NativeBattleWhiteWind297Resources resources = new(Source(22), Source(26), Source(26), new("fixture", []), 0x5578A, new("fixture", []));
        NativeBattleWhiteWind297Frame current = Frame(0);
        current = current with
        {
            State = current.State.WriteModelOwner(CasterGlobal, OriginalOwner(0), aliases),
            Effect = current.Effect with { Parameters = current.Effect.Parameters! with { TargetActors = BattleModelTargets.From(recipients) } }
        };
        current.Memory.Arena!.Write(MorphOffset, [0x81]);
        for (int callback = 0; callback <= 28; callback++)
        {
            current = NativeBattleWhiteWind297Controller.Advance(current, resources, inputs);
        }
        Assert.Equal(new[] { 19, 23, 27, 27, 27 }, births.Select(birth => birth.Age));
        Assert.Equal(recipients, births.Select(birth => birth.Owner));
        Assert.Empty(contacts);
        NativeBattleEffectTaskPool originalTargets = current.Effect.Pools[2];
        Assert.Equal(TargetDescriptor, originalTargets.DescriptorAddress);
        Assert.Equal(recipients.Length, originalTargets.Slots.Length);
        Assert.True(NativeBattlePresentationTaskStorage.IsHostedBank(originalTargets.BankAddress, 0x290, recipients.Length));
        uint oldAge = originalTargets.Slots[0].ReadDword(0x1C);
        current = NativeBattleTestSnapshots.Independent(current);
        NativeBattleEffectTaskPool copied = current.Effect.Pools[2];
        Assert.NotSame(originalTargets.RequireOwner(), copied.RequireOwner());
        Assert.Equal(originalTargets.BankAddress, copied.BankAddress);
        for (int index = 0; index < recipients.Length; index++)
        {
            var captured = Assert.Single(copied.Slots[index].ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(12, 4).Recipients);
            Assert.Equal(recipients[index], captured.Owner);
            Assert.Equal(packets.ResolveContact(header, index), captured.Occurrence);
        }
        current = NativeBattleWhiteWind297Controller.Advance(current, resources, inputs);
        Assert.Equal(oldAge + 1, copied.Slots[0].ReadDword(0x1C));
        Assert.Equal(oldAge, originalTargets.Slots[0].ReadDword(0x1C));
        for (int callback = 0; callback < 64 && current.Effect.ReturnedDescriptor != 0; callback++)
        {
            current = NativeBattleWhiteWind297Controller.Advance(current, resources, inputs);
        }
        Assert.Equal(Enumerable.Range(0, recipients.Length).Select(index => packets.ResolveContact(header, index)), contacts);
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { RootDescriptor, SceneDescriptor }, current.Effect.Pools.Select(pool => pool.DescriptorAddress));
        NativeBattleEffectArena arena = current.Memory.Arena!;
        Assert.All(arena.ReadSnapshot(12, 4).Known, known => Assert.False(known));
        Assert.Equal(aliases.EncodeAddress(OriginalOwner(3))!.Value.ToArray(), arena.Read(0x290 + 12, 4));
        Assert.Equal(new byte[] { 0x81 }, arena.Read(MorphOffset, 1));
    }

    private static NativeBattleGfAnimationSource Source(int duration)
    {
        byte[] bytes = new byte[80];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), duration);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 16);
        return new(0x123400, bytes.ToImmutableArray());
    }

    private static NativeBattleWhiteWind297Frame Frame(uint parity)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, parity)) });
        NativeBattleOriginalEffectState effect = new(297, 297, new(297, Entry, Initializer), new(OriginalOwner(0), 1, BattleModelTargets.From([OriginalOwner(3)]), new BattleActionHeaderReference(0), 0, 0), RootDescriptor) { Pools = [root] };
        return new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(297), effect, new(1), NativeBattleWhiteWind297State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
    }

    private static NativeBattleWhiteWind297Resources Resources() => new(new(0, []), new(0, []), new(0, []), new("fixture", []), 0, new("fixture", []));
    private static NativeBattleWhiteWind297Inputs Inputs() => new(OriginalPackets(), _ => throw new InvalidOperationException("Root parity must not read actors."), _ => throw new InvalidOperationException("Root parity must not read camera."), (_, _) => throw new InvalidOperationException("Root parity must not read anchors."), (_, _) => throw new InvalidOperationException("Root parity must not invoke services."), (_, _, _, _) => throw new InvalidOperationException("Root parity must not draw."));
}
