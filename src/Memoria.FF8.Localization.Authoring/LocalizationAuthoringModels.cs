using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization.Authoring;

public sealed record LocalizationRowIdentity(string Category, string File, string Key);
public sealed record LocalizationTargetValue(bool Present, string? Text);
public sealed record LocalizationRowSnapshot(string ProjectId, LocalizationRowIdentity Identity, LocalizationRecord Row, LocalizationTargetValue Target, string Revision)
{
    public bool Missing { get; init; }
}
public enum LocalizationCommitStatus
{
    Saved,
    Unchanged,
    Conflict,
    Busy,
    Invalid,
    Cancelled,
    Failed
}
public sealed record LocalizationCommitResult(LocalizationCommitStatus Status, LocalizationRowSnapshot? Current, string? Message = null);
public interface ILocalizationProjectStore
{
    Task<LocalizationRowSnapshot> ReadRowAsync(LocalizationRowIdentity identity, CancellationToken cancellation = default);
    Task<LocalizationCommitResult> CommitAsync(LocalizationRowSnapshot expected, LocalizationTargetValue target, CancellationToken cancellation = default);
}

[Flags]
public enum LocalizationEditFields
{
    None = 0,
    Body = 1,
    Position = 2,
    Size = 4,
    Choices = 8,
    Presence = 16,
    All = Body | Position | Size | Choices | Presence
}
public sealed record LocalizationTextSpan(int Start, int Length);
public sealed record LocalizationTargetParts(string Body, string? Position, string? Size, string? Choices, string Prefix = "")
{
    public LocalizationTextSpan BodySpan { get; init; } = new(0, Body.Length);
    public LocalizationTextSpan? PositionSpan { get; init; }
    public LocalizationTextSpan? SizeSpan { get; init; }
    public LocalizationTextSpan? ChoicesSpan { get; init; }
}
public interface ILocalizationTargetSyntax
{
    LocalizationTargetParts Parse(string? text);
    string Compose(LocalizationTargetParts parts);
    LocalizationTargetValue SetOrigin(LocalizationTargetValue target, string source, int x, int y);
}
public sealed record LocalizationChange(Guid Id, LocalizationRowSnapshot Before, LocalizationTargetValue After, LocalizationEditFields Fields, string Summary);
public sealed record LocalizationObservation(Guid Id, DateTimeOffset ObservedAt, LocalizationRowIdentity Identity, string Text, string? Context = null);

public static class LocalizationReplay
{
    public static LocalizationEditFields Changes(ILocalizationTargetSyntax syntax, LocalizationTargetValue before, LocalizationTargetValue after)
    {
        LocalizationTargetParts a = syntax.Parse(before.Text);
        LocalizationTargetParts b = syntax.Parse(after.Text);
        return (a.Body != b.Body ? LocalizationEditFields.Body : 0) | (a.Position != b.Position ? LocalizationEditFields.Position : 0) | (a.Size != b.Size ? LocalizationEditFields.Size : 0) | (a.Choices != b.Choices ? LocalizationEditFields.Choices : 0) | (before.Present != after.Present || before.Text is null != (after.Text is null) || string.IsNullOrEmpty(before.Text) != string.IsNullOrEmpty(after.Text) ? LocalizationEditFields.Presence : 0);
    }

    public static LocalizationTargetValue Propose(ILocalizationTargetSyntax syntax, LocalizationChange change, LocalizationTargetValue current, bool redo, Func<string> source)
    {
        LocalizationTargetValue selected = redo ? change.After : change.Before.Target;
        LocalizationTargetParts existing = syntax.Parse(current.Text);
        LocalizationTargetParts desired = syntax.Parse(selected.Text);
        bool Own(LocalizationEditFields field) => change.Fields.HasFlag(field);
        LocalizationTargetParts merged = existing with
        {
            Body = Own(LocalizationEditFields.Body) ? desired.Body : existing.Body,
            Position = Own(LocalizationEditFields.Position) ? desired.Position : existing.Position,
            Size = Own(LocalizationEditFields.Size) ? desired.Size : existing.Size,
            Choices = Own(LocalizationEditFields.Choices) ? desired.Choices : existing.Choices
        };
        bool hasPrefix = merged.Position is not null || merged.Size is not null || merged.Choices is not null;
        if (merged.Body.Length == 0 && hasPrefix)
        {
            merged = merged with { Body = syntax.Parse(source()).Body };
            if (merged.Body.Length == 0)
            {
                throw new InvalidOperationException("Retained presentation requires a valid source body.");
            }
        }
        if (!hasPrefix && merged.Body.Length == 0 && Own(LocalizationEditFields.Presence))
        {
            return selected;
        }
        return new LocalizationTargetValue(true, syntax.Compose(merged));
    }
}

public static class LocalizationJournalSearch
{
    public static bool Matches(string query, bool caseSensitive, params string?[] values) => values.Any(value => value?.Contains(query, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == true);
}
