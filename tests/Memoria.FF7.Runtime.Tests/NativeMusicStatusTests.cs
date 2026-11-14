using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMusicStatusTests
{
    private static long nextRequestId;

    public static void Run()
    {
        nextRequestId = 0;
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native music status: select original FFVII resources.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        Ff7FieldResources farm = Ff7FieldResources.Load(archive, "farm");
        Ff7NativeMusicTimeline walz = Ff7NativeMusicTimeline.Load(installation, 22);
        Ff7NativeMusicTimeline bokujo = Ff7NativeMusicTimeline.Load(installation, 32);
        int descriptions = 0;
        Ff7NativeMusicTimeline Describe(int track)
        {
            descriptions++;
            return track switch { 22 => walz, 32 => bokujo, _ => throw new InvalidOperationException("Unexpected farm music timeline.") };
        }
        Ff7FieldHost Host(Ff7FieldProcess process) => new(farm, (_, _) => 1, process) { NativeMusicTimeline = Describe };
        Ff7FieldHost host = Host(new() { NativeMusicTrack = 32, MusicTicks = 90, MusicVolume = 0 });
        Check(Query(host) == 1, "The reached farm loop must remain logically playing even with zero volume and no audio device.");
        Command(host, new FieldMusicChangeCommand(1, 0x14), 0x079F);
        Check(host.Process.NativeMusicTrack == 22 && host.Process.MusicTicks == 0 && Query(host) == 1, "Native FD local track1 must resolve the farm WALZ identity through its original AKAO header.");
        for (int callback = 0; callback < 952; callback++)
        {
            host.AdvanceNativeMusicClock();
        }
        Check(Query(host) == 1, "WALZ must remain active at callback952, immediately before its original MIDI endpoint.");
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceNativeMusicClock();
        cold.AdvanceNativeMusicClock();
        string before = AudioFacts(host);
        Check(Query(host) == 0 && Query(cold) == 0 && before == AudioFacts(host) && before == AudioFacts(cold), "WALZ ends at callback953 independently after cold restore; FE only reads and never advances or changes its logical owner.");
        host.PlayNativeMusic(22);
        Check(host.Process.MusicTicks == 0 && Query(host) == 1, "An explicit replay of the same ended finite native track must restart its logical clock.");
        host.AdvanceNativeMusicClock();
        host.PlayNativeMusic(22);
        Check(host.Process.MusicTicks == 1, "A still-playing same native track must preserve its clock.");
        Command(host, new FieldMusicCommand(FieldMusicOperation.Play, 0));
        Check(host.Process.NativeMusicTrack == 32 && Query(host) == 1, "The original farm restore command must return to its loop without a progress write.");
        Ff7FieldHost locked = Host(host.Capture() with { MusicLock = 1 });
        string lockedBefore = AudioFacts(locked);
        int reads = descriptions;
        Check(Command(locked, new FieldMusicChangeCommand(999, 0x14)).Completed && lockedBefore == AudioFacts(locked) && reads == descriptions, "Locked FD must complete before resolving even an invalid local AKAO index, publishing audio or reading timeline resources.");
        host.SubmitNativeAudio(new(0xF0));
        reads = descriptions;
        Check(Query(host) == 0 && descriptions == reads, "An explicitly stopped music owner must return zero without requesting a track resource.");
        Ff7FieldHost legacy = Host(new() { Music = new(FieldMusicOperation.Play, 0), MusicTicks = 1234 });
        Check(Query(legacy) == 1, "Older snapshots with only recorded local music and logical age must derive the original loop owner without added save fields.");
        Ff7FieldHost stub = new(farm, (_, _) => 1, new Ff7FieldProcess { NativeMusicTrack = 22, MusicTicks = 50 });
        stub.PlayNativeMusic(22);
        Check(stub.Process.MusicTicks == 50, "Existing resource-free host stubs preserve same-track behavior until a native status source is configured.");
        try
        {
            Query(stub);
            throw new InvalidOperationException("An active native query without its resource contract was accepted.");
        }
        catch (NotSupportedException)
        {
        }
        Ff7FieldResources upperJunon = Ff7FieldResources.Load(archive, "junair");
        Check(upperJunon.Scripts.Data.Span.Slice(0x0710, 2).SequenceEqual(new byte[] { 0xF0, 4 }), "The reached upper-Junon music request changed.");
        Ff7NativeMusicTimeline wind = Ff7NativeMusicTimeline.Load(installation, 65);
        Ff7FieldHost windHost = new(upperJunon, (_, _) => 1, new Ff7FieldProcess { NativeMusicTrack = 65, MusicTicks = 308 }) { NativeMusicTimeline = _ => wind };
        Command(windHost, new FieldMusicCommand(FieldMusicOperation.Play, 4), 0x0710);
        Check(windHost.Process.NativeMusicTrack == 65 && windHost.Process.MusicTicks == 0 && Query(windHost) == 0, "The original same-track WIND request must complete when its native MIDI archive member is absent, without claiming playback.");
        Ff7FieldHost windCold = new(upperJunon, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(windHost.Capture()))!) { NativeMusicTimeline = _ => wind };
        Check(Query(windCold) == 0 && AudioFacts(windCold) == AudioFacts(windHost), "An absent native MIDI member must preserve stopped status and exact recorded audio facts after cold restore.");
        Console.WriteLine("PASS original farm FD/FE ownership, exact finite MIDI endpoint, loop/stop/replay, zero-volume cold continuation and locked pre-resource completion");
    }

    private static int Query(Ff7FieldHost host)
    {
        string before = AudioFacts(host);
        int receipts = host.Capture().Receipts.Length;
        FieldHostReply reply = Command(host, new FieldMusicPlayingQuery());
        Check(reply.Completed && reply.Value is 0 or 1, "FE must return its normalized status synchronously.");
        Check(before == AudioFacts(host) && host.Capture().Receipts.Length == receipts + 1, "Each fresh FE request adds its normal host receipt without changing process/audio facts.");
        return reply.Value;
    }

    private static FieldHostReply Command(Ff7FieldHost host, FieldCommand command, int pc = 0) => host.Execute(new(++nextRequestId, 9, 2, pc, command));

    private static string AudioFacts(Ff7FieldHost host) => JsonSerializer.Serialize(host.Capture() with { Receipts = [] });

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
