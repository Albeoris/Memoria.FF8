using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayWorldFrontierFace(Ff7WorldTerrainReference Source, Vector3 A, Vector3 B, Vector3 C, int Terrain, int Location);
public sealed record Ff7AutoPlayWorldFieldEntry(int ProgramCounter, ushort? Selector, PcWorldFieldEntry? Destination, string? Reason = null);
public sealed record Ff7AutoPlayWorldScriptObservation(string Id, int Script, Ff7AutoPlayWorldFrontierFace[] Faces, WorldScriptAnalysis Analysis, string PredicateFingerprint, string GeometryFingerprint, Ff7AutoPlayWorldFieldEntry[] FieldEntries, bool Complete, string? Reason);
public sealed record Ff7AutoPlayWorldFrontierCatalog(int WorldId, string ProgramIdentity, int ChunkX, int ChunkZ, Ff7AutoPlayWorldScriptObservation[] Scripts)
{
    public bool Complete => Scripts.All(script => script.Complete);
    public IEnumerable<Ff7AutoPlayWorldScriptObservation> FieldEntryFrontiers => Scripts.Where(script => script.Analysis.MayEnterField);
}

public sealed partial class Ff7WorldRuntime
{
    private sealed record AutoPlayMeshAnalysis(WorldScriptAnalysis Analysis, string ExecutionFingerprint, string PredicateFingerprint);
    private readonly Dictionary<(int X, int Z, int Script, int Budget, bool CurrentEntry), AutoPlayMeshAnalysis> autoPlayMeshAnalyses = [];
    private readonly Queue<(int X, int Z, int Script, int Budget, bool CurrentEntry)> autoPlayMeshAnalysisOrder = new();

