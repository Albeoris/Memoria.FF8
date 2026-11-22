using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutonomousFadeOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTONOMOUS_FADE_SAVE") ?? throw new InvalidOperationException("Select a native autonomous fade fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The accepted field transition requires no new choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && session.FieldGeneration == source.FieldGeneration && timer.Elapsed.TotalSeconds < 10)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native autonomous fade: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}/{session.Field.Name}, tick={session.Machine.Tick}, story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || session.MapId != 324 || session.FieldGeneration <= source.FieldGeneration || session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 1186 || session.Progress.CompletedBattles.Length != source.Progress.CompletedBattles.Length || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original accepted field transition did not finish its real fade and autonomous field transition.", controller.Failure);
        }
    }
}
