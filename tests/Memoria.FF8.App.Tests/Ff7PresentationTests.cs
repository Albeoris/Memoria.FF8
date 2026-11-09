using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using System.Numerics;

namespace Memoria.FF8.Tests;

public sealed class Ff7PresentationTests
{
    [Fact]
    public void FieldCameraKeepsTheNativeSettlingOffsetWithoutMovingUiOrGuessingMovieComposition()
    {
        Memoria.FF7.Runtime.Ff7FieldProcess state = new() { CameraX = 31, CameraY = -20, CameraShakeX = new(Enabled: false, Offset: -7), CameraShakeY = new(Enabled: true, Offset: 4) };
        Assert.Equal(new Vector2(24, -16), Ff7Scene.FieldCameraOffset(state));
        Assert.Equal((sbyte)-7, state.CameraShakeX.Offset);
        Memoria.FF7.Runtime.Ff7FieldProcess movie = state with { Movie = new(0, true, 0, false, 1) };
        Assert.Throws<NotSupportedException>(() => Ff7Scene.FieldCameraOffset(movie));
        Assert.Equal(new Vector2(31, -20), Ff7Scene.FieldCameraOffset(movie with { CameraShakeX = null, CameraShakeY = null }));
    }

    [Theory]
    [InlineData(27)]
    [InlineData(52)]
    [InlineData(54)]
    [InlineData(55)]
    [InlineData(56)]
    public void InstalledOpeningSoundsUseTheSharedPortableWaveDecoder(int sampleIndex)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR for original sound decoding.");
        Ff7SoundSample sample = Ff7SoundResources.Read(Ff7Installation.Discover(directory), sampleIndex);
        PcmAudio pcm = PcmAudioDecoder.ReadWave(sample.WaveBytes);
        Assert.Equal(44100, pcm.SampleRate);
        Assert.Equal(2, pcm.Channels);
        Assert.NotEmpty(pcm.Bytes);
        Assert.Equal(0, pcm.Bytes.Length % 4);
    }

    [Fact]
    public void NativeMovieCameraBudgetCountsLogicalFramesAndReturnsBlankAtEnd()
    {
        PcMovieCamera first = new(new PcFieldCamera(new Vector3(4096, 0, 0), new Vector3(0, 4096, 0), new Vector3(0, 0, 4096), 0, new Vector3(1, 2, 3), 0, 386), 1);
        PcMovieCamera second = first with { Flags = 2 };
        Ff7MovieCameraTrack track = new(new[] { first, second });
        track.Publish(0, 0);
        Assert.Same(first, track.CameraAt(0));
        track.Publish(0, 0);
        Assert.Same(first, track.CameraAt(0));
        track.Publish(1, 10);
        Assert.Same(second, track.CameraAt(1));
        track.Publish(2, 20);
        Assert.Equal(Vector3.Zero, track.CameraAt(2).Camera.AxisZ);
        Assert.Equal(0, track.CameraAt(2).Camera.FocalLength);
        Assert.Throws<InvalidDataException>(() => track.Publish(1, 10));
    }

    [Fact]
    public void RestoredMovieFrameRetainsCameraBudgetWithoutReplayingPreviousFrames()
    {
        PcMovieCamera original = new(new PcFieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, default, 0, 386), 0);
        Ff7MovieCameraTrack track = new(Enumerable.Repeat(original, 1792).ToArray());
        track.Publish(1791, 17910);
        Assert.Same(original, track.CameraAt(1791));
        track.Publish(1792, 17920);
        Assert.Equal(Vector3.Zero, track.CameraAt(1792).Camera.AxisZ);
    }

    [Fact]
    public void NativeMovieCameraUsesAbsoluteFrameWithItsIndependentRestoredReadBudget()
    {
        PcMovieCamera original = new(new PcFieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, default, 0, 386), 0);
        PcMovieCamera[] cameras = Enumerable.Range(0, 10).Select(index => original with { Flags = (ushort)index }).ToArray();
        Ff7MovieCameraTrack track = new(cameras);
        track.Publish(7, 10);
        Assert.Same(cameras[7], track.CameraAt(7));
        track.Publish(8, 20);
        Assert.Same(cameras[8], track.CameraAt(8));
        Assert.Throws<InvalidDataException>(() => track.Publish(9, -10));
    }

    [Fact]
    public void SharedNamePromptAppliesSelectedResourceConstraintsAndDefault()
    {
        string? accepted = null;
        MenuController menus = new();
        menus.OpenNamePrompt(3, "Cloud", name => accepted = name, new NamePromptOptions(9, "Cloud", new UiImageSource(UiImageKind.HostArtwork, 1), text => text.All(character => character is >= ' ' and <= '~')));
        Assert.False(menus.Entries.Single(entry => entry.Action is { Kind: "name-page", Value: "Cyrillic" }).Enabled);
        Assert.Throws<ArgumentException>(() => menus.SetNameDraft("0123456789"));
        Assert.Throws<ArgumentException>(() => menus.SetNameDraft("Борис"));
        menus.SetNameDraft("CloudNine");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("CloudNine", accepted);
        Assert.False(menus.IsOpen);
    }

    [Fact]
    public void ExistingSharedNamePromptKeepsUnicodeAndTwentyCharacterContract()
    {
        string? accepted = null;
        MenuController menus = new();
        menus.OpenNamePrompt(0, "Squall", name => accepted = name);
        Assert.True(menus.Entries.Single(entry => entry.Action is { Kind: "name-page", Value: "Cyrillic" }).Enabled);
        menus.SetNameDraft("Александр");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Александр", accepted);
        Assert.Throws<ArgumentException>(() => menus.SetNameDraft(new string('a', 21)));
    }
}
