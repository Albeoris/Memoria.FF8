using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectIO
{
    private static IReadOnlyList<LocalizationDependencyFile> ReadEncodingFiles(string root, Func<string, byte[]> read)
    {
        string directory = System.IO.Path.Combine(root, "Encoding");
        if (!Directory.Exists(directory))
        {
            return [];
        }
        List<LocalizationDependencyFile> files = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        List<NativeFontProfile> profiles = [];
        foreach (string profileDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (System.IO.Path.GetFileName(profileDirectory).StartsWith('.'))
            {
                continue;
            }
            string relativeRoot = "Encoding/" + System.IO.Path.GetFileName(profileDirectory);
            string profilePath = NativeFontProfileIO.ResolveDependency(root, relativeRoot + "/profile.json");
            byte[] profileBytes = read(profilePath);
            NativeFontProfile profile = JsonSerializer.Deserialize<NativeFontProfile>(profileBytes, NativeFontProfileIO.JsonOptions) ?? throw new InvalidDataException("The encoding profile document is empty.");
            NativeFontProfileIO.ThrowInvalid(NativeFontProfileValidation.Validate(profile));
            if (!ids.Add(profile.Id))
            {
                throw new InvalidDataException("Encoding profile IDs must be unique across the package.");
            }
            profiles.Add(profile);
            files.Add(new(relativeRoot + "/profile.json", profileBytes));
            foreach (string dependency in profile.Atlases.Select(atlas => atlas.Path).Concat(profile.Dependencies.Select(dependency => dependency.Path)).Distinct(StringComparer.Ordinal))
            {
                if (dependency.Equals("profile.json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("An encoding dependency cannot replace profile.json.");
                }
                files.Add(new(relativeRoot + "/" + dependency, read(NativeFontProfileIO.ResolveDependency(profileDirectory, dependency))));
            }
            NativeFontProfileIO.ThrowInvalid(NativeFontProfileIO.ValidateDependencies(profilePath, profile));
        }
        if (profiles.SelectMany(profile => profile.Assignments).Any(assignment => !ids.Contains(assignment.ProfileId)))
        {
            throw new InvalidDataException("An encoding assignment references a profile outside the package.");
        }
        return files;
    }
}
