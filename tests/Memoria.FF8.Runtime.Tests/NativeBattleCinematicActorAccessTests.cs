using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleCinematicActorAccess;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorAccessTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    [Fact]
    public void NativeReadersFollowTheActualHeaderPointerAndRetainedWordKnownness()
    {
        const uint relocated = Header + 0x900;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x279744C, relocated);
        frame = WaterSchedulerFixture.CoreDword(frame, relocated + 0x60 + 4, 0x12345678);
        frame = WaterSchedulerFixture.CoreWord(frame, relocated + 0x4E + 2, 0xF001);
        frame = frame with
        {
            Core = frame.Core.Write(relocated + 0x84 + 8, [1, 0, 0xFE, 0xFF, 3, 0, 0xFC, 0xFF])
                .Write(relocated + 0xD8 + 8, [5, 0, 6, 0, 7, 0, 8, 0])
                .Write(relocated + 0x120 + 8, [9, 0, 10, 0, 11, 0, 12, 0])
                .Write(relocated + 0xCC + 1, [0xFE])
                .MarkUnknown(relocated + 0xD8 + 8 + 4, 1)
        };
        Assert.Equal(7, ActorCapacity(frame));
        Assert.Equal(0x12345678u, ReadModelPointer(frame, 1));
        Assert.Equal((ushort)0xF001, ReadCachedFlags0(frame, 1));
        Assert.Equal(new NativeBattleVector(1, -2, 3), ReadPosition(frame, 1));
        Assert.Equal((short)-4, ReadPositionWord(frame, 1, 3));
        Assert.Equal((short)6, ReadRotationWord(frame, 1, 1));
        Assert.Throws<NotSupportedException>(() => ReadRotation(frame, 1));
        Assert.Equal(new NativeBattleVector(9, 10, 11), ReadScale(frame, 1));
        Assert.Equal((short)12, ReadScaleWord(frame, 1, 3));
        Assert.Equal((byte)0xFE, ReadActorOrdinal(frame, 1));
    }

    [Fact]
    public void HostedCacheRetainsRowTwentyFourAndRejectsHolesAndUnknownLanes()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        Assert.Equal(25, ActorCapacity(frame));
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), ReadModelPointer(frame, 24));
        Assert.Equal((ushort)0x0102, ReadCachedFlags0(frame, 24));
        Assert.Equal(new NativeBattleVector(100, -200, 300), ReadPosition(frame, 24));
        Assert.Equal((short)-400, ReadPositionWord(frame, 24, 3));
        Assert.Equal(new NativeBattleVector(20, -30, 40), ReadRotation(frame, 24));
        Assert.Equal(new NativeBattleVector(1024, -2048, 4096), ReadScale(frame, 24));
        Assert.Equal((short)700, ReadScaleWord(frame, 24, 3));
        Assert.Equal((byte)0, ReadActorOrdinal(frame, 24));
        Assert.Throws<InvalidDataException>(() => ReadModelPointer(frame, 6));
        Assert.Throws<InvalidDataException>(() => ReadModelPointer(frame, 25));
        Assert.Throws<NotSupportedException>(() => ReadPositionWord(frame, 7, 3));
        Assert.Throws<NotSupportedException>(() => ReadRotationWord(frame, 7, 0));
        Assert.Throws<NotSupportedException>(() => ReadRotationWord(frame, 24, 3));
        Assert.Throws<NotSupportedException>(() => ReadScaleWord(frame, 7, 3));
        Assert.Throws<NotSupportedException>(() => ReadActorOrdinal(frame, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadScaleWord(frame, 24, 4));
    }

    [Fact]
    public void NativeSignedCountsAndHostedOrderedListsKeepTheirOwnInterpretations()
    {
        NativeBattleWaterExecutionFrame native = WaterSchedulerFixture.Create();
        native = native with { Core = native.Core.Write(Header + 0x41, [0xFE]).Write(Header + 0x48, [0xFF]).Write(Header + 0x174, [0xFD]).Write(Header + 0x17A, [0x80]) };
        Assert.Equal(-2, ReadCurrentCount(native, signedNative: true));
        Assert.Equal(254, ReadCurrentCount(native));
        Assert.Equal(-128, ReadSavedCount(native, signedNative: true));
        Assert.Equal(128, ReadSavedCount(native, signedNative: false));
        Assert.Equal((byte)255, ReadCurrentActor(native, 0));
        Assert.Equal((byte)253, ReadSavedActor(native, 0));

        NativeBattleWaterExecutionFrame frame = HostedFrame();
        Assert.Throws<NotSupportedException>(() => ReadSavedCount(frame, signedNative: true));
        Assert.Throws<NotSupportedException>(() => ReadSavedActor(frame, 0));
        ImmutableArray<byte> current = [.. Enumerable.Repeat((byte)24, 128), 7, 0, 24];
        ImmutableArray<byte> saved = [7, 24, .. Enumerable.Repeat((byte)7, 128)];
        NativeBattleCinematicActorContext context = frame.Core.PresentationActors!.WithCurrentActors(current).WithSavedActors(saved);
        frame = frame with { Core = frame.Core with { PresentationActors = context } };
        Assert.Equal(current.Length, ReadCurrentCount(frame, signedNative: true));
        Assert.Equal(saved.Length, ReadSavedCount(frame, signedNative: true));
        Assert.Equal(current.ToArray(), Enumerable.Range(0, current.Length).Select(index => ReadCurrentActor(frame, index)));
        Assert.Equal(saved.ToArray(), Enumerable.Range(0, saved.Length).Select(index => ReadSavedActor(frame, index)));
        frame = frame with { Core = frame.Core with { PresentationActors = context.WithCurrentActors([]).WithSavedActors([]) } };
        Assert.Equal(0, ReadCurrentCount(frame));
        Assert.Equal((byte)24, ReadCurrentActor(frame, 0));
        Assert.Equal(0, ReadSavedCount(frame, signedNative: false));
        Assert.Throws<InvalidDataException>(() => ReadCurrentActor(frame, 1));
        Assert.Throws<InvalidDataException>(() => ReadSavedActor(frame, 0));
    }

    [Fact]
    public void HostedReadersRejectAHeaderPointerToAnotherCoreRecord()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(HostedFrame(), 0x279744C, Header + 0x17C);
        Assert.Throws<InvalidDataException>(() => ActorCapacity(frame));
        Assert.Throws<InvalidDataException>(() => ReadModelPointer(frame, 24));
        Assert.Throws<InvalidDataException>(() => ReadCurrentCount(frame));
    }

    [Fact]
    public void ScaledPlacementUsesHostedCachedScaleAndRotationForRowTwentyFour()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x1B, 24);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        frame = frame with { Arena = frame.Arena.Write(0x3000, [128, 0]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 101);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)-202));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 303);
        NativeBattleGraphicsModel model = Models()[24] with { Rotation14 = 500, Rotation16 = 600 };
        uint pointer = NativeBattleGraphicsModelAddress.Encode(24);
        int writes = 0;
        _ = NativeBattleCinematicActorPlacement.DrawScaled65(frame, address =>
        {
            Assert.Equal(pointer, address);
            return model;
        }, (address, updated) =>
        {
            Assert.Equal(pointer, address);
            writes++;
            model = updated;
        });
        Assert.Equal(4, writes);
        Assert.Equal(new NativeBattleVector(101, -202, 303), model.Transform!.Position28);
        Assert.Equal(new NativeBattleVector(512, -1024, 4096), model.Transform.Scale148);
        Assert.Equal((short)0, model.Rotation12);
        Assert.Equal((short)2, model.Rotation14);
        Assert.Equal((short)88, model.Rotation16);
    }

    [Fact]
    public void ApocalypseTintUsesExplicitOpponentSideAndSkipsUnboundRows()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        NativeBattleCinematicActorContext context = frame.Core.PresentationActors!.WithCurrentActors([7]);
        frame = frame with { Core = frame.Core with { PresentationActors = context } };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0x80);
        List<(int Row, ushort Flags, uint? Color)> writes = [];
        NativeBattleApocalypse221ActorTint69.Execute(frame, new((_, address) =>
        {
            int index = NativeBattleGraphicsModelAddress.Decode(address, 25);
            return Models()[index];
        }, (owner, address, actor) =>
        {
            Assert.Equal(NativeBattleGraphicsModelAddress.Encode(actor.Index), address);
            writes.Add((actor.Index, actor.Flags0, actor.Color40));
            return owner;
        }));
        Assert.Equal(new (int, ushort, uint?)[] { (0, 0x0802, 0x02000080), (24, 0x0902, 0x02000080) }, writes);
    }

    private static NativeBattleWaterExecutionFrame HostedFrame()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        rows[0] = new("enemy-zero", false);
        rows[7] = new("ally-seven", true);
        rows[24] = new("enemy-last", false);
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(new([.. rows]), Models()).CaptureCurrentContacts([24, 7], 0);
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        return frame with { Core = frame.Core with { PresentationActors = context } };
    }

    private static ImmutableArray<NativeBattleGraphicsModel> Models()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = [.. Enumerable.Range(0, 25).Select(row => new NativeBattleGraphicsModel(row, 0))];
        NativeBattleGraphicsModel last = new(24, 0x0102)
        {
            Rotation12 = 20,
            Rotation14 = -30,
            Rotation16 = 40,
            Transform = new(default, new(100, -200, 300), -400, 0, 0) { Scale148 = new(1024, -2048, 4096), Scale154 = 700 }
        };
        return models.SetItem(0, new(0, 2)).SetItem(24, last);
    }
}
