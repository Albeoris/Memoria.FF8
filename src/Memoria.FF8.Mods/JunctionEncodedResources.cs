using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Freezes supported encoded Junction replacements in effective catalog order.</summary>
public static partial class JunctionEncodedResources
{
    public const string MainCharacterFormat = "mch";
    private static readonly HashSet<string> FieldExtensions = new(StringComparer.Ordinal) { ".ca", ".id", ".inf", ".jsm", ".map", ".mim", ".mrt", ".msd", ".msk", ".pcb", ".pmd", ".rat", ".sfx" };

    public static bool TryPrepare(ModCatalogSnapshot catalog, out IReadOnlyList<EncodedResource> resources, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<ResourceKey, EncodedResource> selected = [];
        HashSet<ResourceKey> encountered = [];
        List<ModDiagnostic> failures = [];
        List<ModDiagnostic> notices = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Junction && IsUnusedFieldMember(file.RelativePath)))
        {
            notices.Add(new("Junction.UnusedFieldMember", "The loose Field filename does not match a native runtime member and is not applied. Localized suffixes are not aliases for the field basename.", file.PackageId, file.RelativePath));
        }
        HashSet<ResourceKey> acceptedFieldMembers = [];
        var fieldFiles = catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => (File: file, Key: Address(file.RelativePath))).Where(item => item.Key is ResourceKey key && FieldGroup(key) is not null);
        foreach (var group in fieldFiles.GroupBy(item => item.File.PackageId + "\0" + FieldGroup(item.Key!.Value), StringComparer.OrdinalIgnoreCase))
        {
            ResourceKey[] members = group.Select(item => item.Key!.Value).Distinct().ToArray();
            ResourceKey[] overlaps = members.Where(acceptedFieldMembers.Contains).ToArray();
            ModFile owner = group.First().File;
            string field = FieldGroup(members[0])!;
            IReadOnlyList<EncodedResource> contribution = [];
            if (overlaps.Length == 0)
            {
                ModFile[] files = group.Select(item => item.File).ToArray();
                Dictionary<ModFile, byte[]> frozenDirectory = [];
                if (!TryDescribeFieldContribution(field, files, frozenDirectory, out ResourceKey[] described, out ModDiagnostic? failure))
                {
                    failures.Add(failure!);
                    break;
                }
                overlaps = described.Where(acceptedFieldMembers.Contains).ToArray();
                if (overlaps.Length == 0 && !TryReadFieldContribution(field, files, frozenDirectory, out contribution, out failure))
                {
                    failures.Add(failure!);
                    break;
                }
            }
            if (overlaps.Length > 0)
            {
                notices.Add(new("Junction.FieldGroupConflict", $"Field {field} contribution was skipped because a higher-priority package owns {string.Join(", ", overlaps.Select(key => key.Path))}.", owner.PackageId, owner.RelativePath));
                continue;
            }
            foreach (EncodedResource resource in contribution)
            {
                selected.Add(resource.Key, resource);
                acceptedFieldMembers.Add(resource.Key);
            }
        }
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Junction))
        {
            try
            {
                ResourceKey? address = Address(file.RelativePath);
                if (address is not ResourceKey key)
                {
                    continue;
                }
                string? field = FieldGroup(key);
                if (field is not null || !encountered.Add(key))
                {
                    continue;
                }
                byte[] bytes = file.ReadBytes();
                if (bytes.Length == 0 || !IsStaticFileResource(key) && bytes.Length < 16)
                {
                    throw new InvalidDataException("An encoded resource replacement is empty or truncated.");
                }
                selected.Add(key, new EncodedResource(key, Path.GetExtension(key.Path)[1..], file.Revision, $"{file.PackageId}:{file.RelativePath}", bytes));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
            {
                failures.Add(new("Junction.InvalidEncodedResource", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        resources = selected.Values.ToArray();
        diagnostics = failures.Concat(notices).ToArray();
        return failures.Count == 0;
    }

    public static ResourceKey? Address(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').ToLowerInvariant().Split('/');
        int start = parts[0] == "direct" ? 1 : 0;
        if (StaticFileAddress(parts, start) is ResourceKey staticFile)
        {
            return staticFile;
        }
        if (parts.Length - start == 4 && parts[start] == "field" && parts[start + 1] == "mapdata")
        {
            string archive = parts[start + 3];
            string field = Path.GetFileNameWithoutExtension(archive);
            if (IsFieldName(field) && parts[start + 2] == field[..2] && Path.GetExtension(archive) is ".fi" or ".fl" or ".fs")
            {
                return new ResourceKey($"field/{archive}");
            }
            return null;
        }
        if (parts.Length - start == 5 && parts[start] == "field" && parts[start + 1] == "mapdata")
        {
            string field = parts[start + 3];
            string member = parts[start + 4];
            return IsFieldName(field) && parts[start + 2] == field[..2] && IsFieldMember(field, member) ? new ResourceKey($"field/{field}/{member}") : null;
        }
        if (parts.Length - start == 2 && parts[start] == "battle")
        {
            string body = parts[start + 1];
            if (body.Length == 10 && body[0] == 'd' && "0123456789abcdef".Contains(body[1]) && body[2] == 'c' && !body.AsSpan(3, 3).ContainsAnyExceptInRange('0', '9') && body[6..] == ".dat")
            {
                return new ResourceKey("battle/" + body);
            }
            return null;
        }
        if (parts.Length - start != 4 || parts[start] != "field" || parts[start + 1] != "model" || parts[start + 2] != "main_chr")
        {
            return null;
        }
        string name = parts[start + 3];
        if (name.Length != 8 || !char.IsAsciiLetterLower(name[0]) || name[4..] != ".mch" || name.AsSpan(1, 3).ContainsAnyExceptInRange('0', '9'))
        {
            return null;
        }
        return new ResourceKey($"field/main_chr/{name}");
    }

    /// <summary>The canonical field owning a runtime component, independent of its external archive path.</summary>
    public static string? FieldGroup(ResourceKey key)
    {
        string[] parts = key.Path.Split('/');
        if (parts.Length == 2 && parts[0] == "field" && Path.GetExtension(parts[1]) is ".fi" or ".fl" or ".fs")
        {
            string field = Path.GetFileNameWithoutExtension(parts[1]);
            return IsFieldName(field) ? field : null;
        }
        return parts.Length == 3 && parts[0] == "field" && IsFieldName(parts[1]) && IsArchiveMemberName(parts[1], parts[2]) ? parts[1] : null;
    }

    /// <summary>Identifies field symbol metadata that is not a runtime encoded resource.</summary>
    public static bool IsFieldMetadata(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').ToLowerInvariant().Split('/');
        int start = parts[0] == "direct" ? 1 : 0;
        if (parts.Length - start != 5 || parts[start] != "field" || parts[start + 1] != "mapdata")
        {
            return false;
        }
        string field = parts[start + 3];
        return IsFieldName(field) && parts[start + 2] == field[..2] && parts[start + 4] == field + ".sym";
    }

    /// <summary>Player-save and license attachments do not contribute runtime replacements.</summary>
    public static bool IsAncillaryFile(string relativePath)
    {
        string path = relativePath.Replace('\\', '/');
        if (path.StartsWith("direct/", StringComparison.OrdinalIgnoreCase))
        {
            path = path[7..];
        }
        string name = Path.GetFileName(path).ToLowerInvariant();
        return path.StartsWith("save/", StringComparison.OrdinalIgnoreCase) || name is "license" or "license.txt" or "license.md" or "copying" or "copying.txt" or "copying.md";
    }

    private static bool IsFieldName(string name) => name.Length >= 2 && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static bool IsFieldMember(string field, string member) => member == "chara.one" || member.StartsWith(field + ".", StringComparison.Ordinal) && FieldExtensions.Contains(member[field.Length..]);

    public static bool IsUnusedFieldMember(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').ToLowerInvariant().Split('/');
        int start = parts[0] == "direct" ? 1 : 0;
        if (parts.Length - start != 5 || parts[start] != "field" || parts[start + 1] != "mapdata")
        {
            return false;
        }
        string field = parts[start + 3];
        string member = parts[start + 4];
        string extension = Path.GetExtension(member);
        return IsFieldName(field) && parts[start + 2] == field[..2] && (FieldExtensions.Contains(extension) || extension == ".one") && !IsFieldMember(field, member);
    }

    private static bool IsArchiveMemberName(string field, string member) => member == "chara.one" || member.StartsWith(field + ".", StringComparison.Ordinal) && member.Length > field.Length + 1 && member[(field.Length + 1)..].All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
