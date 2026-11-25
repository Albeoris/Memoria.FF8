using System.Text.Json;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Builds native bindings from installed assets, preserving the package's canonical target identities.</summary>
internal static class LocalizationNativeEdition
{
    internal const string TargetsMember = "Edition Targets";

    internal static void ValidateTargets(LocalizationRecord record, LocalizationManifest manifest)
    {
        if (!record.ExtensionData.TryGetValue(TargetsMember, out JsonElement targets))
        {
            return;
        }
        if (manifest.SourceEdition is null || targets.ValueKind != JsonValueKind.Object || targets.EnumerateObject().Any(pair => pair.Name is not ("Classic:en" or "Remastered:en") || pair.Value.ValueKind != JsonValueKind.String || pair.Value.GetString()!.Contains('\0')))
        {
            throw new InvalidDataException($"{record.Key}: invalid edition Targets.");
        }
    }

    internal static bool RequiresProjection(LocalizationProject project, ResourceSourceDescriptor source) => project.Manifest.SourceEdition is string edition && edition != source.Edition.ToString();

    internal static LocalizationProject Activate(LocalizationProject project, ResourceSourceDescriptor descriptor, Lazy<LocalizationSourceCatalog>? source, CancellationToken cancellation)
    {
        if (project.Manifest.SourceEdition is not string edition)
        {
            return project;
        }
        string profile = descriptor.Edition + ":" + descriptor.Language;
        bool alternate = RequiresProjection(project, descriptor);
        bool playerExport = project.Manifest.PlayerExport;
        if (!alternate && !playerExport)
        {
            if (!project.Entries.Any(row => EditionTarget(row, profile, out _)))
            {
                return project;
            }
            LocalizationProject local = project with { Entries = project.Entries.Select(row => EditionTarget(row, profile, out string? target) ? row with { Target = target, TargetPresent = true } : row).ToArray() };
            LocalizationProjectIO.Validate(local);
            return local;
        }
        if (source is null)
        {
            throw new InvalidDataException($"{profile}: native resources are required to bind the shared localization.");
        }
        GameDataEdition origin = Enum.Parse<GameDataEdition>(edition);
        var native = source.Value.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
        List<LocalizationRecord> entries = [];
        FieldMessageTargetSyntax syntax = new();
        foreach (LocalizationRecord row in project.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            bool explicitTarget = EditionTarget(row, profile, out string? target);
            if (!explicitTarget)
            {
                target = row.Target;
            }
            if (string.IsNullOrEmpty(target))
            {
                continue;
            }
            if (row.Category == "System")
            {
                entries.Add(row with { Target = target });
                continue;
            }
            if (!native.TryGetValue(row.Key, out LocalizationSourceEntry? entry) || entry.Category != row.Category || entry.File != row.File)
            {
                if (playerExport && !alternate)
                {
                    throw new InvalidDataException($"{row.Key}: player localization has no matching native resource identity.");
                }
                continue;
            }
            bool field = row.Category == "Fields";
            string baseKey = row.Key.Split("_ctx_", 2, StringSplitOptions.None)[0];
            if (field && alternate && (!FieldCorrespondenceCatalog.TryGetEnglishEditionKey(baseKey, origin, Metadata(row, "Source MSD SHA256") ?? "", descriptor.Edition, entry.Metadata.GetValueOrDefault("Source MSD SHA256") ?? "", out string? nativeKey) || nativeKey != baseKey))
            {
                continue;
            }
            if (playerExport && !alternate && !MatchesOrigin(row, entry))
            {
                throw new InvalidDataException($"{row.Key}: native text or field binding differs from the player localization's source edition.");
            }
            if (alternate && !explicitTarget && !CanReuse(row, entry, baseKey, origin, descriptor.Edition, playerExport))
            {
                continue;
            }
            var bound = row with
            {
                Source = entry.Metadata.GetValueOrDefault("Source Present") == "false" ? null : entry.Text,
                SourcePresent = entry.Metadata.GetValueOrDefault("Source Present") != "false",
                Target = target,
                TargetPresent = true,
                ExtensionData = new Dictionary<string, JsonElement>(row.ExtensionData, StringComparer.Ordinal) { ["sourceContext"] = JsonSerializer.SerializeToElement(entry.Metadata) }
            };
            if (alternate && field && !explicitTarget && target != LocalizationValue.EmptyTarget)
            {
                string nativeTarget = Prefix(entry.Text) + Body(target);
                bool sameScript = Metadata(row, "JSM SHA256") == entry.Metadata.GetValueOrDefault("JSM SHA256");
                LocalizationRecord first = sameScript ? bound : bound with { Target = nativeTarget };
                bound = syntax.ValidateCandidate(first).Count == 0 ? first : sameScript ? bound with { Target = nativeTarget } : bound;
            }
            entries.Add(bound);
        }
        LocalizationProject active = project with { Manifest = project.Manifest with { SourceEdition = descriptor.Edition.ToString(), CompatibleVariants = [profile], Artwork = [] }, Entries = entries, Artwork = [] };
        LocalizationProjectIO.Validate(active);
        return active;
    }

