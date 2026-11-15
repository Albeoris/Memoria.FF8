using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeHiddenFieldBarsTests
{
    [Fact]
    public void Original_field_continuation_without_bar_presentation_passes_its_first_set_commands()
    {
        string source = TestResources.FixturePath("App.Tests.NativeHiddenFieldBarsTests.1");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        for (int callback = 0; callback < 3600 && (session.FieldBars?.First?.Display.Visible != true || session.FieldBars?.Second?.Display.Visible != true); callback++)
        {
            world.Apply(world.CurrentDialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait));
        }
        Assert.Null(world.Events.LastFault);
        Assert.True(session.FieldBars?.First?.Display.Visible);
        Assert.True(session.FieldBars?.Second?.Display.Visible);
        Assert.Equal((short)120, session.FieldBars!.First!.Display.Width);
        Assert.Equal((short)120, session.FieldBars.Second!.Display.Width);
    }

    [Theory]
    [InlineData(0, 216)]
    [InlineData(1, -65537)]
    public void Cold_process_accepts_set_before_display_and_preserves_hidden_words(int id, int value)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        NativeFieldBarsState bars = session.FieldBars!.SetValue(id, value);
        bars.Validate();
        NativeFieldBarSlot slot = (id == 0 ? bars.First : bars.Second)!;
        Assert.Equal(unchecked((short)value), slot.Mirror.Value);
        Assert.Equal(unchecked((short)value), slot.Display.Value);
        Assert.Equal((short)0, slot.Display.Width);
        Assert.Equal((short)0, slot.Display.Fill);
        Assert.Equal((short)0, slot.Mirror.Mode);
        Assert.Equal((byte)0, slot.Display.Flags);
        Assert.Empty(bars.VisibleBars);
        Assert.Equal(bars, bars.Advance(2));
        NativeFieldBarsState restored = JsonSerializer.Deserialize<NativeFieldBarsState>(JsonSerializer.Serialize(bars))!;
        restored.Validate();
        Assert.Equal(bars.SetValue(id, 100), restored.SetValue(id, 100));
    }

    [Fact]
    public void New_game_hides_retained_bars_without_discarding_their_words()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeFieldBarsState bars = session.FieldBars!.DisplayWidth(0, 8, 162, 3, -20, 100, 25, 120).SetValue(0, 80);
        session.SetFieldBars(bars);
        session.StartNewGame();
        Assert.Equal(bars.Hide(0).Hide(1), session.FieldBars);
        Assert.Equal((short)70, session.FieldBars!.SetValue(0, 50).First!.Display.Fill);
    }

    [Fact]
    public void Missing_logical_bar_uses_zero_defaults_while_retained_hidden_bar_remains_exact()
    {
        NativeFieldBarsState retained = NativeFieldBarsState.CreateHidden().DisplayWidth(1, 8, 162, 3, -20, 100, 25, 120).Hide(1);
        GameplaySaveFieldPresentation saved = GameplaySaveFieldPresentation.Capture(retained, null)!;
        GameplaySaveFieldPresentation decoded = JsonSerializer.Deserialize<GameplaySaveFieldPresentation>(JsonSerializer.Serialize(saved))!;
        NativeFieldBarsState restored = decoded.RestoreBars(NativeFieldBarsState.FromColdProcess());
        Assert.Equal(retained.Second, restored.Second);
        Assert.Equal(NativeFieldBarsState.FromColdProcess().First, restored.First);
        Assert.Equal((short)216, restored.SetValue(0, 216).First!.Display.Value);
        Assert.False(restored.SetValue(1, 80).Second!.Display.Visible);
        Assert.Throws<NotSupportedException>(() => restored.SetValue(2, 0));
        NativeFieldBarsState legacy = JsonSerializer.Deserialize<NativeFieldBarsState>("{\"Version\":1,\"First\":null,\"Second\":null}")!;
        Assert.Equal((short)216, legacy.SetValue(0, 216).First!.Display.Value);
        Assert.False(legacy.SetValue(0, 216).First!.Display.Visible);
    }
}
