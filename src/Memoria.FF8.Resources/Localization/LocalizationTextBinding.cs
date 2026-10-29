using System.Runtime.CompilerServices;

namespace Memoria.FF8.Resources.Localization;

/// <summary>Canonical presentation identity. Arguments remain separate from the editable template.</summary>
public sealed record LocalizationTextBinding(string Key, string Source, string Template, string Text)
{
    public string? Category { get; init; }
    public string? File { get; init; }
    public IReadOnlyDictionary<int, string> Names { get; init; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, int?> MessageVariables { get; init; } = new Dictionary<int, int?>();
    public string? PackageId { get; init; }
    public string? ProjectId { get; init; }
    public string? BaseKey { get; init; }
    public string? ResourceFingerprint { get; init; }
    public int? Operation { get; init; }
    public IReadOnlyList<object?> Arguments { get; init; } = [];
    public IReadOnlyList<LocalizationTextFragment> Fragments { get; init; } = [];
}

public sealed record LocalizationTextFragment(int Start, int Length, LocalizationTextBinding Binding);

public sealed record BoundLocalizationText(string Text, LocalizationTextBinding Binding);

/// <summary>
/// Preserves bindings through existing string-returning APIs by instance identity, never by matching displayed wording.
/// Explicit bound-text consumers do not need this compatibility bridge. Disabled inspection retains no strings.
/// </summary>
public static class LocalizationTextTrace
{
    private static readonly ConditionalWeakTable<string, LocalizationTextBinding> bindings = new();
    public static bool Enabled { get; set; }

    public static string Attach(string text, LocalizationTextBinding binding)
    {
        if (!Enabled || text.Length == 0)
        {
            return text;
        }
        string owned = new(text.AsSpan());
        bindings.Add(owned, binding with { Text = owned });
        return owned;
    }

    public static LocalizationTextBinding? Find(string text)
    {
        return Enabled && bindings.TryGetValue(text, out LocalizationTextBinding? binding) ? binding : null;
    }

    public static string Formatted(string template, string formatted, IReadOnlyList<object?> arguments)
    {
        LocalizationTextBinding? binding = Find(template);
        return binding is null ? formatted : Attach(formatted, binding with { Arguments = arguments.ToArray(), Text = formatted });
    }
}
