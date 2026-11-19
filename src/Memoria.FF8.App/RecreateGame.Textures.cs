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

    private void CaptureGameplayTextures()
    {
        if (resourceHost is null)
        {
            return;
        }
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
            if (options.SoftwareRenderer && events is not null)
            {
                foreach (int modelId in events.Actors.Where(actor => actor.Visible).Select(actor => actor.RenderModelId).Distinct())
                {
                    if ((uint)modelId < field.Models.Count && field.Models[modelId].Textures is ResourceTextureList textures)
                    {
                        keys.UnionWith(textures.TextureKeys);
                    }
                }
            }
        }
        textureScene = sampled.Concat(resourceHost.Resources.CaptureTextureSources(keys)).GroupBy(snapshot => snapshot.Target).Select(group => group.First()).ToArray();
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
        browser.Unmatched = null;
        textureReview = null;
        if (scope == 0)
        {
            if (!textureSceneSelectionInitialized)
            {
                browser.Selection.UnionWith(textureScene.Select(snapshot => TextureId(snapshot.Target)));
                textureSceneSelectionInitialized = true;
            }
            browser.SetRows(textureScene.Select(snapshot => new TextureBrowserRow(TextureId(snapshot.Target), snapshot.Target.Subresource + " / palette " + snapshot.Target.Palette, snapshot.Target.Owner, snapshot.Target.CanonicalPath() + " / " + snapshot.Context.Layout + " / " + snapshot.Resolved.Provenance)));
            browser.Message = FormatAuthored("textures.scene.count", "{0} source textures captured before opening Debug Room. Shared and animation companions affect all consumers.", textureScene.Count);
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
        }, LocalizeAuthored("textures.field.expanding", "Reading selected field texture hierarchy…"));
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
                        if (result is IDisposable disposable)
                        {
                            texturePendingResults.TryRemove(disposable, out _);
                            disposable.Dispose();
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
                    browser.Message = FormatAuthored("textures.progress", "{0}: {1} items · {2:N0} bytes · {3}", item.Stage, item.Completed, item.BytesWritten, item.Current ?? "");
                }
            }));
            StartTextureJob(token =>
            {
                using var authoring = createSession();
                GameAssets assets = authoring.Assets;
                using TextureExportJournal journal = new(destination, scope switch { 0 => "current-scene", 1 => "all", _ => "selected" }, assets.Resources.Source.Edition + " / " + string.Join(", ", assets.Resources.Source.Layouts), assets.Resources.ResourceLanguage, resolved);
                foreach (TextureCatalogDiagnostic diagnostic in textureDiagnostics)
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
        Task.WhenAll(new[] { textureWork, texturePreviewWork }.OfType<Task>()).GetAwaiter().GetResult();
        textureCancellation?.Dispose();
        texturePreviewCancellation?.Dispose();
        foreach (IDisposable result in texturePendingResults.Keys)
        {
            result.Dispose();
        }
        texturePendingResults.Clear();
        textureScene = [];
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
