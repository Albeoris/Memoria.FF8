using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedBrothersTargetCommandTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private static readonly NativeBattleWaterScriptProgram Program = new(0, 4, 0x1000);

    [Fact]
    public void HostedCorrectionKeepsOrderedListAndRefreshesOnlyRowThreeFields()
    {
        byte[] selected = [7, 24, 7, 8, 9, 10, 11];
        NativeBattleWaterExecutionFrame frame = Fixture([.. selected]);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        NativeBattleGraphicsModel model = new(3, 0xA133) { Transform = new(default, new(-17, 51, -290), 999, 0, 0), Rotation14 = 124 };
        byte flag = 0;

        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, new(63, () => flag, value => flag = value, _ => model));

        NativeBattleCinematicActorContext actual = result.Core.PresentationActors!;
        NativeBattleCinematicActorCache row3 = before.ReadCachedActor(3);
        Assert.Equal(new byte[] { 7, 24, 7, 8, 9, 10, 11, 3 }, actual.CurrentActors.ToArray());
        Assert.Equal((byte)7, actual.FirstActor);
        Assert.Equal(before.SavedActors, actual.SavedActors);
        Assert.Equal(before.ActorOrdinals, actual.ActorOrdinals);
        Assert.Equal(256, actual.ContactCursor);
        Assert.Equal(before.CachedActors.SetItem(3, row3 with { ModelAddress = NativeBattleGraphicsModelAddress.Encode(3), Position28 = model.Transform!.Position28, Flags0 = model.Flags0 }).ToArray(), actual.CachedActors.ToArray());
        Assert.Equal((byte)1, flag);
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal((byte)8, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(selected[..6], result.Core.Read(Header + 0x48, 6));
        Assert.Equal(new byte[] { 0xED, 0xFE }, result.Core.Read(Header + 0x4E, 2));
        Assert.Equal(new byte[] { 0xA2, 0xA3 }, result.Core.Read(Header + 0xA2, 2));
        Assert.Equal(new byte[] { 9, 6 }, result.Core.Read(Header + 0x174, 2));
        Assert.Equal(0x1D97494u, WaterSchedulerFixture.ReadCoreDword(result, Header + 0x6C));
        Assert.Equal(model.Flags0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(Header + 0x54, 2)));
        Assert.Equal(new short[] { -17, 51, -290 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(Header + 0x9Cu + 2u * (uint)axis, 2))));

        NativeBattleWaterExecutionFrame child = WaterSchedulerFixture.SlotWord(result.WriteTemporaryWord(0x14A, 0x10D5), 0, 0x1B, 3);
        child = WaterSchedulerFixture.CoreDword(child, 0x2797450, 0x1000);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(child))!;
        NativeBattleBrothersTargetServices mode1 = new(63, () => flag, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame next = NativeBattleBrothersTargetCommand.Execute(child, Program, mode1);
        Assert.Equal(0x1006u, Pc(next));
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleBrothersTargetCommand.Execute(restored, Program, mode1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingRowThreeClearsFlagWithoutChangingContextOrHeader(bool emptyCurrent)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(emptyCurrent ? [] : [24, 3, 7], emptyCurrent ? (byte)3 : (byte)24);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        byte[] raw = frame.Core.Read(Header, NativeBattleWaterHeader.Length);
        byte flag = 1;

        NativeBattleWaterExecutionFrame result = NativeBattleBrothersTargetCommand.Execute(frame, Program, new(63, () => flag, value => flag = value, _ => throw new InvalidOperationException()));

        Assert.Equal((byte)0, flag);
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(before, result.Core.PresentationActors);
        Assert.Equal(raw, result.Core.Read(Header, NativeBattleWaterHeader.Length));
    }

    [Fact]
    public void InvalidModelRowFailsBeforePublishingFlagOrContext()
    {
        NativeBattleWaterExecutionFrame frame = Fixture([7, 24]);
        NativeBattleCinematicActorContext before = frame.Core.PresentationActors!;
        Assert.Throws<InvalidDataException>(() => NativeBattleBrothersTargetCommand.Execute(frame, Program, new(63, () => 0, _ => throw new InvalidOperationException(), _ => new(4, 3))));
        Assert.Same(before, frame.Core.PresentationActors);
        Assert.Equal(0x1000u, Pc(frame));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ImmutableArray<byte> selected, byte first = 7)
    {
        BattleModelRoster roster = new([.. Enumerable.Range(0, 25).Select(row => new BattleModelOwner($"actor-{row}", row < 3))]);
        ImmutableArray<NativeBattleGraphicsModel> models = [.. Enumerable.Range(0, 25).Select(row => new NativeBattleGraphicsModel(row, 3) { Rotation14 = -99, Transform = new(default, new(1, 2, 3), -10, 0, 0) { Scale154 = 1024 } })];
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(roster, models).CaptureCurrentContacts([7, 24], first).WithCurrentActors(selected).WithSavedActors([9, 6]).WithContactCursor(256);
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

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
