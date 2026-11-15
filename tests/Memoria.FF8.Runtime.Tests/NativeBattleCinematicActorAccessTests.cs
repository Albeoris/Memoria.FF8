using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleCinematicActorAccess;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorAccessTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;

    private static readonly BattleModelOwner FirstEnemy = new BattleEnemyActorModel(new("enemy-zero"));
    private static readonly BattleModelOwner Ally = new BattlePartyActorModel(new("ally-seven"));
    private static readonly BattleModelOwner LastEnemy = new BattleEnemyActorModel(new("enemy-last"));

    [Fact]
    public void CachePublishesTypedPositionRotationScaleAndOrdinals()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        Assert.Equal((ushort)0x0102, ReadCachedFlags0(frame, LastEnemy));
        Assert.Equal(new NativeBattleVector(100, -200, 300), ReadPosition(frame, LastEnemy));
        Assert.Equal((short)-400, ReadPositionWord(frame, LastEnemy, 3));
        Assert.Equal(new NativeBattleVector(20, -30, 40), ReadRotation(frame, LastEnemy));
        Assert.Equal(new NativeBattleVector(1024, -2048, 4096), ReadScale(frame, LastEnemy));
        Assert.Equal((short)700, ReadScaleWord(frame, LastEnemy, 3));
        Assert.Equal(0, ReadActorOrdinal(frame, LastEnemy));
        Assert.Throws<InvalidDataException>(() => ReadCachedFlags0(frame, new BattleEnemyActorModel(new("retired"))));
        Assert.Throws<NotSupportedException>(() => ReadPositionWord(frame, Ally, 3));
        Assert.Throws<NotSupportedException>(() => ReadRotationWord(frame, Ally, 0));
        Assert.Throws<NotSupportedException>(() => ReadRotationWord(frame, LastEnemy, 3));
        Assert.Throws<NotSupportedException>(() => ReadScaleWord(frame, Ally, 3));
        Assert.Throws<NotSupportedException>(() => ReadActorOrdinal(frame, FirstEnemy));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadScaleWord(frame, LastEnemy, 4));
    }

    [Fact]
    public void OrderedListsIgnoreRetainedCountBytesAndPreserveRepeatedOwners()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        Assert.Throws<NotSupportedException>(() => ReadSavedCount(frame));
        Assert.Throws<NotSupportedException>(() => ReadSavedActor(frame, 0));
        ImmutableArray<BattleModelOwner> current = [.. Enumerable.Repeat(LastEnemy, 258), Ally, FirstEnemy, LastEnemy];
        ImmutableArray<BattleModelOwner> saved = [Ally, LastEnemy, .. Enumerable.Repeat(Ally, 258)];
        NativeBattleCinematicActorContext context = frame.Core.PresentationActors!.WithCurrentActors(current).WithSavedActors(saved);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [0xFE]).Write(Header + 0x17A, [0x80]) with { PresentationActors = context } };
        Assert.Equal(current.Length, ReadCurrentCount(frame));
        Assert.Equal(saved.Length, ReadSavedCount(frame));
        Assert.Equal(current.ToArray(), Enumerable.Range(0, current.Length).Select(index => ReadCurrentActor(frame, index)));
        Assert.Equal(saved.ToArray(), Enumerable.Range(0, saved.Length).Select(index => ReadSavedActor(frame, index)));
        frame = frame with { Core = frame.Core with { PresentationActors = context.WithCurrentActors([]).WithSavedActors([]) } };
        Assert.Equal(0, ReadCurrentCount(frame));
        Assert.Equal(LastEnemy, ReadCurrentActor(frame, 0));
        Assert.Equal(0, ReadSavedCount(frame));
        Assert.Throws<InvalidDataException>(() => ReadCurrentActor(frame, 1));
        Assert.Throws<InvalidDataException>(() => ReadSavedActor(frame, 0));
    }

    [Fact]
    public void ReaderRejectsAHeaderPointerToAnotherCoreRecord()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(HostedFrame(), 0x279744C, Header + 0x17C);
        Assert.Throws<InvalidDataException>(() => ReadCachedFlags0(frame, LastEnemy));
        Assert.Throws<InvalidDataException>(() => ReadCurrentCount(frame));
    }

    [Fact]
    public void ScaledPlacementUsesTheSelectedOwnersCachedScaleAndRotation()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        frame = WriteSlotActor(frame, LastEnemy, new NativeBattleModelAliases(Models().Roster, []));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        frame = frame with { Arena = frame.Arena.Write(0x3000, [128, 0]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 101);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)-202));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 303);
        NativeBattleGraphicsModel model = Models().Get(LastEnemy) with { Rotation14 = 500, Rotation16 = 600 };
        int writes = 0;
        _ = NativeBattleCinematicActorPlacement.DrawScaled65(frame, address =>
        {
            Assert.Equal(LastEnemy, address);
            return model;
        }, (address, updated) =>
        {
            Assert.Equal(LastEnemy, address);
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
    public void ApocalypseTintSelectsTheOppositeSideOfTheFirstOwner()
    {
        NativeBattleWaterExecutionFrame frame = HostedFrame();
        NativeBattleCinematicActorContext context = frame.Core.PresentationActors!.WithCurrentActors([Ally]);
        frame = frame with { Core = frame.Core with { PresentationActors = context } };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0x80);
        int calls = 0;
        NativeBattleApocalypse221ActorTint69.Execute(frame, (current, side, combined, color) =>
        {
            Assert.Equal(BattleSide.Enemies, side);
            Assert.Equal(0x80, combined);
            Assert.Equal(0x02000080u, color);
            calls++;
            return current;
        });
        Assert.Equal(1, calls);
    }

    private static NativeBattleWaterExecutionFrame HostedFrame()
    {
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(Models()).CaptureCurrentContacts([LastEnemy, Ally], FirstEnemy);
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x279744C, Header);
        return frame with { Core = frame.Core with { PresentationActors = context } };
    }

    private static BattleActorModels Models()
    {
        NativeBattleGraphicsModel last = new(0x0102)
        {
            Rotation12 = 20,
            Rotation14 = -30,
            Rotation16 = 40,
            Transform = new(default, new(100, -200, 300), -400, 0, 0) { Scale148 = new(1024, -2048, 4096), Scale154 = 700 }
        };
        return new([new(FirstEnemy, new(2)), new(Ally, new(0)), new(LastEnemy, last)]);
    }
}
