using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldFadeTests
{
    [Fact]
    public void FadeInCompletesSixteenNativeSamplesWithoutChangingSavedResumeWords()
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueFadeIn();
        Assert.False(state.IsFadeComplete);
        state = state.AdvanceCallback();
        Assert.Equal((short)239, state.Physical.Counter);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Subtract, 238, 238, 238), state.Draw);
        for (int sample = 1; sample < 16; sample++)
        {
            state = state.AdvanceCallback();
        }
        Assert.Equal((short)0, state.Physical.Mode);
        Assert.Equal((short)0, state.Physical.Counter);
        Assert.Equal((short)16, state.Physical.Rate);
        Assert.True(state.IsFadeComplete);
        Assert.Equal((short)2, state.Saved.Mode);
        Assert.Equal((short)255, state.Saved.Counter);
        Assert.Equal((byte)0, state.Draw.Red);
        Assert.Equal((short)255, state.ResumeKind6().Physical.Counter);
        Assert.Equal((short)2, state.ResumeKind6().Physical.Mode);
    }

    [Fact]
    public void FadeOutSaturatesAt255AndSyncUsesItsSeparateLastFadeSelector()
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueFadeOut();
        state = state.AdvanceCallback();
        Assert.Equal((short)8, state.Physical.Counter);
        Assert.Equal((byte)7, state.Draw.Red);
        Assert.False(state.IsFadeComplete);
        for (int sample = 1; sample < 32; sample++)
        {
            state = state.AdvanceCallback();
        }
        Assert.Equal((short)255, state.Physical.Counter);
        Assert.Equal((short)3, state.Physical.Mode);
        Assert.Equal((byte)254, state.Draw.Red);
        Assert.True(state.IsFadeComplete);
        Assert.Equal((short)0, state.Saved.Counter);
        Assert.False(state.IssueNone().IsFadeComplete);
    }

    [Fact]
    public void ColorInterpolationUsesSignedWordDifferenceAndNarrowedDrawingBytes()
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(false, -1, 257, 32767, 256, -2, -32768, 2);
        NativeFieldFadeState first = state.AdvanceCallback();
        Assert.Equal((short)127, first.Physical.Red);
        Assert.Equal((short)128, first.Physical.Green);
        Assert.Equal((short)32767, first.Physical.Blue);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Add, 127, 128, 255), first.Draw);
        Assert.False(first.IsColorComplete);
        NativeFieldFadeState terminal = first.AdvanceCallback();
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Add, 0, 254, 0), terminal.Draw);
        Assert.True(terminal.IsColorComplete);
        Assert.Equal((short)0, terminal.Saved.Counter);
        Assert.Equal((short)5, terminal.CompleteColorSync().Saved.Mode);
        Assert.Equal((short)0, terminal.ResumeKind6().Saved.Mode);
        Assert.Equal((short)2, terminal.ResumeKind6().Physical.Mode);
        Assert.Equal((short)256, terminal.ResumeKind6().Physical.StartRed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65535)]
    public void NativeSignedDurationIsNotNormalizedToAnArtificialFrame(int duration)
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(true, 17, 23, 29, 31, 37, 41, duration).AdvanceCallback();
        Assert.Equal(unchecked((short)duration), state.Physical.Counter);
        Assert.Equal(unchecked((short)duration), state.Physical.Rate);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Subtract, 31, 37, 41), state.Draw);
    }

    [Fact]
    public void SavedUnalignedMemoryAndWarmNewGameDoNotReplacePhysicalBank()
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueFadeOut().AdvanceCallback();
        NativeFieldFadeState written = state.WriteSaved(217, 4, unchecked((int)0x89ABCDEF));
        Assert.Equal(unchecked((int)0x89ABCDEF), written.ReadSaved(217, 4));
        Assert.Equal(state.Physical, written.Physical);
        Assert.Equal(unchecked((short)0xEF03), written.Saved.Mode);
        Assert.Equal(unchecked((short)0xABCD), written.Saved.Counter);
        Assert.Equal((short)0x89, written.Saved.Rate);
        NativeFieldFadeState newGame = written.ClearSavedForNewGame();
        Assert.Equal(state.Physical, newGame.Physical);
        Assert.Equal(state.Draw, newGame.Draw);
        Assert.Equal(state.SyncMode, newGame.SyncMode);
        Assert.Equal(NativeFadeWords.Zero(), newGame.Saved);
        Assert.Throws<NotSupportedException>(() => written.WriteSaved(239, 2, 1));
        Assert.Equal(unchecked((int)0x89ABCDEF), written.ReadSaved(217, 4));
    }

    [Fact]
    public void ExactCurrentJsonRetainsNextSampleAndRefusesMissingPhysicalWords()
    {
        NativeFieldFadeState state = NativeFieldFadeState.FromColdProcess().IssueInterpolatedColor(true, 3, 5, 7, 93, 95, 97, 30).AdvanceCallback();
        string json = JsonSerializer.Serialize(state);
        NativeFieldFadeState restored = JsonSerializer.Deserialize<NativeFieldFadeState>(json)!;
        restored.Validate();
        Assert.Equal(state.AdvanceCallback(), restored.AdvanceCallback());
        JsonObject malformed = JsonNode.Parse(json)!.AsObject();
        malformed["Physical"]!.AsObject().Remove("Rate");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFieldFadeState>(malformed.ToJsonString()));
    }
}
