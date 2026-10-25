namespace Memoria.FF8.Resources;

public enum GameDistributionProvider
{
    Unknown,
    Steam,
    Gog
}

public enum GameIntegrationTransport
{
    None,
    SteamApi,
    Galaxy,
    SteamCompatibleGog
}

/// <summary>Static installation identity. It never establishes an authenticated platform user.</summary>
public sealed record GamePlatformContext(
    string InstallationDirectory,
    GameDataEdition? ResourceEdition,
    GameDistributionProvider Provider,
    string? ProductId,
    GameIntegrationTransport Transport,
    IReadOnlyList<string> Evidence,
    bool LocalOnly,
    string Diagnostic)
{
    public bool CanInitializeExternalBackend => !LocalOnly && Provider != GameDistributionProvider.Unknown && Transport != GameIntegrationTransport.None;
}
