using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalTransitionOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SOURCE_TRANSITION_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("This bounded continuation must not replay a choice."));
        Check(session.MapId == 426 && policy.State.Active is null && policy.State.SourceGoal is { Completed: false, AcceptedEntry: 2740 }, "The selected fixture is not the retained post-transition planning boundary.");
        string native = JsonSerializer.Serialize(new { saved.Vm, saved.Process, saved.Progress, policy.State.Coverage });
        policy.SetSourceGoal(null);
        Ff7SessionSave cleared = session.Capture();
        Check(native == JsonSerializer.Serialize(new { cleared.Vm, cleared.Process, cleared.Progress, policy.State.Coverage }), "Clearing an already retired planning intent changed native gameplay or history.");
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        controller.Advance(1);
        Check(controller.Error is null && policy.State.SourceGoal is null && session.MapId == 426 && session.Machine.Tick > saved.Vm.Tick, "The retained post-boss checkpoint could not continue normally.");
        Ff7Progress progress = session.Capture().Progress!;
        Check(JsonSerializer.Serialize(new { progress.CompletedBattles, progress.Gil, progress.Inventory, progress.Materia }) == JsonSerializer.Serialize(new { saved.Progress!.CompletedBattles, saved.Progress.Gil, saved.Progress.Inventory, saved.Progress.Materia }), "The bounded recovery changed recorded boss rewards.");
        Check(SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original checkpoint was modified.");
        Console.WriteLine($"Post-transition recovery: map={session.MapId}, tick={session.Machine.Tick}, callbacks={controller.Steps}; native progress preserved, retired intent cleared normally.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
