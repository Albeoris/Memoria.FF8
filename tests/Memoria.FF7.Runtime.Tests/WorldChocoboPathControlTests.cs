using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldChocoboPathControlTests
{
    public static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP original Chocobo path controls: configure MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(game);
        foreach (int world in new[] { 0, 2, 3 })
        {
            using Ff7WorldResources resources = Ff7WorldResources.Load(installation, world);
            var code = resources.ReadEvents().Code;
            List<string> uses = [];
            for (int pc = 1; pc < code.Count; pc++)
            {
                int opcode = code[pc];
                if (opcode is >= 0x33F and <= 0x342)
                {
                    uses.Add($"{pc:X4}:{opcode:X3}/prior={code[Math.Max(0, pc - 1)]:X4}");
                }
                if (opcode is >= 0x110 and <= 0x11F or 0x200 or 0x201)
                {
                    pc++;
                }
            }
            Check(uses.Count == 0, "Shipped world event programs must retain the audited absence of unused farm path controls33F/340/341/342.");
        }
        byte[] persistent = new byte[1280];
        persistent[0] = 152;
        persistent[29] = 255;
        using Ff7WorldRuntime runtime = new(installation, persistent, 1, new byte[] { 0, 1, 2 });
        for (int callback = 0; callback < 700 && !runtime.CanSave; callback++)
        {
            if (runtime.Process.Dialogue is { AwaitingInput: true, Completed: false } dialogue)
            {
                runtime.CompleteDialogue(dialogue.RequestId, dialogue.Selected);
            }
            runtime.Advance();
        }
        Check(runtime.CanSave, "Original world entry must establish the bounded Chocobo path fixture.");
        int leader = runtime.ControlledModel;
        Check(!Command(runtime, WorldOperation.WaitChocoboPath).Completed && Command(runtime, WorldOperation.WaitChocoboRuns).Completed, "Absent path status waits for initialization while the native empty runner set is already complete.");
        Command(runtime, WorldOperation.BeginChocoboPath);
        Check(runtime.Process.ChocoboPathControl is { InitializationRequested: true, Path: null }, "33F must retain its one-shot terrain initialization request without prematurely resolving geometry.");
        using Ff7WorldRuntime cold = new(installation, runtime.Machine.CopyPersistent(), 1, new byte[] { 0, 1, 2 });
        cold.Restore(Cold(runtime.Process));
        for (int callback = 0; callback < 40 && !Command(runtime, WorldOperation.WaitChocoboPath).Completed; callback++)
        {
            Advance(runtime, cold);
        }
        Check(Command(runtime, WorldOperation.WaitChocoboPath) is { Completed: true, Value: 1 } && runtime.Process.ChocoboPathControl is { InitializationRequested: false, Path.SearchComplete: true, Path.SearchFailed: false }, "A successful native terrain centre consumes33F and completes341 with the independently searched path.");
        Ff7WorldProcess found = runtime.Process;
        foreach (int unsupported in new[] { 1, 2 })
        {
            string before = JsonSerializer.Serialize(runtime.Process);
            try
            {
                Command(runtime, WorldOperation.RunChocoboPath, [unsupported]);
                throw new InvalidOperationException("An unused multi-runner farm contract was accepted without its native implementation.");
            }
            catch (NotSupportedException)
            {
                Check(before == JsonSerializer.Serialize(runtime.Process), "Unsupported farm actions must reject before mutating path, model, script or RNG state.");
            }
        }
        Ff7WorldChocoboRelease path = found.ChocoboPathControl!.Path!;
        runtime.Restore(found with { ChocoboPathControl = new(Path: path with { Path = [path.Path[0] with { TriedEdges = 7 }], Visited = [path.Path[0].Triangle], SearchComplete = false }) });
        runtime.Advance();
        Check(Command(runtime, WorldOperation.WaitChocoboPath) is { Completed: true, Value: -1 }, "341 completes on native failed-search status3 as well as successful status2.");

        Ff7WorldEntity player = found.Entities.Single(entity => entity.Model == leader);
        Ff7WorldEntity bird = player with { Model = 4, Flags = 2, AttachedModel = null };
        runtime.Restore(found with { Entities = [.. found.Entities.Where(entity => entity.Model != 4), bird], Vm = found.Vm with { Models = [.. found.Vm.Models.Where(model => model.Model != 4), new WorldEntitySnapshot(4, 0, 0, 0, false, 0, [])] } });
        Check(Command(runtime, WorldOperation.RunChocoboPath, [3]) is { Completed: true, ActiveModel: 4 } && runtime.Process.Chocobo?.Release is { RunPhase: 7, PathIndex: 0 }, "340 action3 selects the resident caught bird and starts its native forward path phase.");
        Check(!Command(runtime, WorldOperation.WaitChocoboRuns).Completed, "342 remains pending while the native caught-bird runner phase is nonzero.");
        cold.Restore(Cold(runtime.Process));
        for (int callback = 0; callback < 120 && !Command(runtime, WorldOperation.WaitChocoboRuns).Completed; callback++)
        {
            Advance(runtime, cold);
        }
        Check(Command(runtime, WorldOperation.WaitChocoboRuns).Completed && runtime.Process.Entities.All(entity => entity.Model != 4), "Native runner completion removes the released caught bird and completes342 without script replay.");
        Console.WriteLine("PASS original unused farm-opcode inventory, path initialization/readiness/failure, caught-bird action3, cold runner completion and pre-mutation unsupported farm rejection");
    }

    private static WorldHostReply Command(Ff7WorldRuntime runtime, WorldOperation operation, int[]? arguments = null) => runtime.Execute(new(runtime.Process.Vm.NextRequestId, runtime.ControlledModel, runtime.ControlledModel, 0, new(operation, arguments ?? [])));

    private static Ff7WorldProcess Cold(Ff7WorldProcess state) => JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(state))!;

    private static void Advance(Ff7WorldRuntime runtime, Ff7WorldRuntime cold)
    {
        runtime.Advance();
        cold.Advance();
        Check(JsonSerializer.Serialize(runtime.Process) == JsonSerializer.Serialize(cold.Process), "Native path search and caught-bird runners must continue independently and identically after cold restore.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
