using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMusicIdOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MUSIC_ID_SAVE") ?? throw new InvalidOperationException("Select a native music transition fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        int? physicalTrack = session.Host.Process.NativeMusicTrack;
        long musicTicks = session.Host.Process.MusicTicks;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the music entry."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 385 && session.Machine.Tick > 0, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Steps < 1000 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native music ID entry: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, requested={session.Host.Process.NativeMusicRequest}, physical={session.Host.Process.NativeMusicTrack}, musicTicks={session.Host.Process.MusicTicks}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !controller.Completed || session.Host.Process.NativeMusicRequest != -1 || session.Host.Process.NativeMusicTrack != physicalTrack || session.Host.Process.MusicTicks < musicTicks || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original no-play music request did not preserve physical playback across its real transition.", controller.Failure);
        }
        _ = session.Capture();
    }
}
