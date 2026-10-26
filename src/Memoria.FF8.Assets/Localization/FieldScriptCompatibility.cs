using System.Collections.Immutable;
using System.Globalization;

namespace Memoria.FF8.Assets.Localization;

internal sealed record FieldScriptProjection(int ObjectIndex, int ObjectLabel, int ScriptId, int Start, int End, string EntryKey, ImmutableArray<string> Keys, ImmutableArray<int> Starts, string? Uncertainty, bool PresentationOnly);

/// <summary>Independent text correspondence within validated native script identities.</summary>
internal sealed class FieldScriptCompatibility
{
    private readonly ImmutableDictionary<(int Label, int Script), FieldScriptProjection> scripts;

    public FieldScriptCompatibility(FieldCompatibilityInventory inventory, FieldCompatibilityProjection projection)
    {
        if (!inventory.ScriptValidated || projection.LayoutKey is null)
        {
            scripts = ImmutableDictionary<(int, int), FieldScriptProjection>.Empty;
            return;
        }
        string[] layout = inventory.ExecutableLayout!.Split(':');
        byte[] counts = Convert.FromHexString(layout[0]);
        int[] groups = layout[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        int[] pointers = layout[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        var groupKeys = groups.Select((group, index) => (Label: group >> 7, Key: Category(index))).ToDictionary(group => group.Label, group => group.Key);
        Dictionary<int, (int Object, int Label, int Script)> owners = inventory.Instructions.ToDictionary(instruction => instruction.Position.OperationIndex, instruction => (instruction.Position.ObjectIndex, instruction.Position.ObjectLabel, instruction.Position.ScriptId));
        HashSet<(int, int)> foreignEntries = [];
        HashSet<int> scriptStarts = pointers.Take(pointers.Length - 1).Select(pointer => pointer & 0x7FFF).ToHashSet();
        foreach (FieldCompatibilityInstruction instruction in inventory.Instructions.Where(instruction => instruction.Opcode is "JMP" or "JPF"))
        {
            int target = unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter)));
            var source = owners[instruction.Position.OperationIndex];
            if (owners.TryGetValue(target, out var destination) && source != destination && !scriptStarts.Contains(target))
            {
                foreignEntries.Add((destination.Label, destination.Script));
            }
        }
        ImmutableDictionary<(int, int), FieldScriptProjection>.Builder result = ImmutableDictionary.CreateBuilder<(int, int), FieldScriptProjection>();
        int pointerIndex = 0;
        foreach (IGrouping<(int ObjectIndex, int ObjectLabel, int ScriptId), FieldCompatibilityInstruction> group in inventory.Instructions.Where(instruction => instruction.Position.ObjectIndex >= 0).GroupBy(instruction => (instruction.Position.ObjectIndex, instruction.Position.ObjectLabel, instruction.Position.ScriptId)))
        {
            int start = group.First().Position.OperationIndex;
            int end = group.Last().Position.OperationIndex + 1;
            FieldCompatibilityProjectionToken[] tokens = projection.Tokens.Where(token => token.StartOperation >= start && token.EndOperation <= end).ToArray();
            Dictionary<int, int> localBoundaries = projection.Boundaries.Where(boundary => boundary.Key >= start && boundary.Key <= end).ToDictionary(boundary => boundary.Key, boundary => boundary.Value - projection.Boundaries[start]);
            string? uncertainty = foreignEntries.Contains((group.Key.ObjectLabel, group.Key.ScriptId)) ? "External branch enters the script interior." : null;
            List<string> keys = [];
            foreach (FieldCompatibilityProjectionToken token in tokens)
            {
                FieldCompatibilityInstruction instruction = inventory.Instructions[token.StartOperation];
                string key = token.ComparisonKey;
                if (instruction.Opcode is "JMP" or "JPF")
                {
                    int target = unchecked((ushort)(instruction.Position.OperationIndex + unchecked((short)instruction.Parameter)));
                    if (target < start || target >= end || !localBoundaries.TryGetValue(target, out int destination))
                    {
                        uncertainty = "Branch target is outside the script or lacks a projected boundary.";
                    }
                    else
                    {
                        key = "Branch:" + (instruction.EncodedValue & unchecked((int)0xFFFF0000)).ToString("X8", CultureInfo.InvariantCulture) + ":" + destination.ToString(CultureInfo.InvariantCulture);
                    }
                }
                keys.Add(key);
            }
            if (pointerIndex >= pointers.Length - 1 || (pointers[pointerIndex] & 0x7FFF) != start || (pointers[pointerIndex + 1] & 0x7FFF) != end)
            {
                uncertainty = "Script has no matching physical pointer range.";
            }
            string entry = group.Key.ObjectIndex + ":" + groupKeys[group.Key.ObjectLabel] + ":" + pointerIndex + ":" + (pointers[pointerIndex] & 0x8000);
            bool presentationOnly = projection.PresentationOnlyScripts.Contains((group.Key.ObjectLabel, group.Key.ScriptId));
            result.Add((group.Key.ObjectLabel, group.Key.ScriptId), new(group.Key.ObjectIndex, group.Key.ObjectLabel, group.Key.ScriptId, start, end, entry, presentationOnly ? [] : keys.ToImmutableArray(), presentationOnly ? [] : tokens.Select(token => token.StartOperation).ToImmutableArray(), uncertainty, presentationOnly));
            pointerIndex++;
        }
        scripts = result.ToImmutable();

        string Category(int index)
        {
            if (index < counts[1])
            {
                return "Door:" + index;
            }
            index -= counts[1];
            if (index < counts[0])
            {
                return "Area:" + index;
            }
            index -= counts[0];
            if (index < counts[2])
            {
                return "Module:" + index;
            }
            return "Actor:" + (index - counts[2]);
        }
    }

    public static ImmutableArray<FieldScriptIdentityEvidence> Compare(FieldScriptCompatibility english, FieldScriptCompatibility local)
    {
        List<FieldScriptIdentityEvidence> result = [];
        foreach (var key in english.scripts.Keys.Union(local.scripts.Keys).OrderBy(key => key.Label).ThenBy(key => key.Script))
        {
            english.scripts.TryGetValue(key, out FieldScriptProjection? baseline);
            local.scripts.TryGetValue(key, out FieldScriptProjection? localized);
            bool validated = baseline is not null && localized is not null && baseline.Uncertainty is null && localized.Uncertainty is null && baseline.EntryKey == localized.EntryKey;
            int matched = 0;
            if (validated)
            {
                while (matched < baseline!.Keys.Length && matched < localized!.Keys.Length && baseline.Keys[matched] == localized.Keys[matched])
                {
                    matched++;
                }
            }
            bool equal = validated && matched == baseline!.Keys.Length && matched == localized!.Keys.Length;
            result.Add(new(key.Label, key.Script, baseline?.ObjectIndex, localized?.ObjectIndex, validated, equal, matched, Boundary(baseline, matched), Boundary(localized, matched), validated ? equal ? "Complete script text projection equality." : "First script projection difference; no resynchronization within this script." : baseline?.Uncertainty ?? localized?.Uncertainty ?? "Native script identity, category, pointer index or pointer flag differs or is missing.") { PresentationOnly = validated && baseline!.PresentationOnly && localized!.PresentationOnly, EnglishPresentationOnly = baseline?.PresentationOnly == true, LocalPresentationOnly = localized?.PresentationOnly == true });
        }
        return result.ToImmutableArray();
    }

    private static int? Boundary(FieldScriptProjection? script, int matched) => script is null ? null : matched < script.Starts.Length ? script.Starts[matched] : script.End;
}
