using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class WorldDialogueObservationTests
{
    private static NativeWorldMessage Message(int window = 1, byte message = 7, byte channel = 1) => new(window, message, channel, 0, 0, 0, 0, new string('A', 64), false)
    {
        Text = NativeWorldTextState.Create([64, 64, 64, 0], 5120)
    };

    [Fact]
    public void ObservationIdentitySurvivesCallbacksRevealAndNoOpShowButChangesOnReplacementAndReopen()
    {
        NativeWorldMessage message = Message();
        NativeWorldEventUi opened = NativeWorldEventUi.Create().Show(message);
        long occurrence = opened.PresentationOccurrenceId(message.WindowId);
        Assert.True(occurrence > 0);
        NativeWorldEventUi callback = opened.BeginCallback();
        Assert.Equal(occurrence, callback.PresentationOccurrenceId(message.WindowId));
        NativeWorldEventUi revealed = callback.AdvanceText(3);
        Assert.NotEqual(opened.Windows[0].Text, revealed.Windows[0].Text);
        Assert.Equal(occurrence, revealed.PresentationOccurrenceId(message.WindowId));
        NativeWorldEventUi unchanged = revealed.Show(message);
        Assert.Same(revealed, unchanged);
        Assert.Equal(occurrence, unchanged.PresentationOccurrenceId(message.WindowId));

        NativeWorldEventUi replaced = unchanged.Show(message with { MessageId = 8 });
        long replacement = replaced.PresentationOccurrenceId(message.WindowId);
        Assert.NotEqual(occurrence, replacement);
        Assert.Equal((byte)8, replaced.Windows[0].MessageId);
        NativeWorldEventUi reopened = replaced.Close((ushort)message.WindowId).Show(message with { MessageId = 8 });
        Assert.NotEqual(replacement, reopened.PresentationOccurrenceId(message.WindowId));
        Assert.Equal((byte)8, reopened.Windows[0].MessageId);
    }

    [Fact]
    public void OpeningAnotherWindowDoesNotInventANewOccurrenceForExistingWindow()
    {
        NativeWorldEventUi first = NativeWorldEventUi.Create().Show(Message());
        long original = first.PresentationOccurrenceId(1);
        NativeWorldEventUi both = first.Show(Message(2, 9, 2));

        Assert.Equal(original, both.PresentationOccurrenceId(1));
        Assert.True(both.PresentationOccurrenceId(2) > 0);
        Assert.NotEqual(original, both.PresentationOccurrenceId(2));
        Assert.Equal(0, both.PresentationOccurrenceId(12));
    }

    [Fact]
    public void PresentationEvidenceDoesNotChangeNativeRecordEqualityHashOrSerializedSave()
    {
        NativeWorldEventUi observed = NativeWorldEventUi.Create().Show(Message());
        NativeWorldEventUi nativeOnly = new(observed.ScriptMode, observed.CloseSuppressedThisCallback, observed.Windows)
        {
            ClosingChannels = observed.ClosingChannels,
            AliasedWindows = observed.AliasedWindows,
            RetainedChannelReadiness = observed.RetainedChannelReadiness
        };

        Assert.True(observed.PresentationOccurrenceId(1) > 0);
        Assert.Equal(0, nativeOnly.PresentationOccurrenceId(1));
        Assert.Equal(nativeOnly, observed);
        Assert.Equal(nativeOnly.GetHashCode(), observed.GetHashCode());
        string json = JsonSerializer.Serialize(observed);
        Assert.Equal(JsonSerializer.Serialize(nativeOnly), json);
        Assert.DoesNotContain("Occurrence", json, StringComparison.Ordinal);
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(0, restored.PresentationOccurrenceId(1));
    }
}
