using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Assets.Localization;

public sealed record FieldLocationSource(int FieldId, string Field, byte[]? Jsm);
public sealed record FieldLocationOperation(string Opcode, FieldCompatibilityPosition Position, int? Value);
public sealed record FieldLocationNode(int FieldId, string Field, string? JsmSha256, ImmutableArray<FieldLocationOperation> Transitions, ImmutableArray<FieldLocationOperation> PlaceSetters, ImmutableArray<string> Problems);
public enum FieldLocationStatus
{
    Direct,
    Nearest,
    Ambiguous,
    NoPath,
    MissingScript
}
public sealed record FieldLocationResolution(int FieldId, string Field, FieldLocationStatus Status, int? Distance, ImmutableArray<int> PlaceIds, ImmutableArray<string> Names, ImmutableArray<int> SetterFieldIds, bool HasDynamicOperations, bool Incomplete);
public sealed record FieldLocationGraphResult(string Rule, string Fingerprint, ImmutableArray<FieldLocationNode> Nodes, ImmutableArray<FieldLocationResolution> Resolutions);

/// <summary>Finds nearest SETPLACE fields along outgoing potential field transitions, retaining every shortest-path tie.</summary>
public static class FieldLocationGraph
{
    public const string Rule = "field-location-outgoing-shortest-path-v1";