    private static bool EditionTarget(LocalizationRecord row, string profile, out string? target)
    {
        if (row.ExtensionData.TryGetValue(TargetsMember, out JsonElement targets) && targets.TryGetProperty(profile, out JsonElement match))
        {
            target = match.GetString();
            return true;
        }
        target = null;
        return false;
    }

    private static bool CanReuse(LocalizationRecord row, LocalizationSourceEntry entry, string baseKey, GameDataEdition origin, GameDataEdition edition, bool playerExport)
    {
        string old = row.Source ?? "";
        if (row.Category == "Fields")
        {
            return SourceMatches(row, old, entry.Text, playerExport) || FieldCorrespondenceCatalog.IsReviewedEditionTextEquivalent(baseKey);
        }
        string sourceFingerprint = Metadata(row, "Native Source Fingerprint") ?? Metadata(row, "Source SHA256") ?? "";
        string activeFingerprint = entry.Metadata.GetValueOrDefault("Native Source Fingerprint") ?? entry.Metadata.GetValueOrDefault("Source SHA256") ?? "";
        return SourceMatches(row, old, entry.Text, playerExport) || FieldCorrespondenceCatalog.IsReviewedEditionTextEquivalent(baseKey, origin, sourceFingerprint, edition, activeFingerprint);
    }

    private static bool SourceMatches(LocalizationRecord row, string old, string native, bool playerExport)
    {
        if (playerExport)
        {
            return LocalizationProjectIO.PlayerSourceFingerprint(row) == LocalizationProjectIO.FingerprintPlayerSource(row, native);
        }
        return row.Category == "Fields" ? FieldComparisonText(old) == FieldComparisonText(native) : old == native;
    }

    private static bool MatchesOrigin(LocalizationRecord row, LocalizationSourceEntry native)
    {
        string nativeText = native.Metadata.GetValueOrDefault("Source Present") == "false" ? "" : native.Text;
        return SourceMatches(row, "", nativeText, true) && (row.Category != "Fields" ||
            Metadata(row, "Source MSD SHA256") == native.Metadata.GetValueOrDefault("Source MSD SHA256") &&
            (Metadata(row, "JSM SHA256") ?? "") == (native.Metadata.GetValueOrDefault("JSM SHA256") ?? ""));
    }

    internal static string FieldComparisonText(string text) => WithoutEnd(Body(text));

    private static string? Metadata(LocalizationRecord row, string name) => row.ExtensionData.TryGetValue("sourceContext", out JsonElement context) && context.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Prefix(string value) => FieldMessageContext.TryParseTag(0, value, out _, out int length) ? value[..length] : "";
    private static string Body(string value) => value[Prefix(value).Length..];

    private static string WithoutEnd(string value)
    {
        while (value.EndsWith("{End}", StringComparison.Ordinal))
        {
            value = value[..^5];
        }
        return value;
    }
}
