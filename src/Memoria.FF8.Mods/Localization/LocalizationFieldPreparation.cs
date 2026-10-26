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
        FieldCompatibilityContext[] calls = metadata.TryGetProperty("Call Contexts", out JsonElement contexts) && contexts.ValueKind == JsonValueKind.String ? JsonSerializer.Deserialize<FieldCompatibilityContext[]>(contexts.GetString()!) ?? [] : [];
        string? scriptHash = metadata.TryGetProperty("JSM SHA256", out JsonElement hash) && hash.ValueKind == JsonValueKind.String ? hash.GetString() : null;
        if (string.IsNullOrEmpty(scriptHash) && calls.Length == 0 && metadata.TryGetProperty("Source MSD SHA256", out JsonElement bankHash) && bankHash.ValueKind == JsonValueKind.String && bankHash.GetString()?.Length == 64)
        {
            return null;
        }
        if (scriptHash?.Length != 64 || !scriptHash.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException($"{record.Key}: invalid original script fingerprint.");
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
        int authoredLineCount = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Count(character => character == '\n') + 1 + System.Text.RegularExpressions.Regex.Matches(body, "\\{(?:Line|Page|Next)\\}").Count;
        if (presentation?.Choices is { } range && range.LastLine >= authoredLineCount)
        {
            throw new InvalidDataException($"{record.Key}: translated choice lines exceed the authored body.");
        }
        List<LocalizationCallBinding> bindings = [];
        foreach (FieldCompatibilityContext call in calls.Where(call => call.IsMessage))
        {
            if (call.Row is not null)
            {
                LocalizationTextValidation.Validate(record with { Source = call.Row.Text, SourcePresent = true });
            }
            bool choice = call.Opcode is "ASK" or "AASK";
            int? Operand(int index) => index < call.Operands.Length ? call.Operands[index] : null;
            if (choice)
            {
                if (call.Dynamic || Operand(2) is not int first || Operand(3) is not int last)
                {
                    throw new InvalidDataException($"{record.Key}: dynamic choice presentation has no supported translation binding.");
                }
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
            bindings.Add(new(call.Position.OperationIndex, call.MessageId, call.Dynamic, choice ? Operand(2) : null, choice ? Operand(3) : null, choice ? Operand(4) : null, choice ? Operand(5) : null));
        }
        if (presentation?.Choices is not null && !bindings.Any(binding => binding.FirstChoice is not null))
        {
            throw new InvalidDataException($"{record.Key}: choice metadata has no original choice call.");
        }
        return new(target[tagLength..], scriptHash, presentation?.Position?.X, presentation?.Position?.Y, presentation?.Size?.Width, presentation?.Size?.Height, presentation?.Choices?.FirstLine, presentation?.Choices?.LastLine, presentation?.Choices?.BeginLine, presentation?.Choices?.CancelLine, bindings.ToImmutableArray()) { BaseKey = metadata.TryGetProperty("Base Key", out JsonElement baseKey) && baseKey.ValueKind == JsonValueKind.String ? baseKey.GetString() : record.Key };
    }
}
