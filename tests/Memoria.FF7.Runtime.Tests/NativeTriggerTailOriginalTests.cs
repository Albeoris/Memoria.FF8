using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTriggerTailOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRIGGER_TRANSITION_SAVE") ?? throw new InvalidOperationException("Select a native field trigger transition fixture.");
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
        Console.WriteLine($"Native trigger transition: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}/{session.Field.Name}, tick={session.Machine.Tick}, arrows={session.Field.Triggers.Arrows.Count}, story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || session.MapId != 283 || session.FieldGeneration <= source.FieldGeneration || session.Field.Triggers.Arrows.Count != 0 || session.Field.Triggers.Gateways.Count != 12 || session.Field.Triggers.Triggers.Count != 12 || session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 1181 || session.Progress.CompletedBattles.Length != source.Progress.CompletedBattles.Length || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original accepted field transition did not load its bounded native trigger records.", controller.Failure);
        }
    }
}
