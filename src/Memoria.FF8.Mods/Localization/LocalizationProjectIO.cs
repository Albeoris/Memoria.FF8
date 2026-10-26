using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Lossless project conversion. Outputs are new, disabled packages, published by directory rename.</summary>
public static partial class LocalizationProjectIO
{
    public const string ManifestRelativePath = "Localization/localization.manifest.json";
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    internal const long MaximumFileBytes = 128 * 1024 * 1024;

    public static LocalizationProject Read(string packageDirectory, bool allowMissingPayloads = false)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return ReadOnce(packageDirectory, allowMissingPayloads);
            }
            catch (IOException exception) when (attempt < 2 && exception is not LocalizationFileAccessException && exception is not FileNotFoundException && exception is not DirectoryNotFoundException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (IOException exception) when (exception is not LocalizationFileAccessException && (exception.HResult & 0xffff) == 32 && exception.Data["LocalizationPath"] is string)
            {
                throw new LocalizationFileAccessException((string)exception.Data["LocalizationPath"]!, exception);
            }
        }
    }

    private static LocalizationProject ReadOnce(string packageDirectory, bool allowMissingPayloads)
    {
        string root = System.IO.Path.GetFullPath(packageDirectory);
        Dictionary<string, (long Length, DateTime Modified, DateTime Created, LocalizationSnapshotIdentity? Identity)> stamps = new(StringComparer.OrdinalIgnoreCase);
        (long Length, DateTime Modified, DateTime Created, LocalizationSnapshotIdentity? Identity) Stamp(string path)
        {
            try
            {
                FileInfo info = new(path);
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return (info.Length, info.LastWriteTimeUtc, info.CreationTimeUtc, LocalizationSnapshotIdentity.Capture(stream));
            }
            catch (IOException exception)
            {
                exception.Data["LocalizationPath"] = path;
                throw;
            }
        }
        byte[] ReadBytes(string path)
        {
            stamps.Add(path, Stamp(path));
            return Snapshot(path);
        }
        using JsonDocument document = ParseJson(ReadBytes(Resolve(root, ManifestRelativePath)));
        foreach (string member in new[] { "schemaVersion", "payloadKind", "projectId", "sourceLanguage", "targetLanguage", "fieldScriptBaseLanguage", "sourceColumn", "targetColumn", "referenceLanguages", "mappingVersion", "compatibleVariants", "format", "layout", "payloads" })
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(member, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidDataException($"Localization manifest is missing required member {member}.");
            }
        }
        LocalizationManifest manifest = document.RootElement.Deserialize<LocalizationManifest>(JsonOptions) ?? throw new InvalidDataException("The localization manifest is empty.");
        ValidateManifest(manifest);
        if (manifest.Payloads.Count == 0 || (manifest.Layout == "single" && manifest.Payloads.Count != 1))
        {
            throw new InvalidDataException("The manifest must declare its localization payloads; single layout requires exactly one payload.");
        }
        List<LocalizationRecord> rows = [];
        List<LocalizationPayload> missingPayloads = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (LocalizationPayload payload in DiscoverPayloads(root, manifest))
        {
            if (!paths.Add(payload.Path))
            {
                throw new InvalidDataException($"Duplicate payload path: {payload.Path}.");
            }
            string fullPath = Resolve(root, payload.Path);
            byte[] bytes;
            try
            {
                bytes = ReadBytes(fullPath);
            }
            catch (IOException exception) when (allowMissingPayloads && (exception is FileNotFoundException or DirectoryNotFoundException) && manifest.Payloads.Any(declared => declared.Path == payload.Path))
            {
                stamps.Remove(fullPath);
                missingPayloads.Add(payload);
                continue;
            }
            rows.AddRange(manifest.Format == "json" ? LocalizationProjectJson.Read(bytes, manifest, payload) : LocalizationProjectWorkbook.Read(bytes, manifest, payload));
        }
        if (manifest.ExtensionsPath is not null)
        {
            using JsonDocument extensions = ParseJson(ReadBytes(Resolve(root, manifest.ExtensionsPath)));
            Dictionary<string, LocalizationAuthoringExtension> data = extensions.RootElement.Deserialize<Dictionary<string, LocalizationAuthoringExtension>>(JsonOptions) ?? throw new InvalidDataException("Invalid localization extensions.");
            if (data.Values.Any(extension => extension is null || extension.Values is null || extension.References is null || extension.ReferencePresence is null || extension.PresentColumns is null))
            {
                throw new InvalidDataException("Localization authoring extensions cannot contain null records or collections.");
            }
            HashSet<string> used = new(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                LocalizationRecord row = rows[i];
                if (data.TryGetValue(Identity(row), out LocalizationAuthoringExtension? extension))
                {
                    used.Add(Identity(row));
                    Dictionary<string, JsonElement> unknown = new(extension.Values, StringComparer.Ordinal);
                    foreach ((string key, JsonElement value) in row.ExtensionData)
                    {
                        if (value.ValueKind != JsonValueKind.String || value.GetString() != "" || (!extension.Values.ContainsKey(key) && extension.PresentColumns.Contains(key, StringComparer.Ordinal)))
                        {
                            unknown[key] = value;
                        }
                    }
                    rows[i] = row with
                    {
                        SourcePresent = !string.IsNullOrEmpty(row.Source) || extension.SourcePresent,
                        Source = string.IsNullOrEmpty(row.Source) ? extension.Source : row.Source,
                        References = manifest.ReferenceLanguages.Keys.ToDictionary(key => key, key => !string.IsNullOrEmpty(row.References.GetValueOrDefault(key)) ? row.References[key] : extension.References.GetValueOrDefault(key)),
                        ReferencePresence = manifest.ReferenceLanguages.Keys.ToDictionary(key => key, key => !string.IsNullOrEmpty(row.References.GetValueOrDefault(key)) || extension.ReferencePresence.GetValueOrDefault(key)),
                        ExtensionData = unknown
                    };
                }
            }
            bool RemovedPayloadIdentity(string identity)
            {
                string[] parts = identity.Split('/');
                if (parts.Length != 3 || !missingPayloads.Any(payload => manifest.Layout == "single" || payload.Category == parts[0] && payload.File == parts[1]))
                {
                    return false;
                }
                Validate(new LocalizationProject { Manifest = manifest, Entries = [new LocalizationRecord { Category = parts[0], File = parts[1], Key = parts[2], SourcePresent = false }] });
                return true;
            }
            if (data.Keys.Any(key => !used.Contains(key) && !RemovedPayloadIdentity(key)))
            {
                throw new InvalidDataException("Extensions contain an unknown canonical record identity.");
            }
        }
        LocalizationProject result = new() { Manifest = manifest, Entries = rows };
        Validate(result);
        foreach (var (path, stamp) in stamps)
        {
            if (Stamp(path) != stamp)
            {
                throw new IOException("The localization project changed while reading its manifest and payloads; retry after saving completes.");
            }
        }
        foreach (LocalizationPayload missing in missingPayloads)
        {
            try
            {
                System.IO.File.GetAttributes(Resolve(root, missing.Path));
            }
            catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
            throw new IOException("A removed localization payload reappeared during project reading; retry after saving completes.");
        }
        HashSet<string> currentPaths = DiscoverPayloads(root, manifest).Select(payload => payload.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!currentPaths.SetEquals(paths))
        {
            throw new IOException("The localization project's split payload inventory changed during reading.");
        }
        return result;
    }

    public static void Convert(string input, string output, string format, string layout, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        WriteNew(Read(input), output, format, layout, cancellation);
    }

    public static void WriteNew(LocalizationProject project, string directory, string format, string layout, CancellationToken cancellation = default)
    {
        format = format.TrimStart('.').ToLowerInvariant();
        layout = layout.ToLowerInvariant();
        LocalizationManifest manifest = project.Manifest with { Format = format, Layout = layout, Payloads = [], ExtensionsPath = null };
        Validate(project with { Manifest = manifest });
        string destination = System.IO.Path.GetFullPath(directory);
        if (Directory.Exists(destination) || System.IO.File.Exists(destination))
        {
            throw new IOException("The destination already exists; choose a new package directory to preserve translator edits.");
        }
        string parent = System.IO.Path.GetDirectoryName(destination) ?? throw new IOException("A package parent directory is required.");
        Directory.CreateDirectory(parent);
        string staging = System.IO.Path.Combine(parent, ".localization-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            List<LocalizationPayload> payloads = [];
            IEnumerable<IGrouping<string, LocalizationRecord>> groups = project.Entries.GroupBy(row => layout == "single" ? "" : row.Category + "/" + row.File, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal);
            if (project.Entries.Count == 0)
            {
                throw new InvalidDataException("A localization project must contain at least one record.");
            }
            foreach (IGrouping<string, LocalizationRecord> group in groups)
            {
                cancellation.ThrowIfCancellationRequested();
                LocalizationRecord[] rows = group.OrderBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal).ToArray();
                string path = "Localization/" + (layout == "single" ? "localization" : group.Key) + "." + format;
                Dictionary<string, string> worksheets = new(StringComparer.Ordinal);
                HashSet<string> sheetNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (string category in rows.Select(row => row.Category).Distinct(StringComparer.Ordinal))
                {
                    string sheetName = category;
                    if (sheetName.Length > 31 || sheetName.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0 || sheetName.StartsWith('\'') || sheetName.EndsWith('\'') || !sheetNames.Add(sheetName))
                    {
                        string safeName = Regex.Replace(category, "[\\[\\]:*?/\\\\']", "_", RegexOptions.CultureInvariant);
                        sheetName = safeName[..Math.Min(safeName.Length, 20)] + "_" + System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(category)))[..8];
                        if (!sheetNames.Add(sheetName))
                        {
                            throw new InvalidDataException($"Ambiguous Excel worksheet name for category {category}.");
                        }
                    }
                    worksheets.Add(category, sheetName);
                }
                LocalizationPayload payload = new() { Path = path, Category = layout == "split" ? rows[0].Category : null, File = layout == "split" ? rows[0].File : null, Worksheets = worksheets };
                string fullPath = Resolve(staging, path);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
                if (format == "json")
                {
                    System.IO.File.WriteAllBytes(fullPath, LocalizationProjectJson.Write(rows, manifest));
                }
                else
                {
                    LocalizationProjectWorkbook.Write(fullPath, rows, manifest, payload);
                }
                payloads.Add(payload);
            }
            string? extensionsPath = null;
            if (format == "xlsx")
            {
                extensionsPath = "Localization/localization.extensions.json";
                Dictionary<string, LocalizationAuthoringExtension> extensions = project.Entries.ToDictionary(Identity, row => new LocalizationAuthoringExtension
                {
                    SourcePresent = row.SourcePresent || !string.IsNullOrEmpty(row.Source),
                    Source = string.IsNullOrEmpty(row.Source) ? row.Source : null,
                    References = row.References.Where(pair => string.IsNullOrEmpty(pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value),
                    ReferencePresence = manifest.ReferenceLanguages.Keys.ToDictionary(key => key, key => row.ReferencePresence.TryGetValue(key, out bool present) ? present : row.References.ContainsKey(key)),
                    Values = row.ExtensionData.Where(pair => pair.Value.ValueKind != JsonValueKind.String).ToDictionary(pair => pair.Key, pair => pair.Value),
                    PresentColumns = row.ExtensionData.Keys.ToArray()
                });
                System.IO.File.WriteAllBytes(Resolve(staging, extensionsPath), JsonBytes(extensions));
            }
            manifest = manifest with { Payloads = payloads, ExtensionsPath = extensionsPath };
            System.IO.File.WriteAllBytes(Resolve(staging, ManifestRelativePath), JsonBytes(manifest));
            Read(staging);
            cancellation.ThrowIfCancellationRequested();
            string packageName = manifest.ExtensionData.TryGetValue("packageName", out JsonElement name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : "Localization project";
            if (string.IsNullOrWhiteSpace(packageName) || packageName.Any(character => char.IsControl(character) || character is '[' or ']'))
            {
                throw new InvalidDataException("The package name must be nonempty and contain no control characters or INI section delimiters.");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(staging, "mod.ini"), "[Mod]\nname=" + packageName + "\nenabled=0\n", new UTF8Encoding(false));
            Directory.Move(staging, destination);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    public static void Validate(LocalizationProject project)
    {
        ValidateManifest(project.Manifest);
        HashSet<string> keys = new(StringComparer.Ordinal);
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (LocalizationRecord row in project.Entries)
        {
            LocalizationTextValidation.Validate(row);
            SafeStem(row.Category);
            SafeStem(row.File);
            if (files.TryGetValue(row.File, out string? owner) && owner != row.Category + "/" + row.File)
            {
                throw new InvalidDataException($"Contradictory or case-colliding file ownership: {row.File}.");
            }
            files[row.File] = row.Category + "/" + row.File;
            string prefix = "$" + FieldLanguage(row.Key) + "_" + row.File + "_";
            string suffix = row.Key.StartsWith(prefix, StringComparison.Ordinal) ? row.Key[prefix.Length..] : "";
            string keyPattern = row.Category == "Fields" ? "^[0-9]{4,}(_ctx_[a-fA-F0-9]{8,64})?$" : "^[0-9]{4,}$";
            bool authoredSystem = row.Category == "System" && row.File == SystemTextCatalog.File && SystemTextCatalog.Defaults.Keys.Any(id => SystemTextCatalog.Key(id) == row.Key);
            if (!authoredSystem && (!Regex.IsMatch(suffix, keyPattern, RegexOptions.CultureInvariant) || !Regex.IsMatch(FieldLanguage(row.Key), "^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)))
            {
                throw new InvalidDataException($"Malformed canonical key {row.Key} for {row.File}.");
            }
            if (!keys.Add(row.Key))
            {
                throw new InvalidDataException($"Duplicate canonical key: {row.Key}.");
            }
            string[] roles = ["File", "Key", project.Manifest.SourceColumn, project.Manifest.TargetColumn, .. project.Manifest.ReferenceLanguages.Keys];
            if (row.ExtensionData.Keys.Any(key => roles.Contains(key, StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"Extension property conflicts with a role column: {row.Key}.");
            }
            if (row.References.Keys.Any(key => !project.Manifest.ReferenceLanguages.ContainsKey(key)))
            {
                throw new InvalidDataException($"Undeclared reference role: {row.Key}.");
            }
        }
    }

    private static string FieldLanguage(string key)
    {
        int end = key.IndexOf('_');
        return end > 1 ? key[1..end] : "";
    }

    private static void ValidateManifest(LocalizationManifest manifest)
    {
        if (manifest.ReferenceLanguages is null || manifest.CompatibleVariants is null || manifest.Payloads is null || manifest.ExtensionData is null || manifest.Payloads.Any(payload => payload is null || payload.Worksheets is null))
        {
            throw new InvalidDataException("Localization manifest collections and payload declarations cannot be null.");
        }
        if (manifest.SchemaVersion != 1 || manifest.PayloadKind != "localization")
        {
            throw new InvalidDataException("Unsupported localization manifest schema or payload kind.");
        }
        foreach (string value in new[] { manifest.ProjectId, manifest.SourceLanguage, manifest.TargetLanguage, manifest.FieldScriptBaseLanguage, manifest.MappingVersion }.Concat(manifest.ReferenceLanguages.Values))
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            {
                throw new InvalidDataException("Project, mapping and language identifiers must be nonempty and contain no control characters.");
            }
        }
        string[] labels = ["File", "Key", manifest.SourceColumn, manifest.TargetColumn, .. manifest.ReferenceLanguages.Keys];
        if (labels.Any(string.IsNullOrWhiteSpace) || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
        {
            throw new InvalidDataException("Ambiguous localization role columns.");
        }
        if (manifest.Format is not ("json" or "xlsx") || manifest.Layout is not ("single" or "split"))
        {
            throw new InvalidDataException("Localization format must be json/xlsx and layout single/split.");
        }
    }

    private static IEnumerable<LocalizationPayload> DiscoverPayloads(string root, LocalizationManifest manifest)
    {
        foreach (LocalizationPayload payload in manifest.Payloads)
        {
            if (!payload.Path.StartsWith("Localization/", StringComparison.Ordinal) || !payload.Path.EndsWith("." + manifest.Format, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Localization payload paths must stay inside the Localization content root and match the declared format.");
            }
            if (manifest.Layout == "split" && (payload.Category is null || payload.File is null))
            {
                throw new InvalidDataException("Split payload ownership must be declared.");
            }
            if (manifest.Layout == "split" && payload.Path != "Localization/" + payload.Category + "/" + payload.File + "." + manifest.Format)
            {
                throw new InvalidDataException("Split payload paths must use the declared category/file identity.");
            }
            yield return payload;
        }
        if (manifest.Layout != "split")
        {
            yield break;
        }
        HashSet<string> declared = manifest.Payloads.Select(payload => payload.Path.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string content = Resolve(root, "Localization");
        foreach (string path in Directory.EnumerateFiles(content, "*." + manifest.Format, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string relative = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
            string[] parts = relative.Split('/');
            if (declared.Contains(relative) || parts.Length != 3 || parts.Any(part => part.StartsWith('.') || part.StartsWith('~')))
            {
                continue;
            }
            yield return new LocalizationPayload { Path = relative, Category = parts[1], File = System.IO.Path.GetFileNameWithoutExtension(parts[2]), Worksheets = new Dictionary<string, string> { [parts[1]] = parts[1] } };
        }
    }

    internal static string Identity(LocalizationRecord row) => row.Category + "/" + row.File + "/" + row.Key;

    internal static string Resolve(string root, string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(part => part is "" or "." or "..") || System.IO.Path.IsPathRooted(path))
        {
            throw new InvalidDataException($"Unsafe localization payload path: {path}.");
        }
        string current = root;
        foreach (string segment in path.Split('/'))
        {
            SafeStem(segment);
            current = System.IO.Path.Combine(current, segment);
            if ((Directory.Exists(current) || System.IO.File.Exists(current)) && (System.IO.File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Localization payload paths cannot follow symbolic links or junctions.");
            }
        }
        return current;
    }

    internal static void SafeStem(string value)
    {
        if (string.IsNullOrEmpty(value) || value is "." or ".." || value.EndsWith('.') || value.EndsWith(' ') || value.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)) || Regex.IsMatch(value, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"Unsafe localization filename: {value}.");
        }
    }

    internal static byte[] Snapshot(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                FileInfo before = new(path);
                long length = before.Length;
                DateTime timestamp = before.LastWriteTimeUtc;
                if (length > MaximumFileBytes)
                {
                    throw new InvalidDataException($"Localization payload exceeds the {MaximumFileBytes} byte limit.");
                }
                byte[] bytes;
                LocalizationSnapshotIdentity? identity;
                using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan))
                using (MemoryStream copy = new())
                {
                    identity = LocalizationSnapshotIdentity.Capture(stream);
                    byte[] buffer = new byte[64 * 1024];
                    int count;
                    while ((count = stream.Read(buffer)) != 0)
                    {
                        if (copy.Length + count > MaximumFileBytes)
                        {
                            throw new InvalidDataException("Localization snapshot exceeds the size limit.");
                        }
                        copy.Write(buffer, 0, count);
                    }
                    bytes = copy.ToArray();
                    if (identity != LocalizationSnapshotIdentity.Capture(stream))
                    {
                        throw new IOException("Localization file changed while copying its snapshot.");
                    }
                }
                FileInfo after = new(path);
                if (after.Length != length || after.LastWriteTimeUtc != timestamp || bytes.LongLength != length)
                {
                    throw new IOException("Localization file changed during snapshot capture.");
                }
                using (FileStream verification = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (identity != LocalizationSnapshotIdentity.Capture(verification))
                    {
                        throw new IOException("Localization file was replaced during snapshot capture.");
                    }
                }
                return bytes;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) == 32)
            {
                throw new LocalizationFileAccessException(path, exception);
            }
        }
    }

    internal static JsonDocument ParseJson(byte[] bytes)
    {
        JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        try
        {
            CheckDuplicates(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> keys = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate JSON property: {property.Name}.");
                }
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in element.EnumerateArray())
            {
                CheckDuplicates(value);
            }
        }
    }

    internal static byte[] JsonBytes<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        StringBuilder output = new(json.Length);
        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                if (json[i + 1] == 'u' && i + 5 < json.Length && ushort.TryParse(json.AsSpan(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out ushort code) && code >= 32 && code != '"' && code != '\\')
                {
                    output.Append((char)code);
                    i += 5;
                }
                else
                {
                    output.Append(json[i]).Append(json[++i]);
                }
            }
            else
            {
                output.Append(json[i]);
            }
        }
        return new UTF8Encoding(false, true).GetBytes(output.ToString());
    }
}

internal sealed record LocalizationAuthoringExtension
{
    public bool SourcePresent { get; init; }
    public string? Source { get; init; }
    public IReadOnlyDictionary<string, string?> References { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, bool> ReferencePresence { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, JsonElement> Values { get; init; } = new Dictionary<string, JsonElement>();
    public IReadOnlyList<string> PresentColumns { get; init; } = [];
}