    /// <summary>Observes only the requested native collision chunk. A polygon identifies a possible script source; it does not prove physical reachability or terrain-script change admission.</summary>
    public Ff7AutoPlayWorldFrontierCatalog ReadAutoPlayFrontierCatalog(int chunkX, int chunkZ, int instructionBudget = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instructionBudget, 65536);
        PcWorldMesh mesh = GetWorldCollisionMesh(chunkX, chunkZ);
        return BuildAutoPlayFrontierCatalog(Resources.World, Machine.ProgramIdentity, chunkX, chunkZ, mesh, script => ReadAutoPlayMeshAnalysis(chunkX, chunkZ, script, instructionBudget, false), Machine.ReadPredicateFingerprint, selector => (fieldTable ??= Resources.ReadFieldTable()).Select(selector));
    }

    private WorldScriptAnalysis ReadAutoPlayMeshAnalysis(int chunkX, int chunkZ, int script, int instructionBudget, bool currentEntryOnly, int? adviceControlledModel = null)
    {
        WorldSnapshot execution = Machine.Capture() with { Tick = 0, NextRequestId = 1 };
        string executionFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(execution)));
        int controlledOwner = adviceControlledModel ?? ControlledModel;
        Ff7WorldEntity[] hostEntities = entities.Values.ToArray();
        Ff7WorldEntity[] hostFreeEntities = freeEntities.ToArray();
        int? ReadHost(WorldHostDependency dependency) => ProjectAutoPlayHostDependency(dependency, controlledOwner, hostEntities, hostFreeEntities);
        var key = (chunkX, chunkZ, script, instructionBudget, currentEntryOnly);
        if (autoPlayMeshAnalyses.TryGetValue(key, out AutoPlayMeshAnalysis? known) && known.ExecutionFingerprint == executionFingerprint && Machine.ReadPredicateFingerprint(known.Analysis.Reads) == known.PredicateFingerprint && known.Analysis.HostValues.All(value => ReadHost(value.Dependency) == value.Value))
        {
            return known.Analysis;
        }
        WorldScriptAnalysis analysis = Machine.AnalyzeMeshDispatch(chunkX, chunkZ, script, instructionBudget, ReadHost, currentEntryOnly);
        if (analysis.Complete)
        {
            if (!autoPlayMeshAnalyses.ContainsKey(key))
            {
                if (autoPlayMeshAnalysisOrder.Count == 64)
                {
                    autoPlayMeshAnalyses.Remove(autoPlayMeshAnalysisOrder.Dequeue());
                }
                autoPlayMeshAnalysisOrder.Enqueue(key);
            }
            autoPlayMeshAnalyses[key] = new(analysis, executionFingerprint, Machine.ReadPredicateFingerprint(analysis.Reads));
        }
        return analysis;
    }

    internal static int? ProjectAutoPlayHostDependency(WorldHostDependency dependency, int controlledModel, IReadOnlyList<Ff7WorldEntity>? entities = null, IReadOnlyList<Ff7WorldEntity>? freeEntities = null)
    {
        if (dependency.Kind != WorldHostDependencyKind.Special)
        {
            return null;
        }
        if (dependency.Selector == 8)
        {
            return Math.Max(0, controlledModel);
        }
        Ff7WorldEntity? entity;
        if (dependency.Model == -1)
        {
            entity = new(-1);
        }
        else if (dependency.FreeModelIndex is int index)
        {
            entity = (uint)index < (uint)(freeEntities?.Count ?? 0) && freeEntities![index].Model == dependency.Model ? freeEntities[index] : null;
        }
        else
        {
            entity = entities?.SingleOrDefault(entity => entity.Model == dependency.Model);
        }
        return entity is null ? null : ReadEntitySpecial(entity, dependency.Selector);
    }

    internal static Ff7AutoPlayWorldFrontierCatalog BuildAutoPlayFrontierCatalog(int worldId, string programIdentity, int chunkX, int chunkZ, PcWorldMesh mesh, Func<int, WorldScriptAnalysis> analyze, Func<IReadOnlyList<WorldVariableRead>, string> readPredicate, Func<ushort, PcWorldFieldEntry> selectField)
    {
        List<Ff7AutoPlayWorldScriptObservation> scripts = [];
        foreach (var group in mesh.Triangles.Select((face, index) => (Face: face, Index: index)).Where(item => item.Face.Script >= 3).GroupBy(item => item.Face.Script).OrderBy(group => group.Key))
        {
            WorldScriptAnalysis analysis = analyze(group.Key);
            List<Ff7AutoPlayWorldFieldEntry> entries = [];
            string? reason = analysis.Reason;
            foreach (WorldTransitionObservation transition in analysis.Transitions.Where(transition => transition.Operation == WorldOperation.FieldTransition))
            {
                if (transition.Selector is not ushort selector)
                {
                    string unresolved = "The native field-entry selector requires an unresolved script or host dependency.";
                    entries.Add(new(transition.ProgramCounter, null, null, unresolved));
                    reason ??= unresolved;
                    continue;
                }
                try
                {
                    entries.Add(new(transition.ProgramCounter, selector, selectField(selector)));
                }
                catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException)
                {
                    entries.Add(new(transition.ProgramCounter, selector, null, error.Message));
                    reason ??= error.Message;
                }
            }
            Vector3 WorldVertex(int index)
            {
                Vector3 vertex = mesh.Vertices[index];
                return vertex + new Vector3(chunkX << 13, 0, chunkZ << 13);
            }
            Ff7AutoPlayWorldFrontierFace[] faces = group.Select(item => new Ff7AutoPlayWorldFrontierFace(new(chunkX, chunkZ, item.Index), WorldVertex(item.Face.A), WorldVertex(item.Face.B), WorldVertex(item.Face.C), item.Face.Terrain, item.Face.Location)).ToArray();
            string geometry = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                mesh.BlockIndex,
                mesh.MeshIndex,
                Faces = faces.Select(face => new
                {
                    face.Source,
                    A = new[] { BitConverter.SingleToInt32Bits(face.A.X), BitConverter.SingleToInt32Bits(face.A.Y), BitConverter.SingleToInt32Bits(face.A.Z) },
                    B = new[] { BitConverter.SingleToInt32Bits(face.B.X), BitConverter.SingleToInt32Bits(face.B.Y), BitConverter.SingleToInt32Bits(face.B.Z) },
                    C = new[] { BitConverter.SingleToInt32Bits(face.C.X), BitConverter.SingleToInt32Bits(face.C.Y), BitConverter.SingleToInt32Bits(face.C.Z) },
                    face.Terrain,
                    face.Location
                }).ToArray()
            })));
            string predicate = readPredicate(analysis.Reads);
            if (analysis.HostValues.Length != 0)
            {
                predicate = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Variables = predicate, Host = analysis.HostValues })));
            }
            scripts.Add(new($"mesh:{chunkX}:{chunkZ}:script:{group.Key}", group.Key, faces, analysis, predicate, geometry, entries.ToArray(), analysis.Complete && reason is null, reason));
        }
        return new(worldId, programIdentity, chunkX, chunkZ, scripts.ToArray());
    }
}
