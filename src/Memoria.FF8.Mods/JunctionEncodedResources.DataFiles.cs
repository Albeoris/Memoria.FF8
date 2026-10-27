using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static partial class JunctionEncodedResources
{
    private static ResourceKey? DataFileAddress(string[] parts, int start)
    {
        if (parts.Length <= start || parts.Skip(start).Any(part => string.IsNullOrEmpty(part) || part is "." or ".."))
        {
            return null;
        }
        string path = string.Join('/', parts.AsSpan(start).ToArray());
        if (parts.Length - start == 1)
        {
            path = "main/" + path;
        }
        else if (parts.Length - start == 3 && parts[start] == "world" && parts[start + 1] == "dat")
        {
            path = "world/" + parts[start + 2];
        }
        if (path == "world/esk/chara.one")
        {
            path = "world/chara.one";
        }
        if (path is "menu/hires/face_b00.tex" or "menu/hires/face_b01.tex" or "menu/hires/gf_big00.tex" or "menu/hires/gf_big01.tex")
        {
            path = "menu/" + parts[^1];
        }
        ResourceKey key = new(path);
        return NativeDataResourceFiles.Classify(key) is not null ? key : null;
    }

    private static void PrepareDataGroups(ModCatalogSnapshot catalog, Dictionary<ResourceKey, EncodedResource> selected, HashSet<ResourceKey> encountered, List<ModDiagnostic> failures, List<ModDiagnostic> notices)
    {
        var files = catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => (File: file, Key: Address(file.RelativePath))).Where(item => item.Key is ResourceKey key && NativeDataResourceFiles.Group(key) is not null);
        foreach (var group in files.GroupBy(item => item.File.PackageId + "\0" + NativeDataResourceFiles.Group(item.Key!.Value), StringComparer.OrdinalIgnoreCase))
        {
            var members = group.DistinctBy(item => item.Key!.Value).ToArray();
            ModFile owner = members[0].File;
            ResourceKey[] overlaps = members.Select(item => item.Key!.Value).Where(encountered.Contains).ToArray();
            if (overlaps.Length > 0)
            {
                notices.Add(new("Junction.ResourceGroupConflict", $"Compound resource {NativeDataResourceFiles.Group(members[0].Key!.Value)} contribution was skipped because a higher-priority package owns {string.Join(", ", overlaps.Select(key => key.Path))}.", owner.PackageId, owner.RelativePath));
                continue;
            }
            foreach (var member in members)
            {
                encountered.Add(member.Key!.Value);
            }
            foreach (var member in members)
            {
                try
                {
                    byte[] bytes = member.File.ReadBytes();
                    if (bytes.Length < 16)
                    {
                        throw new InvalidDataException("A compound native resource member is empty or truncated.");
                    }
                    ResourceKey key = member.Key!.Value;
                    selected.Add(key, new EncodedResource(key, Path.GetExtension(key.Path)[1..], member.File.Revision, $"{member.File.PackageId}:{member.File.RelativePath}", bytes));
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
                {
                    failures.Add(new("Junction.InvalidEncodedResource", exception.Message, member.File.PackageId, member.File.RelativePath));
                }
            }
        }
    }
}
