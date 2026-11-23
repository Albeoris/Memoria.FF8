using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed record FieldSceneDraft(PreparedFieldScene Prepared, string Revision);

public sealed class FieldScene3DPackageStore
{
    private const string AbsentRevision = "absent";
    public ModPackage Package { get; }
    private string Root => Package.RootDirectory;

    public FieldScene3DPackageStore(ModPackage package)
    {
        Package = package with { RootDirectory = Path.GetFullPath(package.RootDirectory) };
        if (Package.Format != ModFormat.Memoria)
        {
            throw new InvalidDataException("Select a Memoria 3D field scene package.");
        }
        CheckMarker();
    }

    public bool RequiresCopy => Package.IsBundled || Package.Official is not null || (File.GetAttributes(Resolve(FieldSceneDocuments.ComponentPath)) & FileAttributes.ReadOnly) != 0;

    public static FieldScene3DPackageStore Create(string modsRoot, string name, FieldScene3DPackageStore? copy = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 128 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Enter a readable mod name of 1–128 characters.");
        }
        string id = "field-scene-" + Guid.NewGuid().ToString("N")[..12];
        string parent = Path.Combine(Path.GetFullPath(modsRoot), "Memoria");
        string destination = Path.Combine(parent, id);
        string staging = Path.Combine(Path.GetDirectoryName(parent)!, "." + id + ".tmp");
        FieldLightingPackageStore.RejectLinks(destination);
        try
        {
            Directory.CreateDirectory(staging);
            if (copy is not null)
            {
                string source = copy.Resolve("scene3d");
                string target = Path.Combine(staging, "scene3d");
                Directory.CreateDirectory(target);
                FieldLightingPackageStore.CopyDirectory(source, target);
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(staging, "scene3d"));
                File.WriteAllBytes(Path.Combine(staging, FieldSceneDocuments.ComponentPath), FieldSceneDocuments.Component);
            }
            File.WriteAllText(Path.Combine(staging, "mod.ini"), "[Mod]\nenabled=0\n");
            string descriptorPath = Path.Combine(staging, NativeModDescriptor.FileName);
            File.WriteAllBytes(descriptorPath, JsonSerializer.SerializeToUtf8Bytes(new { id, name, package = "Custom 3D field scenes", description = new Dictionary<string, string> { ["en"] = "Authored 3D presentations for individual FFVIII fields." } }));
            NativeModDescriptor descriptor = NativeModDescriptor.Read(descriptorPath);
            Directory.CreateDirectory(parent);
            Directory.Move(staging, destination);
            return new(new(descriptor.RuntimePackageId, name, ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, destination) { Descriptor = descriptor, HasFieldScene3D = true });
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    public FieldSceneDraft Open(string field)
    {
        CheckMarker();
        string path = Resolve(FieldSceneDocuments.ScenePath(field));
        if (!File.Exists(path))
        {
            return new(new(Package.Id, new(1, field, [], []), new Dictionary<string, Assets.FieldSceneAsset>()), AbsentRevision);
        }
        byte[] content = ReadFile(path, FieldSceneDocuments.MaximumDocumentBytes);
        FieldSceneDocument document = FieldSceneDocuments.Read(content, field);
        return new(FieldSceneDocuments.Prepare(Package.Id, document, ReadAsset), FieldSceneDocuments.Digest(content));
    }

    public FieldSceneDraft Save(FieldSceneDocument document, string expectedRevision, IReadOnlyDictionary<string, byte[]> imports)
    {
        if (RequiresCopy)
        {
            throw new IOException("Create a writable copy of this scene package before saving.");
        }
        string path = Resolve(FieldSceneDocuments.ScenePath(document.Field));
        byte[] data = FieldSceneDocuments.Write(document);
        string lockPath = Resolve("scene3d/.authoring.lock.tmp");
        using FileStream coordination = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        CheckMarker();
        VerifyRevision(path, expectedRevision);
        PreparedFieldScene prepared = FieldSceneDocuments.Prepare(Package.Id, document, relative =>
        {
            string digest = Path.GetFileNameWithoutExtension(relative);
            return imports.TryGetValue(digest, out byte[]? bytes) ? bytes : ReadAsset(relative);
        });
        foreach (FieldSceneAssetBinding asset in document.Assets)
        {
            if (!imports.TryGetValue(asset.Sha256, out byte[]? bytes))
            {
                continue;
            }
            string destination = Resolve(FieldSceneDocuments.AssetPath(asset.Sha256));
            if (File.Exists(destination))
            {
                if (FieldSceneDocuments.Digest(ReadFile(destination, FieldSceneDocuments.MaximumGlbBytes)) != asset.Sha256)
                {
                    WriteAtomic(destination, bytes, replace: true);
                }
            }
            else
            {
                WriteAtomic(destination, bytes, replace: false);
            }
        }
        VerifyRevision(path, expectedRevision);
        WriteAtomic(path, data, replace: expectedRevision != AbsentRevision);
        return new(prepared, FieldSceneDocuments.Digest(data));
    }

    public static byte[] ReadGlb(string path) => ReadFile(path, FieldSceneDocuments.MaximumGlbBytes);

    private void CheckMarker() => FieldSceneDocuments.ValidateComponent(ReadFile(Resolve(FieldSceneDocuments.ComponentPath), FieldSceneDocuments.MaximumDocumentBytes));
    private byte[] ReadAsset(string path) => ReadFile(Resolve(path), FieldSceneDocuments.MaximumGlbBytes);

    private string Resolve(string relative)
    {
        string result = Path.GetFullPath(Path.Combine(Root, relative));
        if (!result.StartsWith(Root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Scene files must remain in their package.");
        }
        FieldLightingPackageStore.RejectLinks(result);
        return result;
    }

    private static void VerifyRevision(string path, string expected)
    {
        string actual = File.Exists(path) ? FieldSceneDocuments.Digest(ReadFile(path, FieldSceneDocuments.MaximumDocumentBytes)) : AbsentRevision;
        if (actual != expected)
        {
            throw new IOException("The field scene changed on disk. The unsaved draft is still open.");
        }
    }

    private static byte[] ReadFile(string path, int limit)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > limit)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' exceeds its size limit.");
        }
        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void WriteAtomic(string path, ReadOnlySpan<byte> data, bool replace)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            if (replace)
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path, overwrite: false);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
