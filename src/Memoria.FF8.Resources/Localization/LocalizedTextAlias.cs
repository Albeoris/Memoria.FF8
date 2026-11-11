namespace Memoria.FF8.Resources.Localization;

/// <summary>Redirects a localized text identity while retaining the target's native fallback.</summary>
public sealed record LocalizedTextAlias(string Key, string Original);
