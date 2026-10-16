using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldEventStateTests
{
    [Fact]
    public void SavedChannelsHaveIndependentUnsignedValuesAndRoundTrip()
    {
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        Assert.Equal((byte)0, initial.ReadWorldEventVariable(0));
        Assert.Equal((byte)0, initial.ReadWorldEventVariable(1));
        NativeSavedWorldState saved = initial.WithWorldEventVariable(0, 200).WithWorldEventVariable(1, 255);
        NativeSavedWorldState restored = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(saved))!;
        Assert.Equal((byte)200, restored.ReadWorldEventVariable(0));
        Assert.Equal((byte)255, restored.ReadWorldEventVariable(1));
        Assert.True(restored.MatchesWorldEventVariable(0xFF2D, 0xC800));
        Assert.False(restored.MatchesWorldEventVariable(0xFF2D, 0xC801));
        Assert.True(restored.MatchesWorldEventVariable(0xFF30, 0xC900));
        Assert.False(restored.MatchesWorldEventVariable(0xFF30, 0xC800));
        Assert.True(restored.MatchesWorldEventVariable(0xFF31, 0xFE01));
        Assert.False(restored.MatchesWorldEventVariable(0xFF31, 0xFF01));
        Assert.Same(restored, restored.WithWorldEventVariable(255, 7));
        Assert.Throws<NotSupportedException>(() => restored.ReadWorldEventVariable(2));
        Assert.Throws<NotSupportedException>(() => restored.ReadWorldEventVariable(255));
    }

    [Fact]
    public void MissingHistoricalChannelRemainsUnknownUntilItsOwnExecutedWrite()
    {
        NativeSavedWorldState historical = NativeSavedWorldState.CreateNewGame() with { WorldEventVariable124 = null, WorldEventVariable125 = null, WorldEventFlags116 = null };
        NativeSavedWorldState restored = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(historical))!;
        Assert.Null(restored.WorldEventVariable124);
        Assert.Null(restored.WorldEventVariable125);
        Assert.Throws<NotSupportedException>(() => restored.ReadWorldEventVariable(0));
        Assert.Throws<NotSupportedException>(() => restored.ReadWorldEventVariable(1));
        NativeSavedWorldState reset = restored.WithOrdinaryWorldFieldEntry();
        Assert.Null(reset.WorldEventVariable124);
        Assert.Equal((byte)0, reset.WorldEventVariable125);
        Assert.Null(reset.WorldEventFlags116);
        NativeSavedWorldState written = restored.WithWorldEventVariable(0, 128);
        Assert.Equal((byte)128, written.ReadWorldEventVariable(0));
        Assert.Null(written.WorldEventVariable125);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 31)]
    [InlineData(32, 32)]
    [InlineData(63, 63)]
    [InlineData(64, 32)]
    [InlineData(127, 63)]
    [InlineData(128, 0)]
    [InlineData(255, 31)]
    public void FlagCommandsUseNativeSignedBankAndMaskedShift(int selector, int bit)
    {
        NativeWorldEventFlags unknown = new(0, 0);
        NativeWorldEventFlags set = unknown.WithOperand((ushort)(selector | 0x8000));
        Assert.Equal(1UL << bit, set.Value);
        Assert.Equal(1UL << bit, set.KnownMask);
        Assert.True(set.Matches((ushort)(bit | 0x100)));
        NativeWorldEventFlags cleared = set.WithOperand((ushort)selector);
        Assert.Equal(0UL, cleared.Value);
        Assert.Equal(set.KnownMask, cleared.KnownMask);
        Assert.False(unknown.IsKnown((ushort)bit));
    }

    [Fact]
    public void FlagReadUsesUnsignedBankAndExactExpectedByte()
    {
        NativeWorldEventFlags flags = new NativeWorldEventFlags(0, 0).WithOperand(0x0180);
        Assert.True(flags.Matches(0x0100));
        Assert.Throws<NotSupportedException>(() => flags.Matches(0x0180));
        Assert.False(flags.Matches(0x0200));
        Assert.Throws<InvalidDataException>(() => new NativeWorldEventFlags(1, 0).WithOperand(0));
    }

    [Fact]
    public void TransactionOverlaysOnlyEventBytesOntoTheCandidateSavedWorld()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { WorldEventFlags116 = null };
        WorldState world = new();
        GameState departure = new() { Scene = SceneKind.WorldMap, World = world, SavedWorld = saved };
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(departure).WithWorldEventCommand(0xFF28, 0x0120).WithWorldEventCommand(0xFF2E, 0x8000).WithWorldEventCommand(0xFF2E, 0xFF01);
        NativeSavedWorldVehicleSlot changedSlot = saved.Slot4 with { ShapeId = 73 };
        GameState candidate = batch.ApplyTo(departure with { SavedWorld = saved with { Slot4 = changedSlot } }, world);
        Assert.Equal(changedSlot, candidate.SavedWorld!.Slot4);
        Assert.Equal((byte)128, candidate.SavedWorld.WorldEventVariable124);
        Assert.Equal((byte)255, candidate.SavedWorld.WorldEventVariable125);
        Assert.Equal(new NativeWorldEventFlags(1UL << 32, 1UL << 32), candidate.SavedWorld.WorldEventFlags116);
        Assert.Equal((byte)0, saved.WorldEventVariable124);
        Assert.Null(saved.WorldEventFlags116);
        Assert.Throws<NotSupportedException>(() => batch.WithWorldEventCommand(0xFF00, 0));
    }
}
