using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeCarFuelTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(35)]
    public void ThresholdConsumesOnlyFirstPositivePhysicalFuelSlot(int entityType)
    {
        GameState source = Departure(0x20000, 0, 3, 7);
        NativeWorldSalaryState next = NativeWorldSalaryState.Begin(source).PrepareCarFuel((byte)entityType, 13, 64, Message);

        Assert.Equal(new byte[] { 0, 2, 7 }, next.Economy!.Items.Take(3).Select(item => item.Count).ToArray());
        Assert.Equal(new byte[] { 0, 3, 7 }, source.Economy!.Items.Take(3).Select(item => item.Count).ToArray());
        NativeWorldMessage message = Assert.Single(next.EventUi!.Windows);
        Assert.Equal((byte)254, message.MessageId);
        Assert.Equal((byte)2, message.FuelRemaining);
        Assert.Equal(0u, next.FuelDistance112);
        Assert.Equal(30, next.FuelMessageMovementCallbacks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void LastOrAbsentFuelUsesStaticEmptyMessage(int count)
    {
        NativeWorldSalaryState next = NativeWorldSalaryState.Begin(Departure(0x20000, (byte)count)).PrepareCarFuel(32, 13, 0, Message);

        Assert.Equal((byte)0, next.Economy!.Items[0].Count);
        NativeWorldMessage message = Assert.Single(next.EventUi!.Windows);
        Assert.Equal((byte)43, message.MessageId);
        Assert.Null(message.FuelRemaining);
        Assert.Equal(0u, next.FuelDistance112);
    }

    [Fact]
    public void ConsumptionTestsOldUnsignedDistanceBeforeAddingCurrentVelocity()
    {
        NativeWorldSalaryState before = NativeWorldSalaryState.Begin(Departure(0x1FFFF, 3));
        NativeWorldSalaryState crossed = before.PrepareCarFuel(32, 8, 64);

        Assert.Equal(0x2007Fu, crossed.FuelDistance112);
        Assert.Equal((byte)3, crossed.Economy!.Items[0].Count);
        Assert.Empty(crossed.EventUi!.Windows);
        NativeWorldSalaryState consumed = crossed.PrepareCarFuel(32, 8, 0, Message);
        Assert.Equal(0u, consumed.FuelDistance112);
        Assert.Equal((byte)2, consumed.Economy!.Items[0].Count);

        NativeWorldSalaryState highBit = NativeWorldSalaryState.Begin(Departure(0x80000000, 3)).PrepareCarFuel(35, 13, 0, Message);
        Assert.Equal(0u, highBit.FuelDistance112);
        Assert.Equal((byte)2, highBit.Economy!.Items[0].Count);
        NativeWorldSalaryState empty = NativeWorldSalaryState.Begin(Departure(42, 0)).PrepareCarFuel(32, 8, 64);
        Assert.Equal(42u, empty.FuelDistance112);
    }

    [Theory]
    [InlineData(33, 0)]
    [InlineData(32, 12)]
    [InlineData(35, 14)]
    public void ExcludedEntityAndScriptModesDoNotConsumeOrAccumulate(int entityType, int mode)
    {
        GameState source = Departure(0x20000, 3);
        source = source with { World = source.World! with { EventUi = NativeWorldEventUi.Create().WithScriptMode((byte)mode) } };
        NativeWorldSalaryState next = NativeWorldSalaryState.Begin(source).PrepareCarFuel((byte)entityType, 8, 64, _ => throw new InvalidOperationException("Excluded producer"));

        Assert.Equal(0x20000u, next.FuelDistance112);
        Assert.Same(source.Economy, next.Economy);
        Assert.Empty(next.EventUi!.Windows);
    }

    [Fact]
    public void MessageLifetimeCountsMovingCallbacksAndClosesOnFollowingCallback()
    {
        NativeWorldSalaryState next = NativeWorldSalaryState.Begin(Departure(0x20000, 3)).PrepareCarFuel(32, 13, 0, Message);
        for (int index = 0; index < 30; index++)
        {
            next = next.PrepareCarFuel(32, 13, 0);
            Assert.Equal(30 - index, next.FuelMessageMovementCallbacks);
            Assert.Single(next.EventUi!.Windows);
            next = next.PrepareCarFuel(32, 13, 1);
            Assert.Equal(29 - index, next.FuelMessageMovementCallbacks);
            Assert.Single(next.EventUi!.Windows);
        }
        next = next.PrepareCarFuel(32, 13, 0);
        Assert.Empty(next.EventUi!.Windows);
        Assert.Equal(0, next.FuelMessageMovementCallbacks);
    }

    [Fact]
    public void FailedMessageProducerLeavesDepartureAndTransactionUntouched()
    {
        GameState source = Departure(0x20000, 3);
        NativeWorldSalaryState before = NativeWorldSalaryState.Begin(source);
        Assert.Throws<InvalidOperationException>(() => before.PrepareCarFuel(32, 13, 64, _ => throw new InvalidOperationException("Unavailable message")));
        Assert.Throws<InvalidDataException>(() => before.PrepareCarFuel(32, 13, 64, remaining => Message(remaining) with { FuelRemaining = 99 }));

        Assert.Equal(0x20000u, before.FuelDistance112);
        Assert.Equal(0, before.FuelMessageMovementCallbacks);
        Assert.Empty(before.EventUi!.Windows);
        Assert.Equal((byte)3, before.Economy!.Items[0].Count);
        Assert.Equal((byte)3, source.Economy!.Items[0].Count);
        Assert.Empty(source.World!.EventUi!.Windows);
    }

    [Fact]
    public void PublicationAndJsonRestoreRetainFuelInventoryWindowAndMovementLifetime()
    {
        GameState source = Departure(0x20000, 3);
        NativeWorldSalaryState next = NativeWorldSalaryState.Begin(source).PrepareCarFuel(32, 13, 0, Message).PrepareCarFuel(32, 13, 1).Advance(null, null, null, 0, 0);
        WorldState final = source.World! with { Frame = 1 };
        GameState published = next.ApplyTo(source with { World = final }, final);
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(published))!;
        NativeWorldSalaryState resumed = NativeWorldSalaryState.Begin(restored);

        Assert.Equal(1u, resumed.FuelDistance112);
        Assert.Equal((byte)2, resumed.Economy!.Items[0].Count);
        Assert.Equal(29, resumed.FuelMessageMovementCallbacks);
        Assert.Equal((byte)2, Assert.Single(resumed.EventUi!.Windows).FuelRemaining);
        Assert.Equal(29, restored.WorldProcess!.FuelMessageMovementCallbacks2045C78);
        Assert.Equal(28, resumed.PrepareCarFuel(32, 13, 1).FuelMessageMovementCallbacks);
        Assert.Equal(0x20000u, source.SavedWorld!.FuelDistance112);
    }

    private static NativeWorldMessage Message(byte remaining)
    {
        return new NativeWorldMessage(1, remaining == 0 ? (byte)43 : (byte)254, 1, 0, 0, 0, 0, new string('0', 64), null) { FuelRemaining = remaining == 0 ? null : remaining };
    }

    private static GameState Departure(uint distance, params byte[] fuelCounts)
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        for (int index = 0; index < fuelCounts.Length; index++)
        {
            items[index] = new NativeItemStock(162, fuelCounts[index]);
        }
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = items, Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        return new GameState { Scene = SceneKind.WorldMap, World = new() { EventUi = NativeWorldEventUi.Create() }, SavedWorld = NativeSavedWorldState.CreateNewGame() with { FuelDistance112 = distance }, Economy = economy, WorldProcess = NativeWorldProcess.FromColdProcess() };
    }
}
