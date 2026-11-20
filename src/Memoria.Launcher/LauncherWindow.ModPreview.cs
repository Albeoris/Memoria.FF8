using System.Diagnostics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private Control? CreateLauncherModPreview(ModPackageView? installed, ModLibraryJob? job)
    {
        if (launcherModLibrary is not { } library || installations.SelectedItem is not GameInstallation game)
        {
            return null;
        }
        RemoteModEntry? entry = job is { CanPreview: true } ? job.Entry : null;
        if (entry is null && !ModPreviewDocument.CanOpenInstalled(installed?.Package))
        {
            return null;
        }
        ModPreviewResourcesProfile profile = CatalogPreviewPresentation.CreateProfile(game.Directory, selectedModelSourceDirectory, SelectedResourceLanguage);
        Button button = new() { Content = "Preview changes", Margin = new Thickness(0, 0, 8, 0) };
        button.Click += async (_, _) => await ShowLauncherModPreviewAsync(library, entry, installed?.Package, profile);
        return button;
    }

    private async Task ShowLauncherModPreviewAsync(IModLibraryService library, RemoteModEntry? entry, ModPackage? installed, ModPreviewResourcesProfile profile)
    {
        using CancellationTokenSource lifetime = new();
        CancellationTokenSource? selection = null;
        List<Image> comparisonImages = [];
        CatalogPreviewPresentation.WorldComparison? world = null;
        ModPreviewComparison? current = null;
        ModelPreviewCamera camera = CatalogPreviewPresentation.CreateModelCamera();
        float pose = 0;
        bool modelRenderPending = false;
        Image? beforeModelImage = null;
        Image? afterModelImage = null;
        IPointer? modelDragPointer = null;
        Point modelDragPosition = default;
        LauncherCatalogPreviewPlayer? player = null;
        List<Task> retiredPlayers = [];
        HashSet<PhysicalKey> movementKeys = [];
        DispatcherTimer movementTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        long lastMovement = 0;
        Window window = new()
        {
            Title = (entry?.Name ?? installed?.Name ?? "Mod") + " · Preview changes",
            Width = 1100,
            Height = 630,
            MinWidth = 680,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        Grid root = new() { ColumnDefinitions = new ColumnDefinitions("280,*"), RowDefinitions = new RowDefinitions("*,Auto"), ColumnSpacing = 12, RowSpacing = 8, Margin = new Thickness(12) };
        LauncherModPreviewTree tree = new();
        root.Children.Add(tree);
        Grid comparison = new() { RowDefinitions = new RowDefinitions("*") };
        Grid.SetColumn(comparison, 1);
        root.Children.Add(comparison);
        StackPanel controls = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        StackPanel interaction = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        controls.Children.Add(interaction);
        Button expand = new() { Content = "Expand comparison", IsEnabled = false };
        Button close = new() { Content = "Back", IsCancel = true };
        close.Click += (_, _) => window.Close();
        expand.Click += (_, _) =>
        {
            bool showList = !tree.IsVisible;
            tree.IsVisible = showList;
            root.ColumnDefinitions[0].Width = showList ? new GridLength(280) : new GridLength(0);
            comparison.Margin = showList ? default : new Thickness(-12, 0, 0, 0);
            expand.Content = showList ? "Expand comparison" : "Show change list";
            window.WindowState = showList ? WindowState.Normal : WindowState.Maximized;
            if (showList)
            {
                tree.Focus();
            }
        };
        controls.Children.Add(expand);
        controls.Children.Add(close);
        StackPanel playbackControls = new();
        TextBlock flightHelp = new() { Text = "WASD Move · Q Rise · E Descend", IsVisible = false, HorizontalAlignment = HorizontalAlignment.Right };
        StackPanel footer = new() { Spacing = 8 };
        footer.Children.Add(playbackControls);
        footer.Children.Add(flightHelp);
        footer.Children.Add(controls);
        Grid.SetRow(footer, 1);
        Grid.SetColumnSpan(footer, 2);
        root.Children.Add(footer);
        window.Content = root;
        window.AddHandler(InputElement.KeyDownEvent, (_, key) =>
        {
            if (world is null || current is null || !IsMovementKey(key.PhysicalKey) || (key.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
            {
                return;
            }
            key.Handled = true;
            movementKeys.Add(key.PhysicalKey);
            if (!movementTimer.IsEnabled)
            {
                lastMovement = Stopwatch.GetTimestamp();
                movementTimer.Start();
            }
        }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.KeyUpEvent, (_, key) =>
        {
            if (movementKeys.Remove(key.PhysicalKey))
            {
                key.Handled = true;
                if (movementKeys.Count == 0)
                {
                    movementTimer.Stop();
                }
            }
        }, RoutingStrategies.Tunnel);
        movementTimer.Tick += async (_, _) =>
        {
            if (selection is not null || world is not { } activeWorld)
            {
                return;
            }
            Vector3 direction = new(
                KeyAxis(PhysicalKey.D, PhysicalKey.A),
                KeyAxis(PhysicalKey.Q, PhysicalKey.E),
                KeyAxis(PhysicalKey.W, PhysicalKey.S));
            long now = Stopwatch.GetTimestamp();
            float seconds = MathF.Min(0.1f, (float)Stopwatch.GetElapsedTime(lastMovement, now).TotalSeconds);
            lastMovement = now;
            if (direction != Vector3.Zero)
            {
                const float flightSpeed = 6000;
                Vector3 movement = Vector3.Normalize(direction) * (flightSpeed * seconds);
                await RenderInteraction(token => activeWorld.FlyAsync(movement, token));
            }
        };
        window.Deactivated += (_, _) => StopMovement();
        window.Closed += (_, _) =>
        {
            StopMovement();
            lifetime.Cancel();
            selection?.Cancel();
            EndModelDrag();
            world?.Dispose();
            world = null;
            RetirePlayer();
        };
        ShowMessage("Reading package changes…");
        Task closed = window.ShowDialog(this);
        try
        {
            ModPreviewDocument document = await library.OpenPreviewAsync(entry, installed, lifetime.Token);
            if (lifetime.IsCancellationRequested)
            {
                return;
            }
            tree.SelectionChanged += async (_, _) =>
            {
                if (tree.SelectedItem is ModPreviewTreeNode { Entry: { } item })
                {
                    await ReadComparison(item);
                }
            };
            if (document.Tree.Branches.Count == 0)
            {
                ShowMessage("No changes are available for comparison.");
            }
            else
            {
                expand.IsEnabled = true;
                tree.Load(document.Tree);
                tree.Focus();
            }

            async Task ReadComparison(ModPreviewEntry item)
            {
                StopMovement();
                selection?.Cancel();
                world?.Dispose();
                world = null;
                camera = CatalogPreviewPresentation.CreateModelCamera();
                pose = 0;
                modelRenderPending = false;
                RetirePlayer();
                interaction.Children.Clear();
                flightHelp.IsVisible = false;
                using CancellationTokenSource requested = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                selection = requested;
                ShowMessage("Comparing with the original game…");
                try
                {
                    if (item.IsMedia)
                    {
                        CatalogMediaComparison media = await CatalogMediaComparison.OpenAsync(document, item, profile, mediaDirectory, requested.Token);
                        if (requested.IsCancellationRequested)
                        {
                            await media.ReleaseAsync();
                            return;
                        }
                        try
                        {
                            player = new(media);
                        }
                        catch
                        {
                            await media.ReleaseAsync();
                            throw;
                        }
                        ClearComparison();
                        comparison.ColumnDefinitions = new ColumnDefinitions("*,*");
                        comparison.ColumnSpacing = 12;
                        Grid.SetColumn(player.After, 1);
                        comparison.Children.Add(player.Before);
                        comparison.Children.Add(player.After);
                        playbackControls.Children.Add(player.Controls);
                        return;
                    }
                    ModPreviewComparison result;
                    if (item.Id == ModPreviewDocument.WorldSceneId)
                    {
                        world = new(profile, document.PackageCatalog);
                        result = await world.RenderAsync(0, 0, requested.Token);
                    }
                    else
                    {
                        result = await CatalogPreviewPresentation.CompareAsync(document, item, profile, requested.Token);
                    }
                    if (!requested.IsCancellationRequested)
                    {
                        ShowComparison(result);
                        if (world is { } readyWorld)
                        {
                            flightHelp.IsVisible = true;
                            AddInteraction("West", token => readyWorld.RenderAsync(-900, 0, token));
                            AddInteraction("North", token => readyWorld.RenderAsync(0, -900, token));
                            AddInteraction("South", token => readyWorld.RenderAsync(0, 900, token));
                            AddInteraction("East", token => readyWorld.RenderAsync(900, 0, token));
                        }
                        else if (result.Before.HasModel || result.After.HasModel)
                        {
                            AddModelInteraction("Rotate left", () => camera.Orbit(-0.35f, 0));
                            AddModelInteraction("Next pose", () => pose += 0.15f);
                            AddModelInteraction("Rotate right", () => camera.Orbit(0.35f, 0));
                        }
                    }
                }
                catch (OperationCanceledException) when (requested.IsCancellationRequested)
                {
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or ModTextureLoadException)
                {
                    if (!requested.IsCancellationRequested)
                    {
                        ShowMessage(error.Message);
                    }
                }
                finally
                {
                    if (ReferenceEquals(selection, requested))
                    {
                        selection = null;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.Xml.XmlException)
        {
            ShowMessage(error.Message);
        }
        finally
        {
            await closed;
            ClearComparison();
            await Task.WhenAll(retiredPlayers);
        }

        void AddModelInteraction(string label, Action changeCameraOrPose)
        {
            Button button = new() { Content = label };
            interaction.Children.Add(button);
            button.Click += (_, _) =>
            {
                changeCameraOrPose();
                QueueModelRender();
            };
        }

        void QueueModelRender()
        {
            modelRenderPending = true;
            if (selection is null && current is { } pair && !lifetime.IsCancellationRequested)
            {
                _ = RenderModelChangesAsync(pair);
            }
        }

        async Task RenderModelChangesAsync(ModPreviewComparison pair)
        {
            using CancellationTokenSource requested = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            selection = requested;
            try
            {
                while (modelRenderPending && !requested.IsCancellationRequested)
                {
                    modelRenderPending = false;
                    ModelPreviewCamera view = camera.Snapshot();
                    float seconds = pose;
                    ModPreviewComparison frame = await Task.Run(() => CatalogPreviewPresentation.DrawModels(pair, view, seconds), requested.Token);
                    if (!requested.IsCancellationRequested)
                    {
                        UpdateModelImage(beforeModelImage, frame.Before);
                        UpdateModelImage(afterModelImage, frame.After);
                    }
                }
            }
            catch (OperationCanceledException) when (requested.IsCancellationRequested)
            {
            }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                if (!requested.IsCancellationRequested)
                {
                    ShowMessage(error.Message);
                }
            }
            finally
            {
                if (ReferenceEquals(selection, requested))
                {
                    selection = null;
                }
            }
        }

        void UpdateModelImage(Image? image, ModPreviewSide side)
        {
            if (image is null || side.Image is not { } pixels)
            {
                return;
            }
            UpdateImage(image, pixels);
        }

        void EnableModelNavigation(Image image)
        {
            ToolTip.SetTip(image, "Middle drag to orbit · Shift + middle drag to pan · Ctrl + middle drag or mouse wheel to zoom");
            image.PointerPressed += (_, args) =>
            {
                PointerPoint point = args.GetCurrentPoint(image);
                if (point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed && modelDragPointer is null)
                {
                    modelDragPosition = point.Position;
                    modelDragPointer = args.Pointer;
                    args.Pointer.Capture(image);
                    args.Handled = true;
                }
            };
            image.PointerMoved += (_, args) =>
            {
                if (args.Pointer != modelDragPointer)
                {
                    return;
                }
                PointerPoint point = args.GetCurrentPoint(image);
                if (!point.Properties.IsMiddleButtonPressed)
                {
                    EndModelDrag();
                    return;
                }
                Vector delta = point.Position - modelDragPosition;
                modelDragPosition = point.Position;
                args.Handled = true;
                if (delta.X == 0 && delta.Y == 0)
                {
                    return;
                }
                if ((args.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    if (delta.Y == 0)
                    {
                        return;
                    }
                    camera.ZoomBy(MathF.Exp((float)(-delta.Y * 0.01)));
                }
                else if ((args.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    if (image.Source is not WriteableBitmap bitmap)
                    {
                        return;
                    }
                    PixelSize size = bitmap.PixelSize;
                    double scale = Math.Min(image.Bounds.Width / size.Width, image.Bounds.Height / size.Height);
                    double shortEdge = Math.Min(size.Width, size.Height) * scale;
                    if (shortEdge <= 0)
                    {
                        return;
                    }
                    camera.PanBy((float)(delta.X / shortEdge), (float)(delta.Y / shortEdge));
                }
                else
                {
                    camera.Orbit((float)(delta.X * 0.01), (float)(delta.Y * 0.01));
                }
                QueueModelRender();
            };
            image.PointerReleased += (_, args) =>
            {
                if (args.Pointer == modelDragPointer && !args.GetCurrentPoint(image).Properties.IsMiddleButtonPressed)
                {
                    EndModelDrag();
                    args.Handled = true;
                }
            };
            image.PointerCaptureLost += (_, args) =>
            {
                if (args.Pointer == modelDragPointer)
                {
                    modelDragPointer = null;
                }
            };
            image.PointerWheelChanged += (_, args) =>
            {
                if (args.Delta.Y != 0)
                {
                    camera.ZoomBy(MathF.Pow(1.2f, (float)args.Delta.Y));
                    args.Handled = true;
                    QueueModelRender();
                }
            };
        }

        void EndModelDrag()
        {
            IPointer? pointer = modelDragPointer;
            modelDragPointer = null;
            pointer?.Capture(null);
        }

        void RetirePlayer()
        {
            if (player is not null)
            {
                retiredPlayers.RemoveAll(task => task.IsCompletedSuccessfully);
                retiredPlayers.Add(player.StopAsync());
                player = null;
            }
            playbackControls.Children.Clear();
        }

        void AddInteraction(string label, Func<CancellationToken, Task<ModPreviewComparison>> render)
        {
            Button button = new() { Content = label };
            interaction.Children.Add(button);
            button.Click += async (_, _) => await RenderInteraction(render);
        }

        static bool IsMovementKey(PhysicalKey key)
        {
            return key is PhysicalKey.W or PhysicalKey.A or PhysicalKey.S or PhysicalKey.D or PhysicalKey.Q or PhysicalKey.E;
        }

        int KeyAxis(PhysicalKey positive, PhysicalKey negative)
        {
            return (movementKeys.Contains(positive) ? 1 : 0) - (movementKeys.Contains(negative) ? 1 : 0);
        }

        void StopMovement()
        {
            movementTimer.Stop();
            movementKeys.Clear();
        }

        async Task RenderInteraction(Func<CancellationToken, Task<ModPreviewComparison>> render)
        {
            if (selection is not null)
            {
                return;
            }
            using CancellationTokenSource requested = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            selection = requested;
            try
            {
                ModPreviewComparison result = await render(requested.Token);
                if (!requested.IsCancellationRequested)
                {
                    ShowComparison(result);
                }
            }
            catch (OperationCanceledException) when (requested.IsCancellationRequested)
            {
            }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                if (!requested.IsCancellationRequested)
                {
                    StopMovement();
                    ShowMessage(error.Message);
                }
            }
            finally
            {
                if (ReferenceEquals(selection, requested))
                {
                    selection = null;
                }
            }
        }

        void ClearComparison()
        {
            EndModelDrag();
            current = null;
            beforeModelImage = null;
            afterModelImage = null;
            comparison.Children.Clear();
            foreach (Image image in comparisonImages)
            {
                (image.Source as WriteableBitmap)?.Dispose();
            }
            comparisonImages.Clear();
        }

        void ShowMessage(string message)
        {
            ClearComparison();
            comparison.ColumnDefinitions.Clear();
            comparison.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) });
        }

        void ShowComparison(ModPreviewComparison result)
        {
            if (world is not null && comparisonImages.Count == 2 && result is { Before.Image: { } original, After.Image: { } modded })
            {
                current = result;
                UpdateImage(comparisonImages[0], original);
                UpdateImage(comparisonImages[1], modded);
                return;
            }
            ClearComparison();
            current = result;
            comparison.ColumnDefinitions = new ColumnDefinitions("*,*");
            comparison.ColumnSpacing = 12;
            Control before = CreateSide("Before", result.Before, out beforeModelImage);
            Control after = CreateSide("After", result.After, out afterModelImage);
            Grid.SetColumn(after, 1);
            comparison.Children.Add(before);
            comparison.Children.Add(after);
        }

        static void UpdateImage(Image image, PixelImage pixels)
        {
            WriteableBitmap? previous = image.Source as WriteableBitmap;
            image.Source = CreateModPreviewBitmap(pixels);
            previous?.Dispose();
        }

        Control CreateSide(string label, ModPreviewSide side, out Image? modelImage)
        {
            modelImage = null;
            Grid pane = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 8, Margin = new Thickness(10) };
            pane.Children.Add(new TextBlock { Text = label, FontSize = 19, FontWeight = FontWeight.SemiBold });
            SelectableTextBlock text = new() { Text = side.Text, TextWrapping = TextWrapping.Wrap };
            ScrollViewer scroll = new() { Content = text, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
            if (side.Image is { } pixels)
            {
                Grid.SetRow(scroll, 1);
                scroll.MaxHeight = 110;
                Image image = new() { Source = CreateModPreviewBitmap(pixels), Stretch = Stretch.Uniform, IsVisible = !side.CanUseItem };
                comparisonImages.Add(image);
                Grid.SetRow(image, 2);
                pane.Children.Add(image);
                if (side.HasModel)
                {
                    modelImage = image;
                    EnableModelNavigation(image);
                }
                if (side.CanUseItem)
                {
                    Button use = new() { Content = "Use item" };
                    use.Click += (_, _) => image.IsVisible = !image.IsVisible;
                    Grid.SetRow(use, 3);
                    pane.Children.Add(use);
                }
            }
            else
            {
                Grid.SetRow(scroll, 2);
            }
            pane.Children.Add(scroll);
            return pane;
        }
    }

    private static WriteableBitmap CreateModPreviewBitmap(PixelImage image)
    {
        WriteableBitmap bitmap = new(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using ILockedFramebuffer target = bitmap.Lock();
        byte[] row = new byte[checked(image.Width * 4)];
        for (int y = 0; y < image.Height; y++)
        {
            MemoryMarshal.AsBytes(image.Pixels.AsSpan(y * image.Width, image.Width)).CopyTo(row);
            Marshal.Copy(row, 0, IntPtr.Add(target.Address, y * target.RowBytes), row.Length);
        }
        return bitmap;
    }

}
