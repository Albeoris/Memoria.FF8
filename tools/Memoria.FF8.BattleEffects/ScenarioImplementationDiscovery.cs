using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record ImplementationRegistration(uint Entry, int ComparisonIlOffset, int BranchTargetIlOffset, string CalledMethod, string CalledMethodIlSha256);
public sealed record ImplementationBindingEvidence(int EffectId, uint Entry, string Disposition, ImplementationRegistration[] Registrations);
public sealed record ImplementationDiscoveryEvidence(int SchemaVersion, string Scope, string Dispatcher, string DispatcherIlSha256, string RuntimeAssemblySha256, ImplementationRegistration[] Registrations, string[] OpaqueCalledMethods, ImplementationBindingEvidence[] Bindings, string[] Boundaries);

/// <summary>Reads compiled dispatch structure without invoking combat or claiming playable support.</summary>
public static class ScenarioImplementationDiscovery
{
    private sealed record Instruction(int Offset, OpCode Code, object? Operand);
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => code.Value);

    public static ImplementationDiscoveryEvidence Discover(IEnumerable<NativeBattleEffectBinding> bindings)
    {
        MethodInfo dispatcher = typeof(BattleSession).GetMethod("InvokeNativeOriginalEffectEntry", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidDataException("Compiled native entry dispatcher is unavailable.");
        return Inspect(dispatcher, bindings);
    }

    public static ImplementationDiscoveryEvidence Inspect(MethodInfo dispatcher, IEnumerable<NativeBattleEffectBinding> bindings)
    {
        byte[] bytes = dispatcher.GetMethodBody()?.GetILAsByteArray() ?? throw new InvalidDataException("Compiled dispatcher has no readable IL body.");
        List<Instruction> instructions = Decode(dispatcher, bytes);
        List<ImplementationRegistration> registrations = [];
        for (int index = 2; index < instructions.Count; index++)
        {
            Instruction branch = instructions[index];
            if (branch.Code != OpCodes.Bne_Un && branch.Code != OpCodes.Bne_Un_S)
            {
                continue;
            }
            if (instructions[index - 2].Operand is not MethodInfo getter || getter.DeclaringType != typeof(NativeBattleEffectBinding) || getter.Name != "get_Entry" || !TryConstant(instructions[index - 1], out uint entry) || branch.Operand is not int end || end <= branch.Offset)
            {
                continue;
            }
            Instruction[] body = instructions.Skip(index + 1).TakeWhile(instruction => instruction.Offset < end).ToArray();
            MethodInfo[] calls = body.Where(instruction => instruction.Code == OpCodes.Call || instruction.Code == OpCodes.Callvirt).Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
            if (body.Length is < 2 or > 64 || calls.Length != 1 || calls[0].DeclaringType != dispatcher.DeclaringType || !(calls[0].Name.StartsWith("Enter", StringComparison.Ordinal) || calls[0].Name.StartsWith("Run", StringComparison.Ordinal)) || body[^1].Code != OpCodes.Ret || body.Any(instruction => !IsDirectReturnInstruction(instruction)))
            {
                continue;
            }
            byte[]? targetIl = calls[0].GetMethodBody()?.GetILAsByteArray();
            if (targetIl is not null)
            {
                registrations.Add(new(entry, instructions[index - 2].Offset, end, Name(calls[0]), Hash(targetIl)));
            }
        }
        string[] opaque = instructions.Where(instruction => instruction.Code == OpCodes.Call || instruction.Code == OpCodes.Callvirt).Select(instruction => instruction.Operand).OfType<MethodInfo>().Where(method => method.DeclaringType == dispatcher.DeclaringType && (method.Name.StartsWith("Enter", StringComparison.Ordinal) || method.Name.StartsWith("Run", StringComparison.Ordinal)) && !registrations.Any(registration => registration.CalledMethod == Name(method))).Select(Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        ImplementationBindingEvidence[] links = bindings.Select(binding =>
        {
            ImplementationRegistration[] matches = registrations.Where(registration => registration.Entry == binding.Entry).ToArray();
            return new ImplementationBindingEvidence(binding.Effect, binding.Entry, matches.Length > 0 ? "direct_equality_branch_observed" : "unknown", matches);
        }).ToArray();
        string assemblyPath = dispatcher.Module.Assembly.Location;
        return new(1, "compiled_dispatch_registration_evidence_only", Name(dispatcher), Hash(bytes), string.IsNullOrEmpty(assemblyPath) ? "unavailable" : Hash(File.ReadAllBytes(assemblyPath)), registrations.ToArray(), opaque, links, ["Direct mappings require get_Entry, an immediate integer constant, forward bne.un and a straight-line Enter*/Run* return block. Argument loads may include instance field reads, including compiler-captured arguments; writes and additional control flow remain unknown.", "Opaque calls include generic range/status/cinematic dispatch without safely decoded entry equality; no binding mapping is inferred.", "A registered dispatch branch is not proof of effect completeness, initialization, owner legality, native parity or playable scenario support.", "IL hashes identify this compiled build; metadata tokens and branch layouts can change between builds."]);
    }

    private static bool IsDirectReturnInstruction(Instruction instruction)
    {
        return instruction.Code == OpCodes.Nop || instruction.Code == OpCodes.Ret || instruction.Code == OpCodes.Call || instruction.Code == OpCodes.Callvirt || instruction.Code == OpCodes.Ldfld && instruction.Operand is FieldInfo || instruction.Code.Name?.StartsWith("ldarg", StringComparison.Ordinal) == true || instruction.Code.Name?.StartsWith("ldloc", StringComparison.Ordinal) == true || instruction.Code.Name?.StartsWith("ldc.i4", StringComparison.Ordinal) == true;
    }

    private static bool TryConstant(Instruction instruction, out uint value)
    {
        value = 0;
        if (instruction.Code == OpCodes.Ldc_I4 && instruction.Operand is int integer)
        {
            value = unchecked((uint)integer);
            return true;
        }
        if (instruction.Code == OpCodes.Ldc_I4_S && instruction.Operand is sbyte small)
        {
            value = unchecked((uint)small);
            return true;
        }
        return false;
    }

    private static List<Instruction> Decode(MethodInfo owner, byte[] bytes)
    {
        List<Instruction> result = [];
        int position = 0;
        while (position < bytes.Length)
        {
            int start = position;
            short code = bytes[position++];
            if (code == 0xFE)
            {
                code = unchecked((short)(0xFE00 | bytes[position++]));
            }
            if (!Codes.TryGetValue(code, out OpCode operation))
            {
                throw new InvalidDataException("Unrecognized compiled dispatcher opcode.");
            }
            object? operand = null;
            int size = operation.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + BitConverter.ToInt32(bytes, position) * 4),
                _ => 4
            };
            if (size < 0 || position + size > bytes.Length)
            {
                throw new InvalidDataException("Truncated compiled dispatcher operand.");
            }
            if (operation.OperandType == OperandType.InlineMethod)
            {
                operand = owner.Module.ResolveMethod(BitConverter.ToInt32(bytes, position), owner.DeclaringType?.GetGenericArguments(), owner.GetGenericArguments());
            }
            else if (operation.OperandType == OperandType.InlineField)
            {
                operand = owner.Module.ResolveField(BitConverter.ToInt32(bytes, position), owner.DeclaringType?.GetGenericArguments(), owner.GetGenericArguments());
            }
            else if (operation.OperandType == OperandType.InlineBrTarget)
            {
                operand = position + size + BitConverter.ToInt32(bytes, position);
            }
            else if (operation.OperandType == OperandType.ShortInlineBrTarget)
            {
                operand = position + size + unchecked((sbyte)bytes[position]);
            }
            else if (operation.OperandType == OperandType.InlineI)
            {
                operand = BitConverter.ToInt32(bytes, position);
            }
            else if (operation.OperandType == OperandType.ShortInlineI)
            {
                operand = unchecked((sbyte)bytes[position]);
            }
            result.Add(new(start, operation, operand));
            position += size;
        }
        return result;
    }

    private static string Name(MethodInfo method) => method.DeclaringType?.FullName + "." + method.Name;
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
