using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInnAnimationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_INN_ANIMATION_SAVE") ?? throw new InvalidOperationException("Select a native inn continuation fixture.");
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
        bool childReturned = false;
        bool rootReturned = false;
        bool cleanupDispatched = false;
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
            session.NativeTickCompleted += tick =>
            {
                childReturned |= tick.ScriptReturns.Any(owner => owner.Entity == 13 && owner.Priority == 6);
                rootReturned |= tick.ScriptReturns.Any(owner => owner.Entity == 4 && owner.Priority == 1);
                cleanupDispatched |= (tick.ScriptActivity ?? []).Any(activity => activity.Dispatch is { Accepted: true, TargetEntity: 13, TargetPriority: 6, TargetScript: 4 });
            };
            controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
            controller.Start();
        }
        try
        {
            Open(envelope);
            var original = policy!.State.Active!;
            for (int poll = 0; poll < 8 && policy.State.PendingRestore is null && controller!.Error is null; poll++)
            {
                controller.Advance(1);
                callbacks++;
            }
            bool consumed = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId).Conditions?.HasExecutedEntry(original.ConditionFingerprint) == true;
            if (!consumed || childReturned || policy.State.PendingRestore is null || session!.Machine.Capture().Entities[13].Priority != 6)
            {
                throw new InvalidOperationException("The genuine returned inn source did not complete while preserving its native presentation child.");
            }
            Stopwatch timer = Stopwatch.StartNew();
            bool replayed = false;
            while (controller!.Error is null && timer.Elapsed.TotalSeconds < 30 && callbacks < 2000 && !(childReturned && rootReturned && session!.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0D) == 0 && session.Machine.Capture().Entities[13].Priority == 7 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled))
            {
                if (policy!.State.PendingRestore is { } request)
                {
                    Ff7AutoPlayState state = policy.State.Clone();
                    state.PendingRestore = null;
                    CombinedSessionSave saved = memory.TryGetValue(request.Path, out var temporary) ? temporary : Read(request.Path);
                    Open(saved with { Ff7 = saved.Ff7! with { AutoPlay = state } });
                    replayed = true;
                }
                controller!.Advance(1);
                callbacks++;
            }
            Console.WriteLine($"Inn presentation continuation: callbacks={callbacks}, tick={session!.Machine.Tick}, consumed={consumed}, replayed={replayed}, realChildReturn={childReturned}, realRootReturn={rootReturned}, flag={session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0D)}, childPriority={session.Machine.Capture().Entities[13].Priority}, controls={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, error={controller.Error}");
            if (!replayed || !cleanupDispatched || !childReturned || !rootReturned || session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0D) != 0 || session.Machine.Capture().Entities[13].Priority != 7 || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || controller.Error is not null || hashes.Any(item => !item.Value.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(item.Key)))))
            {
                throw new InvalidOperationException("The ordinary native alternative did not perform its actual presentation cleanup and control return unchanged.");
            }
        }
        finally
        {
            policy?.Dispose();
            session?.Dispose();
        }
    }
}
