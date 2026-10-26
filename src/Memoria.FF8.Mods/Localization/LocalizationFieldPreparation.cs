using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

internal static class LocalizationFieldPreparation
{
    public static LocalizationFieldText? Prepare(LocalizationRecord record)
    {
        if (!record.ExtensionData.TryGetValue("sourceContext", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{record.Key}: a field translation requires its original resource binding.");
        }
        if (!metadata.TryGetProperty("Call Contexts", out JsonElement contexts) || contexts.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{record.Key}: field call bindings must use the declared context representation.");
        }
        FieldCompatibilityContext[] calls = JsonSerializer.Deserialize<FieldCompatibilityContext[]>(contexts.GetString()!) ?? throw new InvalidDataException($"{record.Key}: missing field call bindings.");
        if (calls.Any(call => call is null || call.Position is null || call.Operands.IsDefault || call.CandidateOrdinals.IsDefault || call.Position.OperationIndex < 0))
        {
            throw new InvalidDataException($"{record.Key}: malformed field call binding.");
        }
        string? scriptHash = metadata.TryGetProperty("JSM SHA256", out JsonElement hash) && hash.ValueKind == JsonValueKind.String ? hash.GetString() : null;
        if (string.IsNullOrEmpty(scriptHash) && calls.Length == 0 && metadata.TryGetProperty("Source MSD SHA256", out JsonElement bankHash) && bankHash.ValueKind == JsonValueKind.String && bankHash.GetString() is { Length: 64 } bankFingerprint && bankFingerprint.All(char.IsAsciiHexDigit))
        {
            return null;
        }
        if (scriptHash?.Length != 64 || !scriptHash.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException($"{record.Key}: invalid original script fingerprint.");
        }
        scriptHash = scriptHash.ToUpperInvariant();
        int suffixStart = record.Key.IndexOf('_') + record.File.Length + 2;
        int contextStart = record.Key.IndexOf("_ctx_", suffixStart, StringComparison.Ordinal);
        string expectedBaseKey = contextStart < 0 ? record.Key : record.Key[..contextStart];
        if (metadata.TryGetProperty("Base Key", out JsonElement declaredBase) && (declaredBase.ValueKind != JsonValueKind.String || declaredBase.GetString() != expectedBaseKey))
        {
            throw new InvalidDataException($"{record.Key}: contradictory field context base identity.");
        }
        FieldMessageContext? presentation = null;
        int tagLength = 0;
        string target = record.Target!;
        if (target.StartsWith("{X", StringComparison.Ordinal) || target.StartsWith("{W", StringComparison.Ordinal) || target.StartsWith("{F", StringComparison.Ordinal))
        {
            if (!FieldMessageContext.TryParseTag(0, target, out presentation, out tagLength))
            {
                throw new InvalidDataException($"{record.Key}: malformed field presentation prefix.");
            }
        }
        if (presentation?.Size is { } size && (size.Width <= 0 || size.Height <= 0))
        {
            throw new InvalidDataException($"{record.Key}: field window size must be positive.");
        }
        string body = target[tagLength..];
        int authoredLineCount = LocalizationCatalog.CountAuthoredLines(body);
        if (presentation?.Choices is { } range && range.LastLine >= authoredLineCount)
        {
            throw new InvalidDataException($"{record.Key}: translated choice lines exceed the authored body.");
        }
        List<LocalizationCallBinding> bindings = [];
        ImmutableArray<LocalizationChoiceLineMapEntry> choiceLineMap = [];
        if (metadata.TryGetProperty("Choice Line Map", out JsonElement lineMap))
        {
            if (lineMap.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{record.Key}: choice line map requires its declared typed JSON representation.");
            }
            choiceLineMap = JsonSerializer.Deserialize<ImmutableArray<LocalizationChoiceLineMapEntry>>(lineMap.GetString()!);
            if (choiceLineMap.IsDefault || choiceLineMap.Any(entry => entry is null || entry.SourceLine < 0 || entry.TargetLine < 0) || choiceLineMap.Select(entry => entry.SourceLine).Distinct().Count() != choiceLineMap.Length)
            {
                throw new InvalidDataException($"{record.Key}: choice line map contains missing, negative or duplicate source lines.");
            }
        }
        foreach (FieldCompatibilityContext call in calls.Where(call => call.IsMessage))
        {
            if (call.Row is not null)
            {
                LocalizationTextValidation.Validate(record with { Source = call.Row.Text, SourcePresent = true });
            }
            if (call.Dynamic && presentation?.Choices is not null)
            {
                throw new InvalidDataException($"{record.Key}: dynamic choices require a choice line map instead of literal F/L/B/C.");
            }
            bool choice = call.Opcode is "ASK" or "AASK";
            int? Operand(int index) => index < call.Operands.Length ? call.Operands[index] : null;
            if (choice)
            {
                if (!call.Dynamic && (Operand(2) is null || Operand(3) is null))
                {
                    throw new InvalidDataException($"{record.Key}: static choice binding lacks its proven operands.");
                }
                if (!call.Dynamic)
                {
                    int first = Operand(2)!.Value;
                    int last = Operand(3)!.Value;
                    if (presentation?.Choices is not { } translated || translated.FirstLine < 0 || translated.LastLine < translated.FirstLine || (long)translated.LastLine - translated.FirstLine != (long)last - first || translated.BeginLine < translated.FirstLine || translated.BeginLine > translated.LastLine)
                    {
                        throw new InvalidDataException($"{record.Key}: translated choice must preserve native option count and explicit F/L/B/C.");
                    }
                    int? begin = Operand(4);
                    int? cancel = Operand(5);
                    if (begin is null || cancel is null || translated.BeginLine - translated.FirstLine != begin - first || (cancel < first || cancel > last ? translated.CancelLine != cancel : translated.CancelLine - translated.FirstLine != cancel - first))
                    {
                        throw new InvalidDataException($"{record.Key}: initial/cancel semantic option identity changed.");
                    }
                }
            }
            bindings.Add(new(call.Position.OperationIndex, call.MessageId, call.Dynamic, choice ? Operand(2) : null, choice ? Operand(3) : null, choice ? Operand(4) : null, choice ? Operand(5) : null) { IsChoice = choice });
        }
        if (presentation?.Choices is not null && !bindings.Any(binding => binding.FirstChoice is not null))
        {
            throw new InvalidDataException($"{record.Key}: choice metadata has no original choice call.");
        }
        if (!choiceLineMap.IsEmpty && !bindings.Any(binding => binding.Dynamic && binding.IsChoice))
        {
            throw new InvalidDataException($"{record.Key}: choice line map has no original dynamic choice call.");
        }
        return new(target[tagLength..], scriptHash, presentation?.Position?.X, presentation?.Position?.Y, presentation?.Size?.Width, presentation?.Size?.Height, presentation?.Choices?.FirstLine, presentation?.Choices?.LastLine, presentation?.Choices?.BeginLine, presentation?.Choices?.CancelLine, bindings.ToImmutableArray()) { BaseKey = expectedBaseKey, ChoiceLineMap = choiceLineMap };
    }
}
