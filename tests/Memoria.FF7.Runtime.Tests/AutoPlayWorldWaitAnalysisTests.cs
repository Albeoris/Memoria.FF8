using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldWaitAnalysisTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original world resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_WAIT_SAVE") ?? throw new InvalidOperationException("Select an external world-dialogue analysis boundary.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The fixture has no native world allocation.");
        var events = world.Resources.ReadEvents();
        WorldProgram program = new(events);
        Ff7WorldProcess snapshot = world.Process;
        WorldMachine analyzer = new(program, session.Machine.Capture().PersistentVariables.ToArray(), new RejectHost());
        analyzer.Restore(snapshot.Vm);
        string before = JsonSerializer.Serialize(session.Capture());
        foreach (int selector in new[] { 0, 1, 2, 3, 4, 13, 15 })
        {
            int? projected = Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, snapshot.ControlledModel, selector), snapshot.ControlledModel, snapshot.Entities, snapshot.FreeEntities);
            if (projected != world.ReadSpecial(selector, snapshot.ControlledModel))
            {
                throw new InvalidOperationException("The native entity scalar projection differs from its execution reader.");
            }
        }
        Ff7WorldEntity freed = snapshot.Entities.Single(entity => entity.Model == snapshot.ControlledModel) with { TargetDirection = 0x5A0 };
        if (Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, freed.Model, 4, 0), snapshot.ControlledModel, snapshot.Entities, [freed]) != 0x5A
            || Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, freed.Model + 1, 4, 0), snapshot.ControlledModel, snapshot.Entities, [freed]) is not null
            || Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, freed.Model, 4, 1), snapshot.ControlledModel, snapshot.Entities, [freed]) is not null
            || Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, freed.Model, 16), snapshot.ControlledModel, snapshot.Entities, [freed]) is not null)
        {
            throw new InvalidOperationException("World scalar projection lost free-pool ownership or sampled an unsupported random value.");
        }
        int inspected = 0;
        int complete = 0;
        foreach (var function in events.Functions.Where(function => (function.Header & 0x8000) != 0).GroupBy(function => function.Offset).Select(group => group.First()))
        {
            int chunk = function.Header >> 4 & 0x3FF;
            int script = (function.Header & 15) + 3;
            WorldScriptAnalysis analysis = analyzer.AnalyzeMeshDispatch(chunk % 36, chunk / 36, script, readHost: dependency => Ff7WorldRuntime.ProjectAutoPlayHostDependency(dependency, snapshot.ControlledModel, snapshot.Entities, snapshot.FreeEntities));
            if (!analysis.ProgramCounters.Any(pc => program.ReadInstruction(pc).Opcode is 0x32D or 0x32E or 0x341 or 0x342))
            {
                continue;
            }
            if (analysis.HostReads.Any(read => read.Kind == WorldHostDependencyKind.OperationResult && read.Selector is 0x32D or 0x32E or 0x341 or 0x342))
            {
                throw new InvalidOperationException("An original world acknowledgement still requires a fabricated snapshot value.");
            }
            inspected++;
            complete += analysis.Complete ? 1 : 0;
            Ff7AutoPlayWorldScriptObservation? catalog = world.ReadAutoPlayFrontierCatalog(chunk % 36, chunk / 36).Scripts.SingleOrDefault(item => item.Script == script);
            if (catalog is not { Complete: true })
            {
                throw new InvalidOperationException($"The reached original wait source has no complete native catalog: {catalog?.Reason ?? "missing source geometry"}.");
            }
            if (!analysis.Complete)
            {
                Console.WriteLine($"Original wait source {function.Header:X4}: {analysis.Reason}; host reads={JsonSerializer.Serialize(analysis.HostReads)}");
            }
        }
        if (inspected == 0 || complete == 0 || before != JsonSerializer.Serialize(session.Capture()) || !bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("Original native wait inspection found no reached waits or changed its source.");
        }
        Console.WriteLine($"PASS original read-only world acknowledgements: {inspected} reached handlers, {complete} complete analyses, exact native state and source preserved");
    }

    private sealed class RejectHost : IWorldHost
    {
        public WorldHostReply Execute(WorldHostRequest request) => throw new InvalidOperationException("Inspection dispatched a native host command.");
        public int ReadSpecial(int selector, int activeModel) => throw new InvalidOperationException("Inspection sampled a native host value.");
    }

}
