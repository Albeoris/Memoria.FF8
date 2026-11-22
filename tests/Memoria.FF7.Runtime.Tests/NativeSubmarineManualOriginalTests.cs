using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSubmarineManualOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SUBMARINE_MANUAL_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The selected continuation must reuse its actual choice checkpoint."));
        string key = policy.State.Choice!.Key;
        long request = policy.State.Choice.NativeOwner!.RequestId;
        bool returned = false;
        Action<FieldScriptReturn>? prior = session.Machine.ScriptReturned;
        session.Machine.ScriptReturned = value =>
        {
            prior?.Invoke(value);
            returned |= value.Entity == 9 && value.ProgramCounter == 0x0C9C;
        };
        Ff7AutoPlayController controller = new(session, driver => session.Host.Process.PlayerLocked && policy.State.Choice?.Key == key ? default : policy.ChooseInput(driver), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 30 && controller.Steps < 4096)
        {
            controller.Advance(1);
            if (policy.State.Coverage.Choices.Single(choice => choice.Key == key).ActiveOption == 3)
            {
                break;
            }
        }
        var coverage = policy.State.Coverage.Choices.Single(choice => choice.Key == key);
        Console.WriteLine($"Submarine manual: callbacks={controller.Steps}, tick={session.Machine.Tick}, seconds={timer.Elapsed.TotalSeconds:F3}, nativeChildReturned={returned}, option={coverage.ActiveOption}, deferred={string.Join(',', coverage.PolicyDeferredOptions)}, window={session.Host.Process.Dialogues.FirstOrDefault()?.Text}/{session.Host.Process.Dialogues.FirstOrDefault()?.Page}/{session.Host.Process.Dialogues.FirstOrDefault()?.TokenPosition}, error={controller.Error}.");
        if (controller.Error is not null || !returned || coverage.ActiveOption != 3 || !coverage.PolicyDeferredOptions.Contains(2) || coverage.CompletedOptions.Contains(2) || policy.State.Choice?.NativeOwner?.RequestId == request || policy.State.SourceGoal is not { AcceptedEntry: 0x10E6, Completed: false } || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The actual manual did not return and select its next real mission option.", controller.Failure);
        }
    }
}
