using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldFrontierCatalogTests
{
    public static void Run()
    {
        const int chunkX = 2;
        const int chunkZ = 1;
        List<ushort> code = [0];
        List<PcWorldFunction> functions = [];
        Add(3, [0x100, 0x110, 1, 0x118, 5, 0x318, 0x203]);
        Add(4, [0x100, 0x110, 0x301, 0x33D, 0x203]);
        Add(5, [0x100, 0x110, 1, 0x113, 16, 0x318, 0x203]);
        Add(6, [0x100, 0x110, 43, 0x31D, 0x203]);
        WorldProgram program = new(new(functions, code));
        byte[] persistent = new byte[1280];
        RejectHost host = new();
        WorldMachine machine = new(program, persistent, host);
        string original = JsonSerializer.Serialize(machine.Capture());
        PcWorldMesh mesh = new(3, 1, [new(0, 10, 0), new(100, 20, 0), new(0, 30, 100)], [], [Face(3, 0), Face(3, 1), Face(4, 0), Face(5, 0), Face(6, 0), Face(2, 0)]);
        PcWorldFieldTable table = new([new(11, 22, 3, 91, 4), new(33, 44, 5, 92, 6)]);
        Dictionary<int, int> inspected = [];
        int resolved = 0;
        Ff7AutoPlayWorldFrontierCatalog catalog = Build(mesh);
        Check(catalog.Scripts.Length == 4 && inspected.Count == 4 && inspected.Values.All(count => count == 1), "A lazy chunk catalog must inspect each admitted script once, not every face or unrelated dispatch.");
        Ff7AutoPlayWorldScriptObservation entry = catalog.Scripts.Single(script => script.Script == 3);
        Check(entry.Id == "mesh:2:1:script:3" && entry.Complete && entry.Faces.Length == 2, "One native mesh/script dispatch must retain a stable event identity and every actual source face.");
        Check(entry.Faces[0].Source == new Ff7WorldTerrainReference(chunkX, chunkZ, 0) && entry.Faces[0].A == new Vector3(chunkX << 13, 10, chunkZ << 13) && entry.Faces[1].Terrain == 1, "Catalog source polygons must retain native triangle identity, terrain and exact world-space geometry.");
        Check(entry.FieldEntries.Single() is { Selector: 0x100, Destination.Field: 91, Destination.X: 11, Destination.Y: 22, Destination.Triangle: 3, Destination.Direction: 4 }, "A proven native selector must use the existing field-table row without confusing field spawn facts with world targets.");
        Check(catalog.Scripts.Single(script => script.Script == 4) is { Analysis.MayReloadWorld: true, FieldEntries.Length: 0, Complete: true }, "A world reload must not become a field-entry destination.");
        Ff7AutoPlayWorldScriptObservation incomplete = catalog.Scripts.Single(script => script.Script == 5);
        Check(!incomplete.Complete && incomplete.Reason is not null && incomplete.FieldEntries.Single() is { Selector: null, Destination: null, Reason: not null } && incomplete.Analysis.HostReads.Any(read => read.Kind == WorldHostDependencyKind.Special && read.Selector == 16), "An unresolved native host guard must remain explicit and must not resolve a fabricated field selector.");
        Check(!catalog.Scripts.Single(script => script.Script == 6).Analysis.MayEnterField && catalog.FieldEntryFrontiers.Count() == 2 && !catalog.Complete && resolved == 1, "Ambient scripts and reloads must stay separate from potential field-entry frontiers, including incomplete ones.");
        Check(original == JsonSerializer.Serialize(machine.Capture()) && persistent.All(value => value == 0) && host.Calls == 0, "Catalog construction changed native VM, canonical storage or host/RNG state.");
        persistent[5] = 1;
        Ff7AutoPlayWorldScriptObservation changed = Build(mesh).Scripts.Single(script => script.Script == 3);
        Check(changed.PredicateFingerprint != entry.PredicateFingerprint && changed.Id == entry.Id && changed.FieldEntries.Single().Destination!.Field == 92, "A canonical native predicate change must update its guard and selector while preserving dispatch identity.");
        PcWorldMesh deformed = mesh with { Vertices = [new(0, 11, 0), mesh.Vertices[1], mesh.Vertices[2]] };
        Ff7AutoPlayWorldScriptObservation geometry = Build(deformed).Scripts.Single(script => script.Script == 3);
        Check(geometry.GeometryFingerprint != changed.GeometryFingerprint && geometry.PredicateFingerprint == changed.PredicateFingerprint, "Native geometry changes must have their own exact guard without fabricating a script predicate change.");
        Ff7AutoPlayWorldFrontierCatalog invalid = Ff7WorldRuntime.BuildAutoPlayFrontierCatalog(0, program.Identity, chunkX, chunkZ, mesh, Analyze, machine.ReadPredicateFingerprint, _ => throw new InvalidDataException("The authored field table is truncated."));
        Check(invalid.Scripts.Single(script => script.Script == 3) is { Complete: false, Reason: not null } failed && failed.FieldEntries.Single().Destination is null, "An invalid native selector row must remain an explicit unsupported frontier.");

        void Add(int script, ushort[] body)
        {
            functions.Add(new(checked((ushort)WorldProgram.MeshDispatchHeader(chunkX, chunkZ, script - 3)), checked((ushort)code.Count)));
            code.AddRange(body);
        }

        WorldScriptAnalysis Analyze(int script)
        {
            inspected[script] = inspected.GetValueOrDefault(script) + 1;
            return machine.AnalyzeMeshDispatch(chunkX, chunkZ, script);
        }

        Ff7AutoPlayWorldFrontierCatalog Build(PcWorldMesh source)
        {
            return Ff7WorldRuntime.BuildAutoPlayFrontierCatalog(0, program.Identity, chunkX, chunkZ, source, Analyze, machine.ReadPredicateFingerprint, selector =>
            {
                resolved++;
                return table.Select(selector);
            });
        }
    }

    private static PcWorldTriangle Face(int script, int terrain)
    {
        return new(0, 1, 2, checked((byte)(script << 5 | terrain)), Vector2.Zero, Vector2.Zero, Vector2.Zero, 0);
    }

    private sealed class RejectHost : IWorldHost
    {
        public int Calls { get; private set; }

        public WorldHostReply Execute(WorldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("World catalog observation must not dispatch a native host.");
        }

        public int ReadSpecial(int selector, int activeModel)
        {
            Calls++;
            throw new InvalidOperationException("World catalog observation must not sample a native host.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
