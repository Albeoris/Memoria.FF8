using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Mechanical field correspondence only; no prose matching or semantic suffix resynchronization.</summary>
public static class FieldCompatibilityAudit
{
    public static FieldCompatibilityResult Compare(FieldCompatibilityInput english, FieldCompatibilityInput localized)
    {
        ArgumentNullException.ThrowIfNull(english);
        ArgumentNullException.ThrowIfNull(localized);
        if (english.Field != localized.Field)
        {
            throw new ArgumentException("Counterparts must have the same logical field identity.");
        }
        FieldCompatibilityInventory baseline = Inspect(english);
        FieldCompatibilityInventory local = Inspect(localized);
        bool rawEqual = english.Jsm is not null && localized.Jsm is not null && english.Jsm.AsSpan().SequenceEqual(localized.Jsm);
        int common = 0;
        while (common < baseline.Instructions.Length && common < local.Instructions.Length && SameInstruction(baseline.Instructions[common], local.Instructions[common]))
        {
            common++;
        }
        bool layoutEqual = baseline.ExecutableLayout is not null && baseline.ExecutableLayout == local.ExecutableLayout;
        bool streamEqual = layoutEqual && common == baseline.Instructions.Length && common == local.Instructions.Length;
        bool layoutOnlyDivergence = !layoutEqual && common == baseline.Instructions.Length && common == local.Instructions.Length;
        int? structuralBoundary = layoutEqual ? null : StructuralBoundary(baseline, local);
        int trustedPrefix = Math.Min(common, structuralBoundary ?? common);
        bool complete = baseline.Problems.IsEmpty && local.Problems.IsEmpty;
        bool bankEqual = baseline.BankCountKnown && local.BankCountKnown && baseline.DeclaredTextCount == local.DeclaredTextCount;
        bool compatible = streamEqual && complete && bankEqual;
        FieldCompatibilityKind kind = compatible ? rawEqual ? FieldCompatibilityKind.ByteIdentical : FieldCompatibilityKind.StorageOnly : complete ? FieldCompatibilityKind.Divergent : FieldCompatibilityKind.Incomplete;
        List<string> reasons = [];
        if (!streamEqual)
        {
            reasons.Add("Executable instruction, operand, object, script pointer or category difference; no suffix resynchronization is permitted.");
        }
        if (!bankEqual)
        {
            reasons.Add("Text bank indexing or coverage differs.");
        }
        reasons.AddRange(baseline.Problems.Select(problem => "English: " + problem));
        reasons.AddRange(local.Problems.Select(problem => "Localized: " + problem));
        if (compatible && !rawEqual)
        {
            reasons.Add("Only storage padding/header offsets differ; category, group order, complete script pointers and encoded operations are identical.");
        }
        int? rawDifference = FirstByteDifference(english.Jsm, localized.Jsm);
        bool actualDivergence = !streamEqual;
        int boundary = actualDivergence ? trustedPrefix : int.MaxValue;
        FieldCompatibilityDivergence? divergence = compatible ? null : new(
            reasons.FirstOrDefault() ?? "Unresolved context",
            rawDifference,
            actualDivergence && !layoutOnlyDivergence && common < baseline.Instructions.Length ? baseline.Instructions[common].Position : null,
            actualDivergence && !layoutOnlyDivergence && common < local.Instructions.Length ? local.Instructions[common].Position : null,
            Preceding(baseline, common),
            Preceding(local, common), structuralBoundary);
        ImmutableArray<int> baseOrdinals = Ordinals(baseline);
        ImmutableArray<int> localOrdinals = Ordinals(local);
        // A divergent suffix has no approved semantic correspondence. Keep unused rows
        // and suffix reuse in the bound; exclude only proven prefix-exclusive references.
        ImmutableArray<int> candidates = compatible ? [] : streamEqual && complete ? localOrdinals.Except(baseOrdinals).ToImmutableArray() : CandidateSuffix(local, localOrdinals, trustedPrefix, complete);
        ImmutableArray<int> implicated = compatible ? [] : streamEqual && complete ? baseOrdinals.Except(localOrdinals).ToImmutableArray() : CandidateSuffix(baseline, baseOrdinals, trustedPrefix, complete);
        ImmutableArray<FieldCompatibilityMatch> matches = streamEqual && complete
            ? baseOrdinals.Intersect(localOrdinals).Select(ordinal => new FieldCompatibilityMatch(ordinal, ordinal, "Complete executable stream identity")).ToImmutableArray()
            : baseline.Contexts.Where(context => complete && context.Position.OperationIndex < trustedPrefix && context.IsMessage && context.MessageId is not null)
                .Select(context => context.MessageId!.Value).Intersect(baseOrdinals).Intersect(localOrdinals)
                .Select(ordinal => new FieldCompatibilityMatch(ordinal, ordinal, "Identical instruction prefix reference; suffix remains unapproved")).ToImmutableArray();
        int contexts = baseline.Contexts.Count(context => context.Dynamic) + local.Contexts.Count(context => context.Dynamic) + ConflictingMessageIds(baseline.Contexts).Count() + ConflictingMessageIds(local.Contexts).Count();
        ImmutableArray<FieldCompatibilityReviewCase> reviewCases = compatible ? CreateReviewCases(baseline, local, 0, [], [], false) : CreateReviewCases(baseline, local, boundary, implicated, candidates);
        return new(kind, baseline, local, divergence,
            baseline.Contexts.Where(context => boundary == int.MaxValue ? !compatible : context.Position.OperationIndex >= boundary).ToImmutableArray(),
            local.Contexts.Where(context => boundary == int.MaxValue ? !compatible : context.Position.OperationIndex >= boundary).ToImmutableArray(),
            matches, candidates, implicated, reviewCases.Length, contexts, baseline.BankCountKnown && local.BankCountKnown, reasons.ToImmutableArray(), reviewCases);
    }

