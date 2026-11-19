using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private const string TextureWorkingStateFile = ".memoria-texture-authoring.json";
    private sealed record TextureWorkingEntry(TextureTarget Target, string? ImportedPixels = null, string? DestinationBytes = null, bool ExplicitMapping = false);
    private sealed record TextureImportRow(string RelativePath, TextureTarget? Target, TextureTargetContract? Contract, string InputBytes, string? Pixels, string? DestinationBytes, bool Changed, string Status, TextureExchangeMetadata? Metadata = null, string? Workspace = null, int WorkspaceTile = -1, bool ExplicitMapping = false, string? MetadataFingerprint = null);
    private sealed record TextureImportReview(string Source, string Destination, TextureImportRow[] Rows, Dictionary<string, TextureWorkingEntry> Working, IReadOnlyDictionary<string, string?> ExpectedOutputs);
    private TextureImportReview? textureReview;
    private Dictionary<string, TextureTarget> textureManualMappings = new(StringComparer.Ordinal);

    private async void OpenTextureImport()
    {
        try
        {
            string? source = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new(LocalizeAuthored("textures.import.folder", "Choose edited PNG / DDS texture folder"), textureImportDirectory)));
            if (source is null)
            {
                return;
            }
            string root = Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria");
            string? destination = await ModelBrowserDialogs.DestinationAsync(Window.Handle, root, LocalizeUiText);
            if (destination is null)
            {
                return;
            }
            textureManualMappings.Clear();
            textureImportFiles = null;
            ReadTextureImport(source, destination);
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            textureBrowser!.Message = exception.Message;
        }
        finally
        {
            textureDialogDrain = true;
            keyboard.SuppressHeldNameInput();
        }
    }

    private async void OpenTextureImportFiles()
    {
        try
        {
            DialogResult result = await ModelBrowserDialogs.Desktop.OpenFileAsync(new(LocalizeAuthored("textures.import.files", "Choose edited texture files"), textureImportDirectory, [new("Editable textures (*.png, *.dds, *.ora)", ["png", "dds", "ora"])]) { AllowMultiple = true });
            if (result.Status == DialogStatus.Cancelled)
            {
                return;
            }
            if (result.Status != DialogStatus.Selected || result.Path is null)
            {
                throw new IOException(result.Error ?? "The texture file chooser failed.");
            }
            string[] files = (result.Paths ?? [result.Path]).Select(Path.GetFullPath).ToArray();
            string source = Path.GetDirectoryName(files[0])!;
            while (files.Any(file => Path.GetRelativePath(source, file).StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(Path.GetRelativePath(source, file))))
            {
                source = Path.GetDirectoryName(source) ?? throw new InvalidDataException("Select texture files from the same volume.");
            }
            string root = Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria");
            string? destination = await ModelBrowserDialogs.DestinationAsync(Window.Handle, root, LocalizeUiText);
            if (destination is null)
            {
                return;
            }
            textureImportFiles = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
            textureManualMappings.Clear();
            ReadTextureImport(source, destination);
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            textureBrowser!.Message = exception.Message;
        }
        finally
        {
            textureDialogDrain = true;
            keyboard.SuppressHeldNameInput();
        }
    }

    private void ReadTextureImport(string source, string destination)
    {
        var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
        HashSet<string>? onlyFiles = textureImportFiles is null ? null : new(textureImportFiles, StringComparer.OrdinalIgnoreCase);
        if (textureCatalog.Length == 0)
        {
            StartTextureJob(token =>
            {
                using var authoring = createSession();
                GameAssets assets = authoring.Assets;
                List<TextureCatalogDiagnostic> diagnostics = [];
                TextureCatalogEntry[] entries = assets.EnumerateTextureCatalog(token, diagnostics.Add).ToArray();
                return (entries, diagnostics, review: InspectTextureFolder(assets, entries, source, destination, textureManualMappings, token, onlyFiles));
            }, result =>
            {
                textureCatalog = result.entries;
                textureDiagnostics = result.diagnostics.ToArray();
                ShowTextureReview(result.review);
            }, LocalizeAuthored("textures.import.reading", "Reading / Validating texture folder…"));
        }
        else
        {
            TextureCatalogEntry[] entries = textureCatalog;
            Dictionary<string, TextureTarget> mappings = new(textureManualMappings, StringComparer.Ordinal);
            StartTextureJob(token =>
            {
                using var authoring = createSession();
                GameAssets assets = authoring.Assets;
                return InspectTextureFolder(assets, entries, source, destination, mappings, token, onlyFiles);
            }, ShowTextureReview, LocalizeAuthored("textures.import.reading", "Reading / Validating texture folder…"));
        }
    }

    private void ShowTextureReview(TextureImportReview review)
    {
        textureReview = review;
        textureImportDirectory = review.Source;
        textureDestination = review.Destination;
        SaveTextureToolPreferences();
        TextureBrowserController browser = textureBrowser!;
        browser.Matching = false;
        browser.Match = null;
        browser.Unmatched = BeginTextureManualMatch;
        browser.Selection.Clear();
        browser.SetRows(review.Rows.Select(row => new TextureBrowserRow(row.RelativePath, Path.GetFileName(row.RelativePath), row.Target?.Owner ?? LocalizeAuthored("textures.unmatched", "Needs matching"), row.Status + " · " + row.Target?.CanonicalPath(), row.Target is not null && row.Contract is not null && row.Pixels is not null)));
        foreach (TextureImportRow row in review.Rows.Where(row => row.Changed && row.Target is not null && row.Contract is not null && row.Pixels is not null))
        {
            browser.Selection.Add(row.RelativePath);
        }
        browser.Message = FormatAuthored("textures.review", "{0} files · {1} changed selected · {2} need matching or correction. Choose an unmatched row to assign its catalog target. Import folder again installs the checked files into {3}. Missing files leave the mod unchanged.", review.Rows.Length, browser.Selection.Count, review.Rows.Count(row => row.Target is null || row.Contract is null || row.Pixels is null), Path.GetFileName(review.Destination));
    }

    private void BeginTextureManualMatch(string input)
    {
        if (textureReview is not { } review)
        {
            return;
        }
        TextureBrowserController browser = textureBrowser!;
        browser.Matching = true;
        browser.Unmatched = null;
        ShowTextureCatalog();
        browser.Message = FormatAuthored("textures.match.choose", "Assign {0}: search the same catalog and confirm an exact target. The assignment is remembered after successful import.", input);
        browser.Match = id =>
        {
            TextureCatalogEntry target = textureCatalog.Single(entry => TextureId(entry.Target) == id);
            textureManualMappings[input] = target.Target;
            ReadTextureImport(review.Source, review.Destination);
        };
    }

    private static TextureImportReview InspectTextureFolder(GameAssets assets, TextureCatalogEntry[] catalog, string source, string destination, IReadOnlyDictionary<string, TextureTarget> manual, CancellationToken token, IReadOnlySet<string>? onlyFiles = null)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        RejectTextureReparsePath(source);
        RejectTextureReparsePath(destination);
        Dictionary<string, TextureWorkingEntry> working = ReadTextureWorkingState(source);
        Dictionary<TextureTarget, TextureCatalogEntry> targets = catalog.Where(entry => entry.LoadError is null).GroupBy(entry => entry.Target).ToDictionary(group => group.Key, group => group.First());
        List<TextureImportRow> rows = [];
        Dictionary<TextureTarget, TextureBuildContext> sourceUnit = [];
        TextureBuildContext ReadOriginal(TextureCatalogEntry entry)
        {
            if (!sourceUnit.TryGetValue(entry.Target, out TextureBuildContext? original))
            {
                sourceUnit = assets.LoadCatalogTextureUnit(entry).ToDictionary(context => TextureTarget.FromContext(context));
                original = sourceUnit[entry.Target];
            }
            return original;
        }
        long totalBytes = 0;
        foreach (string file in onlyFiles is null ? EnumerateTextureInputs(source, token) : onlyFiles)
        {
            token.ThrowIfCancellationRequested();
            RejectTextureReparsePath(file);
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            FileInfo info = new(file);
            if (info.Length > 128L * 1024 * 1024 || (totalBytes += info.Length) > 1024L * 1024 * 1024 || rows.Count >= 25000)
            {
                throw new InvalidDataException("The input exceeds the authoring limit (128 MiB per image, 1 GiB per folder, 25,000 images). Import a smaller selection.");
            }
            byte[] bytes = ReadStableTextureBytes(file, token);
            string byteHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (Path.GetExtension(file).Equals(".ora", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string companion = file + ".texture.json";
                    RejectTextureReparsePath(companion);
                    byteHash = TextureWorkspaceInputHash(bytes, ReadStableTextureBytes(companion, token));
                    IReadOnlyList<TextureWorkspaceTile> tiles = assets.ImportFieldTextureWorkspace(file, token);
                    IReadOnlyDictionary<int, string> exportedBaselines = GameAssets.ReadFieldTextureWorkspaceBaselines(file);
                    for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                    {
                        token.ThrowIfCancellationRequested();
                        TextureWorkspaceTile tile = tiles[tileIndex];
                        string rowId = relative + "::" + tileIndex;
                        PixelImage editable = TexturePixelExchange.ToEditable(tile.Image);
                        string pixelHash = TextureExchange.PixelFingerprint(editable);
                        if (!targets.TryGetValue(tile.Target, out TextureCatalogEntry? tileEntry))
                        {
                            throw new InvalidDataException("The field tile target is absent from the installed catalog.");
                        }
                        TextureBuildContext native = ReadOriginal(tileEntry);
                        tile.Contract.Validate(native, tile.Image);
                        string originalHash = TextureExchange.PixelFingerprint(TexturePixelExchange.ToEditable(native.Original));
                        int nativeTile = int.Parse(tile.Target.Subresource.AsSpan("tile/".Length), System.Globalization.CultureInfo.InvariantCulture);
                        if (exportedBaselines.TryGetValue(nativeTile, out string? exportedHash))
                        {
                            originalHash = exportedHash;
                        }
                        string output = Path.Combine(destination, tile.Target.CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
                        RejectTextureReparsePath(output);
                        string? current = File.Exists(output) ? Convert.ToHexString(SHA256.HashData(ReadStableTextureBytes(output, token))) : null;
                        TextureWorkingEntry? priorTile = working.GetValueOrDefault(rowId);
                        bool edited = priorTile?.Target == tile.Target ? pixelHash != priorTile.ImportedPixels : !string.Equals(pixelHash, originalHash, StringComparison.OrdinalIgnoreCase);
                        bool conflict = priorTile?.Target == tile.Target && current != priorTile.DestinationBytes && edited;
                        string detail = conflict ? "Conflict: working tile and destination both changed; explicitly check to replace" : edited ? "Changed layered workspace tile" : "Unchanged workspace tile";
                        rows.Add(new(rowId, tile.Target, tile.Contract, byteHash, pixelHash, current, edited && !conflict, detail, Workspace: relative, WorkspaceTile: tileIndex));
                    }
                }
                catch (Exception exception) when (IsModelBrowserError(exception))
                {
                    rows.RemoveAll(row => row.Workspace == relative);
                    rows.Add(new(relative, null, null, byteHash, null, null, false, exception.Message));
                }
                continue;
            }
            TextureTarget? target = null;
            TextureTargetContract? contract = null;
            string? pixels = null;
            string? destinationBytes = null;
            TextureExchangeMetadata? metadata = null;
            bool explicitMapping = manual.ContainsKey(relative) || working.GetValueOrDefault(relative)?.ExplicitMapping == true;
            bool changed = false;
            string status;
            try
            {
                RejectTextureReparsePath(file + ".texture.json");
                try
                {
                    metadata = TextureExchange.ReadMetadata(file);
                }
                catch (InvalidDataException) when (explicitMapping)
                {
                    metadata = null;
                }
                TextureTarget.TryParsePath(relative, out TextureTarget? pathTarget);
                if (pathTarget is null)
                {
                    string normalized = file.Replace('\\', '/');
                    int canonicalStart = normalized.LastIndexOf("/textures/", StringComparison.Ordinal);
                    if (canonicalStart >= 0)
                    {
                        TextureTarget.TryParsePath(normalized[(canonicalStart + 1)..], out pathTarget);
                    }
                }
                TextureTarget? explicitTarget = manual.GetValueOrDefault(relative) ?? (working.GetValueOrDefault(relative)?.ExplicitMapping == true ? working[relative].Target : null);
                if (explicitTarget is not null)
                {
                    target = explicitTarget;
                    if (metadata?.Target != target)
                    {
                        metadata = null;
                    }
                }
                else
                {
                    if (metadata is not null && pathTarget is not null && metadata.Target != pathTarget)
                    {
                        throw new InvalidDataException("Path and metadata identify different targets. Explicitly choose the intended catalog target.");
                    }
                    target = metadata?.Target ?? pathTarget ?? working.GetValueOrDefault(relative)?.Target;
                }
                if (target is null || !targets.TryGetValue(target, out TextureCatalogEntry? entry))
                {
                    target = null;
                    throw new InvalidDataException("Unknown target: choose this row and assign an installed catalog texture.");
                }
                TextureBuildContext original = ReadOriginal(entry);
                contract = metadata?.Contract ?? TextureTargetContract.FromContext(original);
                PixelImage image = TextureImageDecoder.Decode(bytes, file).Image;
                contract.Validate(original, image);
                pixels = TextureExchange.PixelFingerprint(image);
                PixelImage baseline = TexturePixelExchange.UsesNativeFlags(original) ? TexturePixelExchange.ToEditable(original.Original) : original.Original;
                string baselineHash = TextureExchange.PixelFingerprint(baseline);
                if (metadata?.Target == target && metadata.Contract == TextureTargetContract.FromContext(original) && string.Equals(metadata.OriginalPixelSha256, baselineHash, StringComparison.OrdinalIgnoreCase) && metadata.ExportedPixelSha256 is { } exportedBaseline)
                {
                    baselineHash = exportedBaseline;
                }
                string destinationFile = Path.Combine(destination, target.CanonicalPath(Path.GetExtension(file).ToLowerInvariant()).Replace('/', Path.DirectorySeparatorChar));
                bool matchesDestination = false;
                if (File.Exists(destinationFile))
                {
                    RejectTextureReparsePath(destinationFile);
                    byte[] currentBytes = ReadStableTextureBytes(destinationFile, token);
                    destinationBytes = Convert.ToHexString(SHA256.HashData(currentBytes));
                    matchesDestination = string.Equals(pixels, TextureExchange.PixelFingerprint(TextureImageDecoder.Decode(currentBytes, destinationFile).Image), StringComparison.OrdinalIgnoreCase);
                }
                TextureWorkingEntry? prior = working.GetValueOrDefault(relative);
                if (matchesDestination)
                {
                    status = "Same decoded appearance as destination; omitted";
                }
                else if (prior?.Target == target && prior.ImportedPixels == pixels)
                {
                    status = destinationBytes != prior.DestinationBytes ? "Unchanged working image; destination edit retained" : "Unchanged since last import";
                }
                else if (prior?.Target == target && destinationBytes != prior.DestinationBytes)
                {
                    status = "Conflict: working image and destination both changed. Unchecked; check to explicitly replace the destination.";
                }
                else
                {
                    changed = prior?.Target == target || !string.Equals(pixels, baselineHash, StringComparison.OrdinalIgnoreCase);
                    status = changed ? "Changed: ready to install · " + entry.Name : "Unchanged original appearance; omitted";
                }
            }
            catch (Exception exception) when (IsModelBrowserError(exception))
            {
                status = exception.Message;
                pixels = null;
                contract = null;
            }
            string? metadataFingerprint = metadata is null ? null : Convert.ToHexString(SHA256.HashData(metadata.Serialize()));
            TextureExchangeMetadata? retainedMetadata = metadata is null ? null : metadata with { NativeFlagsBase64 = null };
            rows.Add(new(relative, target, contract, byteHash, pixels, destinationBytes, changed, status, retainedMetadata, ExplicitMapping: explicitMapping, MetadataFingerprint: metadataFingerprint));
        }
        foreach (IGrouping<TextureTarget?, TextureImportRow> duplicate in rows.Where(row => row.Target is not null).GroupBy(row => row.Target).Where(group => group.Count() > 1))
        {
            foreach (TextureImportRow row in duplicate.ToArray())
            {
                rows[rows.IndexOf(row)] = row with { Changed = false, Status = "Duplicate target: select exactly one input for " + row.Target!.CanonicalPath() };
            }
        }
        Dictionary<string, string?> expectedOutputs = new(StringComparer.Ordinal);
        foreach (TextureImportRow row in rows.Where(row => row.Target is not null && row.Contract is not null))
        {
            string output = row.Target!.CanonicalPath(row.Workspace is null ? Path.GetExtension(row.RelativePath).ToLowerInvariant() : ".png");
            expectedOutputs[output] = row.DestinationBytes;
            string companion = Path.Combine(destination, (output + TextureModMapping.Extension).Replace('/', Path.DirectorySeparatorChar));
            RejectTextureReparsePath(companion);
            expectedOutputs[output + TextureModMapping.Extension] = File.Exists(companion) ? Convert.ToHexString(SHA256.HashData(ReadStableTextureBytes(companion, token))) : null;
        }
        return new(source, destination, rows.ToArray(), working, expectedOutputs);
    }

    private async void CompleteTextureImport()
    {
        try
        {
            TextureImportReview review = textureReview!;
            TextureImportRow[] selected = review.Rows.Where(row => textureBrowser!.Selection.Contains(row.RelativePath)).ToArray();
            if (selected.Length == 0 || selected.Any(row => row.Target is null || row.Contract is null || row.Pixels is null))
            {
                throw new InvalidDataException("Select at least one validated texture. Unmatched or invalid inputs cannot be installed.");
            }
            if (selected.Select(row => row.Target).Distinct().Count() != selected.Length)
            {
                throw new InvalidDataException("Several checked inputs target the same texture. Leave exactly one checked for each target.");
            }
            ModPackage? existing = resourceHost!.Mods.Catalog.Packages.FirstOrDefault(package => Path.GetFullPath(package.RootDirectory).Equals(review.Destination, StringComparison.OrdinalIgnoreCase));
            string descriptorPath = Path.Combine(review.Destination, NativeModDescriptor.FileName);
            NativeModDescriptor? descriptor = File.Exists(descriptorPath) ? NativeModDescriptor.Read(descriptorPath) : null;
            string slug = descriptor?.Id ?? Regex.Replace(Path.GetFileName(review.Destination).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length == 0 || slug.Length > 80)
            {
                throw new InvalidDataException("Choose a mod folder whose name contains a short word or number.");
            }
            ModPackage package = existing ?? new(descriptor?.RuntimePackageId ?? "Memoria/" + slug, descriptor?.Name ?? Path.GetFileName(review.Destination), ModFormat.Memoria, null, true, ModEffectiveStatus.Active, review.Destination) { Descriptor = descriptor };
            string activation = package.RequestedEnabled ? "Import and apply through the current mod priority. A higher-priority mod may still own individual targets." : "This mod is disabled. Import saves files and preserves its disabled state; enable it in Mods to use them.";
            string warning = string.Join("\n", selected.Take(80).Select(row => row.RelativePath + " → " + row.Target!.CanonicalPath())) + (selected.Length > 80 ? "\n…" : "") + "\n\n" + activation + "\nUnselected and unrelated mod files, enablement and priority are preserved.";
            if (!await ModelBrowserDialogs.ConfirmAsync(Window.Handle, warning, LocalizeAuthored("textures.import.review.title", "Review texture import"), LocalizeAuthored("textures.import.review.question", "Install these selected textures?"), LocalizeUiText))
            {
                return;
            }
            var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
            StartTextureJob(token => PrepareTextureImportPlan(review, selected, descriptor is null ? slug : null, package.Name, createSession, token), plan => ApplyTextureImportPlan(review, selected, package, plan), LocalizeAuthored("textures.installing", "Validating reviewed inputs / preparing installation…"));
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            textureBrowser!.Message = exception.Message;
        }
        finally
        {
            textureDialogDrain = true;
            keyboard.SuppressHeldNameInput();
        }
    }

    private TextureModInstallPlan PrepareTextureImportPlan(TextureImportReview review, TextureImportRow[] selected, string? newId, string name, Func<TextureAuthoringSession> createSession, CancellationToken token)
    {
        List<TextureModReplacement> replacements = [];
        long retained = 0;
        foreach (TextureImportRow row in selected)
        {
            token.ThrowIfCancellationRequested();
            if (row.Workspace is not null)
            {
                continue;
            }
            string file = Path.Combine(review.Source, row.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            RejectTextureReparsePath(file);
            byte[] bytes = ReadStableTextureBytes(file, token);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != row.InputBytes)
            {
                throw new InvalidDataException($"{row.RelativePath} changed after review. Refresh / reimport before installing.");
            }
            TextureExchangeMetadata? metadata;
            try
            {
                metadata = TextureExchange.ReadMetadata(file);
            }
            catch (InvalidDataException) when (row.ExplicitMapping && row.Metadata is null)
            {
                metadata = null;
            }
            if (row.ExplicitMapping && metadata?.Target != row.Target)
            {
                metadata = null;
            }
            string? metadataFingerprint = metadata is null ? null : Convert.ToHexString(SHA256.HashData(metadata.Serialize()));
            if (metadataFingerprint != row.MetadataFingerprint)
            {
                throw new InvalidDataException($"{row.RelativePath}: authoring metadata changed after review. Refresh before installing.");
            }
            string extension = Path.GetExtension(file).ToLowerInvariant();
            string destination = Path.Combine(review.Destination, row.Target!.CanonicalPath(extension).Replace('/', Path.DirectorySeparatorChar));
            RejectTextureReparsePath(destination);
            string? destinationHash = File.Exists(destination) ? Convert.ToHexString(SHA256.HashData(ReadStableTextureBytes(destination, token))) : null;
            if (destinationHash != row.DestinationBytes)
            {
                throw new InvalidDataException($"{row.RelativePath}: destination changed after review. Refresh before installing.");
            }
            if ((retained += bytes.Length + (long)(metadata?.NativeFlagsBase64?.Length ?? 0) * sizeof(char)) > 512L * 1024 * 1024)
            {
                throw new InvalidDataException("Selected edits exceed the 512 MiB transaction budget. Import a smaller selection.");
            }
            if (row.ExplicitMapping && extension == ".png")
            {
                bytes = LocalizationArtworkExporter.EncodePng(TextureImageDecoder.Decode(bytes, file).Image);
            }
            replacements.Add(new(row.Target, bytes, extension, row.Contract)
            {
                NativeCoverage = metadata?.NativeFlagsBase64 is { } flags ? new(metadata.NativeWidth ?? row.Contract!.LogicalWidth, metadata.NativeHeight ?? row.Contract!.LogicalHeight, flags) : null
            });
        }
        foreach (IGrouping<string?, TextureImportRow> workspace in selected.Where(row => row.Workspace is not null).GroupBy(row => row.Workspace))
        {
            token.ThrowIfCancellationRequested();
            string path = Path.Combine(review.Source, workspace.Key!.Replace('/', Path.DirectorySeparatorChar));
            RejectTextureReparsePath(path);
            RejectTextureReparsePath(path + ".texture.json");
            string workspaceHash = TextureWorkspaceInputHash(ReadStableTextureBytes(path, token), ReadStableTextureBytes(path + ".texture.json", token));
            if (workspace.Any(row => row.InputBytes != workspaceHash))
            {
                throw new InvalidDataException($"{workspace.Key} changed after review. Refresh before installing.");
            }
            using var authoring = createSession();
            GameAssets assets = authoring.Assets;
            IReadOnlyList<TextureWorkspaceTile> tiles = assets.ImportFieldTextureWorkspace(path, token);
            foreach (TextureImportRow row in workspace)
            {
                TextureWorkspaceTile tile = tiles[row.WorkspaceTile];
                string destination = Path.Combine(review.Destination, tile.Target.CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
                RejectTextureReparsePath(destination);
                string? destinationHash = File.Exists(destination) ? Convert.ToHexString(SHA256.HashData(ReadStableTextureBytes(destination, token))) : null;
                if (destinationHash != row.DestinationBytes || tile.Target != row.Target)
                {
                    throw new InvalidDataException($"{workspace.Key}: destination or mapping changed after review. Refresh before installing.");
                }
                byte[] png = LocalizationArtworkExporter.EncodePng(TexturePixelExchange.ToEditable(tile.Image));
                if ((retained += png.Length + ((long)tile.Image.Pixels.Length + 2) / 3 * 8) > 512L * 1024 * 1024)
                {
                    throw new InvalidDataException("Selected edits exceed the 512 MiB transaction budget.");
                }
                replacements.Add(new(tile.Target, png, ".png", tile.Contract)
                {
                    NativeCoverage = new(tile.Image.Width, tile.Image.Height, TexturePixelExchange.CaptureNativeFlags(tile.Image))
                });
            }
        }
        TextureModInstallPlan plan = TextureModInstaller.Plan(replacements);
        if (newId is not null)
        {
            plan = TextureModInstaller.WithNewDescriptor(plan, newId, name);
        }
        return plan with
        {
            ExpectedDestinationHashes = plan.Files.ToDictionary(file => file.RelativePath, file => review.ExpectedOutputs.GetValueOrDefault(file.RelativePath), StringComparer.Ordinal)
        };
    }

    private void ApplyTextureImportPlan(TextureImportReview review, TextureImportRow[] selected, ModPackage package, TextureModInstallPlan plan)
    {
        try
        {
            textureBrowser!.Message = LocalizeAuthored("textures.applying", "Installing / Applying validated texture generation…");
            string root = Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria");
            using TextureModInstallTransaction transaction = TextureModInstaller.PrepareInstall(root, review.Destination, plan);
            if (!resourceHost.Mods.ApplyTextureImport(transaction, package))
            {
                throw new InvalidDataException("Texture publication failed. Previous destination and live textures were retained; inspect Mods diagnostics.");
            }
            Dictionary<string, TextureWorkingEntry> state = new(review.Working, StringComparer.Ordinal);
            foreach (TextureImportRow row in selected)
            {
                TextureModOutput output = plan.Files.Single(file => file.RelativePath == row.Target!.CanonicalPath(row.Workspace is null ? Path.GetExtension(row.RelativePath).ToLowerInvariant() : ".png"));
                state[row.RelativePath] = new(row.Target!, row.Pixels, Convert.ToHexString(SHA256.HashData(output.Content)), row.ExplicitMapping);
            }
            WriteTextureWorkingState(review.Source, state);
            IReadOnlyList<TextureSourceSnapshot> resident = resourceHost.Resources.CaptureTextureSources(selected.Select(row => row.Target!.Key));
            int owned = resident.Count(snapshot => snapshot.Resolved.Provenance.StartsWith(package.Id + "/", StringComparison.Ordinal));
            textureBrowser.Message = package.RequestedEnabled
                ? FormatAuthored("textures.import.applied", "Installed {0} texture edits; {1} resident targets use this mod. Other targets are nonresident or overridden by priority. Refresh / reimport repeats this working folder.", selected.Length, owned)
                : FormatAuthored("textures.import.disabled", "Saved {0} texture edits. The mod remains disabled; enable it in Mods to use them.", selected.Length);
            textureReview = null;
            textureBrowser.Selection.Clear();
            textureBrowser.Unmatched = null;
            textureBrowser.SetRows(selected.Select(row =>
            {
                TextureSourceSnapshot? active = resident.FirstOrDefault(snapshot => snapshot.Target == row.Target);
                string status = !package.RequestedEnabled
                    ? "Installed in disabled mod; enable it in Mods"
                    : active is null
                        ? "Installed; nonresident source loads on demand"
                        : active.Resolved.Provenance.StartsWith(package.Id + "/", StringComparison.Ordinal)
                            ? "Active source from " + active.Resolved.Provenance
                            : "Imported but overridden by " + active.Resolved.Provenance;
                string name = textureCatalog.FirstOrDefault(entry => entry.Target == row.Target)?.Name ?? row.Target!.Subresource;
                return new TextureBrowserRow(TextureId(row.Target!), name, row.Target!.Owner, status + " � " + row.Target.CanonicalPath());
            }));
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            textureBrowser!.Message = exception.Message;
        }
    }

    private static IEnumerable<string> EnumerateTextureInputs(string root, CancellationToken token)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count != 0)
        {
            string directory = pending.Pop();
            RejectTextureReparsePath(directory);
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                token.ThrowIfCancellationRequested();
                string extension = Path.GetExtension(file);
                if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ora", StringComparison.OrdinalIgnoreCase))
                {
                    RejectTextureReparsePath(file);
                    yield return file;
                }
            }
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                RejectTextureReparsePath(child);
                pending.Push(child);
                if (pending.Count > 25000)
                {
                    throw new InvalidDataException("The texture input folder contains too many directories.");
                }
            }
        }
    }

    private static void RejectTextureReparsePath(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Texture authoring paths cannot pass through symbolic links or reparse points.");
            }
        }
    }

    private static string TextureWorkspaceInputHash(byte[] image, byte[] companion)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(image);
        hash.AppendData(companion);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static byte[] ReadStableTextureBytes(string path, CancellationToken token)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                FileInfo before = new(path);
                long length = before.Length;
                DateTime modified = before.LastWriteTimeUtc;
                if (length > 128L * 1024 * 1024)
                {
                    throw new InvalidDataException("The texture exceeds the 128 MiB input limit.");
                }
                byte[] bytes = File.ReadAllBytes(path);
                FileInfo after = new(path);
                if (length == bytes.Length && after.Length == length && after.LastWriteTimeUtc == modified)
                {
                    return bytes;
                }
            }
            catch (IOException) when (attempt < 2)
            {
            }
            if (token.WaitHandle.WaitOne(75))
            {
                token.ThrowIfCancellationRequested();
            }
        }
        throw new IOException($"{Path.GetFileName(path)} is still being saved. Refresh after the editor finishes.");
    }

    private static Dictionary<string, TextureWorkingEntry> ReadTextureWorkingState(string source)
    {
        string path = Path.Combine(source, TextureWorkingStateFile);
        if (!File.Exists(path))
        {
            return new(StringComparer.Ordinal);
        }
        RejectTextureReparsePath(path);
        if (new FileInfo(path).Length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("The texture authoring state exceeds its limit.");
        }
        return JsonSerializer.Deserialize<Dictionary<string, TextureWorkingEntry>>(File.ReadAllBytes(path)) ?? new(StringComparer.Ordinal);
    }

    private static void WriteTextureWorkingState(string source, Dictionary<string, TextureWorkingEntry> state)
    {
        string path = Path.Combine(source, TextureWorkingStateFile);
        RejectTextureReparsePath(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
