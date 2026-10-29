using System.IO.Compression;
using System.Security.Cryptography;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Updates one owned Target cell atomically; all other package bytes and parts remain owned by their original provider.</summary>
public sealed class LocalizationProjectStore(string packageDirectory) : ILocalizationProjectStore
{
    private readonly string root = Path.GetFullPath(packageDirectory);
    public Task<LocalizationRowSnapshot> ReadRowAsync(LocalizationRowIdentity identity, CancellationToken cancellation = default) => Task.Run(() =>
    {
        cancellation.ThrowIfCancellationRequested();
        return Snapshot(LocalizationProjectIO.Read(root), identity);
    }, cancellation);

    public Task<LocalizationCommitResult> CommitAsync(LocalizationRowSnapshot expected, LocalizationTargetValue target, CancellationToken cancellation = default) => Task.Run(() => Commit(expected, target, cancellation), CancellationToken.None);

    public Task<LocalizationCommitResult> SetSourceReferenceAsync(LocalizationRowSnapshot expected, string language, CancellationToken cancellation = default) => Task.Run(() =>
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            using FileStream coordination = LocalizationAuthoringTransaction.Acquire(root);
            LocalizationAuthoringTransaction.Recover(root);
            LocalizationProject project = LocalizationProjectIO.ReadForAuthoring(root);
            LocalizationRowSnapshot current = Snapshot(project, expected.Identity);
            if (current.Revision != expected.Revision || current.ProjectId != expected.ProjectId)
            {
                return new LocalizationCommitResult(LocalizationCommitStatus.Conflict, current, "Project evidence changed before the Source Reference update.");
            }
            if (!project.Manifest.ReferenceLanguages.Values.Contains(language, StringComparer.OrdinalIgnoreCase) && !project.Manifest.FieldScriptBaseLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                return new LocalizationCommitResult(LocalizationCommitStatus.Invalid, current, "Choose an existing declared reference or the script-base dataset.");
            }
            if (project.Manifest.SourceReferenceLanguage == language)
            {
                return new LocalizationCommitResult(LocalizationCommitStatus.Unchanged, current);
            }
            byte[] original = File.ReadAllBytes(LocalizationProjectIO.Resolve(root, LocalizationProjectIO.ManifestRelativePath));
            JsonObject manifest = JsonNode.Parse(original)!.AsObject();
            manifest["sourceReferenceLanguage"] = language;
            if (manifest["sourceDatasets"] is JsonArray datasets)
            {
                foreach (JsonObject dataset in datasets.OfType<JsonObject>())
                {
                    string? id = (string?)(dataset["id"] ?? dataset["Id"]);
                    string[] declarations = dataset.Select(pair => pair.Key).Where(key => key.Equals("primaryReference", StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string declaration in declarations.Length == 0 ? ["primaryReference"] : declarations)
                    {
                        dataset[declaration] = id?.Equals(language, StringComparison.OrdinalIgnoreCase) == true;
                    }
                }
            }
            byte[] candidate = JsonSerializer.SerializeToUtf8Bytes(manifest, LocalizationProjectIO.JsonOptions);
            LocalizationManifest changed = JsonSerializer.Deserialize<LocalizationManifest>(candidate, LocalizationProjectIO.JsonOptions)!;
            LocalizationSourceRoles.Resolve(changed);
            if (original.AsSpan().SequenceEqual(candidate))
            {
                return new LocalizationCommitResult(LocalizationCommitStatus.Unchanged, current);
            }
            if (Snapshot(LocalizationProjectIO.ReadForAuthoring(root), expected.Identity).Revision != expected.Revision)
            {
                return new LocalizationCommitResult(LocalizationCommitStatus.Conflict, Snapshot(LocalizationProjectIO.ReadForAuthoring(root), expected.Identity), "Project evidence changed during Source Reference review.");
            }
            LocalizationAuthoringTransaction.Publish(root, new Dictionary<string, byte[]> { [LocalizationProjectIO.ManifestRelativePath] = candidate }, cancellation, new Dictionary<string, byte[]> { [LocalizationProjectIO.ManifestRelativePath] = original });
            return new LocalizationCommitResult(LocalizationCommitStatus.Saved, Snapshot(project with { Manifest = changed }, expected.Identity));
        }
        catch (OperationCanceledException)
        {
            return new LocalizationCommitResult(LocalizationCommitStatus.Cancelled, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new LocalizationCommitResult(LocalizationCommitStatus.Busy, null, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentException)
        {
            return new LocalizationCommitResult(LocalizationCommitStatus.Invalid, null, exception.Message);
        }
    }, CancellationToken.None);

    public Task<LocalizationRowSnapshot> ReadOrPrepareRowAsync(LocalizationRecord verifiedRow, CancellationToken cancellation = default) => Task.Run(() =>
    {
        cancellation.ThrowIfCancellationRequested();
        LocalizationProject project = LocalizationProjectIO.Read(root);
        LocalizationRowIdentity identity = new(verifiedRow.Category, verifiedRow.File, verifiedRow.Key);
        if (project.Entries.Any(row => Matches(row, identity)))
        {
            return Snapshot(project, identity);
        }
        LocalizationRecord prepared = verifiedRow with
        {
            Target = null,
            TargetPresent = false,
            References = project.Manifest.ReferenceLanguages.Keys.ToDictionary(label => label, label => verifiedRow.References.GetValueOrDefault(label), StringComparer.Ordinal),
            ReferencePresence = project.Manifest.ReferenceLanguages.Keys.ToDictionary(label => label, label => verifiedRow.ReferencePresence.TryGetValue(label, out bool present) ? present : verifiedRow.References.ContainsKey(label), StringComparer.Ordinal),
            ReferenceKeys = project.Manifest.ReferenceLanguages.Keys.ToDictionary(label => label, label => verifiedRow.ReferenceKeys.GetValueOrDefault(label) ?? [], StringComparer.Ordinal)
        };
        LocalizationProject candidate = project with { Entries = project.Entries.Append(prepared).ToArray() };
        LocalizationProjectIO.Validate(candidate);
        return Snapshot(candidate, identity) with { Missing = true };
    }, cancellation);

    private LocalizationCommitResult Commit(LocalizationRowSnapshot expected, LocalizationTargetValue target, CancellationToken cancellation)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            using FileStream coordination = LocalizationAuthoringTransaction.Acquire(root);
            LocalizationAuthoringTransaction.Recover(root);
            LocalizationProject project = LocalizationProjectIO.ReadForAuthoring(root);
            LocalizationRowSnapshot current = CurrentSnapshot(project, expected);
            if (current.ProjectId != expected.ProjectId || current.Revision != expected.Revision || current.Missing != expected.Missing)
            {
                return new(LocalizationCommitStatus.Conflict, current, "The canonical row or its project evidence changed. Review Base/Ours/Theirs before applying.");
            }
            if (target == current.Target)
            {
                return new(LocalizationCommitStatus.Unchanged, current);
            }
            LocalizationRecord changed = current.Row with { Target = target.Text, TargetPresent = target.Present };
            LocalizationRecord[] updatedRows = expected.Missing ? project.Entries.Append(changed).ToArray() : project.Entries.Select(row => Matches(row, expected.Identity) ? changed : row).ToArray();
            LocalizationProjectIO.Validate(project with { Entries = updatedRows });
            IEnumerable<LocalizationPayload> payloads = expected.Missing ? project.Manifest.Payloads : LocalizationProjectIO.DiscoverPayloads(root, project.Manifest);
            LocalizationPayload payload = payloads.Single(item => (item.Category is null || item.Category == expected.Identity.Category) && (item.File is null || item.File == expected.Identity.File) && (expected.Missing || Contains(item, project.Manifest, expected.Identity)));
            string destination = LocalizationProjectIO.Resolve(root, payload.Path);
            // Read sharing permits readers and atomic replacement, while refusing cooperative in-place writers.
            using FileStream original = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using MemoryStream content = new();
            original.CopyTo(content);
            byte[] originalBytes = content.ToArray();
            byte[] candidate = project.Manifest.Format == "json" ? PatchJson(originalBytes, project.Manifest, expected.Identity, target, expected.Missing ? changed : null) : PatchWorkbook(originalBytes, project.Manifest, payload, expected.Identity, target, expected.Missing ? changed : null);
            IReadOnlyList<LocalizationRecord> parsed = project.Manifest.Format == "json" ? LocalizationProjectJson.Read(candidate, project.Manifest, payload) : LocalizationProjectWorkbook.Read(candidate, project.Manifest, payload);
            LocalizationRecord parsedRow = parsed.Single(row => Matches(row, expected.Identity));
            if (parsedRow.Target != target.Text && !(project.Manifest.Format != "json" && target.Text is null && parsedRow.Target == ""))
            {
                throw new InvalidDataException("Candidate Target did not round trip.");
            }
            cancellation.ThrowIfCancellationRequested();
            LocalizationProject latestProject = LocalizationProjectIO.ReadForAuthoring(root);
            LocalizationRowSnapshot latest = CurrentSnapshot(latestProject, expected);
            if (latest.Revision != expected.Revision || latest.ProjectId != expected.ProjectId || latest.Missing != expected.Missing || !File.ReadAllBytes(destination).AsSpan().SequenceEqual(originalBytes))
            {
                return new(LocalizationCommitStatus.Conflict, latest, "The payload changed during preparation; review the refreshed candidate.");
            }
            // Existing rows own one payload; verified insertion additionally owns the binding-sidecar entry.
            Dictionary<string, byte[]> candidates = new() { [payload.Path] = candidate };
            Dictionary<string, byte[]> expectedOriginals = new() { [payload.Path] = originalBytes };
            expectedOriginals[LocalizationProjectIO.ManifestRelativePath] = File.ReadAllBytes(LocalizationProjectIO.Resolve(root, LocalizationProjectIO.ManifestRelativePath));
            foreach (LocalizationDependencyFile dependency in project.EncodingFiles)
            {
                expectedOriginals[dependency.Path] = dependency.Content;
            }
            if (project.Manifest.ExtensionsPath is string evidencePath)
            {
                expectedOriginals[evidencePath] = File.ReadAllBytes(LocalizationProjectIO.Resolve(root, evidencePath));
            }
            if (expected.Missing && project.Manifest.ExtensionsPath is string extensionPath)
            {
                byte[] extensionBytes = File.ReadAllBytes(LocalizationProjectIO.Resolve(root, extensionPath));
                expectedOriginals[extensionPath] = extensionBytes;
                JsonObject extensions = JsonNode.Parse(extensionBytes)!.AsObject();
                extensions.Add(LocalizationProjectIO.Identity(changed), JsonSerializer.SerializeToNode(new LocalizationAuthoringExtension
                {
                    Source = changed.Source,
                    SourcePresent = changed.SourcePresent,
                    References = changed.References,
                    ReferencePresence = changed.ReferencePresence,
                    Values = changed.ExtensionData,
                    PresentColumns = changed.ExtensionData.Keys.ToArray()
                }, LocalizationProjectIO.JsonOptions));
                candidates[extensionPath] = JsonSerializer.SerializeToUtf8Bytes(extensions, LocalizationProjectIO.JsonOptions);
            }
            LocalizationRowSnapshot final = CurrentSnapshot(LocalizationProjectIO.ReadForAuthoring(root), expected);
            if (final.Revision != expected.Revision || final.Missing != expected.Missing || final.ProjectId != expected.ProjectId)
            {
                return new(LocalizationCommitStatus.Conflict, final, "Canonical evidence changed during final preparation.");
            }
            original.Dispose();
            LocalizationAuthoringTransaction.Publish(root, candidates, cancellation, expectedOriginals);
            return new(LocalizationCommitStatus.Saved, Snapshot(project with { Entries = updatedRows }, expected.Identity));
        }
        catch (OperationCanceledException)
        {
            return new(LocalizationCommitStatus.Cancelled, null, "Save cancelled; draft retained.");
        }
        catch (IOException exception)
        {
            return new(LocalizationCommitStatus.Busy, null, "File busy — retry after saving/closing the other editor. " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new(LocalizationCommitStatus.Busy, null, "File busy — retry after saving/closing the other editor. " + exception.Message);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or JsonException or KeyNotFoundException)
        {
            return new(LocalizationCommitStatus.Invalid, null, exception.Message);
        }
    }

