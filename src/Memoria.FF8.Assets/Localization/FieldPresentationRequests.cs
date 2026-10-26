using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Resolved asynchronous requests to closed, read-only presentation routines.</summary>
internal sealed class FieldPresentationRequests
{
    private readonly Dictionary<int, (int Object, int Label, string Signature)> scripts = [];
    private readonly int[] nativeLabels = [];
    private readonly int actorCount;

    public ImmutableHashSet<(int Label, int Script)> ScriptIdentities { get; }

    public FieldPresentationRequests(FieldCompatibilityInventory inventory, FieldCompatibilityProjection projection)
    {
        ImmutableHashSet<(int, int)>.Builder identities = ImmutableHashSet.CreateBuilder<(int, int)>();
        ScriptIdentities = identities.ToImmutable();
        if (!inventory.ScriptValidated || inventory.ExecutableLayout is null || projection.LayoutKey is null)
        {
            return;
        }
        string[] layout = inventory.ExecutableLayout.Split(':');
        byte[] counts = Convert.FromHexString(layout[0]);
        int[] groups = layout[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        int[] pointers = layout[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        actorCount = counts[3];
        nativeLabels = groups.Skip(counts[0] + counts[1] + counts[2]).Concat(groups.Take(counts[1])).Concat(groups.Skip(counts[0] + counts[1]).Take(counts[2])).Concat(groups.Skip(counts[1]).Take(counts[0])).Select(group => group >> 7).ToArray();
        HashSet<int> retainedOperations = projection.Tokens.Select(token => token.StartOperation).ToHashSet();
        var branches = inventory.Instructions.Where(instruction => instruction.Opcode is "JMP" or "JPF").Select(instruction => (Source: instruction.Position.OperationIndex, Target: unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter))))).ToArray();
        for (int pointer = 0; pointer < pointers.Length - 1; pointer++)
        {
            int start = pointers[pointer] & 0x7FFF;
            int end = pointers[pointer + 1] & 0x7FFF;
            if (branches.Any(branch => (branch.Source < start || branch.Source >= end) && branch.Target > start && branch.Target < end))
            {
                continue;
            }
            int depth = 0;
            int outputs = 0;
            bool valid = true;
            HashSet<int> entries = branches.Select(branch => (int)branch.Target).ToHashSet();
            foreach (FieldCompatibilityInstruction instruction in inventory.Instructions.Skip(start).Take(end - start))
            {
                if (entries.Contains(instruction.Position.OperationIndex))
                {
                    depth = 0;
                }
                Jsm.Opcode opcode = Enum.Parse<Jsm.Opcode>(instruction.Opcode);
                int count = FieldPresentationInstructions.OperandCount(opcode);
                if (count >= 0)
                {
                    valid &= depth >= count && !retainedOperations.Contains(instruction.Position.OperationIndex);
                    outputs++;
                    depth = 0;
                }
                else if (opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHSM_L)
                {
                    depth++;
                }
                else if (opcode == Jsm.Opcode.CAL && (uint)instruction.Parameter <= 17 && instruction.Parameter is not (3 or 4))
                {
                    int arity = NativeScalarCalculation.OperandCount(instruction.Parameter);
                    valid &= depth >= arity;
                    depth = Math.Max(0, depth - arity + 1);
                }
                else if (opcode is Jsm.Opcode.WAIT or Jsm.Opcode.JPF)
                {
                    valid &= depth >= 1;
                    depth = 0;
                }
                else if (opcode is Jsm.Opcode.LBL or Jsm.Opcode.RET or Jsm.Opcode.NOP or Jsm.Opcode.JMP)
                {
                    depth = 0;
                }
                else
                {
                    valid = false;
                }
            }
            if (!valid || outputs == 0 || branches.Any(branch => branch.Source >= start && branch.Source < end && (branch.Target < start || branch.Target >= end)))
            {
                continue;
            }
            List<string> keys = [];
            foreach (FieldCompatibilityProjectionToken token in projection.Tokens.Where(token => token.StartOperation >= start && token.EndOperation <= end))
            {
                FieldCompatibilityInstruction instruction = inventory.Instructions[token.StartOperation];
                if (instruction.Opcode == "LBL")
                {
                    continue;
                }
                if (instruction.Opcode is "JMP" or "JPF")
                {
                    int target = unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter)));
                    if (!projection.Boundaries.TryGetValue(target, out int destination))
                    {
                        valid = false;
                        break;
                    }
                    keys.Add("Branch:" + (instruction.EncodedValue & unchecked((int)0xFFFF0000)) + ":" + (destination - projection.Boundaries[start]));
                }
                else
                {
                    keys.Add(token.ComparisonKey[(token.ComparisonKey.IndexOf(':') + 1)..]);
                }
            }
            if (valid)
            {
                FieldCompatibilityPosition owner = inventory.Instructions[start].Position;
                scripts.Add(pointer, (owner.ObjectIndex, owner.ObjectLabel, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((pointers[pointer] & 0x8000) + "\n" + string.Join('\n', keys))))));
                identities.Add((owner.ObjectLabel, owner.ScriptId));
            }
        }
        ScriptIdentities = identities.ToImmutable();
    }

    public bool TryGetKey(int nativeObject, int? priority, int? pointer, int callerObject, out string? key)
    {
        key = null;
        if ((uint)nativeObject >= actorCount || priority is not (>= 0 and <= 7) || pointer is not int index || !scripts.TryGetValue(index, out var script) || nativeLabels[nativeObject] != script.Label)
        {
            return false;
        }
        key = "PresentationRequest:REQ:" + priority + ":" + (script.Object == callerObject ? "Self" : "Cross") + ":" + script.Signature;
        return true;
    }
}
