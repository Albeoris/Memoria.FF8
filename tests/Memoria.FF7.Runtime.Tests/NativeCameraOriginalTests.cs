using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCameraOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_CAMERA_SAVE") ?? throw new InvalidOperationException("Select a native restored-continuation fixture.");
        Dictionary<string, byte[]> hashes = [];
        CombinedSessionSave Read(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            hashes.TryAdd(file, SHA256.HashData(bytes));
            using MemoryStream input = new(bytes, writable: false);
            return CombinedSessionCodec.Read(input);
        }
        CombinedSessionSave envelope = Read(path);
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Dictionary<string, CombinedSessionSave> memory = [];
        Ff7Session? session = null;
        Ff7AutoPlayPolicy? policy = null;
        Ff7AutoPlayController? controller = null;
        bool returned = false;
        int callbacks = 0;
        void Open(CombinedSessionSave saved)
        {
            policy?.Dispose();
            session?.Dispose();
            envelope = saved;
            session = Ff7Session.Restore(installation, saved.Ff7!);
            policy = new(session, (key, native) =>
            {
                string id = "memory:" + key;
                memory[id] = envelope with { Ff7 = native };
                return id;
            });
            session.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(owner => owner.Entity == 18 && owner.Priority == 1);
            controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
            controller.Start();
        }
        try
        {
            Open(envelope);
            string key = policy!.State.Choice!.Key;
            var originalCoverage = policy.State.Coverage.Choices.Single(item => item.Key == key);
            string? primary = originalCoverage.ContinuationCheckpoint;
            int[] completed = originalCoverage.CompletedOptions.ToArray();
            string native = JsonSerializer.Serialize(session!.Capture() with { AutoPlay = null });
            controller!.Advance(1);
            if (policy.State.PendingRestore is null || controller.Steps != 0 || native != JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }))
            {
                throw new InvalidOperationException("A queued native restore advanced or changed the old native scene.");
            }
            Ff7AutoPlayState restored = policy.State.Clone();
            string beforePath = restored.PendingRestore!.Path;
            restored.PendingRestore = null;
            CombinedSessionSave before = Read(beforePath);
            Open(before with { Ff7 = before.Ff7! with { AutoPlay = restored } });
            Stopwatch timer = Stopwatch.StartNew();
            while (controller!.Error is null && timer.Elapsed.TotalSeconds < 60 && callbacks < 6000 && policy!.State.PendingRestore is null && !returned)
            {
                controller.Advance(1);
                callbacks++;
            }
            for (int poll = 0; poll < 4 && returned && policy!.State.PendingRestore is null && controller.Error is null; poll++)
            {
                controller.Advance(1);
                callbacks++;
            }
            var coverage = policy!.State.Coverage.Choices.Single(item => item.Key == key);
            Console.WriteLine($"Restored continuation: callbacks={callbacks}, tick={session!.Machine.Tick}, seconds={timer.Elapsed.TotalSeconds:F3}, realRootReturn={returned}, completed={string.Join(',', coverage.CompletedOptions)}, restore={policy.State.PendingRestore is not null}, controls={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, status={policy.State.Status}, error={controller.Error}");
            if (!returned || controller.Error is not null || !coverage.CompletedOptions.Contains(1) || completed.Any(option => !coverage.CompletedOptions.Contains(option)) || coverage.ContinuationCheckpoint != primary || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || hashes.Any(item => !item.Value.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(item.Key)))))
            {
                throw new InvalidOperationException("The registered current alternative did not produce its own native return while preserving primary coverage and original checkpoints.");
            }
        }
        finally
        {
            policy?.Dispose();
            session?.Dispose();
        }
    }
}
