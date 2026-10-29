using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

static class ArchiveVersion
{
    private const string LinuxManifest = "Data/distribution.json";
    private const string MacManifest = "Memoria.FF8.app/Contents/MacOS/Data/distribution.json";
    private const string MacInfo = "Memoria.FF8.app/Contents/Info.plist";

    public static void Set(string path, string version, string buildId)
    {
        Dictionary<string, byte[]> metadata = ReadMetadata(path);
        string manifestName = metadata.ContainsKey(MacManifest) ? MacManifest : LinuxManifest;
        JsonObject document = JsonNode.Parse(metadata[manifestName])?.AsObject() ?? throw new InvalidDataException("Missing manifest.");
        if (document["BuildId"]?.GetValue<string>() != buildId || document["SchemaVersion"]?.GetValue<int>() != 2)
        {
            throw new InvalidDataException("Archive source/schema does not match the tested payload.");
        }
        bool mac = manifestName == MacManifest;
        document["Version"] = version;
        if (mac)
        {
            if (!Memoria.FF8.Platform.HostPlatforms.IsMacRuntime(document["RuntimeIdentifier"]?.GetValue<string>() ?? "") || !metadata.TryGetValue(MacInfo, out byte[]? originalInfo))
            {
                throw new InvalidDataException("The macOS archive has no complete bundle metadata.");
            }
            JsonObject[] records = document["Files"]?.AsArray().OfType<JsonObject>().Where(file => file["Path"]?.GetValue<string>() == "Contents/Info.plist").ToArray() ?? [];
            if (records.Length != 1 || records[0]["Size"]?.GetValue<long>() != originalInfo.LongLength || records[0]["Sha256"]?.GetValue<string>() != Sha256(originalInfo))
            {
                throw new InvalidDataException("The bundle metadata does not match the tested inventory.");
            }
            byte[] updatedInfo = MacBundleVersion.Stamp(originalInfo, version);
            metadata[MacInfo] = updatedInfo;
            records[0]["Size"] = updatedInfo.LongLength;
            records[0]["Sha256"] = Sha256(updatedInfo);
        }
        else if (metadata.ContainsKey(MacInfo))
        {
            throw new InvalidDataException("Linux archive contains unexpected macOS metadata.");
        }
        metadata[manifestName] = Encoding.UTF8.GetBytes(document.ToJsonString());
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream input = File.OpenRead(path))
            using (GZipStream decompressed = new(input, CompressionMode.Decompress))
            using (TarReader reader = new(decompressed))
            using (FileStream output = new(temporary, FileMode.CreateNew))
            using (GZipStream compressed = new(output, CompressionLevel.Optimal))
            using (TarWriter writer = new(compressed, TarEntryFormat.Pax))
            {
                TarEntry? entry;
                while ((entry = reader.GetNextEntry()) is not null)
                {
                    if (metadata.TryGetValue(entry.Name, out byte[]? changed))
                    {
                        using MemoryStream data = new(changed);
                        writer.WriteEntry(CopyHeader(entry, data));
                    }
                    else if (entry.DataStream is null)
                    {
                        writer.WriteEntry(entry);
                    }
                    else
                    {
                        if (entry.Length > 2L * 1024 * 1024 * 1024)
                        {
                            throw new InvalidDataException("Archive entry exceeds the package size limit.");
                        }
                        using FileStream payload = new(temporary + ".entry", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
                        entry.DataStream.CopyTo(payload);
                        payload.Position = 0;
                        writer.WriteEntry(CopyHeader(entry, payload));
                    }
                }
            }
            // Only the manifest and, for macOS, its inventoried Info.plist may change.
            // Every other payload byte, size, mode and entry type remains identical.
            if (!Inventory(path, mac).OrderBy(pair => pair.Key).SequenceEqual(Inventory(temporary, mac).OrderBy(pair => pair.Key)))
            {
                throw new InvalidDataException("Version mutation changed a tested payload entry.");
            }
            Dictionary<string, byte[]> after = ReadMetadata(temporary);
            if (after.Count != metadata.Count || metadata.Any(item => !after.TryGetValue(item.Key, out byte[]? actual) || !item.Value.AsSpan().SequenceEqual(actual)))
            {
                throw new InvalidDataException("The updated archive metadata failed verification.");
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static PaxTarEntry CopyHeader(TarEntry entry, Stream content)
    {
        // A TarEntry copy constructor shares reader state. Rebuild the header so
        // replacing its data stream cannot alter the input reader's next-entry offset.
        return new PaxTarEntry(entry.EntryType, entry.Name) { DataStream = content, Mode = entry.Mode, ModificationTime = entry.ModificationTime };
    }

    private static Dictionary<string, byte[]> ReadMetadata(string path)
    {
        using FileStream input = File.OpenRead(path);
        using GZipStream decompressed = new(input, CompressionMode.Decompress);
        using TarReader reader = new(decompressed);
        Dictionary<string, byte[]> result = new(StringComparer.Ordinal);
        HashSet<string> names = new(StringComparer.Ordinal);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (!names.Add(entry.Name))
            {
                throw new InvalidDataException("Duplicate archive entry.");
            }
            if (entry.Name is not (LinuxManifest or MacManifest or MacInfo))
            {
                continue;
            }
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.Length > 16L * 1024 * 1024 || entry.DataStream is null)
            {
                throw new InvalidDataException("Invalid archive metadata entry.");
            }
            using MemoryStream content = new();
            entry.DataStream.CopyTo(content);
            result.Add(entry.Name, content.ToArray());
        }
        if (result.ContainsKey(LinuxManifest) == result.ContainsKey(MacManifest))
        {
            throw new InvalidDataException("Archive must contain exactly one distribution manifest.");
        }
        return result;
    }

    private static Dictionary<string, string> Inventory(string path, bool mac)
    {
        using FileStream input = File.OpenRead(path);
        using GZipStream decompressed = new(input, CompressionMode.Decompress);
        using TarReader reader = new(decompressed);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            bool metadata = entry.Name is LinuxManifest or MacManifest || (mac && entry.Name == MacInfo);
            string hash = !metadata && entry.DataStream is not null ? Convert.ToHexStringLower(SHA256.HashData(entry.DataStream)) : "";
            result.Add(entry.Name, metadata ? $"{entry.EntryType}:{entry.Mode}" : $"{entry.EntryType}:{entry.Mode}:{entry.Length}:{hash}");
        }
        return result;
    }

    private static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
