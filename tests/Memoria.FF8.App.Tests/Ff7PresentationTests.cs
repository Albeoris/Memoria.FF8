using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using System.Numerics;

namespace Memoria.FF8.Tests;

public sealed class Ff7PresentationTests
{
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
        track.Publish(0);
        Assert.Same(first, track.CameraAt(0));
        track.Publish(0);
        Assert.Same(first, track.CameraAt(0));
        track.Publish(1);
        Assert.Same(second, track.CameraAt(1));
        track.Publish(2);
        Assert.Equal(Vector3.Zero, track.CameraAt(2).Camera.AxisZ);
        Assert.Equal(0, track.CameraAt(2).Camera.FocalLength);
        Assert.Throws<InvalidDataException>(() => track.Publish(1));
    }

    [Fact]
    public void RestoredMovieFrameRetainsCameraBudgetWithoutReplayingPreviousFrames()
    {
        PcMovieCamera original = new(new PcFieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, default, 0, 386), 0);
        Ff7MovieCameraTrack track = new(Enumerable.Repeat(original, 1792).ToArray());
        track.Publish(1791);
        Assert.Same(original, track.CameraAt(1791));
        track.Publish(1792);
        Assert.Equal(Vector3.Zero, track.CameraAt(1792).Camera.AxisZ);
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
