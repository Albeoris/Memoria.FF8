using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class NativeOwnedInnContinuationTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_INN_SAVE") ?? throw new InvalidOperationException("Select an owned inn fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        CombinedSessionSave Read(string selected)
        {
            using FileStream input = File.OpenRead(selected);
            return CombinedSessionCodec.Read(input);
        }
        CombinedSessionSave original = Read(path);
        Ff7AutoPlayState initial = original.Ff7!.AutoPlay!;
        string key = initial.Choice!.Key;
        var coverage = initial.Coverage.Choices.Single(item => item.Key == key);
        string[] originals = [path, coverage.BeforeCheckpoint!, coverage.ContinuationCheckpoint!];
        Dictionary<string, byte[]> hashes = originals.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(item => item, item => SHA256.HashData(File.ReadAllBytes(item)), StringComparer.OrdinalIgnoreCase);
        string directory = Path.Combine(Path.GetTempPath(), "memoria-owned-inn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Ff7Session? session = null;
        Ff7AutoPlayPolicy? policy = null;
        Ff7AutoPlayController? controller = null;
        CombinedSessionSave envelope = original;
        long callbacks = 0;
        string? last = null;
        bool learned = false;
        bool continued = false;
        bool exited = false;
        try
        {
            void Restore(CombinedSessionSave saved)
            {
                callbacks += controller?.Steps ?? 0;
                policy?.Dispose();
                session?.Dispose();
                envelope = saved;
                session = Ff7Session.Restore(Ff7Installation.Discover(game), saved.Ff7!);
                policy = new(session, (name, state) =>
                {
                    string output = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + ".ffsave");
                    CombinedSessionCodec.Save(output, envelope with { Ff7 = state });
                    return output;
                });
                controller = new(session, driver =>
                {
                    var result = policy.ChooseInput(driver);
                    continued |= learned && policy.State.Choice is null && policy.State.PendingRestore is null && (result.Horizontal != 0 || result.Vertical != 0);
                    return result;
                }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
                controller.Start();
            }
            Restore(original);
            Ff7SessionSave before = Read(coverage.BeforeCheckpoint!).Ff7!;
            var owner = Ff7AutoPlayChoiceWorkflow.ReadOwnedServicePrompt(before, initial.Choice.BeforeActive!);
            if (owner is null)
            {
                throw new InvalidOperationException("The original service prompt has no authenticated executing owner.");
            }
            var proof = session!.ReadChoiceServiceProof(owner.Entity, owner.ProgramCounter);
            Console.WriteLine($"Original owned service certificate: owner={owner.Entity}/{owner.Priority}/{owner.ProgramCounter}, complete={proof.Complete}, reason={proof.Reason}");
            Stopwatch timer = Stopwatch.StartNew();
            for (int polls = 0; polls < 3000 && timer.Elapsed.TotalSeconds < 30 && !exited; polls++)
            {
                if (policy!.State.PendingRestore is { } restore)
                {
                    Ff7AutoPlayState state = policy.State.Clone();
                    state.PendingRestore = null;
                    CombinedSessionSave selected = Read(restore.Path);
                    Restore(selected with { Ff7 = selected.Ff7! with { AutoPlay = state } });
                }
                else
                {
                    controller!.Advance(1);
                }
                learned |= policy!.State.ChoicePreferences.ContainsKey(key);
                exited |= learned && session!.Field.Name != original.Ff7.FieldName;
                string diagnostic = policy.State.ChoiceContinuationDiagnostic ?? string.Empty;
                if (last != diagnostic)
                {
                    last = diagnostic;
                    Console.WriteLine($"INN tick={session!.Machine.Tick} gil={session.Progress.Gil} learned={learned} {diagnostic}");
                }
                if (controller!.Error is not null)
                {
                    throw new InvalidOperationException(controller.Error);
                }
                if (session!.Progress.Gil < original.Ff7.Progress.Gil)
                {
                    break;
                }
            }
            Console.WriteLine($"Original owned inn continuation: callbacks={callbacks + controller!.Steps}, field={session!.Field.Name}, tick={session.Machine.Tick}, gil={session.Progress.Gil}, initialGil={original.Ff7.Progress.Gil}, learned={learned}, moved={continued}, exited={exited}, active={policy!.State.Active?.EventId}, status={policy.State.Status}");
            if (!learned || !continued || !exited || session.Progress.Gil != original.Ff7.Progress.Gil)
            {
                throw new InvalidOperationException("The normal owned inn continuation did not learn a dominating actual continuation and continue without another paid stay.");
            }
        }
        finally
        {
            policy?.Dispose();
            session?.Dispose();
            foreach (var source in hashes)
            {
                if (!source.Value.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(source.Key))))
                {
                    throw new InvalidOperationException("An original inn source or checkpoint dependency changed.");
                }
            }
            foreach (string generated in Directory.GetFiles(directory))
            {
                File.Delete(generated);
            }
            Directory.Delete(directory);
        }
    }
}
