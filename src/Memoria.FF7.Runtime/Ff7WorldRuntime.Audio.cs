using System.Buffers.Binary;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldAudioState(bool Initialized = false, int Song = 0, int PreviousSong = 0, bool PlaybackEnabled = false, int LastPublishedTrack = -1, int LastOperation = 0, long Publications = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldAudioState audio = new();
    private IReadOnlyList<byte> audioSongs = [];

    public Action<FieldAudioCommand>? AudioRequested { get; set; }

    private static int ResolveAudioArea(int world, int selector, byte[] variables)
    {
        if (world != 0)
        {
            return world + 9;
        }
        int story = BinaryPrimitives.ReadInt16LittleEndian(variables);
        int phase = story < 1000 ? 0 : story < 1580 ? 1 + ((variables[0x7A] & 1) | (story >= 1620 ? 2 : 0)) : 5 + ((story >= 1620 ? 1 : 0) | ((variables[0x386] & 16) != 0 ? 2 : 0));
        return selector is >= 0x24 and <= 0x27 ? phase > 0 ? 10 : 9 : phase;
    }

    private void BeginWorldAudio()
    {
        PublishAudio(new(0xF1));
        for (int channel = 0; channel < 4; channel++)
        {
            PublishAudio(new(0xB0 + channel, 0));
        }
        PublishAudio(new(0xC0, 127));
    }

    private void CompleteWorldAudioInitialization()
    {
        if (audio.Song < 6)
        {
            audio = audio with { PlaybackEnabled = true };
        }
        PlayWorldSong(audio.Song == 0 ? 1 : audio.Song);
        audio = audio with { Initialized = true };
    }

    private void PlayWorldSong(int song)
    {
        if (song is < 1 or > 8)
        {
            throw new NotSupportedException("FFVII world music requires an original song-table selector from one through eight.");
        }
        if (audio.PlaybackEnabled)
        {
            int operation = song == 1 ? 0x18 : audio.Song == 1 ? 0x14 : 0x10;
            int track = audioSongs[song - 1];
            PublishAudio(new(operation, track, 4));
            audio = audio with { LastPublishedTrack = track };
        }
        audio = audio with { PreviousSong = audio.Song, Song = song };
    }

    private void PublishAudio(FieldAudioCommand command)
    {
        AudioRequested?.Invoke(command);
        audio = audio with { LastOperation = command.Operation, Publications = checked(audio.Publications + 1) };
    }

    private bool ExecuteWorldAudio(WorldOperation operation, int[] values)
    {
        switch (operation)
        {
            case WorldOperation.Music:
                audio = audio with { PlaybackEnabled = true };
                PlayWorldSong(values[0]);
                if (values[0] != 0)
                {
                    audio = audio with { PlaybackEnabled = false };
                }
                return true;
            case WorldOperation.MusicVolume:
                PublishAudio(new(0xC0, values[0]));
                return true;
            default:
                return false;
        }
    }
}
