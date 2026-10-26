using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Localization;

public sealed record LocalizationSourceContextBinding(int SchemaVersion, string? JsmSha256, string BaseKey, string? VariantKey, string Prefix, IReadOnlyList<int> OperationIndices, bool Dynamic);

/// <summary>Projects direct static operands only. Stateful window settings remain native.</summary>
public static class LocalizationSourceFieldContexts
{
    public static IReadOnlyList<LocalizationSourceEntry> Expand(LocalizationSourceEntry row, FieldCompatibilityInventory inventory)
    {
        int ordinal = int.Parse(row.Metadata["Source Ordinal"]!, CultureInfo.InvariantCulture);
        FieldCompatibilityContext[] calls = inventory.Contexts.Where(context => context.IsMessage && (context.MessageId == ordinal || context.MessageId is null && context.CandidateOrdinals.Contains(ordinal))).ToArray();
        var groups = calls.GroupBy(call => Prefix(call) is string prefix ? "static:" + prefix : "dynamic", StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        bool common = groups.Length == 1 && groups[0].Key.StartsWith("static:", StringComparison.Ordinal);
        List<LocalizationSourceContextBinding> bindings = [];
        List<(string Key, string Prefix, FieldCompatibilityContext[] Calls)> variants = [];
        HashSet<string> hashes = new(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            bool dynamic = group.Key == "dynamic";
            string prefix = dynamic ? "" : group.Key[7..];
            string? variant = null;
            if (!common && !dynamic && groups.Length > 1)
            {
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key)))[..16].ToLowerInvariant();
                if (!hashes.Add(hash))
                {
                    throw new InvalidDataException("Field presentation context identifier collision.");
                }
                variant = row.Key + "_ctx_" + hash;
                variants.Add((variant, prefix, group.ToArray()));
            }
            bindings.Add(new(1, inventory.JsmSha256, row.Key, variant, prefix, group.Select(call => call.Position.OperationIndex).Distinct().Order().ToArray(), dynamic));
        }
        string bindingJson = JsonSerializer.Serialize(bindings);
        Dictionary<string, string?> metadata = new(row.Metadata, StringComparer.Ordinal)
        {
            ["Context Bindings"] = bindingJson,
            ["Window State"] = "Native; WINSIZE control-flow state is unresolved and is never materialized by this exporter.",
            ["Call Contexts"] = JsonSerializer.Serialize(calls)
        };
        if (calls.Any(call => call.Dynamic && call.Opcode is "ASK" or "AASK"))
        {
            metadata["Choice Line Map"] = "[]";
        }
        string basePrefix = common ? groups[0].Key[7..] : "";
        List<LocalizationSourceEntry> result = [row with { Text = basePrefix + row.Text, Metadata = metadata }];
        foreach (var variant in variants)
        {
            Dictionary<string, string?> variantMetadata = new(metadata, StringComparer.Ordinal)
            {
                ["Base Key"] = row.Key,
                ["Context Variant"] = variant.Key,
                ["Call Contexts"] = JsonSerializer.Serialize(variant.Calls)
            };
            result.Add(row with { Key = variant.Key, Text = variant.Prefix + row.Text, Metadata = variantMetadata });
        }
        return result;
    }

    private static string? Prefix(FieldCompatibilityContext call)
    {
        // Dynamic IDs, channels, positions, and choice operands cannot be frozen into literals.
        if (call.Dynamic || call.MessageId is not int id || call.Operands.Any(operand => operand is null))
        {
            return null;
        }
        int Value(int index) => call.Operands[index]!.Value;
        FieldMessagePosition? position = call.Opcode switch
        {
            "AMES" or "AMESW" or "RAMESW" => new(Value(2), Value(3)),
            "AASK" => new(Value(6), Value(7)),
            _ => null
        };
        FieldMessageChoices? choices = call.Opcode is "ASK" or "AASK" ? new(Value(2), Value(3), Value(4), Value(5)) : null;
        return new FieldMessageContext(id, position, choices).FormatTag();
    }
}
