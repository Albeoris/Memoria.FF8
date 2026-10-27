using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionEffectPackageInstalledTests
{
    private static readonly string[] Fixtures = ["effect-textures-1", "effect-textures-2"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Fact]
    public void EveryDeclaredOptionSelectionPublishesOrReportsItsOwningConstraintConflict()
    {
        using Inputs input = new();
        List<object> observations = [];
        int valid = 0;
        int rejected = 0;
        foreach (string fixture in Fixtures)
        {
            foreach (CoverageCase configuration in input.Cases[fixture])
            {
                input.SelectOnly(fixture, configuration.Options);
                using GameResourceHost host = input.Open();
                string packageId = input.PackageId(fixture);
                ModPackage package = Assert.Single(host.Mods.Catalog.Packages, package => package.Id == packageId);
                ModFile[] textures = host.Mods.Catalog.Files.Where(file => file.PackageId == packageId && IsTexture(file.RelativePath)).ToArray();
                Assert.All(host.Mods.Catalog.Packages.Where(candidate => candidate.Format == ModFormat.Junction && candidate.Id != packageId), candidate => Assert.Equal(ModEffectiveStatus.Disabled, candidate.Status));
                Assert.DoesNotContain(host.Mods.Catalog.Files, file => file.Format == ModFormat.Junction && file.PackageId != packageId);
                Assert.Equal(configuration.Options.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase), package.JunctionMetadata!.Options.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
                if (configuration.CompatibilityValid)
                {
                    valid++;
                    Assert.Equal(configuration.DistinctSelectedAliases, textures.Length);
                    Assert.Equal(textures.Length == 0 ? ModEffectiveStatus.NoUsableEntries : ModEffectiveStatus.Active, package.Status);
                    Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == packageId && diagnostic.Code is "junction-setting-conflict" or "junction-import-failed");
                    AssertMalformedPaletteDiagnostics(host, packageId, textures);
                }
                else
                {
                    rejected++;
                    Assert.Equal(ModEffectiveStatus.ReloadFailure, package.Status);
                    Assert.Empty(textures);
                    Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == packageId && diagnostic.Code == "junction-setting-conflict");
                    Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == packageId && diagnostic.Code == "junction-import-failed");
                }
                observations.Add(new { fixture, configuration.Options, configuration.CompatibilityValid, expectedSelectedAliases = configuration.DistinctSelectedAliases, selectedAliases = textures.Length, package.Status, diagnostics = host.Mods.Diagnostics });
            }
        }
        Assert.Equal(input.Cases.Values.Sum(cases => cases.Length), valid + rejected);
        Assert.Equal(input.Cases.Values.SelectMany(cases => cases).Count(configuration => configuration.CompatibilityValid), valid);
        Assert.Equal(input.Cases.Values.SelectMany(cases => cases).Count(configuration => !configuration.CompatibilityValid), rejected);
        Evidence("publication-matrix.json", new { input.ArchiveHashes, configurations = observations.Count, valid, rejected, observations, scope = "Full host package publication and owned diagnostics; image decoding and native rendering are separate checks." });
    }

    [Fact]
    public void CoLoadedAliasesFollowOrdinaryDiscoveryOrderAndEnabledState()
    {
        using Inputs input = new();
        input.SelectBothDefaults();
        List<object> observations = [];
        string[] overlaps;
        using (GameResourceHost host = input.Open())
        {
            string first = input.PackageId(Fixtures[0]);
            string second = input.PackageId(Fixtures[1]);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == first).Status);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == second).Status);
            HashSet<string> firstAliases = Aliases(host, first);
            HashSet<string> secondAliases = Aliases(host, second);
            overlaps = firstAliases.Intersect(secondAliases, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.Equal(2, overlaps.Length);
            Observe(host, "both-enabled", first, second);
            string[] originalOptions = OptionLines(input.WorkingArchive(Fixtures[0]) + ".ini");
            Assert.True(host.Mods.SetEnabled(first, false).Success);
            Assert.True(host.Pump(true));
            Assert.Equal(originalOptions, OptionLines(input.WorkingArchive(Fixtures[0]) + ".ini"));
            Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == first).Status);
            foreach (string alias in overlaps)
            {
                Assert.Equal(second, Assert.Single(host.Mods.Catalog.FindAll(ModFormat.Junction, alias)).PackageId);
            }
            observations.Add(new { phase = "first-disabled", winner = second, aliases = overlaps });
            Assert.True(host.Mods.SetEnabled(first, true).Success);
            Assert.True(host.Pump(true));
            Assert.Equal(originalOptions, OptionLines(input.WorkingArchive(Fixtures[0]) + ".ini"));
            Observe(host, "first-reenabled", first, second);
        }
        input.MoveLaterInDiscoveryOrder(Fixtures[0]);
        using (GameResourceHost host = input.Open())
        {
            Observe(host, "ordinary-order-reversed", input.PackageId(Fixtures[1]), input.PackageId(Fixtures[0]));
        }
        Evidence("co-loaded-priority.json", new { input.ArchiveHashes, observations, scope = "Actual host catalog winners, enable toggles, and ordinary alphabetical discovery order; archives remain byte-identical." });

        void Observe(GameResourceHost host, string phase, string expectedFirst, string expectedSecond)
        {
            List<object> winners = [];
            foreach (string alias in overlaps)
            {
                IReadOnlyList<ModFile> files = host.Mods.Catalog.FindAll(ModFormat.Junction, alias);
                Assert.Equal(new[] { expectedFirst, expectedSecond }, files.Select(file => file.PackageId));
                ModFileSegment segment = Assert.IsType<ModFileSegment>(files[0].ResolveFileSegment());
                ModPackage owner = Assert.Single(host.Mods.Catalog.Packages, package => package.Id == expectedFirst);
                Assert.Equal(owner.RootDirectory, segment.Path);
                winners.Add(new { alias, winner = files[0].PackageId, orderedPackages = files.Select(file => file.PackageId), segment.Offset, segment.Length });
            }
            AssertMalformedPaletteDiagnostics(host, input.PackageId(Fixtures[0]), host.Mods.Catalog.Files.Where(file => file.PackageId == input.PackageId(Fixtures[0]) && IsTexture(file.RelativePath)).ToArray());
            AssertMalformedPaletteDiagnostics(host, input.PackageId(Fixtures[1]), host.Mods.Catalog.Files.Where(file => file.PackageId == input.PackageId(Fixtures[1]) && IsTexture(file.RelativePath)).ToArray());
            observations.Add(new { phase, winners, diagnostics = host.Mods.Diagnostics });
        }
    }

    private static HashSet<string> Aliases(GameResourceHost host, string packageId) => host.Mods.Catalog.Files.Where(file => file.PackageId == packageId && IsTexture(file.RelativePath)).Select(file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AssertMalformedPaletteDiagnostics(GameResourceHost host, string packageId, ModFile[] textures)
    {
        string[] malformed = textures.Where(file => HasMalformedPaletteSuffix(file.RelativePath)).Select(file => file.RelativePath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        ModDiagnostic[] diagnostics = host.Mods.Diagnostics.Where(diagnostic => diagnostic.PackageId == packageId && diagnostic.Code.StartsWith("ffnx-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(malformed, diagnostics.Select(diagnostic => diagnostic.RelativePath!).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("ffnx-unsupported-target", diagnostic.Code);
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.RelativePath));
            Assert.Contains(textures, file => file.RelativePath.Equals(diagnostic.RelativePath, StringComparison.OrdinalIgnoreCase));
            Assert.True(HasMalformedPaletteSuffix(diagnostic.RelativePath!));
        });
    }

    private static bool HasMalformedPaletteSuffix(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (Regex.IsMatch(stem, @"_[0-9]+_[0-9]+_[0-9]+$", RegexOptions.CultureInvariant))
        {
            return false;
        }
        string suffix = stem[(stem.LastIndexOf('_') + 1)..];
        return !int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int palette) || palette > byte.MaxValue || suffix != palette.ToString("00", CultureInfo.InvariantCulture);
    }

    private static bool IsTexture(string path) => path.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(path).ToLowerInvariant() is ".dds" or ".png";

    private static string[] OptionLines(string path) => File.ReadAllLines(path, Encoding.UTF8).Where(line => line.StartsWith("option.", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string FileHash(string path)
    {
        using Stream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static void Evidence(string filename, object value)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        string directory = Path.Combine(root, "EffectPackages");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
    }

    private sealed record CoverageCase(Dictionary<string, int> Options, bool CompatibilityValid, int DistinctSelectedAliases);

    private sealed class Inputs : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-effect-packages-" + Guid.NewGuid().ToString("N"));
        private readonly IsolatedFieldGame game;
        private readonly Dictionary<string, string> working = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sourceHashes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, int>> defaults = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ArchiveHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CoverageCase[]> Cases { get; }

        public Inputs()
        {
            string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_TEXTURE_FIXTURES");
            Assert.SkipUnless(File.Exists(catalogPath) && Directory.Exists(TestResources.GameDirectory), "Configure the optional effect texture fixture catalog and installed game source.");
            Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath!, Encoding.UTF8))!;
            Assert.SkipUnless(Fixtures.Append("effect-texture-options").All(catalog.ContainsKey), "Configure both optional effect texture archives and their option matrix.");
            string fixtureRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
            string matrixPath = Path.GetFullPath(catalog["effect-texture-options"], fixtureRoot);
            Assert.SkipUnless(File.Exists(matrixPath) && Fixtures.All(fixture => File.Exists(Path.GetFullPath(catalog[fixture], fixtureRoot))), "The optional fixture catalog must resolve to existing archives and an option matrix.");
            Cases = JsonSerializer.Deserialize<Dictionary<string, CoverageCase[]>>(File.ReadAllText(matrixPath, Encoding.UTF8), JsonOptions)!;
            Assert.Equal(Fixtures.Order(StringComparer.OrdinalIgnoreCase), Cases.Keys.Order(StringComparer.OrdinalIgnoreCase));
            Assert.All(Cases.Values, cases => Assert.NotEmpty(cases));
            game = new IsolatedFieldGame();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Mods", "Junction"));
                for (int index = 0; index < Fixtures.Length; index++)
                {
                    string fixture = Fixtures[index];
                    string archive = Path.GetFullPath(catalog[fixture], fixtureRoot);
                    string hash = FileHash(archive);
                    sourceHashes.Add(archive, hash);
                    if (File.Exists(archive + ".ini"))
                    {
                        sourceHashes.Add(archive + ".ini", FileHash(archive + ".ini"));
                    }
                    ArchiveHashes.Add(fixture, hash);
                    string destination = Path.Combine(root, "Mods", "Junction", $"fixture-{index + 1}.iroj");
                    File.Copy(archive, destination);
                    working.Add(fixture, destination);
                    defaults.Add(fixture, new(JunctionModImporter.Open(destination).Manifest.Options, StringComparer.OrdinalIgnoreCase));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string WorkingArchive(string fixture) => working[fixture];
        public string PackageId(string fixture) => "Junction/" + Path.GetFileName(working[fixture]);
        public GameResourceHost Open() => new(game.GameDirectory, applicationBaseDirectory: Path.Combine(root, "Application"), watch: false, modsDirectory: Path.Combine(root, "Mods"));

        public void SelectOnly(string fixture, Dictionary<string, int> options)
        {
            foreach (string candidate in Fixtures)
            {
                WriteSettings(candidate, candidate == fixture, candidate == fixture ? options : defaults[candidate]);
            }
        }

        public void SelectBothDefaults()
        {
            foreach (string fixture in Fixtures)
            {
                WriteSettings(fixture, true, defaults[fixture]);
            }
        }

        public void MoveLaterInDiscoveryOrder(string fixture)
        {
            string original = working[fixture];
            string destination = Path.Combine(Path.GetDirectoryName(original)!, "fixture-z.iroj");
            File.Move(original, destination);
            File.Move(original + ".ini", destination + ".ini");
            working[fixture] = destination;
        }

        private void WriteSettings(string fixture, bool enabled, Dictionary<string, int> options)
        {
            IEnumerable<string> lines = new[] { "enabled = " + (enabled ? "1" : "0") }.Concat(options.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"option.{pair.Key} = {pair.Value.ToString(CultureInfo.InvariantCulture)}"));
            File.WriteAllLines(working[fixture] + ".ini", lines, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            try
            {
                foreach ((string path, string hash) in sourceHashes)
                {
                    Assert.Equal(hash, FileHash(path));
                }
                foreach ((string fixture, string path) in working)
                {
                    Assert.Equal(ArchiveHashes[fixture], FileHash(path));
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                finally
                {
                    game.Dispose();
                }
            }
        }
    }
}
