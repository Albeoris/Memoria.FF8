using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Projects canonical targets onto recorded native addresses without changing authoring keys.</summary>
internal static class LocalizationEditionProjection
{
    internal static void ValidateBindings(LocalizationProject project)
    {
        var paths = project.Manifest.EditionBindingPaths;
        if (paths is null && project.EditionBindings.Count == 0)
        {
            return;
        }
        if (paths is null || paths.Count != project.EditionBindings.Count)
        {
            throw new InvalidDataException("Edition binding data must match its manifest declarations.");
        }
        var canonical = project.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string profile, string path) in paths)
        {
            if (!seen.Add(profile) || !files.Add(path) || !path.StartsWith("Localization/EditionBindings/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal) || !project.Manifest.CompatibleVariants.Contains(profile, StringComparer.OrdinalIgnoreCase) || !project.EditionBindings.TryGetValue(profile, out var rows))
            {
                throw new InvalidDataException($"{profile}: invalid edition binding declaration.");
            }
            HashSet<string> addresses = new(StringComparer.Ordinal);
            foreach (LocalizationEditionBinding binding in rows)
            {
                if (binding?.Native is not { } row || !addresses.Add(row.Key) || binding.CanonicalKey is string key && (!canonical.TryGetValue(key, out LocalizationRecord? context) || context.Category != row.Category || context.File != row.File || !string.IsNullOrEmpty(row.Target)))
                {
                    throw new InvalidDataException($"{profile}: duplicate or invalid native edition binding.");
                }
                if (binding.PresentationPrefix is string prefix && (row.Category != "Fields" || binding.CanonicalKey is null || prefix.Length != 0 && (!FieldMessageContext.TryParseTag(0, prefix, out _, out int length) || length != prefix.Length)))
                {
                    throw new InvalidDataException($"{profile}/{row.Key}: invalid edition presentation prefix.");
                }
            }
        }
    }

    internal static LocalizationProject Activate(LocalizationProject project, string profile)
    {
        if (!project.EditionBindings.TryGetValue(profile, out var bindings))
        {
            return project;
        }
        var canonical = project.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
        LocalizationRecord[] rows = bindings.Select(binding =>
        {
            if (binding.CanonicalKey is not string key)
            {
                return binding.Native;
            }
            string? target = canonical[key].Target;
            if (!string.IsNullOrEmpty(target) && target != LocalizationValue.EmptyTarget && binding.PresentationPrefix is string prefix)
            {
                FieldMessageContext.TryParseTag(0, target, out _, out int originalLength);
                target = prefix + target[originalLength..];
            }
            return binding.Native with { Target = target, TargetPresent = canonical[key].TargetPresent };
        }).ToArray();
        LocalizationProject active = new() { Manifest = project.Manifest with { EditionBindingPaths = null, CompatibleVariants = [profile], Artwork = [] }, Entries = rows, EncodingFiles = project.EncodingFiles };
        LocalizationProjectIO.Validate(active);
        return active;
    }
}
