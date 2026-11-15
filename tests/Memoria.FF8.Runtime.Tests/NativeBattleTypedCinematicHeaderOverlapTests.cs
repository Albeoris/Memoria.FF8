using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedCinematicHeaderOverlapTests
{
    [Theory]
    [InlineData(BattleContactTaskStorageContract.OriginalArena, 0x1206, 6)]
    [InlineData(BattleContactTaskStorageContract.ExpandableArena, 0x1234, 7)]
    public void SeventhOriginalActorReconcilesTheCachedFlagsAndSavedList(BattleContactTaskStorageContract contract, ushort expectedFlags, int expectedSavedCount)
    {
        ImmutableArray<BattleModelOwner> owners = Enumerable.Range(0, 7).Select(static index => index < 3
            ? (BattleModelOwner)new BattlePartyActorModel(new($"party-{index}"))
            : new BattleEnemyActorModel(new($"enemy-{index}"))).ToImmutableArray();
        NativeBattleModelBoundary boundary = new(Enumerable.Range(0, 7).Select(static index => new NativeBattleGraphicsModel((ushort)(0x1234 + index))));
        BattleActorModels models = new([]);
        for (byte originalSlot = 0; originalSlot < 7; originalSlot++)
        {
            (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(originalSlot), owners[originalSlot], models);
        }
        NativeBattleWaterSlotBank bank = new(0x1000, 0x1800, 8, 0x2000);
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x10CF);
        NativeBattleWaterHeaderCaptureResult capture = NativeBattleWaterHeaderCapture.CaptureAA4E90(frame.Core.ReadHeader(), frame.Arena, bank, 0, owners[1], owners[0], owners, models, boundary, contract);
        NativeBattleWaterHeader cached = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(capture.Header, models, boundary);
        frame = frame with { Arena = capture.Arena, Core = frame.Core.WithHeader(cached) };

        Assert.Equal(expectedFlags, BinaryPrimitives.ReadUInt16LittleEndian(cached.Read(0x4E, 2).AsSpan()));
        Assert.Equal(expectedFlags, NativeBattleCinematicActorAccess.ReadCachedFlags0(frame, owners[0]));

        NativeBattleWaterContextServices services = new(
            (_, owner) => owner == owners[0] ? owners[1] : null,
            (_, owner) => owner,
            (_, _, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            (_, _, _, _) => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(),
            () => boundary.CaptureAliases(models),
            contract);
        frame = NativeBattleWaterContextCommands.ExecuteRing(frame, services);

        Assert.Equal(expectedSavedCount, frame.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x17A, 1)[0]);
        Assert.Equal(expectedSavedCount, NativeBattleCinematicActorAccess.ReadSavedCount(frame));
        Assert.Equal(owners.Take(expectedSavedCount), frame.Core.PresentationActors!.ReadSavedActors());
        Assert.Equal(owners[0], NativeBattleCinematicActorAccess.ReadSlotActor(frame));
    }
}
