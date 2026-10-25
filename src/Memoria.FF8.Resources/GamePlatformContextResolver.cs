using System.Text.RegularExpressions;

namespace Memoria.FF8.Resources;

/// <summary>Resolves selected installations without loading native code or trusting display labels.</summary>
public static class GamePlatformContextResolver
{
    public static GamePlatformContext Resolve(string installationDirectory, bool localOnly = false)
    {
        string directory = Path.GetFullPath(installationDirectory);
        List<string> evidence = [];
        bool classic = File.Exists(Path.Combine(directory, "FF8_EN.exe"));
        bool remastered = File.Exists(Path.Combine(directory, "main.zzz")) && File.Exists(Path.Combine(directory, "other.zzz")) && File.Exists(Path.Combine(directory, "FFVIII_EN.exe.dat"));
        GameDataEdition? edition = classic == remastered ? null : classic ? GameDataEdition.Classic : GameDataEdition.Remastered;
        GameDistributionProvider provider = GameDistributionProvider.Unknown;
        string? product = null;
        GameIntegrationTransport transport = GameIntegrationTransport.None;
        string diagnostic = "No verified storefront metadata for the selected installation; achievements remain local.";
        try
        {
            List<(GameDistributionProvider Provider, string Product, GameDataEdition Edition)> identities = [];
            DirectoryInfo? parent = Directory.GetParent(directory);
            if (parent?.Name.Equals("common", StringComparison.OrdinalIgnoreCase) == true && parent.Parent is DirectoryInfo steamApps)
            {
                foreach ((string appId, GameDataEdition expectedEdition) in new[] { ("39150", GameDataEdition.Classic), ("1026680", GameDataEdition.Remastered) })
                {
                    string manifest = Path.Combine(steamApps.FullName, $"appmanifest_{appId}.acf");
                    if (!File.Exists(manifest))
                    {
                        continue;
                    }
                    Dictionary<string, string> fields = ReadVdfFields(ReadMetadata(manifest));
                    fields.TryGetValue("appid", out string? declaredAppId);
                    fields.TryGetValue("installdir", out string? installName);
                    if (installName is null || installName != Path.GetFileName(installName) || !installName.Equals(Path.GetFileName(directory), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (declaredAppId != appId)
                    {
                        return Unknown("Conflicting Steam application metadata; external achievements are disabled.");
                    }
                    identities.Add((GameDistributionProvider.Steam, appId, expectedEdition));
                    evidence.Add($"Steam appmanifest {appId}, selected installation matched");
                }
            }
            AddGogIdentity(directory, identities, evidence);
            if (identities.Count > 1 || classic && remastered)
            {
                return Unknown("Conflicting installation identities or resource editions; external achievements are disabled.");
            }
            if (identities.Count == 1)
            {
                var identity = identities[0];
                if (edition != identity.Edition)
                {
                    return Unknown("Storefront metadata does not match the selected resource edition; external achievements are disabled.");
                }
                provider = identity.Provider;
                product = identity.Product;
                diagnostic = "Storefront identity detected; this build's native achievement integration has not been verified. Achievements remain local.";
                if (!localOnly && provider == GameDistributionProvider.Steam)
                {
                    transport = GameIntegrationTransport.SteamApi;
                    evidence.Add("Bundled Steam SDK transport selected from validated storefront metadata");
                    diagnostic = "Steam installation selected; the bundled SDK must validate its runtime product, user and stats readiness before delivery.";
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Text.Json.JsonException or RegexMatchTimeoutException or BadImageFormatException)
        {
            return Unknown("Installation metadata could not be validated; external achievements are disabled.");
        }
        return new GamePlatformContext(directory, edition, provider, product, transport, evidence.ToArray(), localOnly, localOnly ? "External achievements disabled by local-only selection." : diagnostic);

        GamePlatformContext Unknown(string reason)
        {
            return new GamePlatformContext(directory, edition, GameDistributionProvider.Unknown, null, GameIntegrationTransport.None, evidence.ToArray(), localOnly, reason);
        }
    }

    private static void AddGogIdentity(string directory, List<(GameDistributionProvider Provider, string Product, GameDataEdition Edition)> identities, List<string> evidence)
    {
        // GOG product metadata is validated separately from the native transport.
        string metadata = Path.Combine(directory, "goggame-1086370078.info");
        if (!File.Exists(metadata))
        {
            return;
        }
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(ReadMetadata(metadata));
        System.Text.Json.JsonElement root = document.RootElement;
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object || !root.TryGetProperty("gameId", out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String || id.GetString() != "1086370078")
        {
            throw new IOException("GOG product metadata is invalid.");
        }
        if (root.EnumerateObject().Count(property => property.NameEquals("gameId")) != 1 || root.TryGetProperty("rootGameId", out var rootId) && (rootId.ValueKind != System.Text.Json.JsonValueKind.String || rootId.GetString() != "1086370078"))
        {
            throw new IOException("GOG product metadata is conflicting.");
        }
        identities.Add((GameDistributionProvider.Gog, "1086370078", GameDataEdition.Remastered));
        evidence.Add("GOG product metadata 1086370078");
    }

    private static string ReadMetadata(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            throw new IOException("Installation metadata exceeds the supported size.");
        }
        return File.ReadAllText(path);
    }

    private static Dictionary<string, string> ReadVdfFields(string text)
    {
        MatchCollection tokens = Regex.Matches(text, "\\G(?:\\s+|//[^\\r\\n]*|(?<value>\"(?:[^\"\\\\]|\\\\.)*\")|(?<brace>[{}]))", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (tokens.Sum(token => token.Length) != text.Length)
        {
            throw new IOException("Invalid Steam manifest syntax.");
        }
        string[] values = tokens.Where(token => token.Groups["value"].Success || token.Groups["brace"].Success).Select(token => token.Value).ToArray();
        if (values.Length < 3 || !values[0].Equals("\"AppState\"", StringComparison.OrdinalIgnoreCase) || values[1] != "{")
        {
            throw new IOException("Invalid Steam manifest root.");
        }
        int position = 2;
        Dictionary<string, string> fields = ReadObject(0);
        if (position != values.Length)
        {
            throw new IOException("Unexpected Steam manifest content.");
        }
        return fields;

        Dictionary<string, string> ReadObject(int depth)
        {
            if (depth > 16)
            {
                throw new IOException("Steam manifest nesting exceeds the supported depth.");
            }
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            while (position < values.Length && values[position] != "}")
            {
                string key = values[position++];
                if (!key.StartsWith('"') || position >= values.Length)
                {
                    throw new IOException("Invalid Steam manifest field.");
                }
                string value = values[position++];
                if (value == "{")
                {
                    ReadObject(depth + 1);
                }
                else if (!value.StartsWith('"') || !result.TryAdd(key[1..^1], value[1..^1]))
                {
                    throw new IOException("Invalid or duplicate Steam manifest field.");
                }
            }
            if (position >= values.Length || values[position++] != "}")
            {
                throw new IOException("Unclosed Steam manifest object.");
            }
            return result;
        }
    }
}
