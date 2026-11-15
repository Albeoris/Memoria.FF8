using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleAiLocalFlow
{
    // A local can contain a logical reference, a number, or either after a merge. Distinct numerical
    // values join to an unknown number. Each local can widen only three times at any program point.
    internal readonly record struct Value(bool MayBeReference, bool MayBeNumber, uint? Number = null)
    {
        public static Value Numeric(uint number) => new(false, true, number);
        public static Value Reference => new(true, false);
        public bool IsPossible => MayBeReference || MayBeNumber;

        public Value Join(Value other) => new(MayBeReference || other.MayBeReference, MayBeNumber || other.MayBeNumber,
            !MayBeNumber ? other.Number : !other.MayBeNumber || Number == other.Number ? Number : null);

        public Value WithComparison(NativeBattleAiCondition condition, bool passed)
        {
            bool keepsReference = MayBeReference && (!NativeBattleAiCondition.CanCompareActorReference(condition.Comparison, condition.Value) || passed == (condition.Comparison == 3));
            bool keepsNumber = MayBeNumber && (Number is not uint known || NativeBattleAiCondition.CompareNumbers(known, condition.Comparison, condition.Value) == passed);
            uint? number = Number;
            if (keepsNumber && number is null && (condition.Comparison == 0 && passed || condition.Comparison == 3 && !passed))
            {
                number = condition.Value;
            }
            return new(keepsReference, keepsNumber, keepsNumber ? number : null);
        }
    }

    private static readonly ImmutableArray<Value> ResetLocals = ImmutableArray.CreateRange(Enumerable.Repeat(Value.Numeric(0), 8));

    internal static ImmutableArray<Value> RetainedLocals(IReadOnlyList<uint> numbers, IReadOnlyDictionary<int, string> references)
    {
        return numbers.Select((number, index) => references.ContainsKey(index) ? Value.Reference : Value.Numeric(number)).ToImmutableArray();
    }

    internal static void Check(NativeBattleAiDefinition program, int formation, bool originalEnemySlots, int referenceWrites, ImmutableArray<Value> retainedLocals, Action<string> requireOriginalSlots, Action<string> markUnsupported)
    {
        HashSet<string> physicalUses = [];
        HashSet<string> unsupportedUses = [];
        // Init runs before other callbacks for a new allocation. A level-band change keeps the actual
        // locals and never replays init. Counter and turn can run in either order, or never run at all.
        ImmutableArray<Value> entry = retainedLocals.IsDefault ? Trace(0, ResetLocals) : retainedLocals;
        if (entry.IsDefault)
        {
            entry = ResetLocals;
        }
        while (true)
        {
            ImmutableArray<Value> next = entry;
            for (int callback = 1; callback < program.Scripts.Length; callback++)
            {
                next = Join(next, Trace(callback, entry));
            }
            if (next == entry)
            {
                break;
            }
            entry = next;
        }
        foreach (string use in physicalUses)
        {
            requireOriginalSlots(use);
        }
        foreach (string use in unsupportedUses)
        {
            markUnsupported(use);
        }

        ImmutableArray<Value> Trace(int callback, ImmutableArray<Value> start)
        {
            byte[] script = program.Scripts[callback];
            if (script.Length == 0)
            {
                return start;
            }
            Dictionary<int, ImmutableArray<Value>> inputs = new() { [0] = start };
            Stack<int> pending = new();
            pending.Push(0);
            ImmutableArray<Value> exits = default;
            while (pending.TryPop(out int offset))
            {
                if (!NativeBattleAiInstruction.TryDecode(script, offset, out NativeBattleAiInstruction instruction, out _) || !instruction.IsKnownOpcode)
                {
                    continue;
                }
                ImmutableArray<Value> locals = inputs[offset];
                int next = instruction.NextOffset;
                switch (instruction.Opcode)
                {
                    case 0:
                        exits = Join(exits, locals);
                        continue;
                    case 2:
                        NativeBattleAiCondition condition = instruction.Condition;
                        var (subject, selector, comparison, value) = condition;
                        if (!originalEnemySlots && condition.UsesActorSelector && selector is >= 220 and <= 227 && locals[selector - 220].MayBeNumber)
                        {
                            physicalUses.Add($"actor-reference-condition:{subject}/{selector}");
                        }
                        if (subject is >= 220 and <= 227)
                        {
                            int index = subject - 220;
                            // A different actor's local is outside this callback's own state.
                            bool mayReadReference = selector == 200 ? locals[index].MayBeReference : (referenceWrites & (1 << index)) != 0;
                            if (mayReadReference && !NativeBattleAiCondition.CanCompareActorReference(comparison, value))
                            {
                                unsupportedUses.Add($"actor-reference-comparison:{subject}/{comparison}/{value}");
                            }
                            if (selector == 200)
                            {
                                AddComparisonEdge(next, true);
                                AddComparisonEdge(instruction.BranchOffset, false);
                                continue;

                                void AddComparisonEdge(int target, bool passed)
                                {
                                    Value narrowed = locals[index].WithComparison(condition, passed);
                                    if (narrowed.IsPossible)
                                    {
                                        Enqueue(target, locals.SetItem(index, narrowed));
                                    }
                                }
                            }
                        }
                        bool? known = condition.EvaluateKnownSubject(formation, program.LevelBand);
                        if (known != true)
                        {
                            Enqueue(instruction.BranchOffset, locals);
                        }
                        if (known == false)
                        {
                            continue;
                        }
                        break;
                    case 4 when !originalEnemySlots && instruction[0] is >= 220 and <= 227:
                        Value target = locals[instruction[0] - 220];
                        if (target.MayBeNumber && (target.Number is not uint number || NativeBattleTargetRequestAdapter.DecodeAiActorOperand(unchecked((int)number)).Projection.RequiresOriginalSlots))
                        {
                            physicalUses.Add($"actor-reference-target:{instruction[0]}");
                        }
                        break;
                    case 14 when instruction[0] is >= 220 and <= 227:
                        locals = locals.SetItem(instruction[0] - 220, instruction[1] == 203 ? Value.Reference : Value.Numeric(instruction[1]));
                        break;
                    case 18 when instruction[0] is >= 220 and <= 227 && instruction[1] != 203:
                        int variable = instruction[0] - 220;
                        Value before = locals[variable];
                        if (before.MayBeReference)
                        {
                            unsupportedUses.Add($"actor-reference-arithmetic:{instruction[0]}");
                        }
                        locals = locals.SetItem(variable, new(false, true, before.Number is uint current ? unchecked(current + instruction[1]) : null));
                        break;
                    case 35:
                        next = instruction.BranchOffset;
                        break;
                }
                Enqueue(next, locals);
            }
            return exits;

            void Enqueue(int offset, ImmutableArray<Value> locals)
            {
                inputs.TryGetValue(offset, out ImmutableArray<Value> previous);
                ImmutableArray<Value> merged = Join(previous, locals);
                if (merged != previous)
                {
                    inputs[offset] = merged;
                    pending.Push(offset);
                }
            }
        }
    }

    private static ImmutableArray<Value> Join(ImmutableArray<Value> left, ImmutableArray<Value> right)
    {
        if (left.IsDefault)
        {
            return right;
        }
        if (right.IsDefault || left == right)
        {
            return left;
        }
        ImmutableArray<Value>.Builder? merged = null;
        for (int index = 0; index < left.Length; index++)
        {
            Value value = left[index].Join(right[index]);
            if (value != left[index])
            {
                merged ??= left.ToBuilder();
                merged[index] = value;
            }
        }
        return merged is null ? left : merged.MoveToImmutable();
    }
}
