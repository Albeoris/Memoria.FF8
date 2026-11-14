using System.Buffers.Binary;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class NativeMusicTimelineTests
{
    [Fact]
    public void InstalledExistingMusicCatalogMembersRetainTheirMetadataContract()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII MIDI resources for the bounded existing-member catalog check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MidiArchive).Path);
        int checkedMembers = 0;
        for (int track = 1; track < native.MusicNames.Count; track++)
        {
            string name = native.MusicNames[track].ToLowerInvariant() + ".mid";
            IReadOnlyList<LgpEntry> members = archive.Find(name);
            if (members.Count != 1)
            {
                continue;
            }
            Exception? failure = Record.Exception(() => Ff7NativeMusicTimeline.Read(archive.ReadEntry(members[0])));
            Assert.True(failure is null, $"Original music metadata track{track}/{name}: {failure}");
            checkedMembers++;
        }
        Assert.True(checkedMembers > 0);
        Console.WriteLine($"Parsed{checkedMembers} existing uniquely matched nonzero original MIDI catalog entries; absent/ambiguous members are outside this metadata regression.");
    }

    [Fact]
    public void SynchronousTracksUseOrderedTempoAndMaximumEndWithoutDeviceTime()
    {
        byte[] tempo = [0, 0xFF, 0x51, 3, 7, 0xA1, 0x20, 0x83, 0x60, 0xFF, 0x51, 3, 3, 0xD0, 0x90, 0x83, 0x60, 0xFF, 0x2F, 0];
        byte[] notes = [0, 0x90, 60, 100, 0x83, 0x60, 61, 100, 0x87, 0x40, 0xFF, 0x2F, 0];
        Ff7NativeMusicTimeline timeline = Ff7NativeMusicTimeline.Read(Smf(tempo, notes));
        Assert.False(timeline.Loops);
        Assert.Equal(1, timeline.DurationSeconds);
        Assert.True(timeline.IsPlaying(29));
        Assert.False(timeline.IsPlaying(30));
    }

    [Fact]
    public void NamedNativeLoopRemainsActiveBeyondTheFirstPass()
    {
        byte[] track = [0, 0xFF, 6, 9, .. Encoding.ASCII.GetBytes("loopStart"), 0x87, 0x40, 0xFF, 6, 7, .. Encoding.ASCII.GetBytes("loopEnd"), 0, 0xFF, 0x2F, 0];
        Ff7NativeMusicTimeline timeline = Ff7NativeMusicTimeline.Read(Smf(track));
        Assert.True(timeline.Loops);
        Assert.Equal(1, timeline.DurationSeconds);
        Assert.True(timeline.IsPlaying(long.MaxValue));
    }

    [Fact]
    public void NativeLoopEndRewindsWithoutRequiringAStartAndStartAloneDoesNotRewind()
    {
        byte[] endOnly = [0x87, 0x40, 0xFF, 6, 7, .. Encoding.ASCII.GetBytes("loopEnd"), 0, 0xFF, 0x2F, 0];
        Assert.True(Ff7NativeMusicTimeline.Read(Smf(endOnly)).IsPlaying(long.MaxValue));
        byte[] startOnly = [0, 0xFF, 6, 9, .. Encoding.ASCII.GetBytes("loopStart"), 0x87, 0x40, 0xFF, 0x2F, 0];
        Assert.False(Ff7NativeMusicTimeline.Read(Smf(startOnly)).IsPlaying(30));
    }

    [Fact]
    public void TruncationMissingOwnerAndInvalidTimingAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Ff7NativeMusicTimeline.Read(Smf([0, 0xFF, 0x51, 3, 7])));
        Assert.Throws<InvalidDataException>(() => Ff7NativeMusicTimeline.Read(Smf([0, 60, 100, 0, 0xFF, 0x2F, 0])));
        Assert.Throws<InvalidDataException>(() => Ff7NativeMusicTimeline.Read(Smf([0, 0x90, 60, 100])));
        Assert.Throws<InvalidDataException>(() => Ff7NativeMusicTimeline.Read(Smf([0x81, 0x80, 0x80, 0x80, 0, 0xFF, 0x2F, 0])));
        Assert.Throws<InvalidDataException>(() => Ff7NativeMusicTimeline.Read([.. Smf([0, 0xFF, 0x2F, 0]), 0]));
        byte[] smpte = Smf([0, 0xFF, 0x2F, 0]);
        smpte[12] = 0x80;
        Assert.Throws<NotSupportedException>(() => Ff7NativeMusicTimeline.Read(smpte));
    }

    [Fact]
    public void InstalledFarmWaltzHasFiniteOriginalMidiEndAndAmbientHasNativeLoop()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII MIDI resources for the reached farm music check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7NativeMusicTimeline walz = Ff7NativeMusicTimeline.Load(installation, 22);
        Assert.Equal(480, walz.Division);
        Assert.Equal(43_200L * 352_941, walz.DurationNumerator);
        Assert.False(walz.Loops);
        Assert.True(walz.IsPlaying(952));
        Assert.False(walz.IsPlaying(953));
        Ff7NativeMusicTimeline ambient = Ff7NativeMusicTimeline.Load(installation, 32);
        Assert.True(ambient.Loops);
        Assert.True(ambient.IsPlaying(1_000_000));
    }

    private static byte[] Smf(params byte[][] tracks)
    {
        using MemoryStream stream = new();
        stream.Write("MThd"u8);
        Write32(6);
        Write16(tracks.Length == 1 ? 0 : 1);
        Write16(tracks.Length);
        Write16(480);
        foreach (byte[] track in tracks)
        {
            stream.Write("MTrk"u8);
            Write32(track.Length);
            stream.Write(track);
        }
        return stream.ToArray();

        void Write16(int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)value));
            stream.Write(bytes);
        }

        void Write32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, checked((uint)value));
            stream.Write(bytes);
        }
    }
}
