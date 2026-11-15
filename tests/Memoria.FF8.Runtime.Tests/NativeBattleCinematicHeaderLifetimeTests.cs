using static Memoria.FF8.Tests.NativeBattleTypedFixture;
namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicHeaderLifetimeTests
{
    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private const uint MovedHeader = Header + 0x900;

    [Fact]
    public void NativeTintRetainsItsOriginalHeaderWhenTheFirstActorCallbackMovesTheDirectory()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, Header + 0x60 + 3 * 4, 0x1D97494);
        frame = WaterSchedulerFixture.CoreDword(frame, Header + 0x60 + 4 * 4, 0x1D97530);
        frame = WaterSchedulerFixture.CoreWord(frame, Header + 0x4E + 3 * 2, 0x1234);
        frame = WaterSchedulerFixture.CoreWord(frame, Header + 0x4E + 4 * 2, 0x2345);
        frame = WaterSchedulerFixture.CoreDword(frame, MovedHeader + 0x60 + 4 * 4, 0x1D97668);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, ushort.MaxValue);
        frame = NativeBattleCinematicTypedTestFixture.WithContext(frame);
        frame = frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithCurrentActors([OriginalOwner(0)]) } };
        int callbacks = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221ActorTint69.Execute(frame, (current, side, combined, color) =>
        {
            callbacks++;
            Assert.Equal(BattleSide.Enemies, side);
            Assert.Equal(-1, combined);
            Assert.Equal(0u, color);
            Assert.Equal(MovedHeader, WaterSchedulerFixture.ReadCoreDword(WaterSchedulerFixture.CoreDword(current, 0x279744C, MovedHeader), 0x279744C));
            Assert.Equal((ushort)0x1234, NativeBattleApocalypse221ActorTint69.ApplyTint(new(0), combined, color, () => 0x1234).Flags0);
            Assert.Equal((ushort)0x3456, NativeBattleApocalypse221ActorTint69.ApplyTint(new(0), combined, color, () => 0x3456).Flags0);
            return WaterSchedulerFixture.CoreDword(current, 0x279744C, MovedHeader);
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(MovedHeader, WaterSchedulerFixture.ReadCoreDword(result, 0x279744C));
    }

    [Fact]
    public void NativeJointAverageRefreshesItsListAndCountAfterTheAnchorCallbackMovesTheDirectory()
    {
        const uint programAddress = 0x20E0AB8;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x106E);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, programAddress);
        frame = frame with
        {
            Core = frame.Core.Write(Header + 0x41, [1]).Write(Header + 0x48, [3])
                .Write(MovedHeader + 0x41, [2]).Write(MovedHeader + 0x48, [6, 5]),
            Arena = frame.Arena.Write(0x1000, [0x6E, 0x10, 0x23, 0x01])
        };
        List<int> visited = [];
        frame = NativeBattleCinematicTypedTestFixture.WithContext(frame);
        frame = frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithCurrentActors([OriginalOwner(3)]) } };

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.FromTargetJointAverage6E(frame, new(0x1000, 4, programAddress), (current, actor, selector) =>
        {
            Assert.Equal(0x123, selector);
            visited.Add(OriginalSlot(actor));
            if (visited.Count == 1)
            {
                current = current with { Core = current.Core with { PresentationActors = current.Core.PresentationActors!.WithCurrentActors([OriginalOwner(6), OriginalOwner(5)]) } };
            }
            return new(current, new(OriginalSlot(actor) * 100, 0, 0, 0));
        }, (current, _) => current);

        Assert.Equal(new[] { 3, 5 }, visited);
        Assert.Equal(400u << 16, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(programAddress + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }
}
