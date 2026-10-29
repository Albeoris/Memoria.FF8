using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static partial class DialogueTextFormatter
{
    private static string ExpandVariablesWithSourceSpans(string encoded, NativeMessageVariables? variables)
    {
        if (LocalizationTextTrace.Find(encoded) is null)
        {
            return NativeMessageVariables.Expand(encoded, variables);
        }
        return LocalizationTextComposition.Concat(NativeMessageVariables.ExpandSegments(encoded, variables).Select(segment =>
        {
            string source = LocalizationTextComposition.Slice(encoded, segment.SourceStart, segment.SourceLength);
            return segment.IsReplacement ? ExpandSourceToken(source, segment.Text) : source;
        }).ToArray());
    }

    private static string ExpandSourceToken(string source, string replacement)
    {
        if (LocalizationTextTrace.Find(source) is not { } binding)
        {
            return replacement;
        }
        if (binding.Fragments.Count == 0)
        {
            return LocalizationTextTrace.Attach(replacement, binding);
        }
        List<LocalizationTextBinding> owners = binding.Fragments.Where(fragment => fragment.Length > 0).Select(fragment => fragment.Binding).ToList();
        int covered = 0;
        int cursor = 0;
        foreach (LocalizationTextFragment fragment in binding.Fragments.OrderBy(fragment => fragment.Start))
        {
            int end = Math.Max(cursor, fragment.Start + fragment.Length);
            covered += Math.Max(0, end - Math.Max(cursor, fragment.Start));
            cursor = end;
        }
        if (binding.Key.Length != 0 && covered < source.Length)
        {
            owners.Add(binding with { Fragments = [] });
        }
        // A semantic token shared by several source leaves is indivisible; retain every owner instead of inventing a character split.
        LocalizationTextFragment[] fragments = owners.Distinct().Select(owner => new LocalizationTextFragment(0, replacement.Length, owner)).ToArray();
        return LocalizationTextTrace.Attach(replacement, binding with { Fragments = fragments });
    }
}
