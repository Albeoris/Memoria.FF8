using System.Security.Cryptography;
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
    private sealed record TextureImportRow(string RelativePath, TextureTarget? Target, TextureTargetContract? Contract, string InputBytes, string? Pixels, string? DestinationBytes, bool Changed, string Status, TextureExchangeMetadata? Metadata = null, string? Workspace = null, int WorkspaceTile = -1, bool ExplicitMapping = false, string? MetadataFingerprint = null, int? CoverageWidth = null, int? CoverageHeight = null);
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
            }, LocalizeAuthored("textures.import.reading", "Reading / Validating texture folder..."));
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
            }, ShowTextureReview, LocalizeAuthored("textures.import.reading", "Reading / Validating texture folder..."));
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
        browser.ImportedPreviewAvailable = false;
        uiCanvas?.UnregisterHostImage(TextureBrowserController.ImportedPreviewImageId);
        browser.Match = null;
        browser.Unmatched = BeginTextureManualMatch;
        browser.Selection.Clear();
        browser.SetRows(review.Rows.Select(row => new TextureBrowserRow(row.RelativePath, Path.GetFileName(row.RelativePath), row.Target?.Owner ?? LocalizeAuthored("textures.unmatched", "Needs matching"), row.Status + " / " + row.Target?.CanonicalPath(), row.Target is not null && row.Contract is not null && row.Pixels is not null)));
        foreach (TextureImportRow row in review.Rows.Where(row => row.Changed && row.Target is not null && row.Contract is not null && row.Pixels is not null))
        {
            browser.Selection.Add(row.RelativePath);
        }
        browser.Message = FormatAuthored("textures.review", "{0} files / {1} changed selected / {2} need matching or correction. Choose an unmatched row to assign its catalog target. Import folder again installs the checked files into {3}. Missing files leave the mod unchanged.", review.Rows.Length, browser.Selection.Count, review.Rows.Count(row => row.Target is null || row.Contract is null || row.Pixels is null), Path.GetFileName(review.Destination));
    }

    private void BeginTextureManualMatch(string input)
    {
        if (textureReview is not { } review)
        {
            return;
        }
        TextureBrowserController browser = textureBrowser!;
        browser.Matching = true;
        browser.PreviewAvailable = false;
        texturePreviewIdentity = null;
        texturePreviewCancellation?.Cancel();
        browser.ImportedPreviewAvailable = false;
        browser.ClearSearch();
        browser.Unmatched = null;
        ShowTextureCatalog();
        browser.Message = FormatAuthored("textures.match.choose", "Assign {0}: search the same catalog and confirm an exact target. The assignment is remembered after successful import.", input);
        browser.Match = id =>
        {
            TextureCatalogEntry target = textureCatalog.Single(entry => TextureId(entry.Target) == id);
            if (target.Layout == "field-workspace")
            {
                if (!expandedTextureFields.Contains(target.Target))
                {
                    ExpandTextureSource(id);
                }
                else
                {
                    browser.Message = LocalizeAuthored("textures.match.tile.required", "Choose an expanded native tile target. A complete field workspace is imported through its ORA document.");
                }
                return;
            }
            textureManualMappings[input] = target.Target;
            ReadTextureImport(review.Source, review.Destination);
        };
        LoadImportedTexturePreview(review, review.Rows.Single(row => row.RelativePath == input));
    }

    private static TextureImportReview InspectTextureFolder(GameAssets assets, TextureCatalogEntry[] catalog, string source, string destination, IReadOnlyDictionary<string, TextureTarget> manual, CancellationToken token, IReadOnlySet<string>? onlyFiles = null)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        RejectTextureReparsePath(source);
        RejectTextureReparsePath(destination);
        Dictionary<string, TextureWorkingEntry> working = ReadTextureWorkingState(source, destination);
        Dictionary<TextureTarget, TextureCatalogEntry> targets = catalog.Where(entry => entry.LoadError is null).GroupBy(entry => entry.Target).ToDictionary(group => group.Key, group => group.First());
        Dictionary<string, TextureCatalogEntry> fieldWorkspaces = catalog.Where(entry => entry.Layout == "field-workspace").GroupBy(entry => entry.Target.Owner).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> expandedOwners = new(StringComparer.Ordinal);
        TextureCatalogEntry? ResolveEntry(TextureTarget target)
        {
            if (targets.TryGetValue(target, out TextureCatalogEntry? entry))
            {
                return entry;
            }
            if (fieldWorkspaces.TryGetValue(target.Owner, out TextureCatalogEntry? fieldWorkspace) && expandedOwners.Add(target.Owner))
            {
                foreach (TextureCatalogEntry tile in assets.ExpandFieldTextureCatalog(fieldWorkspace, token))
                {
                    targets.TryAdd(tile.Target, tile);
                }
            }
            return targets.GetValueOrDefault(target) ?? assets.FindCatalogTextureTarget(target);
        }
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
                    IReadOnlyDictionary<int, string> semanticBaselines = GameAssets.ReadFieldTextureWorkspaceSemanticBaselines(file);
                    for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                    {
                        token.ThrowIfCancellationRequested();
                        TextureWorkspaceTile tile = tiles[tileIndex];
                        string rowId = relative + "::" + tileIndex;
                        PixelImage editable = TexturePixelExchange.ToEditable(tile.Image);
                        string imageHash = TextureExchange.PixelFingerprint(editable);
                        TextureCatalogEntry tileEntry = ResolveEntry(tile.Target) ?? throw new InvalidDataException("The field tile target is absent from the installed catalog.");
                        TextureBuildContext native = ReadOriginal(tileEntry);
                        TextureValue installedSource = assets.Resources.ResolveOriginalForExport(native);
                        PixelImage installedOriginal = installedSource.Image;
                        tile.Contract.Validate(native, tile.Image);
                        TextureNativeCoverage coverage = new(tile.Image.Width, tile.Image.Height, TexturePixelExchange.CaptureNativeFlags(tile.Image));
                        string pixelHash = TextureImportSemantics.Fingerprint(editable, tile.Target, tile.Contract, native, coverage, installedSource);
                        string originalHash = TextureImportSemantics.Fingerprint(TexturePixelExchange.ToEditable(installedOriginal), tile.Target, TextureTargetContract.FromContext(native), native, installedOriginal: installedSource);
                        int nativeTile = int.Parse(tile.Target.Subresource.AsSpan("tile/".Length), System.Globalization.CultureInfo.InvariantCulture);
                        if (semanticBaselines.TryGetValue(nativeTile, out string? semanticBaseline))
                        {
                            originalHash = semanticBaseline;
                        }
                        else if (exportedBaselines.TryGetValue(nativeTile, out string? exportedHash))
                        {
                            originalHash = string.Equals(imageHash, exportedHash, StringComparison.OrdinalIgnoreCase) ? pixelHash : "legacy-workspace-baseline:" + exportedHash;
                        }
                        string output = Path.Combine(destination, tile.Target.CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
                        RejectTextureReparsePath(output);
                        byte[]? destinationContent = File.Exists(output) ? ReadStableTextureBytes(output, token) : null;
                        string? current = destinationContent is null ? null : Convert.ToHexString(SHA256.HashData(destinationContent));
                        string? destinationPixels = destinationContent is null ? null : ReadDestinationTextureSemantic(output, destinationContent, tile.Target, native, installedSource, token);
                        TextureImportDecision decision = TextureWorkspaceState.Evaluate(tile.Target, pixelHash, originalHash, current, destinationPixels, working.GetValueOrDefault(rowId));
                        rows.Add(new(rowId, tile.Target, tile.Contract, byteHash, pixelHash, current, decision.Selected, decision.Status, Workspace: relative, WorkspaceTile: tileIndex));
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
            int? coverageWidth = null;
            int? coverageHeight = null;
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
                TextureCatalogEntry? entry = target is null ? null : ResolveEntry(target);
                if (entry is null)
                {
                    target = null;
                    throw new InvalidDataException("Unknown target: choose this row and assign an installed catalog texture.");
                }
                target ??= entry.Target;
                TextureBuildContext original = ReadOriginal(entry);
                TextureValue installedSource = assets.Resources.ResolveOriginalForExport(original);
                PixelImage installedOriginal = installedSource.Image;
                contract = metadata?.Contract ?? TextureTargetContract.FromContext(original);
                PixelImage image = TextureImageDecoder.Decode(bytes, file).Image;
                contract.Validate(original, image);
                string imageHash = TextureExchange.PixelFingerprint(image);
                TextureNativeCoverage? coverage = metadata?.NativeFlagsBase64 is { } alpha ? new(metadata.NativeWidth ?? installedOriginal.Width, metadata.NativeHeight ?? installedOriginal.Height, alpha) : null;
                coverageWidth = coverage?.Width;
                coverageHeight = coverage?.Height;
                pixels = TextureImportSemantics.Fingerprint(image, target, contract, original, coverage, installedSource);
                PixelImage baseline = TexturePixelExchange.UsesNativeFlags(original, installedSource) ? TexturePixelExchange.ToEditable(installedOriginal) : installedOriginal;
                string originalImageHash = TextureExchange.PixelFingerprint(baseline);
                string baselineHash = TextureImportSemantics.Fingerprint(baseline, target, TextureTargetContract.FromContext(original), original, installedOriginal: installedSource);
                if (metadata?.Target == target && metadata.Contract == TextureTargetContract.FromContext(original))
                {
                    if (metadata.OriginalSemanticSha256 is { } originalSemantic && string.Equals(originalSemantic, baselineHash, StringComparison.OrdinalIgnoreCase) && metadata.ExportedSemanticSha256 is { } exportedSemantic)
                    {
                        baselineHash = exportedSemantic;
                    }
                    else if (metadata.ExportedSemanticSha256 is null && string.Equals(metadata.OriginalPixelSha256, originalImageHash, StringComparison.OrdinalIgnoreCase) && metadata.ExportedPixelSha256 is { } legacyBaseline)
                    {
                        baselineHash = string.Equals(imageHash, legacyBaseline, StringComparison.OrdinalIgnoreCase) ? pixels : "legacy-pixel-baseline:" + legacyBaseline;
                    }
                }
                string destinationFile = Path.Combine(destination, target.CanonicalPath(Path.GetExtension(file).ToLowerInvariant()).Replace('/', Path.DirectorySeparatorChar));
                string? destinationPixels = null;
                if (File.Exists(destinationFile))
                {
                    RejectTextureReparsePath(destinationFile);
                    byte[] currentBytes = ReadStableTextureBytes(destinationFile, token);
                    destinationBytes = Convert.ToHexString(SHA256.HashData(currentBytes));
                    destinationPixels = ReadDestinationTextureSemantic(destinationFile, currentBytes, target, original, installedSource, token);
                }
                TextureImportDecision decision = TextureWorkspaceState.Evaluate(target, pixels, baselineHash, destinationBytes, destinationPixels, working.GetValueOrDefault(relative));
                changed = decision.Selected;
                status = decision.Status + " / " + entry.Name;
            }
            catch (Exception exception) when (IsModelBrowserError(exception))
            {
                status = exception.Message;
                pixels = null;
                contract = null;
            }
            string? metadataFingerprint = metadata is null ? null : Convert.ToHexString(SHA256.HashData(metadata.Serialize()));
            TextureExchangeMetadata? retainedMetadata = metadata is null ? null : metadata with { NativeFlagsBase64 = null };
            rows.Add(new(relative, target, contract, byteHash, pixels, destinationBytes, changed, status, retainedMetadata, ExplicitMapping: explicitMapping, MetadataFingerprint: metadataFingerprint, CoverageWidth: coverageWidth, CoverageHeight: coverageHeight));
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
            string warning = string.Join("\n", selected.Take(80).Select(row => row.RelativePath + " -> " + row.Target!.CanonicalPath())) + (selected.Length > 80 ? "\n..." : "") + "\n\n" + activation + "\nUnselected and unrelated mod files, enablement and priority are preserved.";
            if (!await ModelBrowserDialogs.ConfirmAsync(Window.Handle, warning, LocalizeAuthored("textures.import.review.title", "Review texture import"), LocalizeAuthored("textures.import.review.question", "Install these selected textures?"), LocalizeUiText))
            {
                return;
            }
            var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
            string allowedRoot = TextureModDestination.Resolve(resourceHost.Mods.ModsDirectories, review.Destination);
            StartTextureJob(token => PrepareTextureImport(review, selected, descriptor is null ? slug : null, package.Name, allowedRoot, createSession, token), plan => ApplyTextureImportPlan(review, selected, package, plan), LocalizeAuthored("textures.installing", "Validating reviewed inputs / preparing installation..."));
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

    private sealed class PreparedTextureImport(TextureInputLeases inputs, TextureModInstallTransaction transaction, Dictionary<string, TextureWorkingEntry> working) : IDisposable
    {
        public TextureInputLeases Inputs { get; } = inputs;
        public TextureModInstallTransaction Transaction { get; } = transaction;
        public Dictionary<string, TextureWorkingEntry> Working { get; } = working;
        public void Dispose()
        {
            Inputs.Dispose();
            Transaction.Dispose();
        }
    }

    private PreparedTextureImport PrepareTextureImport(TextureImportReview review, TextureImportRow[] selected, string? newId, string name, string allowedRoot, Func<TextureAuthoringSession> createSession, CancellationToken token)
    {
        string[] paths = selected.Select(row => row.Workspace ?? row.RelativePath).Distinct(StringComparer.Ordinal).SelectMany(relative =>
        {
            string image = Path.Combine(review.Source, relative.Replace('/', Path.DirectorySeparatorChar));
            RejectTextureReparsePath(image);
            RejectTextureReparsePath(image + ".texture.json");
            return new[] { image, image + ".texture.json" };
        }).ToArray();
        TextureInputLeases inputs = new(paths, token);
        TextureModInstallTransaction? transaction = null;
        try
        {
            TextureModInstallPlan plan = PrepareTextureImportPlan(review, selected, newId, name, createSession, token);
            Dictionary<string, TextureWorkingEntry> state = new(review.Working, StringComparer.Ordinal);
            foreach (TextureImportRow row in selected)
            {
                token.ThrowIfCancellationRequested();
                TextureModOutput output = plan.Files.Single(file => file.RelativePath == row.Target!.CanonicalPath(row.Workspace is null ? Path.GetExtension(row.RelativePath).ToLowerInvariant() : ".png"));
                state[row.RelativePath] = new(row.Target!, row.Pixels, Convert.ToHexString(SHA256.HashData(output.Content)), row.ExplicitMapping, row.Pixels);
            }
            transaction = TextureModInstaller.PrepareInstall(allowedRoot, review.Destination, plan, token);
            inputs.ValidateContents(token);
            token.ThrowIfCancellationRequested();
            return new(inputs, transaction, state);
        }
        catch
        {
            inputs.Dispose();
            transaction?.Dispose();
            throw;
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
                NativeCoverage = metadata?.NativeFlagsBase64 is { } flags ? new(row.CoverageWidth ?? metadata.NativeWidth ?? row.Contract!.LogicalWidth, row.CoverageHeight ?? metadata.NativeHeight ?? row.Contract!.LogicalHeight, flags) : null
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

    private sealed record PendingTextureImport(TextureImportReview Review, TextureImportRow[] Selected, ModPackage Package, PreparedTextureImport Prepared);
    private PendingTextureImport? pendingTextureImport;
    private readonly List<Task> textureCleanupTasks = [];
    private void QueueTextureCleanup(Action action)
    {
        textureCleanupTasks.RemoveAll(task => task.IsCompleted);
        textureCleanupTasks.Add(Task.Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception) when (IsModelBrowserError(exception))
            {
                textureCompletions.Enqueue(() =>
                {
                    if (textureBrowser is not null)
                    {
                        textureBrowser.Message += " Staging cleanup: " + exception.Message;
                    }
                });
            }
        }));
    }

    private void ApplyTextureImportPlan(TextureImportReview review, TextureImportRow[] selected, ModPackage package, PreparedTextureImport prepared)
    {
        pendingTextureImport = new(review, selected, package, prepared);
        textureBrowser!.Busy = true;
    }

    private void PumpTextureImport()
    {
        if (pendingTextureImport is not { } pending)
        {
            return;
        }
        try
        {
            if (textureCancellation?.IsCancellationRequested == true)
            {
                throw new OperationCanceledException("Cancelled before publication. Previous destination and live textures were retained.");
            }
            TextureImportApplyResult result = resourceHost!.Mods.TryApplyTextureImport(pending.Prepared.Transaction, pending.Package, pending.Prepared.Inputs.ValidateStamps);
            if (result == TextureImportApplyResult.Deferred)
            {
                return;
            }
            if (result == TextureImportApplyResult.Failed)
            {
                throw new InvalidDataException("Texture publication failed. Previous destination and live textures were retained. " + string.Join(" ", resourceHost.Mods.Diagnostics.TakeLast(3).Select(diagnostic => diagnostic.Message)));
            }
            pendingTextureImport = null;
            texturePendingResults.TryRemove(pending.Prepared, out _);
            textureBrowser!.Message = LocalizeAuthored("textures.install.finishing", "Textures installed; saving working-folder state...");
            QueueTextureCleanup(() =>
            {
                string? warning = null;
                try
                {
                    WriteTextureWorkingState(pending.Review.Source, pending.Review.Destination, pending.Prepared.Working);
                }
                catch (Exception exception) when (IsModelBrowserError(exception))
                {
                    warning = "Installed successfully, but the working folder could not remember this import: " + exception.Message;
                }
                finally
                {
                    try
                    {
                        pending.Prepared.Dispose();
                    }
                    catch (Exception exception) when (IsModelBrowserError(exception))
                    {
                        warning = (warning is null ? "" : warning + " ") + "Installed successfully, but staging cleanup failed: " + exception.Message;
                    }
                    textureCompletions.Enqueue(() => CompleteTextureImportResult(pending, warning));
                }
            });
        }
        catch (Exception exception) when (exception is OperationCanceledException || IsModelBrowserError(exception))
        {
            resourceHost!.Mods.CancelTextureImport(pending.Prepared.Transaction);
            pendingTextureImport = null;
            texturePendingResults.TryRemove(pending.Prepared, out _);
            QueueTextureCleanup(pending.Prepared.Dispose);
            textureBrowser!.Busy = false;
            textureBrowser.Message = exception.Message;
        }
    }

    private void CompleteTextureImportResult(PendingTextureImport pending, string? bookkeepingWarning)
    {
        if (textureBrowser is null)
        {
            return;
        }
        textureBrowser.Busy = false;
        TextureImportRow[] selected = pending.Selected;
        ModPackage package = resourceHost!.Mods.Catalog.Packages.FirstOrDefault(candidate => candidate.Id == pending.Package.Id) ?? pending.Package;
        IReadOnlyList<TextureSourceSnapshot> resident = resourceHost.Resources.CaptureTextureSources();
        var ownership = selected.ToDictionary(row => row.Target!, row => TextureImportOwnership.Inspect(row.Target!, resident, package.Id));
        int owned = ownership.Count(pair => pair.Value.OwnedConsumers != 0);
        textureBrowser.Message = package.RequestedEnabled
            ? FormatAuthored("textures.import.applied", "Installed {0} texture edits; {1} resident targets use this mod. Other targets are nonresident or overridden by priority. Refresh / reimport repeats this working folder.", selected.Length, owned)
            : FormatAuthored("textures.import.disabled", "Saved {0} texture edits. The mod remains disabled; enable it in Mods to use them.", selected.Length);
        if (bookkeepingWarning is not null)
        {
            textureBrowser.Message += " " + bookkeepingWarning;
        }
        textureReview = null;
        textureBrowser.Selection.Clear();
        textureBrowser.Unmatched = null;
        textureBrowser.SetRows(selected.Select(row =>
        {
            string status = !package.RequestedEnabled ? "Installed in disabled mod; enable it in Mods" : ownership[row.Target!].Describe();
            string name = textureCatalog.FirstOrDefault(entry => entry.Target == row.Target)?.Name ?? row.Target!.Subresource;
            return new TextureBrowserRow(TextureId(row.Target!), name, row.Target!.Owner, status + " / " + row.Target.CanonicalPath());
        }));
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

    private static string ReadDestinationTextureSemantic(string path, byte[] bytes, TextureTarget target, TextureBuildContext source, TextureValue installedSource, CancellationToken token)
    {
        string companion = path + TextureModMapping.Extension;
        RejectTextureReparsePath(companion);
        TextureModMapping? mapping = File.Exists(companion) ? TextureModMapping.Parse(ReadStableTextureBytes(companion, token)) : null;
        TextureExchangeMetadata? embedded = TextureExchange.TryReadEmbeddedMetadata(bytes);
        if (mapping is not null && mapping.Target != target || embedded is not null && embedded.Target != target)
        {
            throw new InvalidDataException("The destination image has conflicting target metadata. Correct it in the mod before importing.");
        }
        TextureTargetContract contract = mapping?.Contract ?? embedded?.Contract ?? TextureTargetContract.FromContext(source);
        PixelImage image = TextureImageDecoder.Decode(bytes, path).Image;
        contract.Validate(source, image);
        TextureNativeCoverage? coverage = mapping?.NativeCoverage;
        if (coverage is null && embedded?.NativeFlagsBase64 is { } flags)
        {
            coverage = new(embedded.NativeWidth ?? installedSource.Image.Width, embedded.NativeHeight ?? installedSource.Image.Height, flags);
        }
        return TextureImportSemantics.Fingerprint(image, target, contract, source, coverage, installedSource);
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

    private static Dictionary<string, TextureWorkingEntry> ReadTextureWorkingState(string source, string destination)
    {
        RejectTextureReparsePath(Path.Combine(source, TextureWorkspaceState.FileName));
        return TextureWorkspaceState.Read(source, destination);
    }

    private static void WriteTextureWorkingState(string source, string destination, Dictionary<string, TextureWorkingEntry> state)
    {
        RejectTextureReparsePath(Path.Combine(source, TextureWorkspaceState.FileName));
        TextureWorkspaceState.Write(source, destination, state);
    }
}
