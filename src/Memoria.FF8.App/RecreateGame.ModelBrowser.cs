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
    private readonly Dictionary<string, ModFile> browserModelFiles = new(StringComparer.OrdinalIgnoreCase);
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
        modelBrowser.SetHints($"Arrows: select / rotate · {keyboard.BindingHint(ControlAction.Confirm)}: view / commands · {keyboard.BindingHint(ControlAction.Cancel)}: back\n{keyboard.BindingHint(ControlAction.CameraLeft)} / {keyboard.BindingHint(ControlAction.CameraRight)}: model · {keyboard.BindingHint(ControlAction.RotateLeft)} / {keyboard.BindingHint(ControlAction.RotateRight)}: zoom · {keyboard.BindingHint(ControlAction.Card)}: variant · Tab: search");
        MouseState mouse = Mouse.GetState();
        previousModelBrowserMouse = mouse.LeftButton == ButtonState.Pressed;
        previousModelBrowserWheel = mouse.ScrollWheelValue;
        previousModelBrowserButtons = keyboard.HeldControlButtons;
        previousModelBrowserEnter = Keyboard.GetState().IsKeyDown(Keys.Enter);
        previousModelBrowserEscape = Keyboard.GetState().IsKeyDown(Keys.Escape);
        modelPreviewIdentity = null;
        try
        {
            modelBrowserAssets ??= new GameAssets(options.GameDirectory, resources: resourceHost.Resources);
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
            browserModelFiles.Clear();
            foreach (ModFile file in resourceHost.Mods.ReadInstalledFiles(ModFormat.Memoria).Where(file => file.RelativePath.StartsWith("models/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)))
            {
                browserModelFiles.TryAdd(file.PackageId + ":" + file.RelativePath, file);
            }
            modelPreviewIdentity = null;
        }
        modelBrowser.Refresh(modelCatalog.Values.Select(entry => new ModelBrowserEntry(entry.Id, entry.Name, entry.Category, entry.SearchText,
            new[] { new ModelBrowserVariant("original", "Original") }.Concat(browserModelFiles.Where(item => item.Value.RelativePath.Equals("models/" + entry.Id + ".glb", StringComparison.OrdinalIgnoreCase)).Select(item => new ModelBrowserVariant(item.Key, item.Value.PackageId + (resourceHost.Mods.Catalog.Packages.Any(package => package.Id == item.Value.PackageId && package.Status == ModEffectiveStatus.Disabled) ? " (disabled)" : "")))).ToArray(),
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

    private bool UpdateModelBrowserUi(InputFrame input, TimeSpan elapsed)
    {
        if (modelBrowser is null)
        {
            return false;
        }
        ReadMoreModelCatalogEntries();
        if (resourceHost is not null && modelBrowserModGeneration != resourceHost.Mods.Catalog.Generation)
        {
            RefreshModelBrowserEntries();
        }
        ModelBrowserController browser = modelBrowser;
        KeyboardState keys = Keyboard.GetState();
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
        else if (input.Confirm || enter && !previousModelBrowserEnter)
        {
            browser.Navigate(MenuInput.Confirm);
        }
        else if (browser.Mode == ModelBrowserMode.Viewer)
        {
            float delta = (float)Math.Min(elapsed.TotalSeconds, 0.1);
            browser.Rotate(input.Horizontal * delta * 2, input.Vertical * delta * 2);
            browser.ZoomBy(MathF.Exp(input.CameraHorizontal * delta * 1.5f));
        }
        else
        {
            if (input.MenuVertical != 0)
            {
                browser.Navigate(input.MenuVertical > 0 ? MenuInput.Down : MenuInput.Up);
            }
            if (input.MenuPage != 0)
            {
                browser.Navigate(input.MenuPage > 0 ? MenuInput.PageDown : MenuInput.PageUp);
            }
        }
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
        if (keyboard.UiDetailRequested)
        {
            browser.Navigate(MenuInput.Alternate);
        }
        ushort pressed = (ushort)(keyboard.HeldControlButtons & ~previousModelBrowserButtons);
        previousModelBrowserButtons = keyboard.HeldControlButtons;
        if ((pressed & 3) != 0 && browser.Mode != ModelBrowserMode.Commands)
        {
            browser.StepModel((pressed & 2) != 0 ? 1 : -1);
        }
        MouseState mouse = Mouse.GetState();
        int wheel = mouse.ScrollWheelValue - previousModelBrowserWheel;
        previousModelBrowserWheel = mouse.ScrollWheelValue;
        if (IsActive && wheel != 0)
        {
            if (browser.Mode == ModelBrowserMode.Catalog)
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
        if (variant.Id == "original")
        {
            return original;
        }
        ModFile mod = browserModelFiles[variant.Id];
        return ModelExchange.ImportGlb(mod.ReadBytes(), original, allowBoneCountChange: true).Model;
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
                modelBrowser.SetMessage($"{modelPreview.Bones.Count(bone => bone.IsJoint)} bones · {modelPreview.Meshes.Count} meshes · {modelPreview.Animations.Count} animations");
            }
            if (modelPreview is null)
            {
                return;
            }
            Rectangle rectangle = ModelBrowserPreviewBounds(GraphicsDevice.Viewport.Bounds);
            modelPreviewGpu ??= new GpuTriangleRenderer(GraphicsDevice);
            ModelPreviewRenderer.Render(modelPreviewGpu, modelPreview, rectangle.Width, rectangle.Height, modelBrowser.RotationY, modelBrowser.RotationX, modelBrowser.Zoom, (float)time.TotalSeconds);
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
        try
        {
            EditableModel model = ReadBrowserModel(entry, variant);
            string? path = ModelBrowserDialogs.Export(Window.Handle, entry.Id.Replace('/', '_'));
            if (path is not null)
            {
                File.WriteAllBytes(path, ModelExchange.ExportGlb(model));
                modelBrowser?.SetMessage("Model exported to GLB.");
            }
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelBrowser?.SetMessage(exception.Message);
        }
        finally
        {
            keyboard.SuppressHeldNameInput();
            modelBrowserDialogInputDrain = true;
        }
    }

    private void ImportBrowserModel(ModelBrowserEntry entry, ModelBrowserVariant variant)
    {
        try
        {
            string? path = ModelBrowserDialogs.Import(Window.Handle);
            if (path is null)
            {
                return;
            }
            EditableModel original = modelBrowserAssets!.LoadCatalogModel(modelCatalog[entry.Id]);
            if (new FileInfo(path).Length > 256 * 1024 * 1024)
            {
                throw new InvalidDataException("The GLB must be no larger than 256 MiB.");
            }
            byte[] bytes = File.ReadAllBytes(path);
            ModelExchangeImportResult imported = ModelExchange.ImportGlb(bytes, original);
            if (imported.Warnings.Count != 0 || imported.RequiresBoneCountApproval)
            {
                string warning = string.Join("\n", imported.Warnings);
                if (imported.RequiresBoneCountApproval)
                {
                    warning = "The number of bones changed. Existing game animations or attachments may be incompatible.\n" + warning;
                }
                if (!ModelBrowserDialogs.Confirm(Window.Handle, warning))
                {
                    return;
                }
                imported = ModelExchange.ImportGlb(bytes, original, allowBoneCountChange: true);
            }
            byte[] converted = ModelExchange.ExportGlb(imported.Model);
            string root = Path.Combine(resourceHost!.Mods.PrimaryModsDirectory, "Memoria");
            string? folder = ModelBrowserDialogs.Destination(Window.Handle, root);
            if (folder is null)
            {
                return;
            }
            string destination = Path.Combine(folder, "models", entry.Id.Replace('/', Path.DirectorySeparatorChar) + ".glb");
            if (!ModelBrowserDialogs.IsPackageDirectory(root, Path.GetDirectoryName(destination)!))
            {
                throw new InvalidDataException("The model destination must remain inside Mods/Memoria.");
            }
            if (File.Exists(destination) && !ModelBrowserDialogs.Confirm(Window.Handle, "This package already contains a replacement for this model. It will be overwritten."))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, converted);
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
            resourceHost.Mods.RequestRefresh();
            bool published = resourceHost.Pump(true);
            RefreshModelBrowserEntries(refreshVariants: true);
            modelPreviewIdentity = null;
            string importedVariant = "Memoria/" + Path.GetFileName(folder) + ":models/" + entry.Id + ".glb";
            int index = modelBrowser!.Selected!.Variants.ToList().FindIndex(item => item.Id.Equals(importedVariant, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                modelBrowser.SelectVariant(index);
            }
            bool active = resourceHost.ModelMods.Variants.Any(item => (item.PackageId + ":" + item.RelativePath).Equals(importedVariant, StringComparison.OrdinalIgnoreCase));
            modelBrowser.SetMessage(active ? "Imported as a model mod. Reload the scene to apply it to existing actors." : published ? "Imported into a disabled package. Enable it in Mods, then reload the scene." : "GLB saved; mod publication failed. Inspect Mods diagnostics.");
        }
        catch (Exception exception) when (IsModelBrowserError(exception))
        {
            modelBrowser?.SetMessage(exception.Message);
        }
        finally
        {
            keyboard.SuppressHeldNameInput();
            modelBrowserDialogInputDrain = true;
        }
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
        modelCatalogReader?.Dispose();
        modelCatalogReader = null;
        modelBrowserAssets?.Dispose();
        modelBrowserAssets = null;
        modelPreviewGpu?.Dispose();
        modelPreviewGpu = null;
        modelBrowser = null;
    }
}
