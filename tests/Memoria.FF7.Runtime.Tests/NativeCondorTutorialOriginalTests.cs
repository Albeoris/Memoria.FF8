using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCondorTutorialOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONDOR_TUTORIAL_SAVE") ?? throw new InvalidOperationException("Select a native tutorial continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The original continuation must reuse its actual registered choice checkpoint."));
        bool exited = false;
        Action<FieldScriptReturn>? previous = session.Machine.ScriptReturned;
        session.Machine.ScriptReturned = value =>
        {
            previous?.Invoke(value);
            exited |= value.Entity == 10 && value.ProgramCounter == 0x0D69;
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        FieldHostRequest? Prompt() => session.Machine.Capture().PendingRequests?.FirstOrDefault(request => request.Command is FieldDialogueCommand { FirstChoice: not null, LastChoice: not null } && request.ProgramCounter != 0x0ACB);
        while (controller.Error is null && !(exited && Prompt() is not null) && timer.Elapsed.TotalSeconds < 20)
        {
            controller.Advance(1);
        }
        FieldHostRequest? prompt = Prompt();
        var tutorial = policy.State.Coverage.Choices.First(choice => choice.OptionCount == 6 && choice.FirstOption == 1);
        Console.WriteLine($"Native tutorial continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Machine.Tick}, childReturned={exited}, prompt={prompt}, activeOption={tutorial.ActiveOption}, deferred={string.Join(',', tutorial.PolicyDeferredOptions)}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !exited || prompt is null || tutorial.ActiveOption != 6 || tutorial.CompletedOptions.Any(option => option is >= 1 and <= 5) || !tutorial.PolicyDeferredOptions.IsSupersetOf([1, 2, 3, 4, 5]) || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original explanation did not finish through its real native exit and parent choice continuation.", controller.Failure);
        }
    }
}

