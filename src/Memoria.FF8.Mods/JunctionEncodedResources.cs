using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Freezes supported encoded Junction replacements in effective catalog order.</summary>
public static class JunctionEncodedResources
{
    public const string MainCharacterFormat = "mch";
    private static readonly HashSet<string> FieldExtensions = new(StringComparer.Ordinal) { ".ca", ".id", ".inf", ".jsm", ".map", ".mim", ".mrt", ".msd", ".msk", ".pcb", ".pmd", ".rat", ".sfx" };

    public static bool TryPrepare(ModCatalogSnapshot catalog, out IReadOnlyList<EncodedResource> resources, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<ResourceKey, EncodedResource> selected = [];
        HashSet<ResourceKey> encountered = [];
        List<ModDiagnostic> failures = [];
        List<ModDiagnostic> notices = [];
        HashSet<string> rejectedGroups = new(StringComparer.OrdinalIgnoreCase);
        HashSet<ResourceKey> acceptedFieldMembers = [];
        var fieldFiles = catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => (File: file, Key: Address(file.RelativePath))).Where(item => item.Key is ResourceKey key && FieldGroup(key) is not null);
        foreach (var group in fieldFiles.GroupBy(item => item.File.PackageId + "\0" + FieldGroup(item.Key!.Value), StringComparer.OrdinalIgnoreCase))
        {
            ResourceKey[] members = group.Select(item => item.Key!.Value).Distinct().ToArray();
            ResourceKey[] overlaps = members.Where(acceptedFieldMembers.Contains).ToArray();
            if (overlaps.Length > 0)
            {
                rejectedGroups.Add(group.Key);
                ModFile owner = group.First().File;
                notices.Add(new("Junction.FieldGroupConflict", $"Field {FieldGroup(members[0])} contribution was skipped because a higher-priority package owns {string.Join(", ", overlaps.Select(key => key.Path))}.", owner.PackageId, owner.RelativePath));
                continue;
            }
            acceptedFieldMembers.UnionWith(members);
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
                if (field is not null && rejectedGroups.Contains(file.PackageId + "\0" + field) || !encountered.Add(key))
                {
                    continue;
                }
                byte[] bytes = file.ReadBytes();
                if (field is null && bytes.Length < 16)
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
        return parts.Length == 3 && parts[0] == "field" && IsFieldName(parts[1]) && IsFieldMember(parts[1], parts[2]) ? parts[1] : null;
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

    private static bool IsFieldName(string name) => name.Length >= 2 && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static bool IsFieldMember(string field, string member) => member == "chara.one" || member.StartsWith(field + ".", StringComparison.Ordinal) && FieldExtensions.Contains(member[field.Length..]);
}