    public static FieldLocationGraphResult Build(IEnumerable<FieldLocationSource> fields, IReadOnlyList<string> placeNames)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(placeNames);
        FieldLocationSource[] sources = fields.OrderBy(field => field.FieldId).ToArray();
        if (sources.Any(source => source.FieldId is < 0 or > ushort.MaxValue || string.IsNullOrWhiteSpace(source.Field)) || sources.Select(source => source.FieldId).Distinct().Count() != sources.Length)
        {
            throw new ArgumentException("Fields require unique native WORD identifiers and nonempty names.", nameof(fields));
        }
        FieldLocationNode[] nodes = sources.Select(Extract).ToArray();
        Dictionary<int, FieldLocationNode> byId = nodes.ToDictionary(node => node.FieldId);
        Dictionary<int, int[]> outgoing = nodes.ToDictionary(node => node.FieldId, node => node.Transitions.Where(operation => operation.Value.HasValue).Select(operation => operation.Value!.Value).Distinct().Order().ToArray());
        Dictionary<int, List<int>> incoming = nodes.ToDictionary(node => node.FieldId, _ => new List<int>());
        foreach (FieldLocationNode node in nodes)
        {
            foreach (int target in outgoing[node.FieldId])
            {
                if (incoming.TryGetValue(target, out List<int>? predecessors))
                {
                    predecessors.Add(node.FieldId);
                }
            }
        }
        Dictionary<int, int> distances = [];
        Queue<int> queue = new();
        foreach (FieldLocationNode node in nodes.Where(node => node.PlaceSetters.Any(operation => operation.Value.HasValue)))
        {
            distances.Add(node.FieldId, 0);
            queue.Enqueue(node.FieldId);
        }
        while (queue.TryDequeue(out int target))
        {
            foreach (int predecessor in incoming[target])
            {
                if (distances.TryAdd(predecessor, distances[target] + 1))
                {
                    queue.Enqueue(predecessor);
                }
            }
        }
        Dictionary<int, SortedSet<int>> places = [];
        Dictionary<int, SortedSet<int>> setters = [];
        Dictionary<int, bool> incomplete = [];
        Dictionary<int, bool> dynamic = [];
        foreach (FieldLocationNode node in nodes.OrderBy(node => distances.GetValueOrDefault(node.FieldId, int.MaxValue)).ThenBy(node => node.FieldId))
        {
            int id = node.FieldId;
            places[id] = [];
            setters[id] = [];
            dynamic[id] = node.Transitions.Concat(node.PlaceSetters).Any(operation => operation.Value is null);
            incomplete[id] = node.Problems.Length > 0 || dynamic[id] || outgoing[id].Any(target => !byId.ContainsKey(target));
            if (!distances.TryGetValue(id, out int distance))
            {
                continue;
            }
            if (distance == 0)
            {
                places[id].UnionWith(node.PlaceSetters.Where(operation => operation.Value.HasValue).Select(operation => operation.Value!.Value));
                setters[id].Add(id);
            }
            else
            {
                foreach (int target in outgoing[id].Where(target => distances.TryGetValue(target, out int next) && next == distance - 1))
                {
                    places[id].UnionWith(places[target]);
                    setters[id].UnionWith(setters[target]);
                    incomplete[id] |= incomplete[target];
                    dynamic[id] |= dynamic[target];
                }
            }
        }
        PropagateUncertainty(dynamic, incoming);
        PropagateUncertainty(incomplete, incoming);
        FieldLocationResolution[] resolutions = nodes.Select(node =>
        {
            int id = node.FieldId;
            int? distance = distances.TryGetValue(id, out int value) ? value : null;
            int[] ids = places[id].ToArray();
            bool missingNames = ids.Any(place => (uint)place >= placeNames.Count || string.IsNullOrEmpty(placeNames[place]));
            FieldLocationStatus status = node.JsmSha256 is null ? FieldLocationStatus.MissingScript : ids.Length > 1 ? FieldLocationStatus.Ambiguous : distance == 0 ? FieldLocationStatus.Direct : distance.HasValue ? FieldLocationStatus.Nearest : FieldLocationStatus.NoPath;
            return new FieldLocationResolution(id, node.Field, status, distance, ids.ToImmutableArray(), ids.Where(place => (uint)place < placeNames.Count).Select(place => placeNames[place]).ToImmutableArray(), setters[id].ToImmutableArray(), dynamic[id], incomplete[id] || missingNames);
        }).ToArray();
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(new { Rule, Nodes = nodes, PlaceNames = placeNames });
        return new(Rule, Convert.ToHexString(SHA256.HashData(identity)), nodes.ToImmutableArray(), resolutions.ToImmutableArray());
    }

    private static void PropagateUncertainty(Dictionary<int, bool> flags, Dictionary<int, List<int>> incoming)
    {
        Queue<int> pending = new(flags.Where(pair => pair.Value).Select(pair => pair.Key));
        while (pending.TryDequeue(out int target))
        {
            foreach (int predecessor in incoming[target])
            {
                if (!flags[predecessor])
                {
                    flags[predecessor] = true;
                    pending.Enqueue(predecessor);
                }
            }
        }
    }

    private static FieldLocationNode Extract(FieldLocationSource source)
    {
        if (source.Jsm is null)
        {
            return new(source.FieldId, source.Field, null, [], [], ["Missing JSM."]);
        }
        FieldCompatibilityInventory inventory = FieldCompatibilityAudit.Inspect(new(source.Field, "en", source.Field, source.Jsm, []));
        List<string> problems = [];
        if (inventory.ExecutableLayout is null)
        {
            return new(source.FieldId, source.Field, inventory.JsmSha256, [], [], inventory.Problems);
        }
        IReadOnlyList<JsmOperation> operations = JsmDocument.ReadOperations(source.Jsm);
        HashSet<int> branchTargets = operations.Where(operation => operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF).Select(operation => (int)unchecked((ushort)(operation.Index + unchecked((short)operation.Parameter)))).ToHashSet();
        List<FieldLocationOperation> transitions = [];
        List<FieldLocationOperation> setters = [];
        foreach (FieldCompatibilityInstruction instruction in inventory.Instructions)
        {
            JsmOperation operation = operations[instruction.Position.OperationIndex];
            int operands = operation.Opcode switch
            {
                Jsm.Opcode.MAPJUMP => 4,
                Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.DISCJUMP => 5,
                Jsm.Opcode.MAPJUMPO => 2,
                Jsm.Opcode.SETPLACE => 1,
                _ => 0
            };
            if (operands == 0)
            {
                if (!Enum.IsDefined(operation.Opcode))
                {
                    problems.Add($"Unknown opcode at operation {operation.Index}.");
                }
                continue;
            }
            int first = operation.Index - operands;
            bool literal = instruction.Position.ObjectIndex >= 0 && instruction.Position.InstructionOrdinal >= operands && !branchTargets.Any(target => target > first && target <= operation.Index);
            for (int index = Math.Max(0, first); index < operation.Index && literal; index++)
            {
                literal = operations[index].Opcode == Jsm.Opcode.PSHN_L;
            }
            int? nativeValue = literal ? unchecked((ushort)operations[first].Parameter) : null;
            FieldLocationOperation entry = new(operation.Opcode.ToString(), instruction.Position, nativeValue);
            if (operation.Opcode == Jsm.Opcode.SETPLACE)
            {
                setters.Add(entry);
            }
            else
            {
                transitions.Add(entry);
            }
        }
        return new(source.FieldId, source.Field, inventory.JsmSha256, transitions.ToImmutableArray(), setters.ToImmutableArray(), problems.ToImmutableArray());
    }
}
