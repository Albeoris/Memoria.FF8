using System.Reflection;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldObservationAnalysisCacheTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            Console.WriteLine("SKIP native analysis memo fixture: select resources.");
            return;
        }
        byte[] persistent = new byte[1280];
        using Ff7WorldRuntime world = new(Ff7Installation.Discover(game), persistent, 1, new byte[] { 0, 1, 2 });
        WorldMachine Install(ushort[] code, PcWorldFunction[] functions)
        {
            WorldMachine machine = new(new(new(functions, code)), persistent, new RejectHost());
            typeof(Ff7WorldRuntime).GetField("<Machine>k__BackingField", Private)!.SetValue(world, machine);
            return machine;
        }
        MethodInfo read = typeof(Ff7WorldRuntime).GetMethod("ReadAutoPlayMeshAnalysis", Private)!;
        WorldScriptAnalysis Read(int x = 0, int z = 0, int model = 5) => (WorldScriptAnalysis)read.Invoke(world, [x, z, 3, 2048, true, model])!;
        PcWorldFunction[] functions = Enumerable.Range(0, 120).Select(index => new PcWorldFunction((ushort)WorldProgram.MeshDispatchHeader(index % 12, index / 12, 0), 1)).ToArray();
        WorldMachine source = Install([0, 0x100, 0x11B, 8, 0x110, 5, 0x70, 0x201, 15, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203], functions);
        Dictionary<(int Index, int Model), WorldScriptAnalysis> results = [];
        using (world.BeginAutoPlayObservation())
        {
            using (world.BeginAutoPlayObservation())
            {
                for (int index = 0; index < 120; index++)
                {
                    foreach (int model in new[] { 5, 0 })
                    {
                        WorldScriptAnalysis analysis = Read(index % 12, index / 12, model);
                        Check(analysis.Complete && (model == 5 ? analysis.MayEnterField : analysis.IsInert), "Projected model analysis used another controller's cached branch.");
                        results[(index, model)] = analysis;
                    }
                }
            }
            Check((int)typeof(Ff7WorldRuntime).GetField("autoPlayObservationDepth", Private)!.GetValue(world)! == 1, "Nested observation disposal ended its outer immutable scope.");
        }
        using (world.BeginAutoPlayObservation())
        {
            foreach (var item in results)
            {
                Check(ReferenceEquals(item.Value, Read(item.Key.Index % 12, item.Key.Index / 12, item.Key.Model)), "A frozen working set larger than102 proofs reentered native analysis or conflated model projections.");
            }
        }
        Check((int)typeof(Ff7WorldRuntime).GetField("autoPlayObservationDepth", Private)!.GetValue(world)! == 0 && typeof(Ff7WorldRuntime).GetField("autoPlayObservationExecution", Private)!.GetValue(world) is null, "Observation scope escaped into later native execution.");
        WorldSnapshot changed = source.Capture();
        byte[] local = changed.Local.ToArray();
        local[0] = 1;
        source.Restore(changed with { Local = local });
        using (world.BeginAutoPlayObservation())
        {
            Check(!ReferenceEquals(results[(0, 5)], Read()), "Changed native VM state reused the old observation's result memo.");
        }
        Install([0, 0x100, 0x114, 802, 0x17, 0x201, 13, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203], [new(0x8000, 1)]);
        persistent[100] = 4;
        using (world.BeginAutoPlayObservation())
        {
            WorldScriptAnalysis closed = Read();
            persistent[100] = 0;
            WorldScriptAnalysis opened = Read();
            Check(closed.IsInert && opened.MayEnterField && !ReferenceEquals(closed, opened), "A typed predicate change inside a stable execution snapshot reused a stale result.");
        }
        source = Install([0, 0x100, 0x110, 0, 0x207, 0x203, 0x100, 0x11B, 0, 0x110, 0, 0x70, 0x201, 20, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203], [new(0x8000, 1), new(0x4003, 6)]);
        source.Restore(source.Capture() with { Models = [new(0, 0, 0, 0, false, 0, [])] });
        Dictionary<int, Ff7WorldEntity> entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(world)!;
        entities[0] = new(0);
        using (world.BeginAutoPlayObservation())
        {
            WorldScriptAnalysis near = Read();
            entities[0] = entities[0] with { X = 8192 };
            WorldScriptAnalysis far = Read();
            Check(near.Complete && near.MayEnterField && far.Complete && !far.MayEnterField && !ReferenceEquals(near, far), "Changed projected host pose reused an observation's earlier spatial branch.");
        }
        Install([0, 0x100, 0x11B, 16, 0x201, 7, 0x203, 0x203], [new(0x8000, 1)]);
        using (world.BeginAutoPlayObservation())
        {
            WorldScriptAnalysis first = Read();
            Check(!first.Complete && !Read().Complete && !ReferenceEquals(first, Read()), "An unknown stochastic host dependency was memoized as a proved branch.");
        }
        Console.WriteLine("PASS240 exact observation proofs, model separation, VM/typed guard/host pose invalidation, nested lifetime and unknown preservation");
    }

    private sealed class RejectHost : IWorldHost
    {
        public WorldHostReply Execute(WorldHostRequest request) => throw new InvalidOperationException("Analysis dispatched actual native work.");
        public int ReadSpecial(int selector, int activeModel) => throw new InvalidOperationException("Analysis sampled actual native host state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
