using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectIO
{
    public const string EditionTargetsColumn = LocalizationNativeEdition.TargetsMember;
    public const string ReferenceEditionsMember = "referenceEditions";

    public static bool? MatchesRecordedSourceText(LocalizationRecord row, string reference)
    {
        string? sourceHash = LocalizationReferenceProvenance.SourceHash(row);
        return sourceHash is null ? null : string.Equals(sourceHash, FingerprintPlayerSource(row, reference), StringComparison.OrdinalIgnoreCase);
    }

    public static LocalizationProject ReadJsonForReview(string input)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        using FileStream coordination = AcquireReadCoordination(root);
        if (LocalizationAuthoringTransaction.RecoveryPending(root))
        {
            throw new IOException("An interrupted localization update requires recovery before lint can read the package. Run a writable localization command to recover it.");
        }
        LocalizationProject project = ReadOnce(root, false, null, validateTargetText: false);
        RequireReviewJson(project.Manifest);
        return project;
    }

    public static LocalizationProject UpdateJsonReferences(string input, Func<LocalizationProject, LocalizationProject> transform, CancellationToken cancellationToken)
    {
        return UpdateReviewJson(input, transform, references: true, cancellationToken);
    }

    public static LocalizationProject UpdateJsonTargets(string input, Func<LocalizationProject, LocalizationProject> transform, CancellationToken cancellationToken)
    {
        return UpdateReviewJson(input, transform, references: false, cancellationToken);
    }

    private static LocalizationProject UpdateReviewJson(string input, Func<LocalizationProject, LocalizationProject> transform, bool references, CancellationToken cancellation)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        using FileStream coordination = AcquireReadCoordination(root);
        LocalizationAuthoringTransaction.Recover(root);
        Dictionary<string, byte[]> originals = new(StringComparer.OrdinalIgnoreCase);
        LocalizationProject project = ReadOnce(root, false, null, validateTargetText: false, originals);
        RequireReviewJson(project.Manifest);
        LocalizationPayload[] payloads = DiscoverPayloads(root, project.Manifest).ToArray();
        LocalizationProject updated = transform(project);
        if (updated.Entries.Count != project.Entries.Count || updated.Entries.Zip(project.Entries).Any(pair => Identity(pair.First) != Identity(pair.Second)))
        {
            throw new InvalidDataException("A translation review cannot add, remove or reorder canonical records.");
        }
        ValidateCore(updated, null, validateTargetText: false);
        LocalizationSourceRoles.Resolve(updated.Manifest);

        Dictionary<string, byte[]> candidates = new(StringComparer.OrdinalIgnoreCase);
        string[] newLabels = references ? updated.Manifest.ReferenceLanguages.Keys.Except(project.Manifest.ReferenceLanguages.Keys, StringComparer.Ordinal).ToArray() : [];
        string[] changedMembers = references
            ? new[] { ReferenceEditionsMember, LocalizationReferenceProvenance.ManifestMember }.Where(member => ManifestMemberChanged(project.Manifest, updated.Manifest, member)).ToArray()
            : [];
        if (newLabels.Length > 0 || changedMembers.Length > 0)
        {
            JsonObject manifest = ParseReviewObject(originals[ManifestRelativePath]);
            JsonObject declarations = manifest["referenceLanguages"]!.AsObject();
            foreach (string label in newLabels)
            {
                declarations.Add(label, updated.Manifest.ReferenceLanguages[label]);
            }
            foreach (string member in changedMembers)
            {
                if (updated.Manifest.ExtensionData.TryGetValue(member, out JsonElement value))
                {
                    manifest[member] = JsonNode.Parse(value.GetRawText());
                }
                else
                {
                    manifest.Remove(member);
                }
            }
            candidates.Add(ManifestRelativePath, JsonBytes(manifest, project.Manifest.PlayerExport));
        }

        var changes = project.Entries.Zip(updated.Entries).Where(pair => pair.First != pair.Second)
            .ToDictionary(pair => (pair.First.Category, pair.First.File, pair.First.Key));
        var changedFiles = changes.Keys.Select(row => (row.Category, row.File)).ToHashSet();
        string? sidecarPath = project.Manifest.ExtensionsPath;
        JsonObject? sidecar = null;
        bool sidecarChanged = false;
        foreach (LocalizationPayload payload in payloads)
        {
            cancellation.ThrowIfCancellationRequested();
            if (newLabels.Length == 0 && (changes.Count == 0 || project.Manifest.Layout == "split" && !changedFiles.Contains((payload.Category!, payload.File!))))
            {
                continue;
            }
            JsonObject document = ParseReviewObject(originals[payload.Path]);
            bool payloadChanged = false;
            foreach ((string category, JsonNode? categoryNode) in document)
            {
                foreach ((string file, JsonNode? fileNode) in categoryNode!.AsObject())
                {
                    foreach ((string key, JsonNode? rowNode) in fileNode!.AsObject())
                    {
                        if (!changes.TryGetValue((category, file, key), out var pair) && newLabels.Length == 0)
                        {
                            continue;
                        }
                        JsonObject row = rowNode!.AsObject();
                        bool updateSidecar = references || pair.Second?.ExtensionData.ContainsKey(EditionTargetsColumn) == true;
                        if (updateSidecar && sidecarPath is not null)
                        {
                            sidecar ??= ParseReviewObject(originals[sidecarPath]);
                        }
                        JsonObject? extension = updateSidecar ? sidecar?[$"{category}/{file}/{key}"] as JsonObject : null;
                        if (references)
                        {
                            // The sidecar also tracks null/empty reference presence on authoring projects.
                            payloadChanged |= PatchReferences(row, extension, pair.First, pair.Second, updated.Manifest, newLabels, ref sidecarChanged);
                        }
                        else
                        {
                            payloadChanged |= PatchTargets(row, extension, project.Manifest.TargetColumn, pair.First!, pair.Second!, ref sidecarChanged);
                        }
                    }
                }
            }
            if (payloadChanged)
            {
                byte[] candidate = JsonBytes(document, project.Manifest.PlayerExport);
                _ = LocalizationProjectJson.Read(candidate, updated.Manifest, payload);
                candidates.Add(payload.Path, candidate);
            }
        }
        if (sidecarChanged)
        {
            candidates.Add(sidecarPath!, JsonBytes(sidecar));
        }
        if (candidates.Count > 0)
        {
            if (!DiscoverPayloads(root, project.Manifest).Select(payload => payload.Path).SequenceEqual(payloads.Select(payload => payload.Path), StringComparer.Ordinal))
            {
                throw new IOException("The split payload inventory changed during translation review. Retry after saving completes.");
            }
            LocalizationAuthoringTransaction.Publish(root, candidates, cancellation, originals);
        }
        return updated;
    }

    private static void RequireReviewJson(LocalizationManifest manifest)
    {
        if (manifest.Format != "json")
        {
            throw new InvalidDataException("Translation review requires a JSON localization package.");
        }
    }

    private static bool ManifestMemberChanged(LocalizationManifest before, LocalizationManifest after, string member)
    {
        bool existed = before.ExtensionData.TryGetValue(member, out JsonElement original);
        bool present = after.ExtensionData.TryGetValue(member, out JsonElement updated);
        return existed != present || existed && !JsonElement.DeepEquals(original, updated);
    }

    private static JsonObject ParseReviewObject(byte[] bytes) => JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions { MaxDepth = 64 })!.AsObject();

    private static bool PatchTargets(JsonObject row, JsonObject? extension, string column, LocalizationRecord before, LocalizationRecord after, ref bool sidecarChanged)
    {
        bool changed = false;
        if (before.Target != after.Target)
        {
            row[column] = after.Target;
            changed = true;
        }
        if (before.ExtensionData.TryGetValue(EditionTargetsColumn, out JsonElement oldOverrides) &&
            after.ExtensionData.TryGetValue(EditionTargetsColumn, out JsonElement newOverrides) &&
            !JsonElement.DeepEquals(oldOverrides, newOverrides))
        {
            if (row.ContainsKey(EditionTargetsColumn))
            {
                row[EditionTargetsColumn] = JsonNode.Parse(newOverrides.GetRawText());
                changed = true;
            }
            else if (extension?["values"] is JsonObject values && values.ContainsKey(EditionTargetsColumn))
            {
                values[EditionTargetsColumn] = JsonNode.Parse(newOverrides.GetRawText());
                sidecarChanged = true;
            }
        }
        return changed;
    }

    private static bool PatchReferences(JsonObject row, JsonObject? extension, LocalizationRecord? before, LocalizationRecord? after, LocalizationManifest manifest, string[] newLabels, ref bool sidecarChanged)
    {
        bool changed = false;
        foreach (string label in manifest.ReferenceLanguages.Keys)
        {
            string? oldText = before?.References.GetValueOrDefault(label);
            string? newText = after?.References.GetValueOrDefault(label);
            bool oldPresent = ReferencePresent(before, label);
            bool newPresent = ReferencePresent(after, label);
            if (after is not null && (oldText != newText || oldPresent != newPresent))
            {
                if (newPresent)
                {
                    row[label] = newText;
                }
                else
                {
                    row.Remove(label);
                }
                if (extension is not null)
                {
                    UpdateSidecarReference(extension, "references", label, newText, newPresent);
                    UpdateSidecarReference(extension, "referencePresence", label, newPresent, newPresent);
                    sidecarChanged = true;
                }
                changed = true;
            }
            IReadOnlyList<string> oldKeys = before?.ReferenceKeys.GetValueOrDefault(label) ?? [];
            IReadOnlyList<string> newKeys = after?.ReferenceKeys.GetValueOrDefault(label) ?? [];
            if (manifest.SchemaVersion >= 2 && (newLabels.Contains(label, StringComparer.Ordinal) || after is not null && !oldKeys.SequenceEqual(newKeys, StringComparer.Ordinal)))
            {
                string keyColumn = "ReferenceKey " + label["Reference ".Length..];
                row[keyColumn] = newKeys.Count switch
                {
                    0 => null,
                    1 => JsonValue.Create(newKeys[0]),
                    _ => JsonSerializer.SerializeToNode(newKeys)
                };
                changed = true;
            }
        }
        return changed;
    }

    private static bool ReferencePresent(LocalizationRecord? row, string label) =>
        row is not null && (row.ReferencePresence.TryGetValue(label, out bool present) ? present : row.References.ContainsKey(label));

    private static void UpdateSidecarReference(JsonObject extension, string member, string column, JsonNode? value, bool present)
    {
        if (present)
        {
            if (extension[member] is not JsonObject entries)
            {
                entries = new JsonObject();
                extension[member] = entries;
            }
            entries[column] = value;
        }
        else if (extension[member] is JsonObject entries)
        {
            entries.Remove(column);
        }
    }
}
