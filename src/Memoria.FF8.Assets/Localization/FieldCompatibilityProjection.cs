using System.Collections.Immutable;
using System.Globalization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Localization;

internal sealed record FieldCompatibilityProjectionToken(int StartOperation, int EndOperation, string ComparisonKey, int ObjectIndex, int ObjectLabel, int ScriptId);

/// <summary>A text-alignment projection; the original executable inventory remains authoritative for validation and diagnostics.</summary>
internal sealed record FieldCompatibilityProjection(ImmutableArray<FieldCompatibilityProjectionToken> Tokens, string? LayoutKey, ImmutableDictionary<int, int> Boundaries)
{
    public static FieldCompatibilityProjection Create(FieldCompatibilityInventory inventory)
    {
        ImmutableArray<FieldCompatibilityInstruction> instructions = inventory.Instructions;
        HashSet<int> entries = [];
        foreach (FieldCompatibilityInstruction instruction in instructions)
        {
            if (instruction.Opcode is "JMP" or "JPF")
            {
                entries.Add(unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter))));
            }
        }
        string[]? layout = inventory.ExecutableLayout?.Split(':');
        int[] pointers = layout is { Length: 3 } ? layout[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray() : [];
        foreach (int pointer in pointers)
        {
            entries.Add(pointer & 0x7FFF);
        }

        Dictionary<int, (int End, string Key)> replacements = [];
        List<Expression> expressions = [];
        FieldCompatibilityOperandState values = new();
        (int Object, int Label, int Script) priorOwner = (-1, -1, -1);
        foreach (FieldCompatibilityInstruction instruction in instructions)
        {
            int index = instruction.Position.OperationIndex;
            var owner = (instruction.Position.ObjectIndex, instruction.Position.ObjectLabel, instruction.Position.ScriptId);
            if (owner != priorOwner || entries.Contains(index) || owner.Item1 < 0)
            {
                expressions.Clear();
                values.Reset();
            }
            priorOwner = owner;
            Jsm.Opcode opcode = Enum.Parse<Jsm.Opcode>(instruction.Opcode);
            int count = MessageOperandCount(opcode);
            if (count > 0 && expressions.Count >= count)
            {
                Expression[] operands = expressions.TakeLast(count).ToArray();
                int? id = values.ReadOperands(count)[1];
                bool contiguous = operands[^1].End == index && operands.Zip(operands.Skip(1)).All(pair => pair.First.End == pair.Second.Start);
                if (id is int known && contiguous && operands.All(expression => expression.Safe) && !entries.Any(entry => entry > operands[0].Start && entry <= index))
                {
                    replacements.Add(operands[0].Start, (index + 1, "Message:" + instruction.Opcode + ":" + known.ToString(CultureInfo.InvariantCulture)));
                }
            }

            if (IsPush(opcode))
            {
                expressions.Add(new(index, index + 1, true));
            }
            else if (opcode == Jsm.Opcode.CAL && (uint)instruction.Parameter <= 17)
            {
                int arity = NativeScalarCalculation.OperandCount(instruction.Parameter);
                if (expressions.Count < arity)
                {
                    expressions.Clear();
                }
                else
                {
                    Expression[] operands = expressions.TakeLast(arity).ToArray();
                    expressions.RemoveRange(expressions.Count - arity, arity);
                    bool contiguous = operands[^1].End == index && (arity == 1 || operands[0].End == operands[1].Start);
                    int?[] exact = values.ReadOperands(arity);
                    bool safe = operands.All(expression => expression.Safe) && contiguous;
                    if (instruction.Parameter is 3 or 4)
                    {
                        safe &= exact.All(value => value is not null) && exact[^1] != 0 && !(exact[0] == int.MinValue && exact[^1] == -1);
                    }
                    expressions.Add(new(operands[0].Start, index + 1, safe));
                }
            }
            else
            {
                expressions.Clear();
            }
            values.Observe(opcode, instruction.Parameter);
        }

        List<FieldCompatibilityProjectionToken> tokens = [];
        ImmutableDictionary<int, int>.Builder boundaries = ImmutableDictionary.CreateBuilder<int, int>();
        for (int index = 0; index < instructions.Length;)
        {
            FieldCompatibilityInstruction instruction = instructions[index];
            boundaries.Add(index, tokens.Count);
            bool replaced = replacements.TryGetValue(index, out var replacement);
            int end = replaced ? replacement.End : index + 1;
            string key = replaced ? replacement.Key : "Raw:" + instruction.EncodedValue.ToString("X8", CultureInfo.InvariantCulture);
            tokens.Add(new(index, end, OwnerKey(instruction) + key, instruction.Position.ObjectIndex, instruction.Position.ObjectLabel, instruction.Position.ScriptId));
            index = end;
        }
        boundaries.Add(instructions.Length, tokens.Count);
        for (int index = 0; index < tokens.Count; index++)
        {
            FieldCompatibilityProjectionToken token = tokens[index];
            FieldCompatibilityInstruction instruction = instructions[token.StartOperation];
            if (instruction.Opcode is "JMP" or "JPF")
            {
                int target = unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter)));
                if (boundaries.TryGetValue(target, out int projectedTarget))
                {
                    tokens[index] = token with { ComparisonKey = OwnerKey(instruction) + "Branch:" + (instruction.EncodedValue & unchecked((int)0xFFFF0000)).ToString("X8", CultureInfo.InvariantCulture) + ":" + projectedTarget.ToString(CultureInfo.InvariantCulture) };
                }
            }
        }
        string? layoutKey = null;
        if (layout is { Length: 3 } && pointers.All(pointer => boundaries.ContainsKey(pointer & 0x7FFF)))
        {
            layoutKey = layout[0] + ":" + layout[1] + ":" + string.Join(',', pointers.Select(pointer => (pointer & 0x8000).ToString(CultureInfo.InvariantCulture) + "/" + boundaries[pointer & 0x7FFF].ToString(CultureInfo.InvariantCulture)));
        }
        return new(tokens.ToImmutableArray(), layoutKey, boundaries.ToImmutable());
    }

    private static string OwnerKey(FieldCompatibilityInstruction instruction) => instruction.Position.ObjectIndex + "/" + instruction.Position.ObjectLabel + "/" + instruction.Position.ScriptId + ":";

    private static int MessageOperandCount(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.MES or Jsm.Opcode.MESW => 2,
        Jsm.Opcode.AMES or Jsm.Opcode.AMESW or Jsm.Opcode.RAMESW => 4,
        Jsm.Opcode.ASK => 6,
        Jsm.Opcode.AASK => 8,
        _ => 0
    };

    private static bool IsPush(Jsm.Opcode opcode) => opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHSM_L;

    private sealed record Expression(int Start, int End, bool Safe);
}
