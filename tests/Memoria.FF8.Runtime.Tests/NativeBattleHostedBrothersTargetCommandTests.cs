using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedBrothersTargetCommandTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private static readonly NativeBattleWaterScriptProgram Program = new(0, 4, 0x1000);

    private static readonly BattleModelOwner AuthoredModel = OriginalOwner(3);
    private static readonly BattleModelOwner First = new BattleEnemyActorModel(new("first"));
    private static readonly BattleModelOwner Second = new BattleEnemyActorModel(new("second"));
    private static readonly BattleModelOwner[] OrderedActors = [First, Second, First, OriginalOwner(0), OriginalOwner(1), OriginalOwner(2), OriginalOwner(4)];
    private static readonly BattleModelRoster Roster = new(OriginalRoster.Owners.Concat(new[] { First, Second }));
    private static readonly NativeBattleModelAliases Aliases = new(Roster, Enumerable.Range(0, 7).Select(slot => (NativeBattleModelSlot.Decode((byte)slot), OriginalOwner(slot))));

    [Fact]
    public void HostedCorrectionKeepsOrderedListAndRefreshesOnlyRowThreeFields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture([.. OrderedActors]);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        NativeBattleGraphicsModel model = new(0xA133) { Transform = new(default, new(-17, 51, -290), 999, 0, 0), Rotation14 = 124 };
        byte flag = 0;

        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(() => flag, value => flag = value, () => AuthoredModel, _ => model));

        NativeBattleCinematicActorContext actual = result.Core.PresentationActors!;
        NativeBattleCinematicActorCache row3 = before.ReadCachedActor(OriginalOwner(3));
        Assert.Equal(OrderedActors.Append(AuthoredModel), actual.CurrentActors);
        Assert.Equal(First, actual.FirstActor);
        Assert.Equal(before.SavedActors, actual.SavedActors);
        Assert.Equal(before.ActorOrdinals, actual.ActorOrdinals);
        Assert.Equal(256, actual.ContactsProcessed);
        Assert.Equal(before.CachedActors.SetItem(AuthoredModel, row3 with { Position28 = model.Transform!.Position28, Flags0 = model.Flags0 }).ToArray(), actual.CachedActors.ToArray());
        Assert.Equal((byte)1, flag);
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal((byte)8, result.Core.Read(Header + 0x41, 1)[0]);
        (byte[] retained, bool[] known) = result.Core.ReadSnapshot(Header + 0x48, 6);
        Assert.Equal(new[] { false, false, false, true, true, true }, known);
        Assert.Equal(new byte[] { 0, 1, 2 }, retained[3..]);
        Assert.Equal(new byte[] { 0xED, 0xFE }, result.Core.Read(Header + 0x4E, 2));
        Assert.Equal(new byte[] { 0xA2, 0xA3 }, result.Core.Read(Header + 0xA2, 2));
        Assert.Equal(new byte[] { 9, 6 }, result.Core.Read(Header + 0x174, 2));
        Assert.Equal(0x1D97494u, WaterSchedulerFixture.ReadCoreDword(result, Header + 0x6C));
        Assert.Equal(model.Flags0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(Header + 0x54, 2)));
        Assert.Equal(new short[] { -17, 51, -290 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(Header + 0x9Cu + 2u * (uint)axis, 2))));

        NativeBattleWaterExecutionFrame child = NativeBattleCinematicActorAccess.WriteSlotActor(result.WriteTemporaryWord(0x14A, 0x10D5), AuthoredModel, Aliases);
        child = WaterSchedulerFixture.CoreDword(child, 0x2797450, 0x1000);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.Independent(child);
        NativeBattleBrothersTargetServices mode1 = Services(() => flag, _ => throw new InvalidOperationException(), () => AuthoredModel, _ => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame next = NativeBattleBrothersTargetCommand.Execute(child, Program, mode1);
        Assert.Equal(0x1006u, Pc(next));
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleBrothersTargetCommand.Execute(restored, Program, mode1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingRowThreeClearsFlagWithoutChangingContextOrHeader(bool emptyCurrent)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(emptyCurrent ? [] : [Second, AuthoredModel, First], emptyCurrent ? AuthoredModel : Second);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        byte[] raw = frame.Core.Read(Header, NativeBattleWaterHeader.Length);
        byte flag = 1;

        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(() => flag, value => flag = value, () => AuthoredModel, _ => throw new InvalidOperationException()));

        Assert.Equal((byte)0, flag);
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(before, result.Core.PresentationActors);
        Assert.Equal(raw, result.Core.Read(Header, NativeBattleWaterHeader.Length));
    }

    [Fact]
    public void UnboundAuthoredModelFailsBeforePublishingFlagOrContext()
    {
        NativeBattleWaterExecutionFrame frame = Fixture([First, Second]);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        Assert.Throws<InvalidDataException>(() => NativeBattleBrothersTargetCommand.Execute(frame, Program, Services(() => 0, _ => throw new InvalidOperationException(), () => new NativeBattleModelAliases(new([First, Second]), []).DecodeLiteral(3), _ => throw new InvalidOperationException())));
        Assert.Same(before, frame.Core.PresentationActors);
        Assert.Equal(0x1000u, Pc(frame));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ImmutableArray<BattleModelOwner> selected, BattleModelOwner? first = null)
    {
        first ??= First;
        BattleActorModels models = new(Roster.Owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new(3) { Rotation14 = -99, Transform = new(default, new(1, 2, 3), -10, 0, 0) { Scale154 = 1024 } })));
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models)
            .CaptureCurrentContacts([First, Second], first).WithCurrentActors(selected).WithSavedActors([OriginalOwner(4), OriginalOwner(6)]).WithContactsProcessed(256);
        if (selected.IsEmpty)
        {
            context = context with { FirstActor = first };
        }
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000).WriteTemporaryWord(0x14A, 0x00D5);
        return frame with
        {
            Arena = frame.Arena.Write(2, [6, 0]),
            Core = frame.Core.Write(Header + 0x4E, [0xED, 0xFE]).Write(Header + 0xA2, [0xA2, 0xA3]).Write(Header + 0x174, [9, 6]) with { PresentationActors = context }
        };
    }

    private static NativeBattleBrothersTargetServices Services(Func<byte> readFlag, Action<byte> writeFlag, Func<BattleModelOwner> resolveModel, Func<BattleModelOwner, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleWaterContextServices context = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(),
            (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
            (_, _, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException(), () => Aliases, BattleContactTaskStorageContract.ExpandableArena);
        return new(63, readFlag, writeFlag, resolveModel, readModel, context);
    }

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
