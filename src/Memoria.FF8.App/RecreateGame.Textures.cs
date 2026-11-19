using System.Collections.Concurrent;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private TextureBrowserController? textureBrowser;
    private IReadOnlyList<TextureSourceSnapshot> textureScene = [];
    private Func<TextureAuthoringSession>? textureSceneOriginalFactory;
    private Func<TextureAuthoringSession>? textureSceneResolvedFactory;
    private bool textureSceneSelectionInitialized;
    private SceneKind? texturePresentedScene;
    private int texturePresentedField = -1;
    private GameWorld? texturePresentedWorldOwner;
    private WorldMapScene? texturePresentedWorld;
    private BattleSession? texturePresentedBattle;
    private string? textureSceneUnavailable;
    private readonly HashSet<TextureTarget> textureSceneCompanions = [];
    private bool textureSceneNormalized;
    private TextureCatalogDiagnostic[] textureSceneDiagnostics = [];
    private TextureCatalogEntry[] textureCatalog = [];
    private TextureCatalogDiagnostic[] textureDiagnostics = [];
    private readonly HashSet<TextureTarget> expandedTextureFields = [];
    private readonly ConcurrentQueue<Action> textureCompletions = new();
    private readonly ConcurrentDictionary<IDisposable, byte> texturePendingResults = new();
    private CancellationTokenSource? textureCancellation;
    private Task? textureWork;
    private bool previousTextureMouse;
    private bool previousTextureSpace;
    private bool textureDialogDrain;
    private int previousTextureWheel;
    private string? textureImportDirectory;
    private string? textureDestination;
    private string? textureExportDirectory;
    private HashSet<string>? textureImportFiles;

    private void InvalidateTexturePresentation()
    {
        texturePresentedScene = null;
        texturePresentedField = -1;
        texturePresentedWorldOwner = null;
        texturePresentedWorld = null;
        texturePresentedBattle = null;
    }

    private void CaptureGameplayTextures()
    {
        if (resourceHost is null)
        {
            return;
        }
        bool battlePresented = ActiveBattle is not null && battleScene is not null && ReferenceEquals(ActiveBattle, texturePresentedBattle);
        bool fieldPresented = ActiveBattle is null && world?.CanPresentCurrentField == true && field?.Id == session.State.FieldId && texturePresentedScene == SceneKind.Field && texturePresentedField == session.State.FieldId && ReferenceEquals(world, texturePresentedWorldOwner);
        bool worldPresented = ActiveBattle is null && world?.WorldScene is { IsPresentationDeferred: false } scene && texturePresentedScene == SceneKind.WorldMap && ReferenceEquals(scene, texturePresentedWorld) && ReferenceEquals(world, texturePresentedWorldOwner);
        bool exclusive = startup.Active || worldTransfer is not null || crossoverSetup is not null || modelBrowser is not null || textureBrowser is not null || sceneInspector is not null || battleDebug is not null || lightingEditor is not null || localizationWorkbench.CapturesSimulation || authoringCandidates is not null || gameMenus is { IsOpen: true, IsDemo: true };
        if (!TextureSceneEligibility.CanCapture(session.State.Scene, InFf7, battlePresented, fieldPresented, worldPresented, fieldFaulted || fieldRenderFaulted, exclusive, options.SoftwareRenderer))
        {
            textureScene = [];
            textureSceneNormalized = true;
            textureSceneDiagnostics = [];
            textureSceneCompanions.Clear();
            textureSceneOriginalFactory = null;
            textureSceneResolvedFactory = null;
            textureSceneUnavailable = options.SoftwareRenderer ? LocalizeAuthored("textures.scene.software", "Current scene provenance is unavailable in the diagnostic software renderer. All textures, Selected and import remain available.") : LocalizeAuthored("textures.scene.unavailable", "Current scene requires an active FFVIII field, world or battle presentation. Title, movies, FFVII and authoring screens are excluded; All textures and import remain available.");
            return;
        }
        textureSceneUnavailable = null;
        textureSceneNormalized = false;
        textureSceneDiagnostics = [];
        textureSceneOriginalFactory = resourceHost.CaptureTextureAuthoringFactory(false);
        textureSceneResolvedFactory = resourceHost.CaptureTextureAuthoringFactory(true);
        HashSet<TextureKey> keys = [];
        GpuTriangleRenderer? activeGpu = ActiveBattle is not null ? battleGpu : fieldGpu;
        IEnumerable<Albeoris.Games.FF8.Imaging.PixelImage> presented = activeGpu?.PresentedTextureImages ?? [];
        if (uiCanvas is not null)
        {
            presented = presented.Concat(uiCanvas.PresentedTextureImages);
        }
        if (nativeTextFont is not null)
        {
            presented = presented.Concat(nativeTextFont.PresentedTextureImages);
        }
        IReadOnlyList<TextureSourceSnapshot> sampled = resourceHost.Resources.CaptureTextureSourcesForImages(presented);
        keys.UnionWith(sampled.Select(snapshot => snapshot.Context.Key));
        if (session.State.Scene == SceneKind.Field && ActiveBattle is null && field is not null)
        {
            // Layered authoring retains the field's palette and animation companions as one workspace.
            keys.UnionWith(field.Background.Select(tile => tile.Texture?.TextureKey).OfType<TextureKey>());
        }
        textureScene = sampled.Concat(resourceHost.Resources.CaptureTextureSources(keys)).GroupBy(snapshot => snapshot.Target).Select(group => group.First()).ToArray();
        HashSet<TextureTarget> contributors = sampled.Select(snapshot => snapshot.Target).ToHashSet();
        textureSceneCompanions.Clear();
        textureSceneCompanions.UnionWith(textureScene.Where(snapshot => !contributors.Contains(snapshot.Target)).Select(snapshot => snapshot.Target));
    }

    private void OpenTextureBrowser()
    {
        if (resourceHost is null)
        {
            return;
        }
        gameMenus?.Close();
        textureSceneSelectionInitialized = false;
        TextureToolPreferences preferences = ReadTextureToolPreferences();
        textureExportDirectory = preferences.ExportDirectory;
        textureImportDirectory = preferences.ImportDirectory;
        textureDestination = preferences.Destination;
        textureImportFiles = preferences.ImportFiles?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        textureBrowser = new(CloseTextureBrowser, ExecuteTextureCommand) { LocalizeText = LocalizeUiText, ScopeDescription = resourceHost.TextureAuthoringScopeDescription, ManageMods = OpenTextureMods, ExpandSourceRequested = ExpandTextureSource };
        textureBrowser.RestoreSourceModes(preferences.SourceModes ?? new Dictionary<int, bool> { [preferences.Scope] = preferences.Resolved });
        previousTextureMouse = physicalInput.ReadMouse().LeftButton == ButtonState.Pressed;
        previousTextureWheel = physicalInput.ReadMouse().ScrollWheelValue;
        textureDialogDrain = true;
        ShowTextureScope(preferences.Scope);
        SynchronizeUiMoviePause();
    }

    private void NormalizeCapturedTextureScene()
    {
        TextureSourceSnapshot[] captured = textureScene.ToArray();
        HashSet<TextureTarget> originalCompanions = new(textureSceneCompanions);
        var createSession = textureSceneResolvedFactory!;
        StartTextureJob(token =>
        {
            using var authoring = createSession();
            Dictionary<TextureTarget, TextureSourceSnapshot> normalized = [];
            HashSet<TextureTarget> companions = [];
            HashSet<TextureTarget> contributors = [];
            HashSet<TextureTarget> blocked = [];
            List<TextureCatalogDiagnostic> diagnostics = [];
            Dictionary<(ResourceKey, long), string> sourceHashes = [];
            bool SameImage(Albeoris.Games.FF8.Imaging.PixelImage first, Albeoris.Games.FF8.Imaging.PixelImage second) => ReferenceEquals(first, second) || first.Width == second.Width && first.Height == second.Height && first.Pixels.AsSpan().SequenceEqual(second.Pixels);
            foreach (TextureSourceSnapshot source in captured)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    foreach (TextureBuildContext context in authoring.Assets.LoadAuthoringTextures(source))
                    {
                        token.ThrowIfCancellationRequested();
                        if (context.EncodedSource is { } encoded && context.AuthoringSourceSha256 is null)
                        {
                            if (!sourceHashes.TryGetValue((encoded.Key, encoded.Revision), out string? sourceHash))
                            {
                                using Stream input = authoring.Assets.Resources.Get(encoded.Key).OpenRead();
                                sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
                                sourceHashes.Add((encoded.Key, encoded.Revision), sourceHash);
                            }
                            context.AuthoringSourceSha256 = sourceHash;
                        }
                        TextureValue original = authoring.Assets.Resources.ResolveOriginalForExport(context);
                        TextureValue resolved = authoring.Assets.Resources.ResolveForExport(context);
                        TextureSourceSnapshot candidate = new(context, resolved, source.Generation) { InstalledOriginal = original };
                        if (blocked.Contains(candidate.Target))
                        {
                            continue;
                        }
                        if (normalized.TryGetValue(candidate.Target, out TextureSourceSnapshot? previous) && (!SameImage(previous.Original, candidate.Original) || !SameImage(previous.Resolved.Image, candidate.Resolved.Image)))
                        {
                            normalized.Remove(candidate.Target);
                            companions.Remove(candidate.Target);
                            contributors.Remove(candidate.Target);
                            blocked.Add(candidate.Target);
                            diagnostics.Add(new(candidate.Target.Owner, "Normalizing captured scene", "Shared source aliases have different captured pixels. Resolve the conflicting per-consumer overrides or choose an explicit installation catalog source."));
                            continue;
                        }
                        normalized.TryAdd(candidate.Target, candidate);
                        if (originalCompanions.Contains(source.Target) || candidate.Target.Variant != (source.Context.RequestedVariant ?? ""))
                        {
                            companions.Add(candidate.Target);
                        }
                        else
                        {
                            contributors.Add(candidate.Target);
                        }
                    }
                }
                catch (Exception exception) when (IsModelBrowserError(exception))
                {
                    diagnostics.Add(new(source.Target.Owner, "Normalizing captured scene", exception.Message));
                }
            }
            companions.ExceptWith(contributors);
            return (sources: normalized.Values.ToArray(), companions, diagnostics);
        }, result =>
        {
            textureScene = result.sources;
            textureSceneCompanions.Clear();
            textureSceneCompanions.UnionWith(result.companions);
            textureSceneDiagnostics = result.diagnostics.ToArray();
            textureSceneNormalized = true;
            ShowTextureScope(0);
        }, LocalizeAuthored("textures.scene.normalizing", "Preparing captured scene identities and source-declared companions..."));
    }

    private void OpenTextureMods()
    {
        if (textureBrowser?.Busy == true)
        {
            return;
        }
        if (textureBrowser is not null)
        {
            textureBrowser.Matching = false;
        }
        CloseTextureBrowser();
        gameMenus?.Open("mods", true);
    }

    private void CloseTextureBrowser()
    {
        if (textureBrowser?.Busy == true)
        {
            textureCancellation?.Cancel();
            return;
        }
        if (textureBrowser?.Matching == true && textureReview is { } review)
        {
            ShowTextureReview(review);
            return;
        }
        SaveTextureToolPreferences();
        texturePreviewCancellation?.Cancel();
        uiCanvas?.UnregisterHostImage(TextureBrowserController.PreviewImageId);
        uiCanvas?.UnregisterHostImage(TextureBrowserController.ImportedPreviewImageId);
        textureBrowser = null;
        textureReview = null;
        texturePreviewIdentity = null;
        gameMenus?.Open("debug-room", true);
        drainUiButtons = true;
        keyboard.SuppressHeldNameInput();
        SynchronizeUiMoviePause();
    }

    private static string TextureId(TextureTarget target) => target.CanonicalPath();

    private void ShowTextureScope(int scope)
    {
        TextureBrowserController browser = textureBrowser!;
        browser.Scope = scope;
        SaveTextureToolPreferences();
        browser.Matching = false;
        browser.ImportedPreviewAvailable = false;
        uiCanvas?.UnregisterHostImage(TextureBrowserController.ImportedPreviewImageId);
        browser.Unmatched = null;
        textureReview = null;
        if (scope == 0)
        {
            if (!textureSceneNormalized && textureSceneResolvedFactory is not null)
            {
                NormalizeCapturedTextureScene();
                return;
            }
            if (!textureSceneSelectionInitialized)
            {
                browser.Selection.UnionWith(textureScene.Select(snapshot => TextureId(snapshot.Target)));
                textureSceneSelectionInitialized = true;
            }
            browser.SetRows(textureScene.Select(snapshot => new TextureBrowserRow(TextureId(snapshot.Target), snapshot.Target.Subresource + " / palette " + snapshot.Target.Palette, snapshot.Target.Owner, (textureSceneCompanions.Contains(snapshot.Target) ? LocalizeAuthored("textures.scene.companion", "Included for editing / animation completeness") : LocalizeAuthored("textures.scene.contributor", "Contributing captured presentation")) + " / " + snapshot.Target.CanonicalPath() + " / " + snapshot.Context.Layout + " / " + snapshot.Resolved.Provenance)));
            browser.Message = textureSceneUnavailable ?? FormatAuthored("textures.scene.count", "{0} source textures captured before opening Debug Room. Extra palette / animation companions are labelled; the active field background exports as one complete layered workspace.", textureScene.Count) + (textureSceneDiagnostics.Length == 0 ? "" : FormatAuthored("textures.scene.diagnostics", " {0} sources could not be normalized; the export report lists them.", textureSceneDiagnostics.Length));
        }
        else if (textureCatalog.Length != 0)
        {
            ShowTextureCatalog();
        }
        else
        {
            DiscoverTextureCatalog();
        }
    }

    private void ShowTextureCatalog()
    {
        textureBrowser!.SetRows(textureCatalog.Select(entry => new TextureBrowserRow(TextureId(entry.Target), entry.Name, entry.Category + "/" + entry.Target.Owner, entry.SearchText + " / " + entry.Layout + (entry.LoadError is null ? "" : " / " + entry.LoadError), entry.LoadError is null) { Expandable = entry.Layout == "field-workspace" && !expandedTextureFields.Contains(entry.Target) }));
    }

    private void ExpandTextureSource(string id)
    {
        TextureCatalogEntry? fieldEntry = textureCatalog.FirstOrDefault(entry => TextureId(entry.Target) == id && entry.Layout == "field-workspace");
        if (fieldEntry is null || expandedTextureFields.Contains(fieldEntry.Target) || textureBrowser?.Busy != false)
        {
            return;
        }
        var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
        StartTextureJob(token =>
        {
            using var authoring = createSession();
            return authoring.Assets.ExpandFieldTextureCatalog(fieldEntry, token).ToArray();
        }, tiles =>
        {
            expandedTextureFields.Add(fieldEntry.Target);
            textureCatalog = textureCatalog.Concat(tiles).GroupBy(entry => entry.Target).Select(group => group.First()).ToArray();
            ShowTextureCatalog();
            textureBrowser!.Message = LocalizeAuthored("textures.field.expanded", "Field source expanded into palette and tile targets. A layered export includes the required field companions.");
        }, LocalizeAuthored("textures.field.expanding", "Reading selected field texture hierarchy..."));
    }

    private void DiscoverTextureCatalog()
    {
        var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
        StartTextureJob(token =>
        {
            using var authoring = createSession();
            GameAssets assets = authoring.Assets;
            List<TextureCatalogDiagnostic> diagnostics = [];
            TextureCatalogEntry[] entries = assets.EnumerateTextureCatalog(token, diagnostics.Add).ToArray();
            return (entries, diagnostics);
        }, result =>
        {
            textureCatalog = result.entries;
            textureDiagnostics = result.diagnostics.ToArray();
            ShowTextureCatalog();
            textureBrowser!.Message = FormatAuthored("textures.catalog.count", "{0} texture targets discovered; {1} source diagnostics. Search includes category, owner, member and palette.", result.entries.Length, result.diagnostics.Count);
        }, LocalizeAuthored("textures.discovering", "Discovering texture sources..."));
    }

    private void ExecuteTextureCommand(int command)
    {
        if (textureBrowser is null || textureBrowser.Busy)
        {
            return;
        }
        if (command < 3)
        {
            ShowTextureScope(command);
        }
        else if (command == 3)
        {
            textureBrowser.Resolved = !textureBrowser.Resolved;
            SaveTextureToolPreferences();
        }
        else if (command == 4)
        {
            ExportTextureFolder();
        }
        else if (command == 5)
        {
            if (textureReview is null)
            {
                OpenTextureImport();
            }
            else
            {
                CompleteTextureImport();
            }
        }
        else if (command == 7 || command == 9)
        {
            string? folder = command == 9 ? textureDestination : textureImportDirectory ?? textureExportDirectory;
            if (folder is not null && Directory.Exists(folder))
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        else if (command == 8)
        {
            OpenTextureImportFiles();
        }
        else if (textureImportDirectory is not null && textureDestination is not null)
        {
            ReadTextureImport(textureImportDirectory, textureDestination);
        }
        else
        {
            resourceHost!.Mods.RequestRefresh();
            resourceHost.Pump(true);
            textureBrowser.Message = LocalizeAuthored("textures.refreshed", "Texture content revalidated. Import a working folder to enable repeated reimport.");
        }
    }

    private void StartTextureJob<T>(Func<CancellationToken, T> operation, Action<T> complete, string message)
    {
        TextureBrowserController browser = textureBrowser!;
        textureCancellation?.Dispose();
        textureCancellation = new();
        CancellationToken token = textureCancellation.Token;
        browser.CancelWork = () => textureCancellation?.Cancel();
        browser.Busy = true;
        browser.Message = message;
        textureWork = Task.Run(() =>
        {
            try
            {
                T result = operation(token);
                if (result is IDisposable owned)
                {
                    texturePendingResults.TryAdd(owned, 0);
                }
                textureCompletions.Enqueue(() =>
                {
                    browser.Busy = false;
                    try
                    {
                        if (token.IsCancellationRequested)
                        {
                            browser.Message = LocalizeAuthored("textures.cancelled", "Cancelled before publication. The previous mod generation was retained.");
                            return;
                        }
                        if (ReferenceEquals(textureBrowser, browser))
                        {
                            complete(result);
                        }
                    }
                    catch (Exception exception) when (IsModelBrowserError(exception))
                    {
                        browser.Message = exception.Message;
                    }
                    finally
                    {
                        if (result is IDisposable disposable && !ReferenceEquals(pendingTextureImport?.Prepared, result))
                        {
                            texturePendingResults.TryRemove(disposable, out _);
                            QueueTextureCleanup(disposable.Dispose);
                        }
                    }
                });
            }
            catch (Exception exception)
            {
                textureCompletions.Enqueue(() =>
                {
                    browser.Busy = false;
                    browser.Message = exception is OperationCanceledException ? LocalizeAuthored("textures.cancelled", "Cancelled between resources. Completed exports remain available; no pending import was published.") : exception.Message;
                });
            }
        });
    }

    private async void ExportTextureFolder()
    {
        try
        {
            TextureBrowserController browser = textureBrowser!;
            string? destination = await ModelBrowserDialogs.PathAsync(ModelBrowserDialogs.Desktop.SelectFolderAsync(new(LocalizeAuthored("textures.export.folder", "Choose a texture export folder"), textureExportDirectory)));
            if (destination is null)
            {
                return;
            }
            textureExportDirectory = destination;
            SaveTextureToolPreferences();
            bool resolved = browser.Resolved;
            TextureSourceSnapshot[] capturedScene = textureScene.ToArray();
            TextureSourceSnapshot[] scene = capturedScene.Where(snapshot => browser.Selection.Contains(TextureId(snapshot.Target))).ToArray();
            TextureCatalogEntry[] entries = textureCatalog.Where(entry => browser.Scope == 1 || browser.Selection.Contains(TextureId(entry.Target))).ToArray();
            int scope = browser.Scope;
            if (scope == 0 && scene.Length == 0 || scope != 0 && entries.Length == 0)
            {
                throw new InvalidDataException(LocalizeAuthored("textures.selection.required", "Select at least one catalog target before exporting."));
            }
            var createSession = scope == 0 ? (resolved ? textureSceneResolvedFactory : textureSceneOriginalFactory) ?? throw new InvalidOperationException("Recapture the gameplay scene before exporting.") : resourceHost!.CaptureTextureAuthoringFactory(resolved);
            TextureProgress progress = new(item => textureCompletions.Enqueue(() =>
            {
                if (ReferenceEquals(textureBrowser, browser) && browser.Busy)
                {
                    browser.Message = FormatAuthored("textures.progress", "{0}: {1} items / {2:N0} bytes / {3}", item.Stage, item.Completed, item.BytesWritten, item.Current ?? "");
                }
            }));
            StartTextureJob(token =>
            {
                using var authoring = createSession();
                GameAssets assets = authoring.Assets;
                using TextureExportJournal journal = new(destination, scope switch { 0 => "current-scene", 1 => "all", _ => "selected" }, assets.Resources.Source.Edition + " / " + string.Join(", ", assets.Resources.Source.Layouts), assets.Resources.ResourceLanguage, resolved);
                foreach (TextureCatalogDiagnostic diagnostic in scope == 0 ? textureSceneDiagnostics : textureDiagnostics)
                {
                    journal.AddDiagnostic(diagnostic);
                }
                try
                {
                    string[] fields = (scope == 0 ? scene.Where(item => item.Context.Layout is "classic-field-tile" or "field-workspace").Select(item => item.Target.Owner) : entries.Where(item => item.Layout is "classic-field-tile" or "field-workspace").Select(item => item.Target.Owner)).Select(owner => owner.Split('/')[1]).Distinct(StringComparer.Ordinal).ToArray();
                    TextureExportResult result = scope == 0
                        ? TextureExchange.Export(assets, scene.Where(item => item.Context.Layout is not ("classic-field-tile" or "field-workspace")), destination, resolved, token, progress, journal)
                        : TextureExchange.Export(assets, entries.Where(item => item.Layout is not ("classic-field-tile" or "field-workspace")), destination, resolved, token, progress, journal);
                    foreach (string fieldName in fields)
                    {
                        token.ThrowIfCancellationRequested();
                        progress.Report(new("Preparing layered workspace", result.Written, result.BytesWritten, fieldName));
                        string workspace = Path.Combine(destination, "fields", fieldName + ".ora");
                        assets.ExportFieldTextureWorkspace(fieldName, workspace, token, resolved: resolved, capturedSources: scope == 0 ? capturedScene.ToDictionary(snapshot => snapshot.Target) : null);
                        journal.AddWorkspace(workspace);
                        result = result with { BytesWritten = result.BytesWritten + new FileInfo(workspace).Length };
                    }
                    journal.Complete();
                    return result with { Written = result.Written + fields.Length };
                }
                catch (Exception exception)
                {
                    journal.AddDiagnostic(new("", "Exporting", exception.Message));
                    throw;
                }
            }, result => browser.Message = FormatAuthored("textures.export.complete", "Exported {0} editable images / layered field workspaces. Keep their .texture.json companions beside them.", result.Written), LocalizeAuthored("textures.writing", "Preparing / Writing texture files..."));
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

    private bool UpdateTextureBrowserUi(InputFrame input)
    {
        if (textureBrowser?.Busy == true && !textureDialogDrain && (input.Cancel || keyboard.DebugRoomRequested))
        {
            textureCancellation?.Cancel();
        }
        while (textureCompletions.TryDequeue(out Action? complete))
        {
            try
            {
                complete();
            }
            catch (Exception exception) when (IsModelBrowserError(exception))
            {
                if (textureBrowser is not null)
                {
                    textureBrowser.Message = exception.Message;
                }
            }
        }
        if (textureBrowser is not { } browser)
        {
            return false;
        }
        browser.AdvanceFiltering();
        UpdateTexturePreview();
        MouseState mouse = physicalInput.ReadMouse();
        bool pressed = physicalInput.CanRead && mouse.LeftButton == ButtonState.Pressed;
        bool space = physicalInput.CanRead && physicalInput.ReadKeyboard().IsKeyDown(Keys.Space);
        if (textureDialogDrain)
        {
            textureDialogDrain = pressed || space || keyboard.HeldControlButtons != 0;
        }
        else
        {
            if (keyboard.DebugRoomRequested)
            {
                browser.Cancel();
            }
            else if (keyboard.UiTabRequested)
            {
                browser.Navigate(MenuInput.Tab);
            }
            else
            {
                NavigateBattleView(browser.Navigate, input);
            }
            if (space && !previousTextureSpace && !browser.SearchFocused && !input.Confirm)
            {
                browser.Toggle();
            }
            if (textureBrowser is not null)
            {
                int wheel = mouse.ScrollWheelValue - previousTextureWheel;
                if (wheel != 0)
                {
                    browser.Select(browser.Index - Math.Sign(wheel) * 3);
                }
                var view = browser.BuildView();
                view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                view.Pointer(mouse.X, mouse.Y, pressed && !previousTextureMouse);
            }
        }
        previousTextureMouse = pressed;
        previousTextureSpace = space;
        previousTextureWheel = mouse.ScrollWheelValue;
        PumpTextureImport();
        return true;
    }

    private sealed class TextureProgress(Action<TextureExportProgress> report) : IProgress<TextureExportProgress>
    {
        private long previous;
        public void Report(TextureExportProgress value)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (previous == 0 || System.Diagnostics.Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds >= 100)
            {
                previous = now;
                report(value);
            }
        }
    }

    private void DisposeTextureTools()
    {
        SaveTextureToolPreferences();
        textureCancellation?.Cancel();
        texturePreviewCancellation?.Cancel();
        if (pendingTextureImport is { } pending)
        {
            resourceHost!.Mods.CancelTextureImport(pending.Prepared.Transaction);
            pendingTextureImport = null;
        }
        Task.WhenAll(new[] { textureWork, texturePreviewWork }.OfType<Task>()).GetAwaiter().GetResult();
        textureCancellation?.Dispose();
        texturePreviewCancellation?.Dispose();
        foreach (IDisposable result in texturePendingResults.Keys)
        {
            result.Dispose();
        }
        texturePendingResults.Clear();
        Task.WhenAll(textureCleanupTasks).GetAwaiter().GetResult();
        textureCleanupTasks.Clear();
        textureScene = [];
        textureSceneCompanions.Clear();
        textureCatalog = [];
        textureReview = null;
        textureBrowser = null;
        while (textureCompletions.TryDequeue(out _))
        {
        }
    }

    private void DrawTextureBrowserUi(Rectangle bounds)
    {
        if (textureBrowser is null || uiCanvas is null)
        {
            return;
        }
        batch.Draw(pixel, bounds, new Color(12, 14, 20));
        var view = textureBrowser.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(uiCanvas, uiTheme);
    }
}
