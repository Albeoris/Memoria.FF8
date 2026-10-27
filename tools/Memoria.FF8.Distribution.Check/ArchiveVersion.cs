using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

static class ArchiveVersion
{
    public static void Set(string path, string version, string buildId)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool found = false;
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
                    bool manifest = entry.Name is "Data/distribution.json" or "Memoria.FF8.app/Contents/MacOS/Data/distribution.json";
                    if (!manifest)
                    {
                        if (entry.DataStream is null)
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
                            PaxTarEntry preserved = new(entry.EntryType, entry.Name) { DataStream = payload, Mode = entry.Mode, ModificationTime = entry.ModificationTime };
                            writer.WriteEntry(preserved);
                        }
                        continue;
                    }
                    if (found || entry.Length > 16L * 1024 * 1024 || entry.DataStream is null)
                    {
                        throw new InvalidDataException("Invalid or duplicate archive manifest.");
                    }
                    found = true;
                    JsonObject document = JsonNode.Parse(entry.DataStream)?.AsObject() ?? throw new InvalidDataException("Missing manifest.");
                    if (document["BuildId"]?.GetValue<string>() != buildId || document["SchemaVersion"]?.GetValue<int>() != 2)
                    {
                        throw new InvalidDataException("Archive source/schema does not match the tested payload.");
                    }
                    document["Version"] = version;
                    using MemoryStream data = new(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString()));
                    PaxTarEntry updated = new(TarEntryType.RegularFile, entry.Name) { DataStream = data, Mode = entry.Mode, ModificationTime = entry.ModificationTime };
                    writer.WriteEntry(updated);
                }
            }
            if (!found)
            {
                throw new InvalidDataException("Archive has no distribution manifest.");
            }
            // Re-read both streams: all non-manifest bytes and modes must be identical.
            if (!Inventory(path).OrderBy(pair => pair.Key).SequenceEqual(Inventory(temporary).OrderBy(pair => pair.Key)))
            {
                throw new InvalidDataException("Version mutation changed a tested payload entry.");
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static Dictionary<string, string> Inventory(string path)
    {
        using FileStream input = File.OpenRead(path);
        using GZipStream decompressed = new(input, CompressionMode.Decompress);
        using TarReader reader = new(decompressed);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.Name.EndsWith("Data/distribution.json", StringComparison.Ordinal))
            {
                continue;
            }
            string hash = entry.DataStream is null ? "" : Convert.ToHexStringLower(SHA256.HashData(entry.DataStream));
            result.Add(entry.Name, $"{entry.EntryType}:{entry.Mode}:{entry.Length}:{hash}");
        }
        return result;
    }
}
