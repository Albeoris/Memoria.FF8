using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Memoria.FF8.Mods;
using Memoria.FF8;

namespace Memoria.Launcher;

internal sealed partial class LauncherWindow
{
    private Control? CreateLauncherModPreview(ModPackageView? installed, RemoteModEntry? entry)
    {
        if (launcherModLibrary is not { } library || installations.SelectedItem is not GameInstallation game)
        {
            return null;
        }
        bool canPreview = installed?.Package is { Format: ModFormat.Memoria or ModFormat.Junction, IsSettingsGroup: false } ||
            entry is not null && library.View.Jobs.Any(job => job.CanPreview && job.Entry.Id == entry.Id && job.Entry.SourceId == entry.SourceId && job.Entry.Version == entry.Version);
        if (!canPreview)
        {
            return null;
        }
        ModPreviewResourcesProfile profile = CatalogPreviewPresentation.CreateProfile(game.Directory, selectedModelSourceDirectory, SelectedResourceLanguage) with
        {
            DecodeMedia = (input, video, seconds, token) => LauncherCatalogPreviewMedia.Decode(mediaDirectory, input, video, seconds, token)
        };
        Button button = new() { Content = "Preview changes", Margin = new Thickness(0, 0, 8, 0) };
        button.Click += async (_, _) => await ShowLauncherModPreviewAsync(library, entry, installed?.Package, profile);
        return button;
    }

    private async Task ShowLauncherModPreviewAsync(IModLibraryService library, RemoteModEntry? entry, ModPackage? installed, ModPreviewResourcesProfile profile)
    {
        using CancellationTokenSource lifetime = new();
        CancellationTokenSource? selection = null;
        List<WriteableBitmap> bitmaps = [];
        CatalogPreviewPresentation.WorldComparison? world = null;
        ModPreviewComparison? current = null;
        float yaw = 0.4f;
        float pose = 0;
        double videoSeconds = 0;
        using LauncherCatalogPreviewMedia.AudioComparison audio = new();
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
        TreeView tree = new()
        {
            ItemTemplate = new FuncTreeDataTemplate<PreviewBranch>((branch, _) => new TextBlock { Text = branch.Label, TextTrimming = TextTrimming.CharacterEllipsis }, branch => branch.Children)
        };
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
        Grid.SetRow(controls, 1);
        Grid.SetColumnSpan(controls, 2);
        root.Children.Add(controls);
        window.Content = root;
        window.Closed += (_, _) =>
        {
            lifetime.Cancel();
            selection?.Cancel();
            world?.Dispose();
            world = null;
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
            PreviewBranch[] branches = document.Entries.GroupBy(item => item.Category).Select(category =>
                new PreviewBranch(category.Key, category.GroupBy(item => item.Group).Select(group =>
                    new PreviewBranch(group.Key, group.Select(item => new PreviewBranch(item.Name, [], item)).ToArray())).ToArray())).ToArray();
            tree.ItemsSource = branches;
            tree.SelectionChanged += async (_, _) =>
            {
                if (tree.SelectedItem is PreviewBranch { Entry: { } item })
                {
                    await ReadComparison(item);
                }
            };
            if (branches.Length == 0)
            {
                ShowMessage("No changes are available for comparison.");
            }
            else
            {
                expand.IsEnabled = true;
                tree.Focus();
                PreviewBranch first = branches[0].Children[0].Children[0];
                tree.SelectedItem = first;
                if (tree.ContainerFromItem(branches[0]) is TreeViewItem category)
                {
                    category.IsExpanded = true;
                    category.ApplyTemplate();
                    if (category.ContainerFromItem(branches[0].Children[0]) is TreeViewItem group)
                    {
                        group.IsExpanded = true;
                    }
                }
            }

            async Task ReadComparison(ModPreviewEntry item)
            {
                selection?.Cancel();
                world?.Dispose();
                world = null;
                current = null;
                yaw = 0.4f;
                pose = 0;
                videoSeconds = 0;
                audio.Dispose();
                interaction.Children.Clear();
                using CancellationTokenSource requested = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                selection = requested;
                ShowMessage("Comparing with the original game…");
                try
                {
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
                            AddInteraction("West", token => readyWorld.RenderAsync(-900, 0, token));
                            AddInteraction("North", token => readyWorld.RenderAsync(0, -900, token));
                            AddInteraction("South", token => readyWorld.RenderAsync(0, 900, token));
                            AddInteraction("East", token => readyWorld.RenderAsync(900, 0, token));
                        }
                        else if (item.IsMedia && item.Category == "Video")
                        {
                            AddInteraction("−3 s", token => StepVideo(-3, token));
                            AddInteraction("+3 s", token => StepVideo(3, token));
                        }
                        else if (item.Category == "Audio")
                        {
                            if (result.Before.AudioPcm is { } before)
                            {
                                AddAudio("Play before", () => audio.Play(before));
                            }
                            if (result.After.AudioPcm is { } after)
                            {
                                AddAudio("Play after", () => audio.Play(after));
                            }
                            if (interaction.Children.Count > 0)
                            {
                                AddAudio("Stop", audio.Dispose);
                            }
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
                Task<ModPreviewComparison> StepVideo(int step, CancellationToken token)
                {
                    videoSeconds = Math.Max(0, videoSeconds + step);
                    return CatalogPreviewPresentation.CompareAsync(document, item, profile with { MediaSeconds = videoSeconds }, token);
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

        void AddAudio(string label, Action play)
        {
            Button button = new() { Content = label };
            button.Click += async (_, _) =>
            {
                try
                {
                    play();
                }
                catch (IOException error)
                {
                    await LauncherDialogs.ShowAsync(window, "Could not play preview", error.Message);
                }
            };
            interaction.Children.Add(button);
        }

        void AddInteraction(string label, Func<CancellationToken, Task<ModPreviewComparison>> render)
        {
            Button button = new() { Content = label };
            interaction.Children.Add(button);
            button.Click += async (_, _) =>
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
            };
        }

        void ClearComparison()
        {
            comparison.Children.Clear();
            foreach (WriteableBitmap bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
            bitmaps.Clear();
        }

        void ShowMessage(string message)
        {
            ClearComparison();
            comparison.ColumnDefinitions.Clear();
            comparison.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) });
        }

        void ShowComparison(ModPreviewComparison result)
        {
            ClearComparison();
            current = result;
            comparison.ColumnDefinitions = new ColumnDefinitions("*,*");
            comparison.ColumnSpacing = 12;
            Control before = CreateSide("Before", result.Before);
            Control after = CreateSide("After", result.After);
            Grid.SetColumn(after, 1);
            comparison.Children.Add(before);
            comparison.Children.Add(after);
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
                WriteableBitmap bitmap = CreateModPreviewBitmap(pixels);
                bitmaps.Add(bitmap);
                Image image = new() { Source = bitmap, Stretch = Stretch.Uniform, IsVisible = !side.CanUseItem };
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

    private sealed record PreviewBranch(string Label, IReadOnlyList<PreviewBranch> Children, ModPreviewEntry? Entry = null);
}
