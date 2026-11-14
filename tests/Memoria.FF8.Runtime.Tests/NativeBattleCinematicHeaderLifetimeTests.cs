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
        List<(uint Address, ushort Flags)> writes = [];

        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221ActorTint69.Execute(frame, new((_, address) =>
        {
            return new(NativeBattleGraphicsModelAddress.Decode(address), 0);
        }, (current, address, actor) =>
        {
            writes.Add((address, actor.Flags0));
            if (writes.Count == 1)
            {
                current = WaterSchedulerFixture.CoreDword(current, 0x279744C, MovedHeader);
                current = WaterSchedulerFixture.CoreWord(current, Header + 0x4E + 4 * 2, 0x3456);
            }
            return current;
        }));

        Assert.Equal(new (uint, ushort)[] { (0x1D97494, 0x1234), (0x1D97530, 0x3456) }, writes);
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

        NativeBattleWaterExecutionFrame result = NativeBattleCinematicEntityPosition.FromTargetJointAverage6E(frame, new(0x1000, 4, programAddress), (current, actor, selector) =>
        {
            Assert.Equal(0x123, selector);
            visited.Add(actor);
            if (visited.Count == 1)
            {
                current = WaterSchedulerFixture.CoreDword(current, 0x279744C, MovedHeader);
            }
            return new(current, new(actor * 100, 0, 0, 0));
        }, (current, _) => current);

        Assert.Equal(new[] { 3, 5 }, visited);
        Assert.Equal(400u << 16, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(programAddress + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }
}
