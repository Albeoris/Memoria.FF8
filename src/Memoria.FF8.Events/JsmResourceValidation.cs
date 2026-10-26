using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record JsmResourceValidationIssue(string Code, int ScriptId, int Operation, string Message);
public sealed record JsmResourceValidationResult(IReadOnlyList<JsmResourceValidationIssue> Errors, int DynamicMessageReferences, int UnprovenStackOperations)
{
    public bool IsValid => Errors.Count == 0;
    public int UnreachableUnsupportedOperations { get; init; }
}

/// <summary>Checks selected field bytecode without executing it. Dynamic state and context-sensitive native ownership remain runtime checks.</summary>
public static class JsmResourceValidation
{
    private const int MaximumTrackedSuffixLength = 80;

    public static JsmResourceValidationResult Validate(byte[] content, int messageCount) => Validate(JsmProgram.Read(content), messageCount);

    public static JsmResourceValidationResult Validate(JsmProgram program, int messageCount)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentOutOfRangeException.ThrowIfNegative(messageCount);
        HashSet<JsmResourceValidationIssue> errors = [];
        HashSet<int> dynamicMessages = [];
        HashSet<int> unproven = [];
        HashSet<int> inspected = [];
        HashSet<int> reachable = program.Objects.SelectMany(obj => obj.Scripts).SelectMany(script => script.ReachableOperations).ToHashSet();
        HashSet<int> globalEntries = reachable.Select(index => program.Operations[index]).Where(operation => operation.Opcode == Jsm.Opcode.GJMP).Select(operation => program.NativeScriptPointers[operation.Parameter].Script.Start).ToHashSet();
        foreach (EventScript script in program.Objects.SelectMany(obj => obj.Scripts))
        {
            Dictionary<int, StackState> states = new() { [script.Start] = new(globalEntries.Contains(script.Start), []) };
            Queue<int> pending = new([script.Start]);
            while (pending.TryDequeue(out int index))
            {
                inspected.Add(index);
                JsmOperation operation = program.Operations[index];
                StackState entering = states[index];
                bool unknown = entering.UnknownPrefix;
                List<int?> values = entering.Values.ToList();
                bool validStack = true;
                int? condition = null;
                if (!EventEngine.SupportsInstructionOpcode(operation.Opcode))
                {
                    Error("unsupported-opcode", $"Required opcode {operation.Opcode} is not supported by the interpreter.");
                    Unknown();
                }
                else if (operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC)
                {
                    values.Add(operation.Parameter);
                }
                else if (operation.Opcode is Jsm.Opcode.PSHI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L)
                {
                    values.Add(null);
                }
                else if (operation.Opcode is Jsm.Opcode.POPI_L or Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
                {
                    _ = Pop(1);
                }
                else if (operation.Opcode == Jsm.Opcode.CAL)
                {
                    try
                    {
                        int count = NativeScalarCalculation.OperandCount(operation.Parameter);
                        int?[] operands = Pop(count);
                        int? result = null;
                        if (validStack && operands.All(value => value.HasValue))
                        {
                            result = NativeScalarCalculation.Resolve(operation.Parameter, count == 1 ? null : NativeScalarValue.Exact(operands[0]!.Value), NativeScalarValue.Exact(operands[^1]!.Value)).RequireExact();
                        }
                        values.Add(result);
                    }
                    catch (Exception exception) when (exception is NotSupportedException or ArithmeticException or InvalidDataException)
                    {
                        Error("invalid-calculation", exception.Message);
                        Unknown();
                    }
                }
                else if (operation.Opcode == Jsm.Opcode.JPF)
                {
                    condition = Pop(1)[0];
                }
                else if (MessageOperands(operation.Opcode) is int messageOperandCount)
                {
                    int?[] arguments = Pop(messageOperandCount);
                    if (validStack && arguments[1] is int message)
                    {
                        if (message < 0 || message >= messageCount)
                        {
                            Error("message-out-of-range", $"Message {message} is outside the selected MSD table ({messageCount} messages).");
                        }
                    }
                    else if (validStack)
                    {
                        dynamicMessages.Add(index);
                    }
                }
                else if (operation.Opcode is Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW)
                {
                    _ = Pop(2);
                    if ((uint)operation.Parameter >= program.NativeObjects.Count)
                    {
                        Error("invalid-object-reference", $"Request object {operation.Parameter} is outside the selected native object table.");
                    }
                }
                else if (operation.Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.JMP or Jsm.Opcode.RET or Jsm.Opcode.HALT))
                {
                    Unknown();
                }
                if (!validStack || operation.Opcode is Jsm.Opcode.RET or Jsm.Opcode.HALT)
                {
                    continue;
                }
                // Keep a bounded proved suffix; no stack-capacity claim is made without native frame ownership.
                if (values.Count > MaximumTrackedSuffixLength)
                {
                    values.RemoveRange(0, values.Count - MaximumTrackedSuffixLength);
                    unknown = true;
                    unproven.Add(index);
                }
                StackState next = new(unknown, values.ToArray());
                if (operation.Opcode == Jsm.Opcode.GJMP)
                {
                    Add(program.NativeScriptPointers[operation.Parameter].Script.Start, new(true, []));
                    next = new(true, []);
                }
                if (operation.Opcode == Jsm.Opcode.JMP || operation.Opcode == Jsm.Opcode.JPF && condition is null or 0)
                {
                    Add(JsmProgram.JumpTarget(index, operation.Parameter), next);
                }
                if (operation.Opcode != Jsm.Opcode.JMP && (operation.Opcode != Jsm.Opcode.JPF || condition is null || condition != 0))
                {
                    Add(index + 1, next);
                }

                void Error(string code, string message) => errors.Add(new(code, script.Id, index, message));
                void Unknown()
                {
                    unknown = true;
                    values.Clear();
                    unproven.Add(index);
                }
                int?[] Pop(int count)
                {
                    int?[] result = new int?[count];
                    if (!unknown && values.Count < count)
                    {
                        Error("operand-stack-underflow", $"{operation.Opcode} needs {count} operands; only {values.Count} are available on this proved path.");
                        validStack = false;
                    }
                    for (int operand = count - 1; operand >= 0; operand--)
                    {
                        if (values.Count > 0)
                        {
                            result[operand] = values[^1];
                            values.RemoveAt(values.Count - 1);
                        }
                        else if (unknown)
                        {
                            unproven.Add(index);
                        }
                    }
                    return result;
                }
                void Add(int target, StackState state)
                {
                    if (!script.ReachableOperations.Contains(target))
                    {
                        return;
                    }
                    if (!states.TryGetValue(target, out StackState? previous))
                    {
                        states.Add(target, state);
                        pending.Enqueue(target);
                        return;
                    }
                    StackState merged = Merge(previous, state);
                    if (previous.UnknownPrefix != merged.UnknownPrefix || !previous.Values.SequenceEqual(merged.Values))
                    {
                        states[target] = merged;
                        pending.Enqueue(target);
                    }
                }
            }
        }
        return new(errors.OrderBy(error => error.ScriptId).ThenBy(error => error.Operation).ThenBy(error => error.Code).ToArray(), dynamicMessages.Count, unproven.Count)
        {
            UnreachableUnsupportedOperations = program.Operations.Count(operation => !inspected.Contains(operation.Index) && !EventEngine.SupportsInstructionOpcode(operation.Opcode))
        };
    }

    private static int? MessageOperands(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.MES or Jsm.Opcode.MESW => 2,
        Jsm.Opcode.AMES or Jsm.Opcode.AMESW or Jsm.Opcode.RAMESW => 4,
        Jsm.Opcode.ASK => 6,
        Jsm.Opcode.AASK => 8,
        _ => null
    };

    private sealed record StackState(bool UnknownPrefix, int?[] Values);

    private static StackState Merge(StackState left, StackState right)
    {
        int count = Math.Min(left.Values.Length, right.Values.Length);
        int?[] values = new int?[count];
        for (int index = 1; index <= count; index++)
        {
            int? a = left.Values[^index];
            int? b = right.Values[^index];
            values[^index] = a == b ? a : null;
        }
        return new(left.UnknownPrefix || right.UnknownPrefix || left.Values.Length != right.Values.Length, values);
    }
}
