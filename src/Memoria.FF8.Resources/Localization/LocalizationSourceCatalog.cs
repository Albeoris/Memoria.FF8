namespace Memoria.FF8.Resources.Localization;

public sealed record LocalizationSourceEntry(string Category, string File, string Key, string Text, string Language, IReadOnlyDictionary<string, string?> Metadata);
public sealed record LocalizationSourceGap(string Category, string Resource, string Reason);
public sealed record LocalizationSourceAvailability(string Language, bool Available, string? Reason);
public sealed record LocalizationSourceCatalog(IReadOnlyList<LocalizationSourceEntry> Entries, IReadOnlyList<LocalizationSourceGap> Gaps, IReadOnlyList<LocalizationSourceAvailability> Availability);
