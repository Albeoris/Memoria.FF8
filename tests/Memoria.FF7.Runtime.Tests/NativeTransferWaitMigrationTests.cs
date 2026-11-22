using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTransferWaitMigrationTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REPLACED_WAITER_SAVE")!;
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF7_REPLACED_WAITER_OUTPUT")!;
        byte[] original = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(original));
        Check(hash == Environment.GetEnvironmentVariable("MEMORIA_FF7_REPLACED_WAITER_SHA256") && Path.GetFullPath(path) != Path.GetFullPath(output), "The selected migration source or output does not match its explicit authorization.");
        using MemoryStream input = new(original, false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave source = envelope.Ff7!;
        FieldHostRequest request = source.Vm.PendingRequests!.Single();
        FieldEntitySnapshot entity = source.Vm.Entities[1];
        Check(request is { Id: 98, Entity: 1, Priority: 7, ProgramCounter: 0x490, Command: FieldActorTurn { Direction: 240, DirectionPolicy: 2, Steps: 8, Mode: 1 } } && entity.Priority == 7 && entity.ProgramCounter == 0x42A && entity.SavedProgramCounters[7] == 0x490, "The selected save does not contain the explicitly proved abandoned transfer waiter.");
        Ff7SessionSave migrated = source with { Vm = source.Vm with { PendingRequests = [] } };
        CombinedSessionSave result = envelope with { Ff7 = migrated };
        Check(JsonSerializer.Serialize(result with { Ff7 = migrated with { Vm = migrated.Vm with { PendingRequests = source.Vm.PendingRequests } } }) == JsonSerializer.Serialize(envelope), "Migration changed state outside the one authorized VM wait.");
        if (!File.Exists(output))
        {
            using FileStream destination = new(output, FileMode.CreateNew);
            CombinedSessionCodec.Write(destination, result);
        }
        using FileStream saved = File.OpenRead(output);
        CombinedSessionSave restored = CombinedSessionCodec.Read(saved);
        Check(JsonSerializer.Serialize(restored) == JsonSerializer.Serialize(result), "The migration output differs from the authorized semantic change.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), restored.Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("This bounded ownership continuation must not create an exploration anchor."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int i = 0; i < 8 && controller.Error is null; i++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null, controller.Failure?.ToString() ?? controller.Error ?? "Continuation failed.");
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash, "Migration changed its original save.");
        Console.WriteLine($"Explicit replaced-waiter migration: source={hash}, output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))}, callbacks={controller.Steps}, map={session.MapId}, tick={session.Machine.Tick}, story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
