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
using Memoria.FF8.Mods;
using Memoria.FF8;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private Control? CreateLauncherModPreview(ModPackageView? installed, RemoteModEntry? entry)
    {
        if (launcherModLibrary is not { } library || installations.SelectedItem is not GameInstallation game)
        {
            return null;
        }
        bool canPreview = ModPreviewDocument.CanOpenInstalled(installed?.Package) ||
            entry is not null && library.View.Jobs.Any(job => job.CanPreview && job.Entry.Id == entry.Id && job.Entry.SourceId == entry.SourceId && job.Entry.Version == entry.Version);
        if (!canPreview)
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
        float yaw = 0.4f;
        float pose = 0;
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
                current = null;
                yaw = 0.4f;
                pose = 0;
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
                        else if (result.Before.Model is not null)
                        {
                            AddInteraction("Rotate left", token => MoveModel(-0.35f, 0, token));
                            AddInteraction("Next pose", token => MoveModel(0, 0.15f, token));
                            AddInteraction("Rotate right", token => MoveModel(0.35f, 0, token));
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

        Task<ModPreviewComparison> MoveModel(float turn, float step, CancellationToken token)
        {
            yaw = MathF.IEEERemainder(yaw + turn, MathF.Tau);
            pose += step;
            ModPreviewComparison pair = current!;
            float angle = yaw;
            float seconds = pose;
            return Task.Run(() => CatalogPreviewPresentation.DrawModels(pair, angle, seconds), token);
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
            current = result;
            if (world is not null && comparisonImages.Count == 2 && result is { Before.Image: { } original, After.Image: { } modded })
            {
                UpdateImage(comparisonImages[0], original);
                UpdateImage(comparisonImages[1], modded);
                return;
            }
            ClearComparison();
            comparison.ColumnDefinitions = new ColumnDefinitions("*,*");
            comparison.ColumnSpacing = 12;
            Control before = CreateSide("Before", result.Before);
            Control after = CreateSide("After", result.After);
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

        Control CreateSide(string label, ModPreviewSide side)
        {
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
