using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReactorEntryTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REACTOR_SAVE") ?? throw new InvalidOperationException("Select an external reactor entry fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7Installation installation = Ff7Installation.Discover(game);
        string directory = Path.Combine(Path.GetTempPath(), "memoria-native-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Ff7Session? session = null;
        Ff7AutoPlayPolicy? policy = null;
        Ff7AutoPlayController? controller = null;
        bool admitted = false;
        int? admittedHeading = null;
        int checkpoints = 0;
        int restores = 0;
        long callbacks = 0;
        try
        {
            Ff7SessionSave native = envelope.Ff7 ?? throw new InvalidOperationException("The fixture has no native continuation.");
            var goal = native.AutoPlay?.NavigationGoal ?? throw new InvalidOperationException("The fixture has no retained native entry goal.");
            int field = int.Parse(goal.NavigationKey.Split(':')[1]);
            string[] identity = goal.EventId!.Split(':');
            int entity = int.Parse(identity[3]);
            int script = int.Parse(identity[5]);
            string Write(string key, Ff7SessionSave snapshot)
            {
                string target = Path.Combine(directory, (++checkpoints).ToString() + ".ffsave");
                using FileStream output = File.Create(target);
                CombinedSessionCodec.Write(output, envelope with { Ff7 = snapshot });
                return target;
            }
            void Start(Ff7SessionSave snapshot, bool freshAttempt)
            {
                policy?.Dispose();
                session?.Dispose();
                session = Ff7Session.Restore(installation, snapshot);
                policy = new(session, Write);
                if (freshAttempt)
                {
                    // Mirror the existing explicit App start's observational watchdog window.
                    policy.State.FrontierProgressKey = null;
                    policy.State.FrontierIdleSteps = 0;
                }
                session.NativeInteractionScriptRequested += admission =>
                {
                    if (session.MapId == field && admission.Entity == entity && admission.Script == script && admission.Accepted && admission.Started)
                    {
                        admittedHeading = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Direction;
                        Check(admittedHeading > 104 && admittedHeading < 232, "The actual native pickup entry was admitted outside its original facing guard.");
                        admitted = true;
                    }
                };
                int? Choose(Ff7Dialogue dialogue)
                {
                    string before = JsonSerializer.Serialize(session.CaptureAutoPlayNavigation());
                    int? selection = policy.ChooseDialogueSelection(dialogue);
                    Check(before == JsonSerializer.Serialize(session.CaptureAutoPlayNavigation()), "Choice evaluation changed actual native state, RNG or prompt before selection.");
                    return selection;
                }
                controller = new(session, policy.ChooseInput, chooseDialogueSelection: Choose, readPlanningProgress: () => policy.WorkProgress);
                controller.Start();
            }
            Start(native, true);
            Check((session!.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 0x50) & 128) == 0, "The fixture already has its native pickup completion flag.");
            int beforeMateria = session.Progress.Materia.Count(materia => materia.MateriaId == 78);
            Stopwatch timer = Stopwatch.StartNew();
            while (controller!.Running && timer.Elapsed < TimeSpan.FromSeconds(30))
            {
                long before = controller.Steps;
                controller.Advance(1);
                callbacks += controller.Steps - before;
                if ((session!.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 0x50) & 128) != 0 && session.Progress.Materia.Count(materia => materia.MateriaId == 78) > beforeMateria)
                {
                    break;
                }
                if (policy!.State.PendingRestore is { } request)
                {
                    Ff7AutoPlayState retained = policy.State.Clone();
                    retained.PendingRestore = null;
                    using FileStream saved = File.OpenRead(request.Path);
                    Ff7SessionSave restored = CombinedSessionCodec.Read(saved).Ff7 ?? throw new InvalidOperationException("A native choice checkpoint lost its native continuation.");
                    Start(restored with { AutoPlay = retained }, false);
                    restores++;
                }
            }
            Check(controller!.Error is null && admitted && (session!.Machine.Variables.ReadByte(FieldBank.Persistent4Byte, 0x50) & 128) != 0 && session.Progress.Materia.Count(materia => materia.MateriaId == 78) > beforeMateria, $"The normal controller did not complete its unconsumed reactor entry: {controller.Error}; status={policy!.Status}; callbacks={callbacks}; admitted={admitted}; heading={admittedHeading}.");
            Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Reactor entry continuation changed its authoritative source.");
            Console.WriteLine($"PASS original native reactor entry heading={admittedHeading}, pickup flag and materia award; callbacks={callbacks}; checkpoints={checkpoints}; restores={restores}; elapsed={timer.Elapsed.TotalSeconds:F3}s; source unchanged");
        }
        finally
        {
            policy?.Dispose();
            session?.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
