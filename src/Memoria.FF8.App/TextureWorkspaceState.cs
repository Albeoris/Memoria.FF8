using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed record TextureWorkingEntry(TextureTarget Target, string? ImportedPixels = null, string? DestinationBytes = null, bool ExplicitMapping = false, string? DestinationSemantic = null);
internal sealed record TextureImportDecision(bool Selected, string Status);

internal static class TextureWorkspaceState
{
    internal const string FileName = ".memoria-texture-authoring.json";
    private sealed record Document(int SchemaVersion, Dictionary<string, Dictionary<string, TextureWorkingEntry>> Destinations, Dictionary<string, TextureTarget> Mappings);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true, MaxDepth = 16 };

    internal static string DestinationKey(string destination)
    {
        string canonical = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            canonical = canonical.ToUpperInvariant();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    internal static Dictionary<string, TextureWorkingEntry> Read(string source, string destination)
    {
        Document document = ReadDocument(source);
        Dictionary<string, TextureWorkingEntry> result = document.Destinations.TryGetValue(DestinationKey(destination), out Dictionary<string, TextureWorkingEntry>? saved) ? new(saved, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        foreach ((string input, TextureTarget target) in document.Mappings)
        {
            result.TryAdd(input, new(target, ExplicitMapping: true));
        }
        return result;
    }

    internal static void Write(string source, string destination, IReadOnlyDictionary<string, TextureWorkingEntry> state)
    {
        Document document = ReadDocument(source);
        string key = DestinationKey(destination);
        if (!document.Destinations.TryGetValue(key, out Dictionary<string, TextureWorkingEntry>? entries))
        {
            entries = new(StringComparer.Ordinal);
            document.Destinations.Add(key, entries);
        }
        foreach ((string input, TextureWorkingEntry value) in state)
        {
            value.Target.Validate();
            entries[input] = value;
            if (value.ExplicitMapping)
            {
                document.Mappings[input] = value.Target;
            }
        }
        string path = Path.Combine(source, FileName);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
            if (bytes.Length > 16 * 1024 * 1024)
            {
                throw new InvalidDataException("The texture authoring state exceeds its 16 MiB limit.");
            }
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static TextureImportDecision Evaluate(TextureTarget target, string sourcePixels, string exportedBaseline, string? destinationBytes, string? destinationPixels, TextureWorkingEntry? previous)
    {
        if (destinationPixels is not null && Equal(sourcePixels, destinationPixels))
        {
            return new(false, "Same decoded appearance as destination; omitted");
        }
        bool recordedImport = previous?.Target == target && previous.ImportedPixels is not null;
        bool destinationChanged = recordedImport && (previous!.DestinationSemantic is not null ? !Equal(destinationPixels, previous.DestinationSemantic) : destinationBytes != previous.DestinationBytes);
        if (recordedImport && Equal(sourcePixels, previous!.ImportedPixels))
        {
            return new(false, destinationChanged ? "Unchanged working image; destination edit retained" : "Unchanged since last import");
        }
        if (recordedImport && destinationChanged)
        {
            return new(false, "Conflict: working image and destination both changed. Unchecked; check to explicitly replace the destination.");
        }
        bool changed = recordedImport || !Equal(sourcePixels, exportedBaseline);
        return new(changed, changed ? "Changed: ready to install" : "Unchanged exported appearance; omitted");
    }

    private static bool Equal(string? first, string? second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static Document ReadDocument(string source)
    {
        string path = Path.Combine(source, FileName);
        if (!File.Exists(path))
        {
            return new(3, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("Texture authoring state must be a bounded regular file.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument root = JsonDocument.Parse(bytes);
        if (root.RootElement.TryGetProperty("schemaVersion", out JsonElement version) || root.RootElement.TryGetProperty("SchemaVersion", out version))
        {
            Document result = JsonSerializer.Deserialize<Document>(bytes, Json) ?? throw new InvalidDataException("Missing texture authoring state.");
            if (version.GetInt32() is not (2 or 3) || result.Destinations is null || result.Mappings is null)
            {
                throw new InvalidDataException("Unsupported texture authoring state version.");
            }
            if (version.GetInt32() == 2)
            {
                foreach (Dictionary<string, TextureWorkingEntry> entries in result.Destinations.Values)
                {
                    foreach (string input in entries.Keys.ToArray())
                    {
                        entries[input] = entries[input] with { ImportedPixels = null, DestinationBytes = null, DestinationSemantic = null };
                    }
                }
            }
            return result with { SchemaVersion = 3 };
        }
        Dictionary<string, TextureWorkingEntry> legacy = JsonSerializer.Deserialize<Dictionary<string, TextureWorkingEntry>>(bytes, Json) ?? new();
        // Earlier authoring state did not identify a destination. Preserve explicit assignments,
        // but do not attribute its import baseline to an arbitrary mod.
        return new(3, new(StringComparer.Ordinal), legacy.Where(pair => pair.Value.ExplicitMapping).ToDictionary(pair => pair.Key, pair => pair.Value.Target, StringComparer.Ordinal));
    }
}
