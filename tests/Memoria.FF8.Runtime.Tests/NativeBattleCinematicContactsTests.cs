using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicContactsTests
{
    private const uint HeaderBase = 0x1D280C4;
    private const uint Manager = 0x2797690;
    private static readonly BattleModelOwner ExtraAlly = new BattlePartyActorModel(new("extra-ally"));
    private static readonly BattleModelOwner ExtraEnemy = new BattleEnemyActorModel(new("extra-enemy"));
    private static readonly NativeBattleGraphicsState Original = OriginalGraphics(Enumerable.Range(0, 7).Select(slot => (slot, OriginalOwner(slot), new NativeBattleGraphicsModel(0))));
    private static readonly BattleActorModels Models = Original.ActorModels.Add(ExtraAlly, new(0)).Add(ExtraEnemy, new(0));

    [Theory]
    [InlineData(48)]
    [InlineData(255)]
    public void NextContactRetainsOccurrencesAndFullCursorAcrossPrivateContinuation(int first)
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)8, 257));
        NativeBattleWaterExecutionFrame frame = Frame(bank, first);
        List<BattleContactOccurrence> applied = [];
        NativeBattleWaterContextServices services = Services(bank, applied);

        frame = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(first + 1, frame.Core.PresentationActors!.ContactsProcessed);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(restored, services);

        Assert.Equal(first + 2, result.Core.PresentationActors!.ContactsProcessed);
        Assert.Equal(new[] { new BattleContactOccurrence(first), new BattleContactOccurrence(first + 1) }, applied);
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
                Assert.Equal(new BattleContactOccurrence(48), address);
                return current with { Core = current.Core with { PresentationActors = current.Core.PresentationActors!.WithContactsProcessed(255) } };
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextContact12E.Execute(frame, services);
        Assert.Equal(256, result.Core.PresentationActors!.ContactsProcessed);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicNextContact12E.Execute(frame with { Core = frame.Core with { PresentationActors = null } }, services));
    }

    [Fact]
    public void SavedActorModeUsesFullReverseListAndOnlyFirstMatchingOccurrences()
    {
        byte[] saved = [1, 2, 3, 4, 5, 6, 8];
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 48).Concat(saved).Append((byte)8));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 0).WriteTemporaryWord(0x14A, 0x1022);
        frame = frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithSavedActors([.. saved.Select(Owner)]) } };
        List<BattleContactOccurrence> applied = [];

        _ = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(bank, applied));

        Assert.Equal(Enumerable.Range(48, saved.Length).Reverse().Select(ordinal => new BattleContactOccurrence(ordinal)), applied);
    }

    [Fact]
    public void AllContactModePublishesEveryOccurrenceThroughOrdinal256()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)8, 257));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 0).WriteTemporaryWord(0x14A, 0x8022);
        List<BattleContactOccurrence> applied = [];

        _ = NativeBattleIfritContactCommand.Execute(frame, Services(bank, applied));

        Assert.Equal(Enumerable.Range(0, 257).Select(ordinal => new BattleContactOccurrence(ordinal)), applied);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostedContactBranchCrossesBankAndRejectsMissingActorBeforeFlagRead(bool present)
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 49).Append(present ? (byte)8 : (byte)7));
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicActorAccess.WriteSlotActor(Frame(bank, 0), ExtraEnemy, bank.ModelAliases);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100).WriteTemporaryWord(0x14A, 0x0246);
        NativeBattleWaterScriptProgram program = new(0, 4, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(2, [8, 0]) };
        if (present)
        {
            NativeBattleWaterExecutionFrame result = Execute();
            Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Execute());
        }

        NativeBattleWaterExecutionFrame Execute() => NativeBattleCinematicContactBranch46.Execute(frame, program, Services(bank, []), (_, _) => throw new InvalidOperationException());
    }

    [Theory]
    [InlineData(0u, 0x1004u)]
    [InlineData(0x2000u, 0x1008u)]
    public void HostedStatusBranchDoesNotReadUnrelatedContactFlags(uint status, uint expectedPc)
    {
        NativeBattleActionPacketsState bank = Bank(new byte[] { 7 });
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicActorAccess.WriteSlotActor(Frame(bank, 0), ExtraEnemy, bank.ModelAliases);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100).WriteTemporaryWord(0x14A, 0x0846);
        frame = frame with { Arena = frame.Arena.Write(2, [8, 0]) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContactBranch46.Execute(frame, new(0, 4, 0x1000), Services(bank, []), (_, actor) =>
        {
            Assert.Equal(ExtraEnemy, actor);
            return status;
        });

        Assert.Equal(expectedPc, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void NextActionRefreshUsesSelectedHeaderAndPreservesFullCursor()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 48), Enumerable.Repeat((byte)8, 256).Append((byte)6));
        NativeBattleWaterExecutionFrame frame = Frame(bank, 256);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 4);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD0, [1]) };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNextActionAE.CaptureCurrent(frame, 0x400, relative =>
        {
            BattleActionHeaderReference header = new(relative);
            return new(OriginalOwner(0), header, HeaderBase + relative * 20u, ExtraAlly, bank.ReadHeaderContacts(header).Select(packet => packet.Primary.Recipient).ToImmutableArray(), Models, Original.NativeBindings, BattleContactTaskStorageContract.ExpandableArena);
        });

        Assert.Equal(HeaderBase + 20, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xCC));
        Assert.Equal(new[] { ExtraEnemy, OriginalOwner(6) }, result.Core.PresentationActors!.CurrentActors.ToArray());
        Assert.Equal(256, result.Core.PresentationActors.ContactsProcessed);
    }

    [Fact]
    public void Effect312FindUsesFullCountAndCrossesThePhysicalContactEnd()
    {
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)7, 256).Append((byte)8));
        int? found = NativeBattleEffect312Contacts.Find(bank, new BattleActionHeaderReference(0), ExtraEnemy);

        Assert.Equal(256, found);
    }

    [Fact]
    public void Effect312LateThrowPublishesContactAcrossThePhysicalEnd()
    {
        const uint poolAddress = 0x223E1A8;
        const uint taskAddress = 0x223E1B8;
        NativeBattleActionPacketsState bank = Bank(Enumerable.Repeat((byte)1, 48), new byte[] { 1, 1 });
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(poolAddress, taskAddress, 36, 100);
        (pool, int slot) = pool.Append(0x5A0BF0);
        BattleModelOwner owner = OriginalOwner(1);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 28);
        task.WriteExecutionReference(14, 2, new BattleExecutionReference.Model(owner), bank.ModelAliases.EncodeOperand(owner, 2));
        BattleExecutionReference.ContactOffset offset = new(1);
        task.WriteExecutionReference(26, 2, offset, offset.EncodeOriginalWord());
        pool = pool.WriteSlot(slot, task);
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, 0x59CB90, 0x59CB70), new(OriginalOwner(3), 0, BattleModelTargets.From([OriginalOwner(0)]), new BattleActionHeaderReference(1), 0, 0), null) { Pools = [pool] };
        effect = effect with { Effect312SavedTransforms = effect.Effect312SavedTransforms.Add(owner, NativeBattleEffectArena.Clear()) };
        NativeBattleEffect312Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(83), NativeBattleEffect312State.FromColdProcess().DetachBank(1));
        List<BattleContactOccurrence> applied = [];
        NativeBattleEffect312Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, _ => bank, _ => Models, (_, _, _) => throw new InvalidOperationException(),
            (_, _) => NativeBattleEffectArena.Clear(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(),
            (_, _, _) => throw new InvalidOperationException(), (_, _) => 33, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, operation) =>
        {
            if (operation is NativeBattleEffect312Contact506690 contact)
            {
                applied.Add(contact.Occurrence);
            }
            return current;
        });

        NativeBattleEffect312Step result = NativeBattleEffect312Late.Advance(frame, taskAddress, null!, inputs);

        Assert.Equal(2, result.Result);
        Assert.Equal(new BattleContactOccurrence(49), Assert.Single(applied));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void TypedTraversalRejectsTheWrongUnpreparedHeader()
    {
        NativeBattleActionPacketsState bank = Bank(new byte[] { 8 });
        NativeBattleWaterExecutionFrame frame = Frame(bank, 0);
        NativeBattleWaterContextServices services = Services(bank, []) with { ReadCurrentHeader = _ => new BattleActionHeaderReference(1) };
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicNextContact12E.Execute(frame, services));
    }

    private static NativeBattleWaterExecutionFrame Frame(NativeBattleActionPacketsState bank, int contactsProcessed)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, Manager);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, NativeBattleScriptedEffectMemory.Address);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(Models).WithContactsProcessed(contactsProcessed) } };
    }

    private static NativeBattleActionPacketsState Bank(params IEnumerable<byte>[] groups)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(Models.Roster, NativeBattleRetainedActionPackets.Fresh(), Original.NativeBindings.CaptureAliases(Models));
        foreach (IEnumerable<byte> group in groups)
        {
            byte[] recipients = group.ToArray();
            (packets, _) = packets.Prepare(new(OriginalOwner(0), 2, 5, -1, 6, 7, null));
            packets = packets.AppendContacts(recipients.Select(actor => new NativeBattleContactPacket(new(Owner(actor), 0, 0, 4, 0, 0, 0), null)));
            (packets, _) = packets.Publish(recipients.Length);
        }
        return packets;
    }

    private static BattleModelOwner Owner(byte fixtureMember)
    {
        return fixtureMember switch
        {
            7 => ExtraAlly,
            8 => ExtraEnemy,
            _ => OriginalOwner(fixtureMember)
        };
    }

    private static NativeBattleWaterContextServices Services(NativeBattleActionPacketsState bank, List<BattleContactOccurrence> applied)
    {
        return new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (frame, occurrence) =>
        {
            applied.Add(occurrence);
            return frame;
        }, _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => new BattleActionHeaderReference(0), () => bank, () => bank.ModelAliases, BattleContactTaskStorageContract.ExpandableArena);
    }
}
