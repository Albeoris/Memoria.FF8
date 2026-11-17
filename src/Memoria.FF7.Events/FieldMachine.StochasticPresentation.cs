using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Events;

public sealed record FieldStochasticPresentation(string Fingerprint, string DomainFingerprint, int RandomProgramCounter, FieldVariableRead Output, int[] ConvergenceProgramCounters);

public sealed partial class FieldMachine
{
    private sealed class StochasticNode(int pc, byte opcode)
    {
        public int Pc { get; } = pc;
        public byte Opcode { get; } = opcode;
        public HashSet<int> Successors { get; } = [];
        public HashSet<FieldVariableRead> Reads { get; } = [];
        public HashSet<FieldVariableRead> Writes { get; } = [];
        public List<FieldCommand> Commands { get; } = [];
    }

    private sealed class StochasticInspection
    {
        public Dictionary<int, StochasticNode> Nodes { get; } = [];
        public StochasticNode Node(int pc, byte opcode)
        {
            if (!Nodes.TryGetValue(pc, out StochasticNode? node))
            {
                node = new(pc, opcode);
                Nodes.Add(pc, node);
            }
            return node;
        }
    }

    /// <summary>Verifies one native random draw whose dependent branches only present text before a common material continuation.</summary>
    public FieldScriptAnalysis AnalyzeStochasticPresentation(int entity, int script, FieldSpatialRandomDomain domain, int instructionBudget = 8192)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if ((uint)entity >= entities.Length || script is < 0 or > 31 || instructionBudget is < 4 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(entity));
        }
        StochasticInspection inspection = new();
        FieldScriptAnalysis full = AnalyzeScriptCore(entity, script, program.Entry(entity, script), instructionBudget / 2, null, 0, stochastic: inspection);
        if (!full.Complete)
        {
            return full;
        }
        FieldScriptAnalysis Reject(string reason) => full with { Complete = false, Reason = reason };
        StochasticNode[] draws = inspection.Nodes.Values.Where(node => node.Commands.Any(command => command is FieldRandomCommand)).ToArray();
        if (draws.Length != 1 || draws[0].Opcode != 0x99 || draws[0].Writes.Count != 1)
        {
            return Reject("Stochastic presentation requires exactly one direct native byte draw.");
        }
        StochasticNode draw = draws[0];
        FieldVariableRead output = draw.Writes.Single();
        if (output.Bank != FieldBank.MapByte || output.Word)
        {
            return Reject("Stochastic presentation requires a map-local byte output.");
        }
        bool Aliases(FieldVariableRead read) => Variables.SpatialSourceBytes(read).Intersect(Variables.SpatialSourceBytes(output)).Any();
        FieldScriptEntryDependencies dependencies = AnalyzeEntryDependencies(entity, script, full, instructionBudget / 2);
        if (!dependencies.Complete || dependencies.Reads.Any(Aliases))
        {
            return Reject(dependencies.Reason ?? "The random output is not defined on every incoming path before its use.");
        }
        Dictionary<int, HashSet<int>> postdominators = [];
        HashSet<int> visiting = [];
        Stack<(int Pc, bool Exit)> traversal = new();
        traversal.Push((program.Entry(entity, script), false));
        while (traversal.TryPop(out var frame))
        {
            if (postdominators.ContainsKey(frame.Pc))
            {
                continue;
            }
            if (!inspection.Nodes.TryGetValue(frame.Pc, out StochasticNode? node))
            {
                return Reject("Stochastic presentation has an unknown native successor.");
            }
            if (!frame.Exit)
            {
                if (!visiting.Add(frame.Pc))
                {
                    return Reject("Stochastic presentation requires a finite acyclic native continuation.");
                }
                traversal.Push((frame.Pc, true));
                foreach (int successor in node.Successors)
                {
                    traversal.Push((successor, false));
                }
                continue;
            }
            HashSet<int>? common = null;
            foreach (int successor in node.Successors)
            {
                if (common is null)
                {
                    common = new(postdominators[successor]);
                }
                else
                {
                    common.IntersectWith(postdominators[successor]);
                }
            }
            common ??= [];
            common.Add(frame.Pc);
            postdominators.Add(frame.Pc, common);
            visiting.Remove(frame.Pc);
        }
        List<int> convergences = [];
        foreach (StochasticNode node in inspection.Nodes.Values)
        {
            if (node != draw && node.Writes.Any(Aliases))
            {
                return Reject("The random output is overwritten or aliased by another instruction.");
            }
            FieldVariableRead[] tainted = node.Reads.Where(Aliases).ToArray();
            if (tainted.Length == 0)
            {
                continue;
            }
            if (node.Opcode is < 0x14 or > 0x19 || tainted.Any(read => read.Word || read.Bank != FieldBank.MapByte) || node.Successors.Count != 2)
            {
                return Reject("A random value can affect a material operand or an unsupported alias.");
            }
            HashSet<int> common = new(postdominators[node.Successors.First()]);
            common.IntersectWith(postdominators[node.Successors.Last()]);
            int convergence = common.OrderByDescending(pc => postdominators[pc].Count).FirstOrDefault(-1);
            if (convergence < 0)
            {
                return Reject("Random-dependent paths do not reconverge before returning.");
            }
            HashSet<int> region = [];
            Queue<int> pending = new(node.Successors);
            while (pending.TryDequeue(out int pc))
            {
                if (pc == convergence || !region.Add(pc))
                {
                    continue;
                }
                StochasticNode branch = inspection.Nodes[pc];
                bool presentation = branch.Writes.Count == 0 && branch.Commands.All(command => command is FieldWindowCommand or FieldDialogueCommand { FirstChoice: null, LastChoice: null });
                bool control = branch.Opcode is >= 0x10 and <= 0x19 or 0x24;
                if (!presentation || (branch.Commands.Count == 0 && !control))
                {
                    return Reject("A random-dependent path contains a material or unsupported effect before convergence.");
                }
                foreach (int successor in branch.Successors)
                {
                    pending.Enqueue(successor);
                }
            }
            convergences.Add(convergence);
        }
        if (convergences.Count == 0)
        {
            return Reject("The native draw has no verified presentation branch.");
        }
        string identity = $"stochastic-presentation:1:{domain.Fingerprint}:{entity}:{script}:{draw.Pc}:{output}:{string.Join(',', convergences.Order())}";
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return full with
        {
            HostReads = full.HostReads.Where(read => read.Kind != FieldHostReadKind.Random).ToArray(),
            StochasticPresentation = new(fingerprint, domain.Fingerprint, draw.Pc, output, convergences.Distinct().Order().ToArray())
        };
    }
}
