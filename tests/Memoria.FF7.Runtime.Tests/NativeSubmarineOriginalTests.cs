using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSubmarineOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SUBMARINE_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The affected module boundary must preserve its live committed choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        controller.Advance(1);
        Check(controller.Error is null && session.Submarine is not null, "The actual unfinished minigame command did not initialize its native submarine module.");
        Check(policy.State.Choice is { NativeExecutionCommitted: true }, "The actual module admission left its selected native choice replayable.");
        Ff7SessionSave entered = session.Capture();
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(entered))!);
        for (int tick = 0; tick < 8; tick++)
        {
            ushort held = Ff7SubmarineAutoPlay.ChooseInput(session.Submarine!);
            ushort pressed = (ushort)(held & ~session.Submarine!.PreviousInput);
            session.SetSubmarineInput(held, pressed);
            cold.SetSubmarineInput(held, pressed);
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The actual submarine continuation diverged after a JSON cold restore.");
        }
        long generation = session.FieldGeneration;
        int priorHealth = session.Submarine!.Frame.Ships[0].Health;
        int damage = 0;
        int outcome = 0;
        long resultTick = -1;
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && session.Submarine is not null && timer.Elapsed.TotalSeconds < 45 && controller.Steps < 10000)
        {
            controller.Advance(1);
            if (session.Submarine is not { } module)
            {
                break;
            }
            int health = module.Frame.Ships[0].Health;
            if (health < priorHealth)
            {
                damage += priorHealth - health;
            }
            priorHealth = health;
            if (module.Phase == Ff7SubmarinePhase.Result && resultTick < 0)
            {
                outcome = module.Frame.Outcome;
                resultTick = module.Tick;
            }
        }
        Console.WriteLine($"Original submarine: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, moduleTick={session.Submarine?.Tick}, phase={session.Submarine?.Phase}, objectiveHP={priorHealth}, actualDamage={damage}, outcome={outcome}, resultTick={resultTick}, map={session.MapId}, generation={session.FieldGeneration}, error={controller.Error}.");
        Check(controller.Error is null && session.Submarine is null && session.FieldGeneration == generation + 1 && damage >= 6000 && outcome == 1 && resultTick >= 0, "The real native submarine mission did not return after actual objective damage and its result countdown.");
        Check(session.Machine.Variables.ReadWord(FieldBank.Persistent3Word, 0x5E) == 1 && policy.State.Active is { TransitionOwned: true }, "The actual module result or owned native field return was lost.");
        Check(SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original module-boundary checkpoint was modified.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
