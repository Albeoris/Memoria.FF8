namespace Memoria.FF8.Platform;

public sealed record GameLanguageProfile(string Edition, string ResourceLanguage);

public sealed record GameLanguageChoice(string DisplayTag, string ProviderId, string? Option, GameLanguageProfile Profile, string? PackageId)
{
    public string? InstallationDirectory { get; init; }
}
