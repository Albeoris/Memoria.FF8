using System.Globalization;
using System.Text;

namespace Memoria.FF8.Resources.Localization;

/// <summary>Composes known string instances while retaining the canonical owners of their displayed spans.</summary>
public static class LocalizationTextComposition
{
    public static string Concat(params string?[] values) => Join("", values);

    public static string Join(string separator, IEnumerable<string?> values)
    {
        if (!LocalizationTextTrace.Enabled)
        {
            return string.Join(separator, values);
        }
        StringBuilder result = new();
        List<LocalizationTextFragment> fragments = [];
        bool first = true;
        foreach (string? value in values)
        {
            if (!first)
            {
                Append(result, fragments, separator);
            }
            first = false;
            Append(result, fragments, value ?? "");
        }
        return Attach(result.ToString(), fragments, null);
    }

    public static string Format(string template, params object?[] arguments)
    {
        if (!LocalizationTextTrace.Enabled)
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        CompositeFormat.Parse(template);
        StringBuilder result = new();
        List<LocalizationTextFragment> fragments = [];
        for (int cursor = 0; cursor < template.Length;)
        {
            char current = template[cursor++];
            if (current == '{' && cursor < template.Length && template[cursor] == '{' || current == '}' && cursor < template.Length && template[cursor] == '}')
            {
                result.Append(current);
                cursor++;
                continue;
            }
            if (current != '{')
            {
                result.Append(current);
                continue;
            }
            int close = template.IndexOf('}', cursor);
            string token = template[cursor..close];
            int delimiter = token.IndexOfAny([',', ':']);
            string indexText = delimiter < 0 ? token : token[..delimiter];
            int index = int.Parse(indexText.Trim(), CultureInfo.InvariantCulture);
            if ((uint)index >= arguments.Length)
            {
                throw new FormatException("A localization format argument is missing.");
            }
            string suffix = delimiter < 0 ? "" : token[delimiter..];
            string formatted = string.Format(CultureInfo.CurrentCulture, "{0" + suffix + "}", arguments[index]);
            if (arguments[index] is string text)
            {
                int leftPadding = 0;
                int comma = token.IndexOf(',');
                int colon = token.IndexOf(':');
                if (comma >= 0 && (colon < 0 || comma < colon))
                {
                    int alignment = int.Parse(token[(comma + 1)..(colon < 0 ? token.Length : colon)].Trim(), CultureInfo.InvariantCulture);
                    leftPadding = alignment > 0 ? Math.Max(0, formatted.Length - text.Length) : 0;
                }
                AddFragments(fragments, text, result.Length + leftPadding);
            }
            result.Append(formatted);
            cursor = close + 1;
        }
        LocalizationTextBinding? binding = LocalizationTextTrace.Find(template);
        return Attach(result.ToString(), fragments, binding is null ? null : binding with { Arguments = arguments.ToArray() });
    }

    private static void Append(StringBuilder result, List<LocalizationTextFragment> fragments, string value)
    {
        AddFragments(fragments, value, result.Length);
        result.Append(value);
    }

    private static void AddFragments(List<LocalizationTextFragment> destination, string text, int offset)
    {
        if (LocalizationTextTrace.Find(text) is not { } binding)
        {
            return;
        }
        int cursor = 0;
        foreach (LocalizationTextFragment fragment in binding.Fragments.OrderBy(fragment => fragment.Start))
        {
            if (binding.Key.Length != 0 && fragment.Start > cursor)
            {
                destination.Add(new(offset + cursor, fragment.Start - cursor, binding with { Fragments = [] }));
            }
            destination.Add(fragment with { Start = offset + fragment.Start });
            cursor = fragment.Start + fragment.Length;
        }
        if (binding.Key.Length != 0 && cursor < text.Length)
        {
            destination.Add(new(offset + cursor, text.Length - cursor, binding with { Fragments = [] }));
        }
    }

    private static string Attach(string text, IReadOnlyList<LocalizationTextFragment> fragments, LocalizationTextBinding? binding)
    {
        if (!LocalizationTextTrace.Enabled || binding is null && fragments.Count == 0)
        {
            return text;
        }
        return LocalizationTextTrace.Attach(text, (binding ?? new LocalizationTextBinding("", "", "", text)) with { Fragments = fragments });
    }
}
