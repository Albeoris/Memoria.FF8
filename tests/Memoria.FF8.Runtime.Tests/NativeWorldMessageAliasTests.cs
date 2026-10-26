using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldMessageAliasTests
{
    [Fact]
    public void ShowingSharedChannelReplacesPhysicalTextAndRetainsLogicalMessageBinding()
    {
        NativeWorldMessage fuel = Message(1, 43);
        NativeWorldEventUi before = NativeWorldEventUi.Create().Show(fuel).AdvanceText(4);
        NativeWorldEventUi after = before.Show(Message(2, 10));

        NativeWorldMessage physical = Assert.Single(after.Windows);
        Assert.Equal(2, physical.WindowId);
        Assert.Equal((short)2048, physical.Text!.Openness);
        NativeWorldMessage alias = Assert.Single(after.AliasedWindows);
        Assert.Equal(1, alias.WindowId);
        Assert.Equal((byte)43, alias.MessageId);
        Assert.Null(alias.Text);
        Assert.Null(alias.TextComplete);
        Assert.Same(after, after.Show(fuel));
        Assert.Equal(1, Assert.Single(before.Windows).WindowId);
        Assert.Empty(before.AliasedWindows);
        after.Validate();
    }

    [Fact]
    public void AliasReadinessUsesCurrentSharedPhysicalText()
    {
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(Message(1, 43)).Show(Message(2, 10));
        Assert.True(ui.MatchesTextComplete(1, 0));
        Assert.True(ui.MatchesTextComplete(2, 0));
        ui = ui.AdvanceText(20);
        Assert.True(ui.MatchesTextComplete(1, 1));
        Assert.True(ui.MatchesTextComplete(2, 1));
        Assert.False(ui.MatchesTextComplete(3, 1));
    }

    [Fact]
    public void ClosedLogicalBindingStaysNotReadyWhenSiblingReopensAndCompletesSharedChannel()
    {
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(Message(1, 43)).AdvanceText(4).Show(Message(2, 10)).Close(1);
        ui = ui.Show(Message(2, 11)).AdvanceText(20);

        Assert.Empty(ui.AliasedWindows);
        Assert.Equal(2, Assert.Single(ui.Windows).WindowId);
        Assert.True(ui.MatchesTextComplete(2, 1));
        Assert.False(ui.MatchesTextComplete(1, 1));
        Assert.True(ui.MatchesTextComplete(1, 0));
        Assert.False(ui.MatchesTextComplete(1, 2));
        Assert.False(ui.MatchesTextComplete(3, 1));
        Assert.True(ui.MatchesTextComplete(3, 0));
        ui.Validate();
    }

    [Fact]
    public void SharedChannelReadinessPersistsThroughCloseUntilANewMessageResetsIt()
    {
        NativeWorldEventUi ready = NativeWorldEventUi.Create().Show(Message(1, 43)).Show(Message(2, 10)).AdvanceText(20);
        NativeWorldEventUi closing = ready.Close(1);

        Assert.Empty(closing.Windows);
        Assert.True(closing.MatchesTextComplete(2, 1));
        Assert.False(closing.MatchesTextComplete(1, 1));
        Assert.True(Assert.Single(closing.RetainedChannelReadiness).TextComplete);
        NativeWorldEventUi closed = closing.AdvanceText(8);
        Assert.Empty(closed.ClosingChannels);
        Assert.True(closed.MatchesTextComplete(2, 1));
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(closed))!;
        restored.Validate();
        Assert.True(restored.MatchesTextComplete(2, 1));
        Assert.Same(restored, restored.Show(Message(2, 10)));

        NativeWorldEventUi reset = restored.Show(Message(1, 44));
        Assert.Empty(reset.RetainedChannelReadiness);
        Assert.False(reset.MatchesTextComplete(2, 1));
        Assert.False(reset.MatchesTextComplete(1, 1));
        Assert.True(reset.AdvanceText(20).MatchesTextComplete(2, 1));
        reset.Validate();
    }

    [Fact]
    public void SharedChannelUnknownHistoricalReadinessRemainsUnknownAfterClose()
    {
        NativeWorldMessage historical = Message(1, 43) with { Text = null, TextComplete = null };
        NativeWorldEventUi closing = NativeWorldEventUi.Create().Show(historical).Show(historical with { WindowId = 2, MessageId = 10 }).BeginCallback().Close(1);

        Assert.Null(closing.MatchesTextComplete(2, 1));
        Assert.Null(closing.AdvanceText(8).MatchesTextComplete(2, 1));
        Assert.False(closing.MatchesTextComplete(1, 1));
        closing.Validate();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void ClosingEitherBindingClosesPhysicalChannelAndClearsOnlyTargetLogicalBinding(int target, int retained)
    {
        NativeWorldEventUi before = NativeWorldEventUi.Create().Show(Message(1, 43)).AdvanceText(4).Show(Message(2, 10));
        NativeWorldEventUi closed = before.Close((ushort)target);

        Assert.Empty(closed.Windows);
        NativeWorldMessage alias = Assert.Single(closed.AliasedWindows);
        Assert.Equal(retained, alias.WindowId);
        Assert.Equal((short)2048, Assert.Single(closed.ClosingChannels).Openness);
        Assert.True(closed.MatchesTextComplete((sbyte)retained, 0));
        Assert.True(closed.MatchesTextComplete((sbyte)target, 0));
        NativeWorldMessage retainedMessage = retained == 1 ? Message(1, 43) : Message(2, 10);
        Assert.Same(closed, closed.Show(retainedMessage));

        NativeWorldEventUi reopened = closed.Show(Message(target, 11));
        Assert.Equal(target, Assert.Single(reopened.Windows).WindowId);
        Assert.Equal((short)2048, Assert.Single(reopened.Windows).Text!.Openness);
        Assert.Empty(reopened.ClosingChannels);
        reopened.Validate();
        NativeWorldEventUi forgotten = closed.Close((ushort)retained);
        Assert.Empty(forgotten.AliasedWindows);
        Assert.Empty(forgotten.AdvanceText(4).ClosingChannels);
    }

    [Fact]
    public void DynamicFuelMessageReclaimsAliasedChannelAndRestartsItsText()
    {
        NativeWorldMessage fuel = Message(1, 254) with { FuelRemaining = 3 };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(fuel).AdvanceText(4).Show(Message(2, 10));
        NativeWorldMessage updated = fuel with { FuelRemaining = 2 };
        NativeWorldEventUi replaced = ui.Show(updated);

        Assert.Equal(1, Assert.Single(replaced.Windows).WindowId);
        Assert.Equal((byte)2, Assert.Single(replaced.Windows).FuelRemaining);
        Assert.Equal((short)2048, Assert.Single(replaced.Windows).Text!.Openness);
        Assert.Equal(0, Assert.Single(replaced.Windows).Text!.Position);
        Assert.Equal(2, Assert.Single(replaced.AliasedWindows).WindowId);
        replaced.Validate();
    }

    [Fact]
    public void AliasBindingsSurviveJsonWhilePhysicalChannelsRemainUnique()
    {
        NativeWorldEventUi source = NativeWorldEventUi.Create().Show(Message(1, 43)).Show(Message(2, 10)).Show(Message(3, 11) with { Channel = 2 });
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(2, restored.Windows.Length);
        Assert.Single(restored.AliasedWindows);
        Assert.Same(restored, restored.Show(Message(1, 43)));
        NativeWorldEventUi next = restored.BeginCallback().AdvanceText(20);
        Assert.True(next.MatchesTextComplete(1, 1));
        Assert.True(next.MatchesTextComplete(3, 1));
        Assert.Equal(3, Assert.Single(next.Close(1).Windows).WindowId);

        Assert.Throws<InvalidDataException>(() => (source with { AliasedWindows = source.AliasedWindows.Add(source.Windows[0] with { Text = null, TextComplete = null }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (source with { AliasedWindows = [Message(4, 12)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (source with { Windows = source.Windows.Add(Message(4, 12)) }).Validate());
    }

    private static NativeWorldMessage Message(int window, byte message)
    {
        NativeWorldTextState text = NativeWorldTextState.Create(ImmutableArray.Create<byte>(65, 0), 0);
        return new NativeWorldMessage(window, message, 1, 0, 0, 0, 0, new string('0', 64), false) { Text = text };
    }
}
