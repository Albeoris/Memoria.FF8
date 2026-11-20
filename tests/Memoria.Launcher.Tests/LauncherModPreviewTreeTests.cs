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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestsCameraTracksUnderTheirMovies(bool includeUnmatchedTrack)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "catalog-preview-tree-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "payload");
        string movies = Path.Combine(payload, "movies");
        Directory.CreateDirectory(movies);
        try
        {
            File.WriteAllText(Path.Combine(payload, "mod.xml"), "<ModInfo><Name>Movie sample</Name><FFNxConfig><ffmpeg_video_ext>qt</ffmpeg_video_ext></FFNxConfig></ModInfo>");
            File.WriteAllBytes(Path.Combine(movies, "disc00_00h.qt"), []);
            File.WriteAllBytes(Path.Combine(movies, "DISC00_00.CAM"), []);
            File.WriteAllBytes(Path.Combine(movies, "disc00_01h.qt"), []);
            File.WriteAllBytes(Path.Combine(payload, "disc00_01.cam"), []);
            File.WriteAllBytes(Path.Combine(movies, "disc00_02h.qt"), []);
            if (includeUnmatchedTrack)
            {
                File.WriteAllBytes(Path.Combine(movies, "disc00_03.cam"), []);
            }
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            await library.StopAsync();
            ModPackage package = new("Junction/movies", "Movie sample", ModFormat.Junction, null, false, ModEffectiveStatus.Disabled, payload);
            ModPreviewDocument document = await library.OpenPreviewAsync(null, package, token);

            ModPreviewTreeNode video = Assert.Single(document.Tree.Branches);
            Assert.Equal(includeUnmatchedTrack ? "Video (6)" : "Video (5)", video.Label);
            ModPreviewTreeNode movieGroup = Assert.Single(video.Children, branch => branch.Label == "Movies (3)");
            Assert.Collection(movieGroup.Children,
                movie => AssertTrack(movie, "DISC00_00.CAM"),
                movie => AssertTrack(movie, "disc00_01.cam"),
                movie => Assert.Empty(movie.Children));
            if (includeUnmatchedTrack)
            {
                ModPreviewTreeNode cameraGroup = Assert.Single(video.Children, branch => branch.Label == "Camera tracks (1)");
                Assert.Equal("disc00_03.cam", Assert.Single(cameraGroup.Children).Label);
            }
            else
            {
                Assert.Same(movieGroup, Assert.Single(video.Children));
                Assert.Same(movieGroup.Children[0], document.Tree.InitialPath[^1]);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }

        static void AssertTrack(ModPreviewTreeNode movie, string name)
        {
            Assert.True(movie.Entry?.IsMedia);
            ModPreviewTreeNode track = Assert.Single(movie.Children);
            Assert.Equal(name, track.Label, ignoreCase: true);
            Assert.False(track.Entry?.IsMedia);
        }
    }

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
