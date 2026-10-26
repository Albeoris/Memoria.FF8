using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static partial class JunctionEncodedResources
{
    private static bool TryDescribeFieldContribution(string field, IReadOnlyList<ModFile> files, Dictionary<ModFile, byte[]> frozenDirectory, out ResourceKey[] keys, out ModDiagnostic? diagnostic)
    {
        ModFile current = files[0];
        try
        {
            HashSet<ResourceKey> members = files.Select(file => Address(file.RelativePath)!.Value).ToHashSet();
            if (members.Any(key => key.Path.Split('/').Length == 2))
            {
                current = files.FirstOrDefault(file => Address(file.RelativePath) == new ResourceKey($"field/{field}.fl")) ?? throw new InvalidDataException($"Field {field} archive requires FL, FI and FS from the same package.");
                byte[] listing = current.ReadBytes();
                frozenDirectory.Add(current, listing);
                members.UnionWith(FieldArchiveSnapshot.ReadMemberNames(field, listing).Select(member => new ResourceKey($"field/{field}/{member}")));
            }
            keys = members.ToArray();
            diagnostic = null;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or FormatException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        {
            keys = [];
            diagnostic = new("Junction.InvalidEncodedResource", exception.Message, current.PackageId, current.RelativePath);
            return false;
        }
    }

    private static bool TryReadFieldContribution(string field, IReadOnlyList<ModFile> files, Dictionary<ModFile, byte[]> frozenDirectory, out IReadOnlyList<EncodedResource> resources, out ModDiagnostic? diagnostic)
    {
        Dictionary<ResourceKey, EncodedResource> selected = [];
        ModFile current = files[0];
        bool archiveRead = false;
        try
        {
            foreach (ModFile file in files)
            {
                current = file;
                ResourceKey key = Address(file.RelativePath)!.Value;
                if (key.Path.Split('/').Length == 2)
                {
                    if (archiveRead)
                    {
                        continue;
                    }
                    archiveRead = true;
                    ModFile[] archiveFiles = new[] { ".fl", ".fi", ".fs" }.Select(extension => files.FirstOrDefault(candidate => Address(candidate.RelativePath) == new ResourceKey($"field/{field}{extension}")) ?? throw new InvalidDataException($"Field {field} archive requires FL, FI and FS from the same package.")).ToArray();
                    byte[][] bytes = archiveFiles.Select(candidate => frozenDirectory.TryGetValue(candidate, out byte[]? listing) ? listing : candidate.ReadBytes()).ToArray();
                    IReadOnlyList<FieldArchiveMember> members = FieldArchiveSnapshot.Read(field, bytes[0], bytes[1], bytes[2]);
                    ResourceKey scope = new($"field/{field}");
                    for (int index = 0; index < archiveFiles.Length; index++)
                    {
                        ModFile part = archiveFiles[index];
                        ResourceKey partKey = Address(part.RelativePath)!.Value;
                        selected.TryAdd(partKey, Freeze(part, partKey, bytes[index], scope));
                    }
                    ModFile contentOwner = archiveFiles[2];
                    foreach (FieldArchiveMember member in members)
                    {
                        ResourceKey memberKey = new($"field/{field}/{member.Name}");
                        selected.TryAdd(memberKey, Freeze(contentOwner, memberKey, member.Data, scope));
                    }
                }
                else if (!selected.ContainsKey(key))
                {
                    selected.Add(key, Freeze(file, key, file.ReadBytes()));
                }
            }
            resources = selected.Values.ToArray();
            diagnostic = null;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or FormatException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException)
        {
            resources = [];
            diagnostic = new("Junction.InvalidEncodedResource", exception.Message, current.PackageId, current.RelativePath);
            return false;
        }
    }

    private static EncodedResource Freeze(ModFile owner, ResourceKey key, byte[] bytes, ResourceKey? scope = null) => new(key, Path.GetExtension(key.Path)[1..], owner.Revision, $"{owner.PackageId}:{owner.RelativePath}", bytes, scope);
}
