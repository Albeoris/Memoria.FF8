using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMusicHeaderTests
{
    internal static void Run()
    {
        byte[] bytes = Section();
        byte[] original = bytes.ToArray();
        Ff7FieldResources field = new("music-header-fixture", null!, PcFieldScripts.Read(bytes), null!, [], [], null!, null!, null!);
        Ff7FieldHost Host(Ff7FieldProcess? process = null) => new(field, (_, _) => 1, process);
        Ff7FieldHost host = Host(new() { NativeMusicTrack = 7, MusicTicks = 123, MusicVolume = 83, MusicVolumeFade = new(0, -28, 12), MusicTransition = new(7, 20, Ff7MusicTransitionPhase.FadeIn) });
        Check(host.ResolveNativeMusicTrack(0) == -1, "A bounded non-AKAO directory entry with ID0 was rejected or converted into stop.");
        Check(host.ResolveNativeMusicTrack(1) == 254, "Native local music ID255 was narrowed or interpreted as signed.");
        foreach (int index in new[] { -1, 2, 3 })
        {
            bool rejected = false;
            try
            {
                host.ResolveNativeMusicTrack(index);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected, "An invalid directory index or truncated ID read was admitted.");
        }
        string Physical(Ff7FieldHost actual) => JsonSerializer.Serialize(new { actual.Process.NativeMusicTrack, actual.Process.MusicTicks, actual.Process.MusicVolume, actual.Process.MusicVolumeFade, actual.Process.MusicTransition });
        string playing = Physical(host);
        Check(host.Execute(new(1, 0, 7, 0, new FieldMusicCommand(FieldMusicOperation.Play, 0))).Completed, "A native no-play local music request was not acknowledged.");
        Check(host.Process.NativeMusicRequest == -1 && Physical(host) == playing, "F0 ID0 stopped or reset the actual ongoing music instead of retaining its raw request.");
        host.Execute(new(2, 0, 7, 0, new FieldMusicCommand(FieldMusicOperation.SaveCurrentAndPlay, 1)));
        Check(host.Process.NativeMusicRequest == 254 && host.Process.SavedNativeMusicTrack == -1 && Physical(host) == playing, "F3 saved physical playback instead of the raw previous no-play request.");
        host.Execute(new(3, 0, 7, 0, new FieldMusicCommand(FieldMusicOperation.SwapSaved, 0)));
        Check(host.Process.NativeMusicRequest == -1 && host.Process.SavedNativeMusicTrack == 254 && Physical(host) == playing, "F4 lost the raw request or changed real playback while swapping no-play requests.");
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Check(cold.Process.NativeMusicRequest == -1 && cold.Process.SavedNativeMusicTrack == 254 && Physical(cold) == playing, "Cold restoration lost no-play request identity or ongoing audio state.");
        foreach (int track in new[] { -1, 99, 254 })
        {
            host.Execute(new(10 + track, 0, 7, 0, new FieldAudioCommand(0x10, track)));
            Check(Physical(host) == playing, "Audio10 no-play input stopped, restarted or changed the current native music fade.");
        }
        host.Execute(new(300, 0, 7, 0, new FieldAudioCommand(0x10, 0)));
        Check(host.Process.NativeMusicTrack == 0 && host.Process.MusicTicks == 0, "Audio10 track0 did not retain its distinct native stop behavior.");
        Ff7FieldProcess legacy = JsonSerializer.Deserialize<Ff7FieldProcess>("{\"NativeMusicTrack\":7,\"MusicTicks\":123}")!;
        Check(legacy.NativeMusicRequest is null, "An old process save invented a raw native request.");
        Ff7FieldHost old = Host(legacy);
        old.Execute(new(301, 0, 7, 0, new FieldMusicCommand(FieldMusicOperation.SaveCurrentAndPlay, 0)));
        Check(old.Process.SavedNativeMusicTrack == 7 && old.Process.NativeMusicTrack == 7 && old.Process.NativeMusicRequest == -1, "An old save failed to derive its prior request from recorded physical playback.");
        byte[] legacyBytes = original.ToArray();
        legacyBytes[132] = 8;
        Ff7FieldResources legacyField = field with { Scripts = PcFieldScripts.Read(legacyBytes) };
        Ff7FieldProcess localOnly = new() { Music = new(FieldMusicOperation.Play, 0), NativeMusicTrack = null, MusicTicks = 321, MusicVolume = 83, MusicVolumeFade = new(0, -28, 12) };
        Ff7FieldHost legacyLocal = new(legacyField, (_, _) => 1, localOnly);
        legacyLocal.Execute(new(302, 0, 7, 0, new FieldMusicCommand(FieldMusicOperation.Play, 1)));
        Check(legacyLocal.Process.NativeMusicTrack == 7 && legacyLocal.Process.NativeMusicRequest == 254 && legacyLocal.Process.MusicTicks == 321 && legacyLocal.Process.MusicVolume == 83 && legacyLocal.Process.MusicVolumeFade == localOnly.MusicVolumeFade, "An old local-only playable owner was lost or restarted when a no-play request replaced its local metadata.");
        Ff7FieldHost changedByModule = Host(new() { NativeMusicTrack = 7, NativeMusicRequest = 5, MusicTicks = 321, MusicVolume = 83, MusicVolumeFade = localOnly.MusicVolumeFade });
        Ff7FieldProcess next = changedByModule.CreateNextFieldProcess();
        Check(next.NativeMusicTrack == 7 && next.NativeMusicRequest == 5 && next.MusicTicks == 321 && next.MusicVolumeFade == localOnly.MusicVolumeFade, "A field change replayed the historical raw request over current module playback or discarded its metadata.");
        Check(bytes.SequenceEqual(original) && field.Scripts.Data.Span.SequenceEqual(original), "Music header resolution or request accounting modified the original field section.");
        Console.WriteLine("Native local music IDs preserve no-play requests separately from playback and retain strict section bounds.");
    }

    private static byte[] Section()
    {
        byte[] bytes = new byte[256];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x0502);
        bytes[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 116);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 3);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 512);
        foreach ((int slot, uint offset) in new[] { (0, 128u), (1, 160u), (2, 252u) })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40 + slot * 4), offset);
        }
        for (int script = 0; script < 32; script++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + script * 2), 120);
        }
        "FAKE"u8.CopyTo(bytes.AsSpan(128));
        "AKAO"u8.CopyTo(bytes.AsSpan(160));
        bytes[164] = 255;
        return bytes;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