    private bool Contains(LocalizationPayload payload, LocalizationManifest manifest, LocalizationRowIdentity identity)
    {
        byte[] bytes = File.ReadAllBytes(LocalizationProjectIO.Resolve(root, payload.Path));
        return (manifest.Format == "json" ? LocalizationProjectJson.Read(bytes, manifest, payload) : LocalizationProjectWorkbook.Read(bytes, manifest, payload)).Any(row => Matches(row, identity));
    }
    private static bool Matches(LocalizationRecord row, LocalizationRowIdentity identity) => row.Category == identity.Category && row.File == identity.File && row.Key == identity.Key;
    private static LocalizationRowSnapshot CurrentSnapshot(LocalizationProject project, LocalizationRowSnapshot expected)
    {
        if (project.Entries.Any(row => Matches(row, expected.Identity)))
        {
            return Snapshot(project, expected.Identity);
        }
        if (expected.Missing)
        {
            return Snapshot(project with { Entries = project.Entries.Append(expected.Row).ToArray() }, expected.Identity) with { Missing = true };
        }
        throw new InvalidDataException("The canonical row was deleted; restore a valid binding before saving.");
    }
    private static LocalizationRowSnapshot Snapshot(LocalizationProject project, LocalizationRowIdentity identity)
    {
        LocalizationRecord row = project.Entries.Single(item => Matches(item, identity));
        JsonNode evidence = JsonSerializer.SerializeToNode(new { project.Manifest, Row = row, Encoding = project.EncodingFiles.OrderBy(file => file.Path, StringComparer.Ordinal).Select(file => new { file.Path, Sha256 = Convert.ToHexString(SHA256.HashData(file.Content)) }) })!;
        string revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Canonical(evidence))));
        return new(project.Manifest.ProjectId, identity, row, new(row.TargetPresent, row.Target), revision);
    }
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => KeyValuePair.Create(pair.Key, Canonical(pair.Value))).ToArray()),
        JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => JsonNode.Parse(NormalizeNumber(value.ToJsonString())),
        _ => node?.DeepClone()
    };
    private static string NormalizeNumber(string text)
    {
        bool negative = text.StartsWith('-');
        string unsigned = negative ? text[1..] : text;
        int exponentIndex = unsigned.IndexOfAny(['e', 'E']);
        string mantissa = exponentIndex < 0 ? unsigned : unsigned[..exponentIndex];
        BigInteger exponent = exponentIndex < 0 ? BigInteger.Zero : BigInteger.Parse(unsigned[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
        int decimalIndex = mantissa.IndexOf('.');
        if (decimalIndex >= 0)
        {
            exponent -= mantissa.Length - decimalIndex - 1;
        }
        string digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
        {
            return "0";
        }
        int trimmed = digits.Length - digits.TrimEnd('0').Length;
        digits = digits.TrimEnd('0');
        exponent += trimmed;
        return (negative ? "-" : "") + digits + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }
    private static byte[] PatchJson(byte[] bytes, LocalizationManifest manifest, LocalizationRowIdentity identity, LocalizationTargetValue target, LocalizationRecord? insertion)
    {
        JsonNode document = JsonNode.Parse(bytes)!;
        if (insertion is not null)
        {
            document[identity.Category] ??= new JsonObject();
            document[identity.Category]![identity.File] ??= new JsonObject();
            document[identity.Category]![identity.File]![identity.Key] = JsonNode.Parse(LocalizationProjectJson.Write([insertion], manifest))![identity.Category]![identity.File]![identity.Key]!.DeepClone();
        }
        JsonObject row = document[identity.Category]![identity.File]![identity.Key]!.AsObject();
        if (target.Present)
        {
            row[manifest.TargetColumn] = target.Text;
        }
        else
        {
            row.Remove(manifest.TargetColumn);
        }
        return JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
    }
    private static byte[] PatchWorkbook(byte[] bytes, LocalizationManifest manifest, LocalizationPayload payload, LocalizationRowIdentity identity, LocalizationTargetValue target, LocalizationRecord? insertion)
    {
        using MemoryStream stream = new();
        stream.Write(bytes);
        using (ZipArchive zip = new(stream, ZipArchiveMode.Update, true))
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XDocument Xml(string name)
            {
                using Stream input = zip.GetEntry(name)!.Open();
                return XDocument.Load(input, LoadOptions.PreserveWhitespace);
            }
            string[] shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(ns + "si").Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value))).ToArray();
            string Value(XElement? cell) => cell is null ? "" : (string?)cell.Attribute("t") == "s" ? shared[int.Parse(cell.Element(ns + "v")!.Value, System.Globalization.CultureInfo.InvariantCulture)] : (string?)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)) : cell.Element(ns + "v")?.Value ?? "";
            string sheetName = payload.Worksheets[identity.Category];
            XElement sheet = Xml("xl/workbook.xml").Descendants(ns + "sheet").Single(item => (string?)item.Attribute("name") == sheetName);
            string relationship = (string)sheet.Attribute(rel + "id")!;
            string part = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().Single(item => (string?)item.Attribute("Id") == relationship).Attribute("Target")!.Value;
            string path = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), part).AbsolutePath.TrimStart('/');
            XDocument worksheet = Xml(path);
            XElement[] rows = worksheet.Descendants(ns + "sheetData").Elements(ns + "row").ToArray();
            string Column(XElement cell) => new(((string?)cell.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray());
            Dictionary<string, string> columns = rows.Single(row => (string?)row.Attribute("r") == "1").Elements(ns + "c").Where(cell => Value(cell).Length != 0).ToDictionary(Value, Column, StringComparer.Ordinal);
            if (insertion is not null)
            {
                uint next = rows.Select(item => uint.Parse(item.Attribute("r")!.Value, System.Globalization.CultureInfo.InvariantCulture)).Max() + 1;
                if (next > 1048576)
                {
                    throw new InvalidDataException("The worksheet has reached Excel's row limit.");
                }
                XElement added = new(ns + "row", new XAttribute("r", next));
                foreach ((string label, string column) in columns)
                {
                    string? value = label == "File" ? insertion.File : label == "Key" ? insertion.Key : label == manifest.SourceColumn ? insertion.Source : label == manifest.TargetColumn ? insertion.Target : insertion.References.TryGetValue(label, out string? reference) ? reference : label.StartsWith("ReferenceKey [", StringComparison.Ordinal) ? JsonSerializer.Serialize(insertion.ReferenceKeys.GetValueOrDefault("Reference " + label["ReferenceKey ".Length..]) ?? []) : insertion.ExtensionData.TryGetValue(label, out JsonElement extra) && extra.ValueKind == JsonValueKind.String ? extra.GetString() : null;
                    if (value is not null)
                    {
                        added.Add(new XElement(ns + "c", new XAttribute("r", column + next), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))));
                    }
                }
                worksheet.Descendants(ns + "sheetData").Single().Add(added);
                rows = rows.Append(added).ToArray();
            }
            XElement row = rows.Single(row => Value(row.Elements(ns + "c").FirstOrDefault(cell => Column(cell) == columns["File"])) == identity.File && Value(row.Elements(ns + "c").FirstOrDefault(cell => Column(cell) == columns["Key"])) == identity.Key);
            string address = columns[manifest.TargetColumn] + row.Attribute("r")!.Value;
            XElement? existing = row.Elements(ns + "c").FirstOrDefault(cell => (string?)cell.Attribute("r") == address);
            if (!target.Present)
            {
                existing?.Remove();
            }
            else
            {
                XElement cell = existing ?? new XElement(ns + "c", new XAttribute("r", address));
                cell.Elements().Where(item => item.Name == ns + "v" || item.Name == ns + "is" || item.Name == ns + "f").Remove();
                cell.SetAttributeValue("t", target.Text is null ? null : "inlineStr");
                if (target.Text is not null)
                {
                    cell.AddFirst(new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), target.Text)));
                }
                if (existing is null)
                {
                    XElement? after = row.Elements(ns + "c").FirstOrDefault(item => Column(item).Length > columns[manifest.TargetColumn].Length || Column(item).Length == columns[manifest.TargetColumn].Length && string.CompareOrdinal(Column(item), columns[manifest.TargetColumn]) > 0);
                    if (after is null)
                    {
                        row.Add(cell);
                    }
                    else
                    {
                        after.AddBeforeSelf(cell);
                    }
                }
            }
            ZipArchiveEntry entry = zip.GetEntry(path)!;
            entry.Delete();
            using Stream output = zip.CreateEntry(path).Open();
            worksheet.Save(output, SaveOptions.DisableFormatting);
        }
        return stream.ToArray();
    }
}
