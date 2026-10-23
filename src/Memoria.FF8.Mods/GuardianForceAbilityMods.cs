using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed class GuardianForceAbilityMods
{
    public const string PayloadPath = "gameplay/gf-abilities.json";
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<GuardianForceAbilityOperationKind>() }
    };
    public static GuardianForceAbilityMods Empty { get; } = new([]);
    public GuardianForceAbilityMods(ImmutableArray<GuardianForceAbilityPatchLayer> layers)
    {
        Layers = layers;
    }
    public ImmutableArray<GuardianForceAbilityPatchLayer> Layers
    {
        get;
    }

    public static GuardianForceAbilityPatchDocument Parse(byte[] bytes)
    {
        GuardianForceAbilityPatchDocument document = JsonSerializer.Deserialize<GuardianForceAbilityPatchDocument>(bytes, JsonOptions) ?? throw new InvalidDataException("A GF payload must be a document object.");
        document.Validate();
        return document;
    }

    public static byte[] Serialize(GuardianForceAbilityPatchDocument document)
    {
        document.Validate();
        using JsonDocument json = JsonSerializer.SerializeToDocument(document, JsonOptions);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteCanonical(writer, json.RootElement);
        }
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement child in element.EnumerateArray())
            {
                WriteCanonical(writer, child);
            }
            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out GuardianForceAbilityMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<GuardianForceAbilityPatchLayer> layers = [];
        List<ModDiagnostic> errors = [];
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.Equals(PayloadPath, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                GuardianForceAbilityPatchDocument document = Parse(file.ReadBytes());
                if (!identities.Add(document.DocumentId))
                {
                    throw new InvalidDataException("Duplicate GF document identity: " + document.DocumentId);
                }
                layers.Add(new(file.PackageId, document));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add(new("Memoria.InvalidGuardianForcePatch", exception.Message, file.PackageId, PayloadPath));
            }
        }
        result = errors.Count == 0 ? new(layers.ToImmutableArray()) : Empty;
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }
}

public sealed record GuardianForceAbilityPackageDocument(string PackageDirectory, GuardianForceAbilityPatchDocument Document, string Revision);

/// <summary>Reads disabled payloads and writes only the selected author's document. Package settings and unrelated payloads are untouched.</summary>
public static class GuardianForceAbilityPackageStore
{
    public static GuardianForceAbilityPackageDocument Open(ModPackage package)
    {
        if (package.Format != ModFormat.Memoria)
        {
            throw new InvalidDataException("GF editing requires a native Memoria package.");
        }
        string path = Path.Combine(package.RootDirectory, GuardianForceAbilityMods.PayloadPath);
        RejectLinks(package.RootDirectory, path);
        byte[] bytes = File.ReadAllBytes(path);
        return new(Path.GetFullPath(package.RootDirectory), GuardianForceAbilityMods.Parse(bytes), Revision(bytes));
    }

    public static GuardianForceAbilityPatchDocument IndependentCopy(GuardianForceAbilityPatchDocument document)
    {
        document.Validate();
        string newId = Guid.NewGuid().ToString("D");
        return document with
        {
            DocumentId = newId,
            Operations = document.Operations.Select(operation => operation with { ProducerDocumentId = operation.ProducerDocumentId == document.DocumentId ? newId : operation.ProducerDocumentId, Producers = operation.Producers.Select(reference => reference.DocumentId == document.DocumentId ? reference with { DocumentId = newId } : reference).ToImmutableArray() }).ToImmutableArray()
        };
    }

    public static string ValidateDestination(string primaryModsDirectory, string packageDirectory)
    {
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageDirectory));
        RejectLinks(Path.GetPathRoot(destination)!, destination);
        return destination;
    }

    public static GuardianForceAbilityPackageDocument Save(string primaryModsDirectory, string packageDirectory, GuardianForceAbilityPatchDocument document, string? expectedRevision = null, IReadOnlyList<ModPackage>? installedPackages = null)
    {
        byte[] bytes = GuardianForceAbilityMods.Serialize(document);
        string destination = ValidateDestination(primaryModsDirectory, packageDirectory);
        string primaryPackageRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(primaryModsDirectory, "Memoria")));
        string packageId = "Memoria/" + Path.GetFileName(destination);
        ModPackage? owner = string.Equals(Path.GetDirectoryName(destination), primaryPackageRoot, StringComparison.OrdinalIgnoreCase)
            ? installedPackages.FirstOrDefault(package => package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            : null;
        if (owner is not null && !Path.GetFullPath(owner.RootDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A package in another mod root owns this identity; use a new package name.");
        }
        string path = Path.Combine(destination, GuardianForceAbilityMods.PayloadPath);
        RejectLinks(Path.GetPathRoot(destination)!, path);
        VerifyRevision(path, expectedRevision, document.DocumentId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".gf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            ValidateDestination(primaryModsDirectory, destination);
            RejectLinks(Path.GetPathRoot(destination)!, path);
            VerifyRevision(path, expectedRevision, document.DocumentId);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        return new(destination, document, Revision(bytes));
    }

    private static void VerifyRevision(string path, string? expected, string documentId)
    {
        if (File.Exists(path))
        {
            byte[] existing = File.ReadAllBytes(path);
            if (expected is null || !Revision(existing).Equals(expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The GF document changed externally or already exists. Reload it or use Save As.");
            }
            if (!GuardianForceAbilityMods.Parse(existing).DocumentId.Equals(documentId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("In-place edits must retain the existing document identity. Save an independent copy into a new package.");
            }
        }
        else if (expected is not null)
        {
            throw new InvalidDataException("The GF document was removed externally. Reload it or use Save As.");
        }
    }

    private static string Revision(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void RejectLinks(string root, string path)
    {
        string current = Path.GetFullPath(root);
        foreach (string segment in new[] { string.Empty }.Concat(Path.GetRelativePath(current, Path.GetFullPath(path)).Split(Path.DirectorySeparatorChar)))
        {
            current = Path.Combine(current, segment);
            if (new DirectoryInfo(current).LinkTarget is not null || new FileInfo(current).LinkTarget is not null)
            {
                throw new InvalidDataException("Mod authoring paths must not contain symbolic links, including dangling links.");
            }
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Mod authoring paths must not contain directory links or reparse points.");
            }
        }
    }
}
