using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class DemasterBattleInstalledTests
{
    private const string Layout = "native-battle-stage-page";
    private static readonly Regex TargetName = new("^a0stg([0-9]{3})_(1[6-9]|2[01])(?:_(0|[1-9][0-9]*))?$", RegexOptions.CultureInvariant);

    [Fact]
    public void InstalledPackageImagesHaveSourceBackedTargetsOrAreUnreachableByTheNativeLoader()
    {
        string package = PackageDirectory();
        string game = GameDirectory();
        string directory = Path.Combine(package, "textures", "battle.fs", "hd_new");
        Assert.True(Directory.Exists(directory), "The configured package must contain textures/battle.fs/hd_new.");
        string[] images = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(IsImage).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(images);
        List<Target> targets = [];
        List<object> report = [];
        foreach (string path in images)
        {
            ModFile file = FileEntry(package, path);
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            Assert.True(decoded.Image.Width > 0 && decoded.Image.Height > 0, file.RelativePath);
            Assert.NotEmpty(decoded.MipLevels);
            Match match = TargetName.Match(Path.GetFileNameWithoutExtension(path));
            if (!match.Success || !Path.GetDirectoryName(path)!.Equals(directory, StringComparison.OrdinalIgnoreCase))
            {
                report.Add(new { file = file.RelativePath, classification = "unused", reason = "Outside the native stage/page/frame filename grammar or search directory", width = decoded.Image.Width, height = decoded.Image.Height });
                continue;
            }
            targets.Add(new(path, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null));
        }
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        using BattleAssets assets = new(game, resources);
        HashSet<int> stageIds = assets.StageIds.ToHashSet();
        int consumed = 0;
        foreach (IGrouping<int, Target> stageTargets in targets.GroupBy(target => target.Stage))
        {
            HashSet<(int Page, int Clut)> native = [];
            if (stageIds.Contains(stageTargets.Key))
            {
                using BattleAssets stageAssets = new(game, new ResourceService(source));
                BattleStage stage = stageAssets.LoadStage(stageTargets.Key);
                foreach (BattleFace face in stage.Meshes.SelectMany(mesh => mesh.Faces))
                {
                    NativeBattleFaceMaterial material = Assert.IsType<NativeBattleFaceMaterial>(face.NativeMaterial);
                    native.Add((material.TexturePage & 31, material.Clut & 0x7FFF));
                }
            }
            foreach (IGrouping<int, Target> pageTargets in stageTargets.GroupBy(target => target.Page))
            {
                Target[] pageFiles = pageTargets.ToArray();
                int[] cluts = native.Where(pair => pair.Page == pageTargets.Key).Select(pair => pair.Clut).Distinct().ToArray();
                if (cluts.Length == 0)
                {
                    foreach (Target target in pageFiles)
                    {
                        report.Add(new { file = Relative(package, target.Path), classification = "unused", reason = stageIds.Contains(target.Stage) ? "No original stage face references this native page" : "No installed original stage owns this filename", target.Stage, target.Page, target.Frame });
                    }
                    continue;
                }
                ModFile[] files = pageFiles.Select(target => FileEntry(package, target.Path)).ToArray();
                DemasterTextureResolver resolver = new(new(1, [], files));
                List<Target> selected = [];
                for (int frame = 0; ; frame++)
                {
                    Target? candidate = Preferred(pageFiles.Where(target => target.Frame == frame));
                    if (candidate is null)
                    {
                        break;
                    }
                    selected.Add(candidate);
                }
                bool animated = selected.Count != 0;
                if (!animated && Preferred(pageFiles.Where(target => target.Frame is null)) is { } single)
                {
                    selected.Add(single);
                }
                TextureReplacement? replacement = resolver.Resolve(Context(stageTargets.Key, pageTargets.Key, cluts[0], source.Descriptor));
                if (selected.Count == 0)
                {
                    Assert.Null(replacement);
                }
                else
                {
                    Assert.NotNull(replacement);
                    Assert.Equal(256, replacement.LogicalWidth);
                    Assert.Equal(256, replacement.LogicalHeight);
                    Assert.Equal(animated ? selected.Count : 0, replacement.Animation?.Frames.Count ?? 0);
                    for (int index = 0; index < selected.Count; index++)
                    {
                        ModFile file = Assert.Single(files, file => file.RelativePath == Relative(package, selected[index].Path));
                        PixelImage decoded = ModImageDecoder.Decode(file).Image;
                        PixelImage actual = animated ? replacement.Animation!.Frames[index] : replacement.Image;
                        Assert.Equal(decoded.Width, actual.Width);
                        Assert.Equal(decoded.Height, actual.Height);
                        Assert.True(decoded.Pixels.AsSpan().SequenceEqual(actual.Pixels), file.RelativePath);
                        Assert.Contains(file.RelativePath, replacement.Provenance);
                    }
                    foreach (int clut in cluts.Skip(1))
                    {
                        TextureReplacement other = Assert.IsType<TextureReplacement>(resolver.Resolve(Context(stageTargets.Key, pageTargets.Key, clut, source.Descriptor)));
                        Assert.Equal(replacement.Provenance, other.Provenance);
                    }
                    if (animated)
                    {
                        TextureAnimation animation = replacement.Animation!;
                        Assert.Equal(100, animation.FrameDurationMilliseconds);
                        Assert.Same(animation.Frames[0], animation.At(TimeSpan.FromMilliseconds(selected.Count * 100L)));
                    }
                }
                foreach (Target target in pageFiles)
                {
                    bool used = selected.Contains(target);
                    consumed += used ? 1 : 0;
                    report.Add(new { file = Relative(package, target.Path), classification = used ? "consumed" : "unused", reason = used ? "Original face page and contiguous native frame selection" : "Shadowed extension/base or frame after first missing frame", target.Stage, target.Page, target.Frame, cluts });
                }
            }
        }
        Assert.True(consumed > 0, "The configured package must replace at least one original stage page.");
        Assert.Equal(images.Length, report.Count);
        Evidence("demaster-stage-targets.json", new { images = images.Length, consumed, unused = images.Length - consumed, sourceStages = stageIds.Count, targets = report });
    }

    private static string PackageDirectory()
    {
        string? package = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_STAGE_PACKAGE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(package) && Directory.Exists(package), "Set MEMORIA_FF8_DEMASTER_STAGE_PACKAGE to an optional unpacked stage package.");
        return Path.GetFullPath(package!);
    }

    private static string GameDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable("FF8_GAME_DIR");
        return !string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured) ? configured : TestResources.GameDirectory;
    }

    private static TextureBuildContext Context(int stage, int page, int clut, ResourceSourceDescriptor source)
    {
        return new(new(new($"battle/a0stg{stage:D3}.x"), $"page/{page}", clut), source, 1, Layout, 256, 256, () => throw new InvalidOperationException("Semantic stage lookup must not decode the original image."));
    }

    private static Target? Preferred(IEnumerable<Target> targets)
    {
        return targets.OrderBy(target => Path.GetExtension(target.Path).Equals(".dds", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
    }

    private static bool IsImage(string path)
    {
        return Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase);
    }

    private static ModFile FileEntry(string package, string path)
    {
        return new("Demaster/StageFixture", ModFormat.Demaster, Relative(package, path), 1, () => File.ReadAllBytes(path));
    }

    private static string Relative(string package, string path) => Path.GetRelativePath(package, path).Replace('\\', '/');

    private static void Evidence(string filename, object value)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed record Target(string Path, int Stage, int Page, int? Frame);
}
