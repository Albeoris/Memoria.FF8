using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicContactsTests
{
    private const uint HeaderBase = 0x1D280C4;
    private const uint Manager = 0x2797690;
    private const uint Parameters = 0x1D99A78;

    [Theory]
    [InlineData(0x7F, 127)]
    [InlineData(0x80, -128)]
    [InlineData(0xFF, -1)]
    public void NativeCountInterpretationRemainsOpcodeSpecific(byte raw, int signed)
    {
        Assert.Equal(signed, NativeBattleCinematicContacts.ReadCount(HeaderBase, Read, null, signedNative: true));
        Assert.Equal((int)raw, NativeBattleCinematicContacts.ReadCount(HeaderBase, Read, null, signedNative: false));
        Assert.Equal(NativeBattleContactAddress.Encode(48) + 24, NativeBattleCinematicContacts.Offset(NativeBattleContactAddress.Encode(48), 1, null));

        byte[] Read(uint address, int length)
        {
            Assert.Equal(HeaderBase + 16, address);
            Assert.Equal(1, length);
            return [raw];
        }
    }

    [Theory]
    [InlineData(48)]
    [InlineData(255)]
    public void NextContactRetainsOccurrencesAndFullCursorAcrossJson(int first)
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)8, 257));
        NativeBattleWaterExecutionFrame frame = Frame(bank, first);
        List<uint> applied = [];
        NativeBattleWaterContextServices services = Services(bank, applied);

        frame = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(first + 1, frame.Core.PresentationActors!.ContactCursor);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(restored, services);

        Assert.Equal(first + 2, result.Core.PresentationActors!.ContactCursor);
        Assert.Equal(new[] { NativeBattleContactAddress.Encode(first), NativeBattleContactAddress.Encode(first + 1) }, applied);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void NextContactRereadsCursorAfterApplyingAndRejectsMissingContext()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)8, 257));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 48);
        NativeBattleWaterContextServices services = Services(bank, []) with
        {
            ApplyContact506690 = (current, address) =>
            {
                Assert.Equal(NativeBattleContactAddress.Encode(48), address);
                return current with { Core = current.Core with { PresentationActors = current.Core.PresentationActors!.WithContactCursor(255) } };
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(256, result.Core.PresentationActors!.ContactCursor);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicNextContact12E.Execute(frame with { Core = frame.Core with { PresentationActors = null } }, services));
    }

    [Fact]
    public void SavedActorModeUsesFullReverseListAndOnlyFirstMatchingOccurrences()
    {
        byte[] saved = [1, 2, 3, 4, 5, 6, 8];
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 48).Concat(saved).Append((byte)8));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 0).WriteTemporaryWord(0x14A, 0x1022);
        frame = frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithSavedActors([.. saved]) } };
        List<uint> applied = [];

        _ = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(bank, applied));

        Assert.Equal(Enumerable.Range(48, saved.Length).Reverse().Select(NativeBattleContactAddress.Encode), applied);
    }

    [Fact]
    public void AllContactModePublishesEveryOccurrenceThroughOrdinal256()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)8, 257));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 0).WriteTemporaryWord(0x14A, 0x8022);
        List<uint> applied = [];

        _ = NativeBattleIfritContactCommand.Execute(frame, Services(bank, applied));

        Assert.Equal(Enumerable.Range(0, 257).Select(NativeBattleContactAddress.Encode), applied);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostedContactBranchCrossesBankAndRejectsMissingActorBeforeFlagRead(bool present)
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 49).Append(present ? (byte)8 : (byte)7));
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Frame(bank, 0), 0, 0x1B, 8);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100).WriteTemporaryWord(0x14A, 0x0246);
        NativeBattleWaterScriptProgram program = new(0, 4, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(2, [8, 0]) };
        int flagReads = 0;

        if (present)
        {
            NativeBattleWaterExecutionFrame result = Execute();
            Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
            Assert.Equal(1, flagReads);
        }
        else
        {
            Assert.Contains("no contact for the current actor", Assert.Throws<InvalidDataException>(() => Execute()).Message);
            Assert.Equal(0, flagReads);
        }

        NativeBattleWaterExecutionFrame Execute() => NativeBattleCinematicContactBranch46.Execute(frame, program, (_, address, length) =>
        {
            if (address == NativeBattleContactAddress.Encode(49) + 3)
            {
                flagReads++;
            }
            return Read(bank, address, length);
        }, (_, _) => throw new InvalidOperationException(), bank);
    }

    [Theory]
    [InlineData(0u, 0x1004u)]
    [InlineData(0x2000u, 0x1008u)]
    public void HostedStatusBranchDoesNotReadUnrelatedContactFlags(uint status, uint expectedPc)
    {
        NativeBattleActionPacketsState bank = Bank(new byte[] { 7 });
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Frame(bank, 0), 0, 0x1B, 8);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100).WriteTemporaryWord(0x14A, 0x0846);
        frame = frame with { Arena = frame.Arena.Write(2, [8, 0]) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContactBranch46.Execute(frame, new(0, 4, 0x1000), (_, _, _) => throw new InvalidOperationException(), (_, actor) =>
        {
            Assert.Equal((byte)8, actor);
            return status;
        }, bank);

        Assert.Equal(expectedPc, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void NextActionRefreshUsesSelectedHeaderAndPreservesFullCursor()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 48), Enumerable.Repeat((byte)8, 256).Append((byte)6));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 256);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xC0, Parameters);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 4);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, [1]) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextActionAE.CaptureCurrent(frame, 0x400, (_, address, length) => Read(bank, address, length), Models(), bank);

        Assert.Equal(HeaderBase + 20, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xCC));
        Assert.Equal(new byte[] { 8, 6 }, result.Core.PresentationActors!.CurrentActors.ToArray());
        Assert.Equal(256, result.Core.PresentationActors.ContactCursor);
    }

    [Fact]
    public void Effect312FindUsesFullCountAndCrossesThePhysicalContactEnd()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 256).Append((byte)8));
        int found = NativeBattleEffect312Contacts.Find(Parameters, 8, (address, length) => NativeBattleEffectArena.Clear().Write(0, Read(bank, address, length)), bank);

        Assert.Equal(256, found);
    }

    [Fact]
    public void Effect312LateThrowPublishesContactAcrossThePhysicalEnd()
    {
        const uint poolAddress = 0x223E1A8;
        const uint taskAddress = 0x223E1B8;
        const uint savedActor = 0x223EFE4;
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)1, 48), new byte[] { 1, 1 });
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(poolAddress, taskAddress, 36, 100);
        (pool, int slot) = pool.Append(0x5A0BF0);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, 28).WriteWord(14, 1).WriteWord(26, 1));
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, 0x59CB90, 0x59CB70), new(OriginalOwner(3), 0, BattleModelTargets.From([OriginalOwner(0)]), new BattleActionHeaderReference(1), 0, 0), null) { Pools = [pool] };
        NativeBattleEffect312Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(83), NativeBattleEffect312State.FromColdProcess().DetachBank(1).WriteDword(0x2240EB8, Parameters))
        {
            ReadPhysical = (address, length) => NativeBattleEffectArena.Clear().Write(0, address == Parameters + 4 ? BitConverter.GetBytes(HeaderBase + 20) : Read(bank, address, length))
        };
        frame = NativeBattleEffect312Controller.WriteBytes(frame, savedActor, NativeBattleEffectArena.Clear(), 20);
        List<uint> applied = [];
        NativeBattleEffect312Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(),
            (_, _) => NativeBattleEffectArena.Clear(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(),
            (_, _, _) => throw new InvalidOperationException(), (_, _) => 33, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, operation) =>
        {
            if (operation is NativeBattleEffect312Contact506690 contact)
            {
                applied.Add(contact.TargetArgumentAddress);
            }
            return current;
        }) { HostedActionPackets = bank };

        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, taskAddress, null!, inputs);

        Assert.Equal(2, result.Result);
        Assert.Equal(NativeBattleContactAddress.Encode(49), Assert.Single(applied));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData(HeaderBase + 1)]
    [InlineData(HeaderBase + 20)]
    [InlineData(HeaderBase - 20)]
    public void SemanticCountRejectsNoncanonicalOrUnpreparedAddresses(uint header)
    {
        NativeBattleActionPacketsState bank = Bank(new byte[] { 8 });
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicContacts.ReadCount(header, (_, _) => throw new InvalidOperationException(), bank, signedNative: false));
    }

    private static NativeBattleWaterExecutionFrame Frame(NativeBattleActionPacketsState bank, int cursor)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xCC, HeaderBase);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(bank.PresentationRoster!, Models()).WithContactCursor(cursor) } };
    }

    private static ImmutableArray<NativeBattleGraphicsModel> Models() => [.. Enumerable.Range(0, 9).Select(row => new NativeBattleGraphicsModel(0))];

    private static NativeBattleActionPacketsState Bank(params IEnumerable<byte>[] groups)
    {
        BattleModelRoster roster = new([.. Enumerable.Range(0, 9).Select(row => new BattleModelOwner($"actor-{row}", row < 3))]);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(roster, 320);
        foreach (IEnumerable<byte> group in groups)
        {
            byte[] recipients = group.ToArray();
            (packets, _) = packets.Prepare(new(OriginalOwner(0), 2, 5, -1, 6, 7, null));
            packets = packets.AppendContacts(recipients.Select(actor => new NativeBattleContactPacket(new(actor, 0, 0, 4, 0, 0, 0), null)));
            (packets, _) = packets.Publish(recipients.Length);
        }
        return packets;
    }

    private static byte[] Read(NativeBattleActionPacketsState bank, uint address, int length)
    {
        if (address == Parameters)
        {
            return [0];
        }
        if (address == Parameters + 4)
        {
            return BitConverter.GetBytes(HeaderBase);
        }
        if (address >= HeaderBase && address < NativeBattleContactAddress.Encode(0))
        {
            uint relative = address - HeaderBase;
            return bank.ExportRetained().Headers[(int)(relative / 20)].Payload.AsSpan((int)(relative % 20), length).ToArray();
        }
        (int row, int offset) = NativeBattleContactAddress.ResolveField(address, length, bank.StorageCapacity);
        return bank.ReadContactBytes(row).AsSpan(offset, length).ToArray();
    }

    private static NativeBattleWaterContextServices Services(NativeBattleActionPacketsState bank, List<uint> applied) => new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, address, length) => Read(bank, address, length), (frame, address) =>
    {
        applied.Add(address);
        return frame;
    }, _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException()) { ActionPackets = bank };
}
