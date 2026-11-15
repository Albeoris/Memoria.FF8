using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleCinematicActorAccess;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorWriterTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private const uint Pc = 0x20E0AB8;

    [Fact]
    public void ExpandedOrderPreservesRepeatsAndMirrorsSixAuthoredOperands()
    {
        byte[] order = [4, 1, 4, 1, 1, 4, 1, 4];
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(Models()).CaptureCurrentContacts([OriginalOwner(1), OriginalOwner(4)], OriginalOwner(1));
        NativeBattleWaterExecutionFrame frame = Frame(0x809A, OriginalOwner(1), context, OriginalAliases);
        frame = frame with
        {
            Core = frame.Core.Write(Header + 0x4E, [0xB4, 0xFA]),
            Arena = frame.Arena.Write(0x1000, [0x9A, 0x80, .. order.SelectMany(operand => new byte[] { operand, 0xFF })])
        };
        NativeBattleWaterContextServices services = Services(OriginalAliases, BattleContactTaskStorageContract.ExpandableArena);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorOrder9A.Execute(frame, new(0x1000, 18, Pc), services);
        Assert.Equal(order.Select(slot => OriginalOwner(slot)), result.Core.PresentationActors!.CurrentActors);
        Assert.Equal(OriginalOwner(4), ReadCurrentActor(result, 0));
        Assert.Equal(1, ReadActorOrdinal(result, OriginalOwner(4)));
        Assert.Equal(new byte[] { 4, 1, 4, 1, 1, 4, 0xB4, 0xFA }, result.Core.Read(Header + 0x48, 8));
        Assert.Equal((byte)8, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(Pc + 18, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));

        result = NativeBattleCinematicActorOrder9A.Execute(result.WriteTemporaryWord(0x14A, 0x9A), new(-1, 0, 0), services);
        Assert.Equal(0, ReadCurrentCount(result));
        Assert.Equal(OriginalOwner(4), ReadCurrentActor(result, 0));
        Assert.Equal((byte)0, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal((byte)4, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal(Pc + 20, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void ExpandedRingRetainsFullSavedOwnersAndOnlyProjectsActualAliases(int mode, bool linked)
    {
        BattleModelOwner first = new BattleEnemyActorModel(new("extra-first"));
        BattleModelOwner second = new BattlePartyActorModel(new("extra-second"));
        BattleActorModels models = Models().Add(first, new(0)).Add(second, new(0));
        NativeBattleModelAliases aliases = new(models.Roster, Enumerable.Range(0, 7).Select(slot => (NativeBattleModelSlot.Decode((byte)slot), OriginalOwner(slot))));
        ImmutableArray<BattleModelOwner> original = [first, second, .. Enumerable.Range(0, 7).Reverse().Select(OriginalOwner)];
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models).CaptureCurrentContacts(original, first).WithContactsProcessed(49);
        NativeBattleWaterExecutionFrame frame = Frame((ushort)(0xCF | mode << 12), first, context, aliases);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [99]).Write(Header + 0x4E, [0xC0, 0xFA]).Write(Header + 0x174, [0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xFE, 0xCD, 0xEF]) };
        int resolves = 0;
        NativeBattleWaterContextServices services = Services(aliases, BattleContactTaskStorageContract.ExpandableArena) with
        {
            ReadLinkedModel = (_, owner) =>
            {
                Assert.Equal(first, owner);
                return linked ? second : null;
            },
            ResolveRing501FF0 = (_, owner) =>
            {
                Assert.Equal(first, owner);
                resolves++;
                return second;
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterContextCommands.ExecuteRing(frame, services);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(result));
        ImmutableArray<BattleModelOwner> expected = !linked ? [] : mode == 0 ? [first] : original;
        Assert.Equal(expected, restored.Core.PresentationActors!.ReadSavedActors());
        Assert.Equal(expected.Length, ReadSavedCount(restored));
        Assert.Equal(linked ? second : first, ReadCurrentActor(restored, 0));
        Assert.Equal(49, restored.Core.PresentationActors.ContactsProcessed);
        Assert.Equal(linked ? 1 : 0, resolves);
        Assert.Equal(linked ? second : first, ReadSlotActor(restored));
        Assert.Equal(linked ? (byte)1 : (byte)99, restored.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(new byte[] { 0xC0, 0xFA }, restored.Core.Read(Header + 0x4E, 2));
        Assert.Equal(new byte[] { (byte)expected.Length, 0xCD, 0xEF }, restored.Core.Read(Header + 0x17A, 3));
        if (linked)
        {
            Assert.Throws<NotSupportedException>(() => restored.Core.Read(Header + 0x174, 1));
        }
        Assert.Equal(Pc + 2, WaterSchedulerFixture.ReadCoreDword(restored, 0x2797450));
    }

    [Fact]
    public void OriginalRingConsumesTheSavedCountAfterTheSeventhOperandOverwritesIt()
    {
        ImmutableArray<BattleModelOwner> original = [.. Enumerable.Range(0, 7).Select(OriginalOwner)];
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(Models()).CaptureCurrentContacts(original, OriginalOwner(0));
        NativeBattleWaterExecutionFrame frame = Frame(0x10CF, OriginalOwner(0), context, OriginalAliases);
        NativeBattleWaterContextServices services = Services(OriginalAliases, BattleContactTaskStorageContract.OriginalArena) with
        {
            ReadLinkedModel = (_, _) => OriginalOwner(1),
            ResolveRing501FF0 = (_, _) => OriginalOwner(1)
        };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterContextCommands.ExecuteRing(frame, services);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, result.Core.Read(Header + 0x174, 7));
        Assert.Equal(6, ReadSavedCount(result));
        Assert.Equal(original.Take(6), result.Core.PresentationActors!.ReadSavedActors());
        Assert.Equal(OriginalOwner(1), ReadSlotActor(result));
        Assert.Equal(Pc + 2, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Frame(ushort instruction, BattleModelOwner actor, NativeBattleCinematicActorContext context, NativeBattleModelAliases aliases)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, Header);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        return WriteSlotActor(frame with { Core = frame.Core with { PresentationActors = context } }, actor, aliases);
    }

    private static BattleActorModels Models() => new(Enumerable.Range(0, 7).Select(slot => new BattleModelEntry<BattleModelOwner>(OriginalOwner(slot), new(0))));

    private static NativeBattleWaterContextServices Services(NativeBattleModelAliases aliases, BattleContactTaskStorageContract storage)
    {
        return new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), _ => null, () => throw new InvalidOperationException(), () => aliases, storage);
    }
}