    public static FieldCompatibilityInventory Inspect(FieldCompatibilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<string> problems = input.Errors?.ToList() ?? [];
        ImmutableArray<FieldCompatibilityRow> bank = input.Bank?.Select(row => new FieldCompatibilityRow(row.Ordinal, row.Text, row.Tokens?.ToImmutableArray() ?? [])).ToImmutableArray() ?? [];
        int? count = input.DeclaredTextCount ?? input.Bank?.Count;
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Declared bank count must be nonnegative.");
        }
        if (input.Bank is null)
        {
            problems.Add("Text bank is missing or could not be decoded.");
        }
        if (input.Bank is not null && (bank.Length != count || bank.Where((row, index) => row.Ordinal != index).Any()))
        {
            problems.Add("Text bank must preserve every contiguous zero-based ordinal.");
        }
        List<FieldCompatibilityInstruction> instructions = [];
        List<FieldCompatibilityContext> contexts = [];
        string? layout = null;
        if (input.Jsm is null)
        {
            problems.Add("JSM resource is missing.");
        }
        else
        {
            try
            {
                layout = Parse(input.Jsm, count, bank, instructions, contexts, problems);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or OverflowException)
            {
                problems.Add("JSM parsing failed: " + exception.Message);
            }
        }
        int[] referenced = contexts.Where(context => context.IsMessage && context.MessageId is not null).Select(context => context.MessageId!.Value).Distinct().Order().ToArray();
        foreach (int id in referenced.Where(id => id < 0 || count is null || id >= count))
        {
            problems.Add($"Referenced text ordinal {id} cannot be validated against the bank.");
        }
        foreach (FieldCompatibilityContext context in contexts.Where(context => context.Dynamic))
        {
            problems.Add($"Unresolved {context.Opcode} context at operation {context.Position.OperationIndex}; candidate bound is {(count is null ? "unknown" : count.ToString())} bank rows.");
        }
        return new(input.Field, input.Language, input.ResourceIdentity,
            input.Jsm is null ? null : Convert.ToHexString(SHA256.HashData(input.Jsm)), input.Jsm?.Length,
            "field-compatibility-v1/" + typeof(JsmDocument).Assembly.GetName().Version,
            count is not null, count, bank, instructions.ToImmutableArray(), contexts.ToImmutableArray(), referenced.ToImmutableArray(),
            count is int known ? Enumerable.Range(0, known).Except(referenced).ToImmutableArray() : [],
            bank.Where(row => row.Text.Length == 0 || row.Text == "{End}").Select(row => row.Ordinal).ToImmutableArray(), problems.ToImmutableArray(), layout);
    }

    private static string Parse(byte[] data, int? bankCount, ImmutableArray<FieldCompatibilityRow> bank, List<FieldCompatibilityInstruction> instructions, List<FieldCompatibilityContext> contexts, List<string> problems)
    {
        if (data.Length < 8)
        {
            throw new InvalidDataException("Incomplete header.");
        }
        int groups = data[0] + data[1] + data[2] + data[3];
        int scriptsOffset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
        if (8 + groups * 2 > scriptsOffset || scriptsOffset > operationsOffset || operationsOffset > data.Length || (data.Length - operationsOffset) % 4 != 0)
        {
            throw new InvalidDataException("Invalid group/script/operation boundaries.");
        }
        if ((data.Length - operationsOffset) / 4 > ushort.MaxValue + 1)
        {
            throw new InvalidDataException("Operations exceed the native unsigned WORD instruction-pointer domain.");
        }
        ushort[] rawGroups = Enumerable.Range(0, groups).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8 + index * 2))).ToArray();
        int scriptCount = rawGroups.Sum(group => (group & 127) + 1);
        if (scriptsOffset + (scriptCount + 1) * 2 > operationsOffset)
        {
            throw new InvalidDataException("Incomplete script pointer table.");
        }
        ushort[] pointers = Enumerable.Range(0, scriptCount + 1).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(scriptsOffset + index * 2))).ToArray();
        IReadOnlyList<JsmOperation> operations = JsmDocument.ReadOperations(data);
        HashSet<int> branchTargets = [];
        foreach (JsmOperation operation in operations.Where(operation => operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF))
        {
            int target = unchecked((ushort)(operation.Index + unchecked((short)operation.Parameter)));
            if ((uint)target >= operations.Count)
            {
                throw new InvalidDataException("Branch target is outside the operation stream.");
            }
            branchTargets.Add(target);
        }
        List<(int Object, int Label, int Script, int Start, int End)> ranges = [];
        int pointer = 0;
        int objectIndex = 0;
        HashSet<int> scriptLabels = [];
        foreach (ushort group in rawGroups.OrderBy(group => group >> 7))
        {
            for (int script = 0; script <= (group & 127); script++)
            {
                int start = pointers[pointer] & 0x7FFF;
                int end = pointers[++pointer] & 0x7FFF;
                if (start >= end || end > operations.Count)
                {
                    throw new InvalidDataException("Invalid script operation range.");
                }
                if (!scriptLabels.Add((group >> 7) + script))
                {
                    throw new InvalidDataException("Duplicate or overlapping script labels.");
                }
                if (operations[start].Opcode != Jsm.Opcode.LBL || operations[end - 1].Opcode != Jsm.Opcode.RET)
                {
                    throw new InvalidDataException("Script must begin with LBL and end with RET.");
                }
                ranges.Add((objectIndex, group >> 7, (group >> 7) + script, start, end));
            }
            objectIndex++;
        }
        Dictionary<(int Object, int Script), SortedDictionary<string, string>> observedStates = [];
        for (int index = 0; index < operations.Count; index++)
        {
            JsmOperation operation = operations[index];
            var owners = ranges.Where(range => range.Start <= index && index < range.End).ToArray();
            var owner = owners.Length == 1 ? owners[0] : (-1, -1, -1, 0, 0);
            FieldCompatibilityPosition position = new(owner.Item1, owner.Item2, owner.Item3, owners.Length == 1 ? index - owner.Item4 : index, index, operation.Offset);
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(operation.Offset));
            instructions.Add(new(position, operation.Opcode.ToString(), encoded, operation.Parameter));
            if (!Enum.IsDefined(operation.Opcode))
            {
                problems.Add($"Unsupported opcode {encoded:X8} at operation {index}.");
            }
            (int operandCount, bool message) = ContextOperands(operation.Opcode);
            if (operandCount == 0)
            {
                continue;
            }
            int?[] operands = new int?[operandCount];
            // Only direct literal pushes in the same script prove an operand. Expressions,
            // branches and stack values inherited across instructions stay unresolved.
            bool direct = owners.Length == 1 && index - operandCount >= owner.Item4;
            if (branchTargets.Any(target => target > index - operandCount && target <= index))
            {
                direct = false;
            }
            bool resultContext = operation.Opcode is Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.JPF or Jsm.Opcode.CAL;
            for (int argument = 0; argument < operandCount && direct; argument++)
            {
                JsmOperation push = operations[index - operandCount + argument];
                if (push.Opcode != Jsm.Opcode.PSHN_L)
                {
                    direct = false;
                }
                else
                {
                    operands[argument] = push.Parameter;
                }
            }
            if (!direct)
            {
                Array.Clear(operands);
            }
            if (resultContext)
            {
                operands = [operation.Parameter];
                direct = true;
            }
            int? messageId = message ? operands[1] : null;
            bool dynamic = !direct;
            ImmutableArray<int> bound = message && dynamic && bankCount is int known ? Enumerable.Range(0, known).ToImmutableArray() : [];
            var ownerKey = (position.ObjectIndex, position.ScriptId);
            if (!observedStates.TryGetValue(ownerKey, out SortedDictionary<string, string>? observedState))
            {
                observedState = new(StringComparer.Ordinal);
                observedStates.Add(ownerKey, observedState);
            }
            if (!message && !resultContext)
            {
                string key = operation.Opcode + ":" + (operands[0]?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "dynamic");
                observedState[key] = string.Join(',', operands.Select(value => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "dynamic"));
            }
            // These are local observations, never an assertion of the effective runtime
            // window state: field entry, other scripts and control flow remain unresolved.
            string state = "EntryAndControlFlowStateUnresolved:" + string.Join(';', observedState.Select(pair => pair.Key + "=" + pair.Value));
            contexts.Add(new(position, operation.Opcode.ToString(), operands.ToImmutableArray(), messageId, message, dynamic, bound, messageId is int id ? bank.FirstOrDefault(row => row.Ordinal == id) : null, state));
        }
        return Convert.ToHexString(data.AsSpan(0, 4)) + ":" + string.Join(',', rawGroups) + ":" + string.Join(',', pointers);
    }

    private static (int Count, bool Message) ContextOperands(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.MES or Jsm.Opcode.MESW => (2, true),
        Jsm.Opcode.AMES or Jsm.Opcode.AMESW or Jsm.Opcode.RAMESW => (4, true),
        Jsm.Opcode.ASK => (6, true),
        Jsm.Opcode.AASK => (8, true),
        Jsm.Opcode.WINSIZE => (5, false),
        Jsm.Opcode.WINCLOSE or Jsm.Opcode.MESSYNC or Jsm.Opcode.MESFORCUS => (1, false),
        Jsm.Opcode.MESMODE => (3, false),
        Jsm.Opcode.MESVAR or Jsm.Opcode.SETMESSPEED => (2, false),
        // Result expressions and stores are retained for choice/result consumers.
        Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.JPF or Jsm.Opcode.CAL => (1, false),
        _ => (0, false)
    };

    private static ImmutableArray<int> Ordinals(FieldCompatibilityInventory inventory) => inventory.DeclaredTextCount is int count ? Enumerable.Range(0, count).ToImmutableArray() : inventory.Bank.Select(row => row.Ordinal).Distinct().Order().ToImmutableArray();

    private static ImmutableArray<int> CandidateSuffix(FieldCompatibilityInventory inventory, ImmutableArray<int> ordinals, int boundary, bool complete)
    {
        if (!complete || inventory.Contexts.Any(context => context.IsMessage && context.Dynamic && context.Position.OperationIndex >= boundary))
        {
            return ordinals;
        }
        IEnumerable<int> prefix = inventory.Contexts.Where(context => context.IsMessage && context.MessageId is not null && context.Position.OperationIndex < boundary).Select(context => context.MessageId!.Value);
        IEnumerable<int> suffix = inventory.Contexts.Where(context => context.IsMessage && context.MessageId is not null && context.Position.OperationIndex >= boundary).Select(context => context.MessageId!.Value);
        return ordinals.Except(prefix.Except(suffix)).ToImmutableArray();
    }

    private static ImmutableArray<FieldCompatibilityReviewCase> CreateReviewCases(FieldCompatibilityInventory baseline, FieldCompatibilityInventory local, int boundary, ImmutableArray<int> englishOrdinals, ImmutableArray<int> localOrdinals, bool includeAlignment = true)
    {
        List<FieldCompatibilityReviewCase> cases = [];
        string prefix = baseline.Field + "/" + local.Language + "/";
        FieldCompatibilityContext[] englishContexts = baseline.Contexts.Where(context => boundary == int.MaxValue || context.Position.OperationIndex >= boundary).ToArray();
        FieldCompatibilityContext[] localContexts = local.Contexts.Where(context => boundary == int.MaxValue || context.Position.OperationIndex >= boundary).ToArray();
        if (includeAlignment)
        {
            cases.Add(new(prefix + "alignment", boundary == int.MaxValue ? "UnmatchedBankRun" : "UnmatchedExecutableSuffix", englishContexts.Select(context => context.Position).ToImmutableArray(), localContexts.Select(context => context.Position).ToImmutableArray(), englishOrdinals, localOrdinals));
        }
        AddContextCases(englishContexts, true);
        AddContextCases(localContexts, false);
        return cases.ToImmutableArray();

        void AddContextCases(FieldCompatibilityContext[] contexts, bool english)
        {
            string side = english ? "english" : "localized";
            foreach (IGrouping<string, FieldCompatibilityContext> group in contexts.Where(context => includeAlignment && (context.Dynamic || context.Opcode is "ASK" or "AASK")).GroupBy(ContextSignature))
            {
                FieldCompatibilityContext first = group.First();
                string kind = first.Dynamic ? "DynamicContext" : "ChoiceGroup";
                string signature = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(group.Key)))[..16];
                ImmutableArray<FieldCompatibilityPosition> positions = group.Select(context => context.Position).ToImmutableArray();
                ImmutableArray<int> ordinals = group.Where(context => context.MessageId is not null).Select(context => context.MessageId!.Value).Distinct().ToImmutableArray();
                cases.Add(new(prefix + side + "/" + kind + "/" + signature, kind, english ? positions : [], english ? [] : positions, english ? ordinals : [], english ? [] : ordinals));
            }
            foreach (IGrouping<int, FieldCompatibilityContext> group in contexts.Where(context => context.IsMessage && context.MessageId is not null).GroupBy(context => context.MessageId!.Value).Where(group => group.Select(ContextSignature).Distinct().Skip(1).Any()))
            {
                ImmutableArray<FieldCompatibilityPosition> positions = group.Select(context => context.Position).ToImmutableArray();
                cases.Add(new(prefix + side + "/repeated-use/" + group.Key, "RepeatedUseContextConflict", english ? positions : [], english ? [] : positions, english ? [group.Key] : [], english ? [] : [group.Key]));
            }
        }
    }

    private static string ContextSignature(FieldCompatibilityContext context) => context.Position.ObjectIndex + "/" + context.Position.ObjectLabel + "/" + context.Position.ScriptId + ":" + context.Opcode + ":" + string.Join(',', context.Operands.Select(value => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "dynamic")) + ":" + context.PresentationStateFingerprint;

    private static IEnumerable<int> ConflictingMessageIds(IEnumerable<FieldCompatibilityContext> contexts) => contexts.Where(context => context.IsMessage && context.MessageId is not null).GroupBy(context => context.MessageId!.Value).Where(group => group.Select(ContextSignature).Distinct().Skip(1).Any()).Select(group => group.Key);

    private static int? StructuralBoundary(FieldCompatibilityInventory baseline, FieldCompatibilityInventory local)
    {
        if (baseline.ExecutableLayout is null || local.ExecutableLayout is null)
        {
            return 0;
        }
        string[] englishLayout = baseline.ExecutableLayout.Split(':');
        string[] localLayout = local.ExecutableLayout.Split(':');
        // Native categories and physical group ordering precede script execution.
        // Any such change invalidates correspondence from the field entry.
        if (englishLayout[0] != localLayout[0] || englishLayout[1] != localLayout[1])
        {
            return 0;
        }
        int[] englishPointers = englishLayout[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        int[] localPointers = localLayout[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        if (englishPointers.Length != localPointers.Length)
        {
            return 0;
        }
        int? boundary = null;
        for (int index = 0; index < englishPointers.Length; index++)
        {
            if (englishPointers[index] == localPointers[index])
            {
                continue;
            }
            // A moved later boundary must not discard an earlier identical instruction
            // prefix. Pointer flag changes still invalidate their associated script.
            int affected = Math.Min(englishPointers[index] & 0x7FFF, localPointers[index] & 0x7FFF);
            boundary = Math.Min(boundary ?? affected, affected);
        }
        return boundary;
    }

    private static bool SameInstruction(FieldCompatibilityInstruction left, FieldCompatibilityInstruction right) => left.EncodedValue == right.EncodedValue && left.Position.ObjectIndex == right.Position.ObjectIndex && left.Position.ObjectLabel == right.Position.ObjectLabel && left.Position.ScriptId == right.Position.ScriptId;

    private static ImmutableArray<FieldCompatibilityInstruction> Preceding(FieldCompatibilityInventory inventory, int boundary) => inventory.Instructions.Take(Math.Min(boundary, inventory.Instructions.Length)).TakeLast(4).ToImmutableArray();

    private static int? FirstByteDifference(byte[]? left, byte[]? right)
    {
        if (left is null || right is null)
        {
            return null;
        }
        int index = 0;
        while (index < left.Length && index < right.Length && left[index] == right[index])
        {
            index++;
        }
        return index < left.Length || index < right.Length ? index : null;
    }
}
