using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ModelBrowserController? modelBrowser;
    private GameAssets? modelBrowserAssets;
    private IEnumerator<ModelCatalogEntry>? modelCatalogReader;
    private bool modelCatalogComplete;
    private readonly Dictionary<string, ModelCatalogEntry> modelCatalog = new(StringComparer.OrdinalIgnoreCase);
    private ModelMods browserInstalledModelMods = ModelMods.Empty;
    private GpuTriangleRenderer? modelPreviewGpu;
    private EditableModel? modelPreview;
    private string? modelPreviewIdentity;
    private bool modelPreviewRendered;
    private bool previousModelBrowserMouse;
    private int previousModelBrowserWheel;
    private ushort previousModelBrowserButtons;
    private bool previousModelBrowserEnter;
    private bool previousModelBrowserEscape;
    private bool modelBrowserDialogInputDrain;
    private long modelBrowserModGeneration = -1;

    private void OpenModelBrowser()
    {
        if (resourceHost is null)
        {
            return;
        }
        gameMenus?.Close();
        modelBrowser = new([], CloseModelBrowser, ExportBrowserModel, ImportBrowserModel);
        modelBrowser.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        UpdateModelBrowserHints();
        MouseState mouse = Mouse.GetState();
        previousModelBrowserMouse = mouse.LeftButton == ButtonState.Pressed;
        previousModelBrowserWheel = mouse.ScrollWheelValue;
        previousModelBrowserButtons = keyboard.HeldControlButtons;
        previousModelBrowserEnter = Keyboard.GetState().IsKeyDown(Keys.Enter);
        previousModelBrowserEscape = Keyboard.GetState().IsKeyDown(Keys.Escape);
        modelPreviewIdentity = null;
        try
        {
            modelBrowserAssets ??= new GameAssets(options.GameDirectory);
            if (!modelCatalogComplete && modelCatalogReader is null)
            {
                modelCatalogReader = modelBrowserAssets.EnumerateModelCatalog().GetEnumerator();
            }
            else
            {
                RefreshModelBrowserEntries(refreshVariants: true);
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelBrowser.SetMessage(exception.Message);
        }
        SynchronizeUiMoviePause();
    }

    private void CloseModelBrowser()
    {
        browserAssetReader?.Dispose();
        browserAssetReader = null;
        browserAssets.Clear();
        browserAssetModels.Clear();
        browserAssetFingerprints.Clear();
        browserDiscoveryCache.Clear();
        browserPendingDiscovery.Clear();
        browserImportOriginals.Clear();
        browserImport = null;
        modelBrowser = null;
        modelPreview = null;
        modelPreviewIdentity = null;
        gameMenus?.Open("debug-room", true);
        drainUiButtons = true;
        keyboard.SuppressHeldNameInput();
        SynchronizeUiMoviePause();
    }

    private void RefreshModelBrowserEntries(bool refreshVariants = false)
    {
        if (modelBrowser is null || resourceHost is null)
        {
            return;
        }
        if (refreshVariants || modelBrowserModGeneration != resourceHost.Mods.Catalog.Generation)
        {
            modelBrowserModGeneration = resourceHost.Mods.Catalog.Generation;
            browserDiscoveryCache.Clear();
            ModCatalogSnapshot installed = new(modelBrowserModGeneration, resourceHost.Mods.Catalog.Packages, resourceHost.Mods.ReadInstalledFiles(ModFormat.Memoria));
            if (!ModelMods.TryPrepare(installed, out browserInstalledModelMods, out IReadOnlyList<ModDiagnostic> diagnostics))
            {
                modelBrowser.SetMessage(string.Join(" · ", diagnostics.Select(item => item.Message)));
            }
            modelPreviewIdentity = null;
        }
        modelBrowser.Refresh(modelCatalog.Values.Select(entry => new ModelBrowserEntry(entry.Id, entry.Name, entry.Category, entry.SearchText,
            new[] { new ModelBrowserVariant("original", "Original"), new ModelBrowserVariant("resolved", "Resolved") }.Concat(browserInstalledModelMods.Variants.Select(item => item.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).Select(id => new ModelBrowserVariant(id, id + (resourceHost.Mods.Catalog.Packages.Any(package => package.Id == id && package.Status == ModEffectiveStatus.Disabled) ? " (disabled)" : "")))).ToArray(),
            [entry.ResourcePath, entry.LoadError ?? entry.Id])));
    }

    private void ReadMoreModelCatalogEntries()
    {
        if (modelCatalogReader is null || modelBrowser is null)
        {
            return;
        }
        try
        {
            // Incremental discovery keeps the search and cancel controls responsive.
            for (int count = 0; count < 16; count++)
            {
                if (!modelCatalogReader.MoveNext())
                {
                    modelCatalogReader.Dispose();
                    modelCatalogReader = null;
                    modelCatalogComplete = true;
                    break;
                }
                ModelCatalogEntry entry = modelCatalogReader.Current;
                modelCatalog.TryAdd(entry.Id, entry);
            }
            RefreshModelBrowserEntries();
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelCatalogReader?.Dispose();
            modelCatalogReader = null;
            modelBrowser.SetMessage("Catalog discovery: " + exception.Message);
        }
    }

    private void UpdateModelBrowserHints()
    {
        if (modelBrowser is null)
        {
            return;
        }
        string directions = $"{keyboard.BindingPrompt(ControlAction.MoveUp)} / {keyboard.BindingPrompt(ControlAction.MoveDown)} / {keyboard.BindingPrompt(ControlAction.MoveLeft)} / {keyboard.BindingPrompt(ControlAction.MoveRight)}";
        modelBrowser.SetHints(modelBrowser.AssetsOpen
            ? $"{keyboard.BindingPrompt(ControlAction.UiTab)}: preview / list · {directions}: navigate · {keyboard.BindingPrompt(ControlAction.Confirm)} / Space: checkbox · Click label: preview\nObjects / Clips: switch list · Select all includes hidden rows · {keyboard.BindingPrompt(ControlAction.Cancel)}: cancel selection"
            : $"{directions}: select / rotate · {keyboard.BindingPrompt(ControlAction.Confirm)}: view / commands · {keyboard.BindingPrompt(ControlAction.Cancel)}: back\n{keyboard.BindingPrompt(ControlAction.CameraLeft)} / {keyboard.BindingPrompt(ControlAction.CameraRight)}: model · {keyboard.BindingPrompt(ControlAction.RotateLeft)} / {keyboard.BindingPrompt(ControlAction.RotateRight)}: zoom · {keyboard.BindingPrompt(ControlAction.Card)}: variant · {keyboard.BindingPrompt(ControlAction.UiTab)}: search");
    }

    private bool UpdateModelBrowserUi(InputFrame input, TimeSpan elapsed)
    {
        if (modelBrowser is null)
        {
            return false;
        }
        ReadMoreModelCatalogEntries();
        UpdateModelBrowserHints();
        ReadMoreBrowserAssets();
        modelBrowser.AdvancePlayback((float)Math.Min(elapsed.TotalSeconds, 0.1));
        if (resourceHost is not null && modelBrowserModGeneration != resourceHost.Mods.Catalog.Generation)
        {
            RefreshModelBrowserEntries();
        }
        ModelBrowserController browser = modelBrowser;
        KeyboardState keys = windowModeHotkey.Filter(Keyboard.GetState());
        bool enter = IsActive && keys.IsKeyDown(Keys.Enter);
        bool escape = IsActive && keys.IsKeyDown(Keys.Escape);
        if (modelBrowserDialogInputDrain)
        {
            previousModelBrowserEnter = enter;
            previousModelBrowserEscape = escape;
            previousModelBrowserButtons = keyboard.HeldControlButtons;
            previousModelBrowserMouse = Mouse.GetState().LeftButton == ButtonState.Pressed;
            modelBrowserDialogInputDrain = enter || escape || keyboard.HeldControlButtons != 0 || previousModelBrowserMouse;
            return true;
        }
        if (input.Cancel || escape && !previousModelBrowserEscape || keyboard.DebugRoomRequested)
        {
            browser.Navigate(MenuInput.Cancel);
        }
        else if (!browser.SearchFocused && (input.Confirm || enter && !previousModelBrowserEnter))
        {
            browser.Navigate(MenuInput.Confirm);
        }
        else if (browser.Mode == ModelBrowserMode.Viewer && (!browser.AssetsOpen || browser.PreviewFocused))
        {
            float delta = (float)Math.Min(elapsed.TotalSeconds, 0.1);
            browser.Rotate(input.Horizontal * delta * 2, input.Vertical * delta * 2);
            browser.ZoomBy(MathF.Exp(input.CameraHorizontal * delta * 1.5f));
        }
        else
        {
            if (!browser.SearchFocused && input.MenuVertical != 0)
            {
                browser.Navigate(input.MenuVertical > 0 ? MenuInput.Down : MenuInput.Up);
            }
            if (browser.AssetsOpen && !browser.SearchFocused && input.MenuHorizontal != 0)
            {
                browser.Navigate(input.MenuHorizontal > 0 ? MenuInput.Right : MenuInput.Left);
            }
            if (!browser.SearchFocused && input.MenuPage != 0)
            {
                browser.Navigate(input.MenuPage > 0 ? MenuInput.PageDown : MenuInput.PageUp);
            }
        }
        bool space = IsActive && keys.IsKeyDown(Keys.Space);
        if (browser.AssetsOpen && !browser.SearchFocused && !browser.PreviewFocused && space && !previousBrowserSpace && !input.Confirm && !(enter && !previousModelBrowserEnter))
        {
            browser.Navigate(MenuInput.Confirm);
        }
        previousBrowserSpace = space;
        previousModelBrowserEnter = enter;
        previousModelBrowserEscape = escape;
        if (modelBrowser is null)
        {
            return true;
        }
        if (keyboard.UiTabRequested)
        {
            browser.Navigate(MenuInput.Tab);
        }
        if (keyboard.UiDetailRequested && !browser.AssetsOpen)
        {
            browser.Navigate(MenuInput.Alternate);
        }
        ushort pressed = (ushort)(keyboard.HeldControlButtons & ~previousModelBrowserButtons);
        previousModelBrowserButtons = keyboard.HeldControlButtons;
        if ((pressed & 3) != 0 && browser.Mode != ModelBrowserMode.Commands && !browser.AssetsOpen)
        {
            browser.StepModel((pressed & 2) != 0 ? 1 : -1);
        }
        MouseState mouse = Mouse.GetState();
        int wheel = mouse.ScrollWheelValue - previousModelBrowserWheel;
        previousModelBrowserWheel = mouse.ScrollWheelValue;
        if (IsActive && wheel != 0)
        {
            if (browser.AssetsOpen && !browser.PreviewFocused)
            {
                browser.SelectAssetRow(browser.AssetIndex - Math.Sign(wheel) * 3, true);
            }
            else if (browser.Mode == ModelBrowserMode.Catalog)
            {
                browser.Select(browser.SelectedIndex - Math.Sign(wheel) * 3);
            }
            else
            {
                browser.ZoomBy(MathF.Pow(1.1f, Math.Sign(wheel)));
            }
        }
        bool mousePressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        UiView view = browser.BuildView();
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        view.Pointer(mouse.X, mouse.Y, mousePressed && !previousModelBrowserMouse);
        previousModelBrowserMouse = mousePressed;
        return true;
    }

    private EditableModel ReadBrowserModel(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        EditableModel original = modelBrowserAssets!.LoadCatalogModel(modelCatalog[entry.Id]);
        return ResolveBrowserModel(entry.Id, original, variant);
    }

    private void RenderModelBrowserPreview(TimeSpan time)
    {
        modelPreviewRendered = false;
        if (modelBrowser?.Selected is not { } entry || modelBrowser.CurrentVariant is not { } variant)
        {
            return;
        }
        try
        {
            string identity = entry.Id + "\0" + variant.Id;
            if (modelPreviewIdentity != identity)
            {
                modelPreviewIdentity = identity;
                modelPreview = null;
                modelPreview = ReadBrowserModel(entry, variant);
                ResetBrowserAssets(entry, variant);
                modelBrowser.SetMessage($"{modelPreview.Bones.Count(bone => bone.IsJoint)} bones · {modelPreview.Meshes.Count} meshes · {modelPreview.Animations.Count} animations");
            }
            if (modelPreview is null)
            {
                return;
            }
            Rectangle rectangle = ModelBrowserPreviewBounds(GraphicsDevice.Viewport.Bounds);
            modelPreviewGpu ??= new GpuTriangleRenderer(GraphicsDevice);
            UpdateBrowserAssemblyPreview();
            ModelPreviewRenderer.Render(modelPreviewGpu, modelPreview, rectangle.Width, rectangle.Height, modelBrowser.RotationY, modelBrowser.RotationX, modelBrowser.Zoom, modelBrowser.AssetsOpen ? modelBrowser.PlaybackSeconds : (float)time.TotalSeconds, modelBrowser.AssetsOpen && modelPreview.Animations.Count == 0 ? -1 : 0, !modelBrowser.AssetsOpen);
            modelPreviewRendered = true;
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelPreviewGpu?.Abort();
            modelBrowser.SetMessage(exception.Message);
        }
    }

    private Rectangle ModelBrowserPreviewBounds(Rectangle viewport)
    {
        UiRect preview = modelBrowser!.PreviewBounds;
        float scale = Math.Min(viewport.Width / 960f, viewport.Height / 672f);
        float x = (viewport.Width - 960 * scale) / 2;
        float y = (viewport.Height - 672 * scale) / 2;
        return new Rectangle(viewport.X + (int)Math.Round(x + preview.X * scale), viewport.Y + (int)Math.Round(y + preview.Y * scale), Math.Max(1, (int)Math.Round(preview.Width * scale)), Math.Max(1, (int)Math.Round(preview.Height * scale)));
    }

    private void DrawModelBrowserUi(Rectangle bounds)
    {
        if (modelBrowser is null || uiCanvas is null)
        {
            return;
        }
        batch.Draw(pixel, bounds, new Color(12, 14, 20));
        if (modelPreviewRendered)
        {
            batch.Draw(modelPreviewGpu!.Target, ModelBrowserPreviewBounds(bounds), Color.White);
        }
        UiView view = modelBrowser.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(uiCanvas, uiTheme);
    }

    private void ExportBrowserModel(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        if (browserImport is not null || browserAssetIdentity != entry.Id + "\0" + variant.Id)
        {
            ResetBrowserAssets(entry, variant);
        }
        modelBrowser!.OpenAssetPanel(CompleteBrowserExport);
        modelBrowser.SetHints("Tab: preview / list · Arrows: navigate · Confirm / Space: checkbox · Click label: preview\nObjects / Clips: switch list · Select all includes hidden rows · Back: cancel selection");
    }

    private void ImportBrowserModel(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        OpenBrowserImport(entry, variant);
    }

    private static bool IsModelBrowserError(Exception exception) => exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or IndexOutOfRangeException or KeyNotFoundException or InvalidOperationException or System.Text.Json.JsonException;

    private bool TryDispatchModelBrowserControl(ControlRequest request, UiControlCommand command)
    {
        if (request.Command.Equals("ui-open", StringComparison.OrdinalIgnoreCase) && command.Screen == "model-browser")
        {
            OpenModelBrowser();
            return true;
        }
        if (modelBrowser is null)
        {
            return false;
        }
        if (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase))
        {
            CloseModelBrowser();
        }
        else if (request.Command.Equals("ui-open", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("debug-room", StringComparison.OrdinalIgnoreCase))
        {
            CloseModelBrowser();
            return false;
        }
        else if (request.Command.Equals("ui-input", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Search is not null)
            {
                modelBrowser.SetSearch(command.Search);
            }
            if (command.Variant is int variant)
            {
                modelBrowser.SelectVariant(variant);
            }
            if (command.Input is MenuInput input)
            {
                modelBrowser.Navigate(input);
            }
            if (modelBrowser is not null && command.X is float x && command.Y is float y)
            {
                UiView view = modelBrowser.BuildView();
                view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                view.Pointer(x, y, command.Pressed && !uiControlPointerPressed);
                uiControlPointerPressed = command.Pressed;
            }
        }
        else if (!request.Command.Equals("ui-status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private object? ModelBrowserUiStatus()
    {
        return modelBrowser is null ? null : new
        {
            modelBrowser.Mode,
            modelBrowser.Search,
            modelBrowser.SearchFocused,
            modelBrowser.SelectedIndex,
            SelectedId = modelBrowser.Selected?.Id,
            Variant = modelBrowser.CurrentVariant,
            modelBrowser.RotationX,
            modelBrowser.RotationY,
            modelBrowser.Zoom,
            modelBrowser.Message,
            Discovering = modelCatalogReader is not null,
            Count = modelBrowser.Entries.Count,
            Entries = modelBrowser.Entries.Skip(modelBrowser.Page * ModelBrowserController.RowsPerPage).Take(ModelBrowserController.RowsPerPage).Select(entry => new { entry.Id, entry.Name, entry.Category, entry.Variants }).ToArray()
        };
    }

    private void DisposeModelBrowser()
    {
        browserAssetReader?.Dispose();
        browserAssetReader = null;
        modelCatalogReader?.Dispose();
        modelCatalogReader = null;
        modelBrowserAssets?.Dispose();
        modelBrowserAssets = null;
        modelPreviewGpu?.Dispose();
        modelPreviewGpu = null;
        modelBrowser = null;
    }
}
