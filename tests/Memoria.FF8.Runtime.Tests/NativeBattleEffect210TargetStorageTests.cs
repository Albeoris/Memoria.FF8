using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210TargetStorageTests
{
    private const uint Main = 0x20DFAB8 + 0xA750;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(11)]
    public void Captured_targets_survive_actual_echo_stores_and_addressed_bounds(int count)
    {
        Fixture fixture = new(count);
        NativeBattleEffect210Frame frame = fixture.Capture();
        Assert.Equal(count, Read(frame, Main).ReadWord(14));
        Assert.Equal(count > 3, frame.State.TargetRecords is not null);
        Assert.Equal(count > 3, frame.Memory.PresentationTaskNextAddress.HasValue);
        Assert.Equal(count == 0 ? new NativeBattleVector(0, 0, 0) : new NativeBattleVector(100, 200, 300), Vector(ReadBytes(frame, Main + 0x58, 6)));
        if (frame.State.TargetRecords is { } storage)
        {
            Assert.Equal((count - 3) * 24, storage.Length);
        }
        frame = NativeBattleEffect210Lower.InitializeEcho6A1230(frame, Main + 0x60);
        fixture.AssertRecords(frame);
        frame = InitializeTargetMotion(frame, Main, fixture.Inputs);
        Assert.Equal(fixture.Targets, fixture.Operations.OfType<NativeBattleEffect210Bounds9800>().Select(operation => operation.Actor));
        for (int index = 0; index < count; index++)
        {
            uint address = TargetRecordAddress(frame, Main, index);
            Assert.Equal(index < 3 ? Main + 16u + (uint)(index * 24) : frame.State.TargetRecords!.Address + (uint)((index - 3) * 24), address);
            Assert.Equal(new byte[] { 0x30, 0, 0xF0, 0xFF }, ReadBytes(frame, address + 20, 4).Read(0, 4));
        }
        fixture.AssertRecords(frame);
    }

    [Fact]
    public void Private_forks_preserve_knownness_and_captured_owners_without_sharing_spill_writes()
    {
        Fixture fixture = new(11);
        NativeBattleEffect210Frame original = fixture.Capture();
        NativeBattleEffect210Frame first = NativeBattleStorageSnapshots.Fork(original);
        NativeBattleEffect210Frame second = NativeBattleStorageSnapshots.Fork(original);
        uint record = TargetRecordAddress(first, Main, 3);
        first = WriteBytes(first, record + 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xBEEF)), 2);
        Assert.Equal(0xBEEF, BitConverter.ToUInt16(ReadBytes(first, record + 12, 2).Read(0, 2)));
        Assert.Equal(0, BitConverter.ToUInt16(ReadBytes(second, record + 12, 2).Read(0, 2)));
        fixture.AssertRecords(second);
        fixture.AssertRecords(original);
        fixture.Operations.Clear();
        first = Write(first, Main, Read(first, Main).WriteWord(12, 235));
        NativeBattleEffect210Inputs changedHeader = fixture.Inputs with { ReadPackets = _ => new Fixture(0).Packets };
        first = AdvanceMain6F7310(first, Main, null!, changedHeader).Frame;
        NativeBattleEffect210WriteModelBytes[] restored = fixture.Operations.OfType<NativeBattleEffect210WriteModelBytes>().Where(operation => operation.Offset == 28 && operation.Length == 8).ToArray();
        Assert.Equal(fixture.Targets, restored.Select(operation => operation.Actor));
        for (int index = 0; index < restored.Length; index++)
        {
            Assert.Equal(fixture.Model(fixture.Targets[index]).ReadSnapshot(28, 8).Known, restored[index].Data.ReadSnapshot(0, 8).Known);
        }
    }

    [Theory]
    [InlineData(115)]
    [InlineData(194)]
    [InlineData(195)]
    [InlineData(234)]
    public void Motion_uses_all_captured_rows_in_order_and_preserves_original_points(int age)
    {
        Fixture fixture = new(11);
        NativeBattleEffect210Frame frame = InitializeTargetMotion(fixture.Capture(), Main, fixture.Inputs);
        fixture.Operations.Clear();
        frame = MoveTargets(frame, Main, age, fixture.Inputs);
        Assert.Equal(fixture.Targets, fixture.Operations.OfType<NativeBattleEffect210DrawModel9960>().Select(operation => operation.Actor));
        Assert.Equal(fixture.Targets, fixture.Operations.OfType<NativeBattleEffect210WriteModelBytes>().Where(operation => operation.Offset == 30).Select(operation => operation.Actor));
        fixture.AssertRecords(NativeBattleStorageSnapshots.Fork(frame));
    }

    [Fact]
    public void Original_mode_rejects_overlap_and_corrupt_or_missing_hosted_rows_fail_explicitly()
    {
        Fixture original = new(3);
        Assert.Null(original.Capture(BattleContactTaskStorageContract.OriginalArena).State.TargetRecords);
        Fixture fixture = new(4);
        Assert.Throws<NotSupportedException>(() => fixture.Capture(BattleContactTaskStorageContract.OriginalArena));
        NativeBattleEffect210Frame frame = fixture.Capture();
        Assert.Throws<InvalidDataException>(() => ReadBytes(frame, frame.State.TargetRecords!.Address + 23, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetRecordAddress(frame, Main, 4));
        NativeBattleEffect210Frame absent = frame with { State = frame.State with { TargetRecords = null } };
        Assert.Throws<NotSupportedException>(() => TargetRecordAddress(absent, Main, 0));
        frame = Write(frame, Main, Read(frame, Main).WriteWord(14, 3));
        Assert.Throws<InvalidDataException>(() => TargetRecordAddress(frame, Main, 0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public void Final_batch_admits_exact_count_once_and_restores_native_prefix_after_actual_drain(int count)
    {
        Fixture fixture = new(count);
        NativeBattleEffect210Frame frame = fixture.Capture();
        frame = DetachCommonPool(frame, EarlyDescriptor, true);
        frame = InitializeCommonPool(frame, ActDescriptor);
        NativeBattleEffectTaskPool original = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ActDescriptor);
        uint nativeBank = original.BankAddress;
        ushort stride = original.Stride;
        for (int index = 0; index < original.Slots.Length; index++)
        {
            original.Slots[index].WriteByte(0x700, (byte)(0xA0 + index)).MarkPayloadUnknown(0x701, 1);
        }
        frame = Write(frame, Main, Read(frame, Main).WriteWord(12, 243));
        NativeBattleEffect210Resources resources = EmptyFinalProgramme();
        NativeCrtRandomState random = frame.Random;
        NativeBattleActionPacketsState changedPackets = NativeBattleActionPacketsState.Create(fixture.Packets.Roster, NativeBattleRetainedActionPackets.Fresh(), fixture.Packets.ModelAliases);
        (changedPackets, _) = changedPackets.Prepare(new(OriginalOwner(3), 8, 54, -1, 315, 210, null));
        (changedPackets, _) = changedPackets.Publish(1);
        NativeBattleEffect210Inputs currentHeader = fixture.Inputs with { ReadPackets = _ => changedPackets };
        frame = AdvanceMain6F7310(frame, Main, resources, currentHeader).Frame;
        NativeBattleEffectTaskPool final = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ActDescriptor);
        Assert.Equal(count, final.Slots.Length);
        Assert.Equal(count, final.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(count > 3, final.BankAddress != nativeBank);
        Assert.Equal(random, frame.Random);
        Assert.Equal(0, Assert.Single(fixture.Operations.OfType<NativeBattleEffect210AllContacts506BA0>()).Count);
        Assert.All(final.Slots, task => Assert.Equal(0x6F9AA0u, task.Callback));
        if (count > 3)
        {
            NativeBattleEffect210Frame repeated = NativeBattleStorageSnapshots.Fork(frame);
            repeated = Write(repeated, Main, Read(repeated, Main).WriteWord(12, 243));
            Assert.Throws<InvalidDataException>(() => AdvanceMain6F7310(repeated, Main, resources, fixture.Inputs));
            Assert.Throws<InvalidDataException>(() => DetachCommonPool(frame, ActDescriptor, false));
        }
        frame = NativeBattleStorageSnapshots.Fork(frame);
        (frame, int survivors) = AdvancePool(frame, ActDescriptor, resources, fixture.Inputs);
        Assert.Equal(0, survivors);
        frame = DetachCommonPool(frame, ActDescriptor, false);
        Assert.DoesNotContain(frame.Effect.Pools, pool => pool.DescriptorAddress == ActDescriptor);
        for (int index = 0; index < 3; index++)
        {
            uint address = nativeBank + (uint)(index * stride);
            Assert.Equal((byte)(0xA0 + index), ReadBytes(frame, address + 0x700, 1).Read(0, 1)[0]);
            Assert.False(ReadBytes(frame, address + 0x701, 1).ReadSnapshot(0, 1).Known[0]);
            Assert.Equal(index < 2 ? address + stride : 0u, BitConverter.ToUInt32(ReadBytes(frame, address + 4, 4).Read(0, 4)));
        }
    }

    [Fact]
    public void Drain_releases_spill_and_next_capture_uses_only_its_actual_count()
    {
        Fixture fixture = new(11);
        NativeBattleEffect210Frame frame = fixture.Capture();
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            frame = DetachCommonPool(frame, shape.Descriptor, true);
        }
        frame = Advance(frame, null!, fixture.Inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.State.TargetRecords);
        Fixture next = new(3);
        NativeBattleEffect210Frame recaptured = next.Capture();
        Assert.Null(recaptured.State.TargetRecords);
        next.AssertRecords(recaptured);
    }

    private sealed class Fixture
    {
        internal ImmutableArray<BattleModelOwner> Targets { get; }
        internal NativeBattleActionPacketsState Packets { get; }
        internal NativeBattleEffect210Inputs Inputs { get; }
        internal List<NativeBattleEffect210Operation> Operations { get; } = [];

        internal Fixture(int count)
        {
            Targets = Enumerable.Range(0, count).Select(index => index < 3 ? OriginalOwner(index) : new BattlePartyActorModel(new($"effect210-target-{index}"))).ToImmutableArray();
            BattleModelRoster roster = new(OriginalRoster.Owners.Concat(Targets.Skip(3)));
            NativeBattleModelAliases aliases = new(roster, Enumerable.Range(0, 7).Select(index => (NativeBattleModelSlot.Decode((byte)index), OriginalOwner(index))));
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
            (packets, _) = packets.Prepare(new(OriginalOwner(3), 8, 54, -1, 315, 210, null));
            foreach (BattleModelOwner actor in Targets)
            {
                (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 0, 7, 0), null));
            }
            (packets, _) = packets.Publish(1);
            Packets = packets;
            Inputs = new NativeBattleEffect210ControllerTests.Harness().Inputs() with
            {
                StorageContract = BattleContactTaskStorageContract.ExpandableArena,
                ReadPackets = _ => Packets,
                ReadModelArena9C = (_, actor) => Model(actor),
                Execute = (frame, operation) =>
                {
                    Operations.Add(operation);
                    return operation is NativeBattleEffect210Bounds9800 bounds
                        ? WriteBytes(frame, bounds.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x30, 0, 0xF0, 0xFF }), 4)
                        : frame;
                }
            };
        }

        internal NativeBattleEffect210Frame Capture(BattleContactTaskStorageContract storage = BattleContactTaskStorageContract.ExpandableArena)
        {
            NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(1, false);
            frame = frame with { State = frame.State.WriteModelAddress(CasterGlobal, OriginalOwner(3), Packets.ModelAliases) };
            return AdvanceRoot6F6890(frame, RootBank, null!, Inputs with { StorageContract = storage }).Frame;
        }

        internal NativeBattleEffectArena Model(BattleModelOwner actor)
        {
            int index = Targets.IndexOf(actor);
            NativeBattleEffectArena model = NativeBattleEffectArena.Clear().Write(28, BitConverter.GetBytes((short)(index + 10))).Write(30, BitConverter.GetBytes((short)(index + 20))).Write(32, BitConverter.GetBytes((short)(index + 30))).Write(34, BitConverter.GetBytes((ushort)0xBEEF)).Write(38, BitConverter.GetBytes((short)700));
            return (index & 1) == 0 ? model : model.MarkUnknown(34, 1);
        }

        internal void AssertRecords(NativeBattleEffect210Frame frame)
        {
            for (int index = 0; index < Targets.Length; index++)
            {
                NativeBattleEffectArena row = ReadBytes(frame, TargetRecordAddress(frame, Main, index), 24);
                Assert.Equal(Targets[index], row.ReadExecutionReference<BattleExecutionReference.Model>(0, 4).Owner);
                (byte[] bytes, bool[] known) = Model(Targets[index]).ReadSnapshot(28, 8);
                (byte[] actual, bool[] actualKnown) = row.ReadSnapshot(4, 8);
                Assert.Equal(known, actualKnown);
                for (int lane = 0; lane < 8; lane++)
                {
                    if (known[lane])
                    {
                        Assert.Equal(bytes[lane], actual[lane]);
                    }
                }
                if (index >= 3)
                {
                    Assert.All(row.ReadSnapshot(0, 4).Known, value => Assert.False(value));
                }
            }
        }
    }

    private static NativeBattleEffect210Resources EmptyFinalProgramme()
    {
        const uint address = 0x146ED2C;
        NativeBattleGfAnimationSource source = new(address, new byte[8].ToImmutableArray());
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(address, source), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }
}
