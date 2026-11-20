using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

[Collection("Incident dialog UI")]
public sealed class LauncherModPreviewTreeTests(LauncherHeadlessFixture fixture)
{
    [Fact]
    public async Task RevealsTheFirstAtlasMemberAndKeepsUserCollapse()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "catalog-preview-tree-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "payload");
        string cards = Path.Combine(payload, "mods", "textures", "cardgame");
        Directory.CreateDirectory(cards);
        try
        {
            File.WriteAllText(Path.Combine(payload, "mod.xml"), "<ModInfo><Name>Atlas example</Name></ModInfo>");
            File.WriteAllBytes(Path.Combine(cards, "cards_00.png"), []);
            File.WriteAllBytes(Path.Combine(cards, "icons_00.png"), []);
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/atlas", "Atlas example", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);

            await fixture.Session.Dispatch(() =>
            {
                Application.Current!.Styles.Add(new FluentTheme());
                LauncherModPreviewTree tree = new() { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
                Window window = new() { Content = tree, Width = 1280, Height = 720 };
                try
                {
                    window.Show();
                    tree.Load(document.Tree);
                    Dispatcher.UIThread.RunJobs();
                    ModPreviewTreeNode[] path = document.Tree.InitialPath.ToArray();
                    Assert.Equal(4, path.Length);
                    Assert.Equal("Textures (2)", path[0].Label);
                    Assert.Equal("Triple Triad (2)", path[1].Label);
                    Assert.Equal("cards_00.png (111)", path[2].Label);
                    Assert.Same(path[^1], tree.SelectedItem);

                    TreeViewItem category = Assert.IsType<TreeViewItem>(tree.ContainerFromItem(path[0]));
                    TreeViewItem group = Assert.IsType<TreeViewItem>(category.ContainerFromItem(path[1]));
                    TreeViewItem atlas = Assert.IsType<TreeViewItem>(group.ContainerFromItem(path[2]));
                    TreeViewItem member = Assert.IsType<TreeViewItem>(atlas.ContainerFromItem(path[^1]));
                    ModPreviewTreeNode sibling = Assert.Single(path[1].Children, branch => branch != path[2]);
                    Assert.Equal("icons_00.png (14)", sibling.Label);
                    Assert.True(category.IsExpanded);
                    Assert.True(group.IsExpanded);
                    Assert.True(atlas.IsExpanded);
                    Assert.True(member.IsSelected);
                    Assert.False(Assert.IsType<TreeViewItem>(group.ContainerFromItem(sibling)).IsExpanded);
                    Point position = Assert.IsType<Point>(member.TranslatePoint(default, tree));
                    Assert.InRange(position.Y, 0, tree.Bounds.Height - member.Bounds.Height);

                    atlas.IsExpanded = false;
                    tree.IsVisible = false;
                    Dispatcher.UIThread.RunJobs();
                    tree.IsVisible = true;
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(atlas.IsExpanded);
                    Assert.Same(path[^1], tree.SelectedItem);
                    TreeViewItem otherAtlas = Assert.IsType<TreeViewItem>(group.ContainerFromItem(sibling));
                    tree.SelectedItem = sibling;
                    Dispatcher.UIThread.RunJobs();
                    Assert.Same(sibling, tree.SelectedItem);
                    Assert.True(otherAtlas.IsSelected);
                    Assert.False(otherAtlas.IsExpanded);
                    Assert.False(atlas.IsExpanded);
                }
                finally
                {
                    window.Close();
                }
            }, token);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
