using Memoria.FF8.Runtime;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterContextCommandsTests
{
    [Fact]
    public void NullRingOnlyClearsBackupCountAndPreservesOtherContextAndCachedRows()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        frame = frame with { Core = frame.Core.Write(Header + 0x174, new byte[] { 6 }).Write(Header + 0x17A, new byte[] { 3 }).Write(Header + 0x48, new byte[] { 5 }).Write(Header + 0x41, new byte[] { 9 }) };
        byte[] slot = frame.Arena.Read(0x2000, 0x100);
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ReadLinkedModel = (_, model) =>
        {
            Assert.Equal(OriginalOwner(4), model);
            return null;
        } });
        Assert.Equal((byte)0, result.Core.Read(Header + 0x17A, 1)[0]);
        Assert.Equal((byte)6, result.Core.Read(Header + 0x174, 1)[0]);
        Assert.Equal((byte)5, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)9, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(slot, result.Arena.Read(0x2000, 0x100));
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void RingResolverReceivesActualModelAfterBackupsAndPublishesPhysicalMinimumIndex()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        byte[] before = frame.Arena.Read(0x2000, 0x100);
        NativeBattleWaterContextServices services = Services() with
        {
            ReadLinkedModel = (_, model) =>
            {
                Assert.Equal(OriginalOwner(4), model);
                return OriginalOwner(2);
            },
            ResolveRing501FF0 = (value, model) =>
            {
                Assert.Equal(OriginalOwner(4), model);
                Assert.Equal((byte)4, value.Core.Read(Header + 0x174, 1)[0]);
                Assert.Equal((byte)1, value.Core.Read(Header + 0x17A, 1)[0]);
                Assert.Equal(new[] { OriginalOwner(4) }, value.Core.PresentationActors!.ReadSavedActors());
                Assert.Equal(OriginalOwner(4), NativeBattleCinematicActorAccess.ReadSlotActor(value));
                return OriginalOwner(1);
            }
        };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal((byte)1, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x41, 1)[0]);
        before[0x1B] = 1;
        Assert.Equal(before, result.Arena.Read(0x2000, 0x100));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Execute(restored, services)));
    }

    [Fact]
    public void BrokenRingReturnOriginalIsRetainedAndInvalidResolverResultFails()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        NativeBattleWaterContextServices services = Services() with { ReadLinkedModel = (_, _) => OriginalOwner(1), ResolveRing501FF0 = (_, model) => model };
        Assert.Equal((byte)4, Execute(frame, services).Arena.Read(0x201B, 1)[0]);
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ResolveRing501FF0 = (_, _) => new BattlePartyActorModel(new("retired")) }));
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ResolveRing501FF0 = (_, _) => throw new InvalidDataException("Actual ring does not return to its origin.") }));
        frame = frame with { Core = frame.Core with { PresentationActors = null } };
        Assert.Throws<InvalidDataException>(() => Execute(frame, services));
    }

    [Fact]
    public void ContactWithoutBackupsUsesTheFirstDuplicateAndReloadsPcAfterTheCallback()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022);
        NativeBattleActionPacketsState packets = PacketBank(4, 4, 2);
        var calls = new List<BattleContactOccurrence>();
        NativeBattleWaterExecutionFrame result = Execute(frame, Services(packets) with
        {
            ApplyContact506690 = (value, contact) =>
            {
                calls.Add(contact);
                return WaterSchedulerFixture.CoreDword(value, 0x2797450, 0xFFFFFFFE);
            }
        });
        Assert.Equal(new BattleContactOccurrence(0), Assert.Single(calls));
        Assert.Equal(0u, Pc(result));
    }

    [Fact]
    public void SavedActorsReadTheirCurrentListAndPreserveTheHeaderExtentDuringCallbacks()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022);
        frame = frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithSavedActors([OriginalOwner(4), OriginalOwner(2)]) } };
        NativeBattleActionPacketsState packets = PacketBank(2, 4);
        var calls = new List<BattleContactOccurrence>();
        NativeBattleWaterExecutionFrame result = Execute(frame, Services(packets) with
        {
            ApplyContact506690 = (value, contact) =>
            {
                calls.Add(contact);
                if (calls.Count == 1)
                {
                    return value with { Core = value.Core with { PresentationActors = value.Core.PresentationActors!.WithSavedActors([OriginalOwner(2), OriginalOwner(4)]) } };
                }
                return value;
            }
        });
        Assert.Equal(new[] { new BattleContactOccurrence(0), new BattleContactOccurrence(0) }, calls);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOrUnmatchedContactListsNeverInvokeTheCallback(bool hasDifferentActor)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022);
        NativeBattleActionPacketsState packets = hasDifferentActor ? PacketBank(2) : PacketBank();
        Assert.Equal(0x1002u, Pc(Execute(frame, Services(packets))));
    }

    [Fact]
    public void CameraInvokesCurrentOwnerWithoutReadingUnusedOperandAndReloadsPc()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1039);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ApplyCamera50A730 = value =>
        {
            calls++;
            return WaterSchedulerFixture.CoreDword(value, 0x2797450, 0xFFFFFFFE);
        } });
        Assert.Equal(1, calls);
        Assert.Equal(2u, Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1002, 2));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(128, 2048)]
    [InlineData(32767, 4096)]
    public void StageColorClampsSignedChannelsAndDepthAndWritesEveryPhysicalGroup(short rawDepth, short expectedDepth)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-1));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 128);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 300);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, unchecked((ushort)rawDepth));
        List<int> groups = [];
        NativeBattleWaterContextServices services = Services() with { WriteStageColor = (value, index, depth, packed) =>
        {
            groups.Add(index);
            Assert.Equal(expectedDepth, depth);
            Assert.Equal(0x00FF8000u, packed);
            Assert.Equal(0u, value.ReadTemporaryDword(0xF0));
            Assert.Equal(128u, value.ReadTemporaryDword(0xF4));
            Assert.Equal(255u, value.ReadTemporaryDword(0xF8));
            Assert.Equal(packed, value.ReadTemporaryDword(0xFC));
            return value;
        } };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal(new[] { 0, 1, 2, 3 }, groups);
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(frame.Arena.Read(0x2000, 0x100), result.Arena.Read(0x2000, 0x100));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Execute(restored, services)));
    }

    [Theory]
    [InlineData(0x0022)]
    [InlineData(0x0039)]
    [InlineData(0x1020)]
    public void UnselectedModesRemainUnsupported(ushort instruction)
    {
        Assert.Throws<NotSupportedException>(() => Execute(Fixture(instruction)));
    }

    [Theory]
    [InlineData(0x10CF)]
    [InlineData(0x1ECF)]
    public void Ring_mode1_copies_actual_actor_list_before_minimum_selection_and_preserves_original_order(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [3]).Write(Header + 0x48, [4, 2, 6]) with { PresentationActors = frame.Core.PresentationActors!.WithCurrentActors([OriginalOwner(4), OriginalOwner(2), OriginalOwner(6)]) }, Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterContextServices services = Services() with
        {
            ReadLinkedModel = (_, _) => OriginalOwner(2),
            ResolveRing501FF0 = (current, _) =>
            {
                Assert.Equal(new byte[] { 4, 2, 6 }, current.Core.Read(Header + 0x174, 3));
                Assert.Equal((byte)3, current.Core.Read(Header + 0x17A, 1)[0]);
                Assert.Equal(new[] { OriginalOwner(4), OriginalOwner(2), OriginalOwner(6) }, current.Core.PresentationActors!.ReadSavedActors());
                Assert.Equal((byte)4, current.Core.Read(Header + 0x48, 1)[0]);
                return OriginalOwner(1);
            }
        };
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal(new byte[] { 4, 2, 6 }, result.Core.Read(Header + 0x174, 3));
        Assert.Equal((byte)1, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
        NativeBattleWaterExecutionFrame copy = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(result));
        Assert.Equal(result.Core.PresentationActors!.ReadSavedActors().ToArray(), copy.Core.PresentationActors!.ReadSavedActors().ToArray());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)255)]
    public void Ring_mode1_empty_selection_does_not_read_retained_actor_bytes(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x10CF);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [count]).MarkUnknown(Header + 0x48, 3) };
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ReadLinkedModel = (_, _) => OriginalOwner(2), ResolveRing501FF0 = (_, _) => OriginalOwner(1) });
        Assert.Equal((byte)0, result.Core.Read(Header + 0x17A, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void Other_ring_modes_are_literal_returns_without_actor_or_PC_reads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x20CF);
        frame = frame with { Core = frame.Core.MarkUnknown(Header, 0x17C).MarkUnknown(0x2797450, 4), Arena = frame.Arena.MarkUnknown(0x201B, 1) };
        Assert.Same(frame, Execute(frame));
    }

    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, Header);
        var (models, _) = OriginalModels(Enumerable.Range(0, 7).Select(slot => (slot, new NativeBattleGraphicsModel(0))));
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models).CaptureCurrentContacts([], OriginalOwner(4)).WithSavedActors([]);
        frame = frame with { Core = frame.Core with { PresentationActors = context } };
        return NativeBattleCinematicActorAccess.WriteSlotActor(frame, OriginalOwner(4), OriginalAliases);
    }

    private static NativeBattleActionPacketsState PacketBank(params int[] slots)
    {
        (NativeBattleActionPacketsState packets, _) = OriginalPackets().Prepare(new(OriginalOwner(4), 2, 0, -1, 0, 201, null));
        packets = packets.AppendContacts(slots.Select(slot => new NativeBattleContactPacket(new(OriginalOwner(slot), 0, 0, 0, 0, 0, 0), null)));
        return packets.Publish(slots.Length).State;
    }

    private static NativeBattleWaterContextServices Services(NativeBattleActionPacketsState? packets = null)
    {
        return new((_, _) => throw new Xunit.Sdk.XunitException("Unexpected linked-model read."),
            (_, _) => throw new Xunit.Sdk.XunitException("Unexpected ring resolver."),
            (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected external action packet read."),
            (_, _) => throw new Xunit.Sdk.XunitException("Unexpected actual contact callback."),
            _ => throw new Xunit.Sdk.XunitException("Unexpected camera callback."),
            (_, _, _, _) => throw new Xunit.Sdk.XunitException("Unexpected stage color callback."),
            _ => packets is null ? null : new BattleActionHeaderReference(0), () => packets ?? throw new Xunit.Sdk.XunitException("Unexpected packet bank read."),
            () => OriginalAliases, BattleContactTaskStorageContract.OriginalArena);
    }

    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices? services = null)
    {
        return NativeBattleWaterContextCommands.Execute(frame, new(0x1000, 0x100, 0x1000), services ?? Services());
    }

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
