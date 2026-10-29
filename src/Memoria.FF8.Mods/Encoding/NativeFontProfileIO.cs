using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.EncodingProfiles;

public static class NativeFontProfileIO
{
    public static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = false };

    public static NativeFontProfile Load(string path)
    {
        string json = File.ReadAllText(path);
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("formatVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int versionNumber) || versionNumber != 1 || !root.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || !root.TryGetProperty("displayName", out JsonElement name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
            {
                throw new InvalidDataException("Profile JSON requires explicit formatVersion 1, stable id and displayName properties.");
            }
            RejectDuplicateProperties(document.RootElement);
        }
        NativeFontProfile profile = JsonSerializer.Deserialize<NativeFontProfile>(json, JsonOptions) ?? throw new InvalidDataException("Profile document is empty.");
        ThrowInvalid(NativeFontProfileValidation.Validate(profile));
        return profile;
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate profile property '{property.Name}'.");
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                RejectDuplicateProperties(child);
            }
        }
    }

    public static void Save(string path, NativeFontProfile profile)
    {
        ThrowInvalid(NativeFontProfileValidation.Validate(profile));
        string full = Path.GetFullPath(path);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, JsonOptions));
            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static IReadOnlyList<NativeTextDiagnostic> ValidateDependencies(string path, NativeFontProfile profile)
    {
        List<NativeTextDiagnostic> diagnostics = [.. NativeFontProfileValidation.Validate(profile)];
        if (diagnostics.Count != 0)
        {
            return diagnostics;
        }
        string root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Dictionary<string, string?> dependencies = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string relative, string? hash) in profile.Atlases.Select(atlas => (atlas.Path, atlas.Sha256)).Concat(profile.Dependencies.Select(dependency => (dependency.Path, dependency.Sha256))))
        {
            try
            {
                if (dependencies.TryGetValue(relative, out string? previous))
                {
                    string first = dependencies.Keys.First(key => StringComparer.OrdinalIgnoreCase.Equals(key, relative));
                    if (first != relative || !StringComparer.OrdinalIgnoreCase.Equals(previous, hash))
                    {
                        diagnostics.Add(new("dependency-collision", $"Dependency '{relative}' has a case-sensitive collision or conflicting integrity declarations."));
                    }
                    continue;
                }
                dependencies.Add(relative, hash);
                string full = ResolveDependency(root, relative);
                if (!File.Exists(full))
                {
                    diagnostics.Add(new("missing-dependency", $"Missing dependency '{relative}'."));
                    continue;
                }
                if (hash is not null && (hash.Length != 64 || !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).Equals(hash, StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(new("integrity", $"Dependency '{relative}' does not match its SHA-256 declaration."));
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                diagnostics.Add(new("dependency", exception.Message));
            }
        }
        foreach (NativeFontAtlas atlas in profile.Atlases)
        {
            try
            {
                string full = ResolveDependency(root, atlas.Path);
                if (!File.Exists(full))
                {
                    continue;
                }
                var image = NativeInstalledFontProfile.DecodeAtlas(File.ReadAllBytes(full), full, atlas.Palette);
                if (image.Width != atlas.Width || image.Height != atlas.Height)
                {
                    diagnostics.Add(new("atlas-remapping", $"Atlas '{atlas.Id}' changed dimensions; its existing image assignments require review."));
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
            {
                diagnostics.Add(new("atlas-decode", $"Atlas '{atlas.Id}': {exception.Message}"));
            }
        }
        return diagnostics;
    }

    public static string ResolveDependency(string root, string relative)
    {
        if (!NativeFontProfileValidation.IsPortablePath(relative))
        {
            throw new InvalidDataException($"Unsafe profile dependency '{relative}'.");
        }
        string fullRoot = Path.GetFullPath(root);
        string current = Path.GetPathRoot(fullRoot)!;
        foreach (string part in Path.GetRelativePath(current, fullRoot).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Concat(relative.Split('/')))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Symbolic links/reparse points are not portable profile dependencies: '{relative}'.");
            }
            string? parent = Path.GetDirectoryName(current);
            if (parent is not null && Directory.Exists(parent))
            {
                string name = Path.GetFileName(current);
                string[] matches = Directory.EnumerateFileSystemEntries(parent).Where(entry => Path.GetFileName(entry).Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 1 || matches.Length == 1 && Path.GetFileName(matches[0]) != name)
                {
                    throw new InvalidDataException($"Dependency path casing does not match its portable declaration: '{relative}'.");
                }
            }
        }
        return current;
    }

    internal static void ThrowInvalid(IReadOnlyList<NativeTextDiagnostic> diagnostics)
    {
        if (diagnostics.Count != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
    }
}

public sealed record EncodingProfileExport(string ProfilePath, string FolderName, bool Enabled = true);

/// <summary>Exports enabled profiles as independent, self-contained Encoding/Name closures.</summary>
public static class EncodingProfilePackage
{
    public static IReadOnlyList<string> Export(IEnumerable<EncodingProfileExport> selections, string modRoot)
    {
        var profiles = selections.Where(selection => selection.Enabled).Select(selection => (Selection: selection, Profile: NativeFontProfileIO.Load(selection.ProfilePath))).ToArray();
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> folders = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in profiles)
        {
            if (!NativeFontProfileValidation.IsPortablePath(item.Selection.FolderName) || item.Selection.FolderName.Contains('/') || !folders.Add(item.Selection.FolderName) || !ids.Add(item.Profile.Id))
            {
                throw new InvalidDataException("Each enabled profile requires a unique stable ID and portable, case-distinct directory name.");
            }
            NativeFontProfileIO.ThrowInvalid(NativeFontProfileIO.ValidateDependencies(item.Selection.ProfilePath, item.Profile));
        }
        foreach (var item in profiles)
        {
            if (item.Profile.Assignments.Any(assignment => !ids.Contains(assignment.ProfileId)))
            {
                throw new InvalidDataException("An enabled profile assignment references a profile outside the exported enabled selection.");
            }
        }
        string root = Path.GetFullPath(modRoot);
        Directory.CreateDirectory(root);
        string encoding = NativeFontProfileIO.ResolveDependency(root, "Encoding");
        Directory.CreateDirectory(encoding);
        List<string> exported = [];
        foreach (var item in profiles)
        {
            string destination = NativeFontProfileIO.ResolveDependency(encoding, item.Selection.FolderName);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException($"Profile export destination already exists: '{item.Selection.FolderName}'. Export into a new directory.");
            }
        }
        // Stage the complete selection; failures publish none of the staged profile directories.
        string stage = Path.Combine(encoding, ".profiles-" + Guid.NewGuid().ToString("N"));
        List<string> published = [];
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var item in profiles)
            {
                string sourceRoot = Path.GetDirectoryName(Path.GetFullPath(item.Selection.ProfilePath))!;
                string staged = Path.Combine(stage, item.Selection.FolderName);
                Directory.CreateDirectory(staged);
                foreach (string relative in item.Profile.Atlases.Select(atlas => atlas.Path).Concat(item.Profile.Dependencies.Select(dependency => dependency.Path)).Distinct(StringComparer.Ordinal))
                {
                    if (relative.Equals("profile.json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("A dependency cannot replace profile.json.");
                    }
                    string target = NativeFontProfileIO.ResolveDependency(staged, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(NativeFontProfileIO.ResolveDependency(sourceRoot, relative), target);
                }
                NativeFontProfileIO.Save(Path.Combine(staged, "profile.json"), item.Profile);
                NativeFontProfileIO.ThrowInvalid(NativeFontProfileIO.ValidateDependencies(Path.Combine(staged, "profile.json"), item.Profile));
            }
            foreach (var item in profiles)
            {
                string destination = NativeFontProfileIO.ResolveDependency(encoding, item.Selection.FolderName);
                Directory.Move(Path.Combine(stage, item.Selection.FolderName), destination);
                published.Add(destination);
                exported.Add(Path.Combine(destination, "profile.json"));
            }
            return exported;
        }
        catch
        {
            foreach (string directory in published)
            {
                Directory.Delete(directory, recursive: true);
            }
            throw;
        }
        finally
        {
            Directory.Delete(stage, recursive: true);
        }
    }
}
