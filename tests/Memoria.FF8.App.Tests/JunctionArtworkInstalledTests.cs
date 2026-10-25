using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionArtworkInstalledTests
{
    private static readonly string[] Roles = ["world-map", "menu-portraits", "menu-buttons", "card-artwork-names"];

    [Fact]
    public void ExternalArtworkPackagesPublishTogetherWithoutUnsupportedTargets()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        foreach (string archive in input.Archives.Values)
        {
            ModPackage package = input.Package(host, archive);
            Assert.Equal(ModEffectiveStatus.Active, package.Status);
            Assert.Contains(host.Mods.Catalog.Files, file => file.PackageId == package.Id);
            Console.WriteLine($"junction-artwork=Active name={package.Name} entries={host.Mods.Catalog.Files.Count(file => file.PackageId == package.Id)}");
        }
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "ffnx-unsupported-target" or "junction-import-failed" or "Junction.InvalidTextResource" || diagnostic.Code.StartsWith("junction-dependency-", StringComparison.Ordinal));
    }

    [Fact]
    public void PortraitAndCardConsumersRefreshThroughTheSameCachedArtworkProvider()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        input.Select(host);
        MenuArtworkProvider artwork = new(TestResources.GameDirectory, host.Resources);
        UiImageSource portrait = new(UiImageKind.CharacterPortrait, 0);
        UiImageSource card = new(UiImageKind.Card, 0);
        string originalPortrait = Signature(artwork.Read(portrait));
        string originalCard = Signature(artwork.Read(card));
        input.Select(host, "menu-portraits");
        Assert.NotEqual(originalPortrait, Signature(artwork.Read(portrait)));
        Assert.NotEqual(originalCard, Signature(artwork.Read(card)));
        input.Select(host);
        Assert.Equal(originalPortrait, Signature(artwork.Read(portrait)));
        Assert.Equal(originalCard, Signature(artwork.Read(card)));
        input.Select(host, "card-artwork-names");
        Assert.NotEqual(originalCard, Signature(artwork.Read(card)));
        Assert.Equal(originalPortrait, Signature(artwork.Read(portrait)));
        input.Select(host);
        Assert.Equal(originalCard, Signature(artwork.Read(card)));
    }

    [Fact]
    public void ControllerGlyphsAndPrintedButtonImagesUseTheSparseReplacementPages()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        input.Select(host);
        MenuArtworkProvider artwork = new(TestResources.GameDirectory, host.Resources);
        UiImageSource[] buttons = Enumerable.Range(0, 12).Select(bit => new UiImageSource(UiImageKind.MagazineButton, bit)).ToArray();
        string[] original = buttons.Select(button => Signature(artwork.Read(button))).ToArray();
        input.Select(host, "menu-buttons");
        string[] replacement = buttons.Select(button => Signature(artwork.Read(button))).ToArray();
        Assert.Contains(Enumerable.Range(0, buttons.Length), bit => original[bit] != replacement[bit]);
        NativeMenuArtwork native = NativeMenuArtwork.Load(TestResources.GameDirectory, host.Resources);
        for (int bit = 0; bit < buttons.Length; bit++)
        {
            Assert.Equal(replacement[bit], Signature(native.GetControllerButtonIcon(128 + bit)));
        }
        input.Select(host);
        Assert.Equal(original, buttons.Select(button => Signature(artwork.Read(button))).ToArray());
    }

    [Fact]
    public void CardNamesPublishToExistingMenuAndTripleTriadCatalogsAndRestoreOnDisable()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        input.Select(host);
        using GameAssets assets = new(TestResources.GameDirectory, resources: host.Resources);
        LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
        TripleTriadCatalog triad = TripleTriadCatalog.Load(TestResources.GameDirectory, host.Resources);
        string?[] originalMenu = Enumerable.Range(0, 110).Select(menu.CardName).ToArray();
        string[] originalTriad = Enumerable.Range(0, 110).Select(triad.Name).ToArray();
        input.Select(host, "card-artwork-names");
        File.AppendAllText(input.Archives["card-artwork-names"] + ".ini", Environment.NewLine + File.ReadAllText(TestResources.FixturePath("junction-card-name-settings")));
        host.Mods.RequestRefresh();
        host.Pump(true);
        StaticTextResource text = Assert.IsType<StaticTextResource>(host.Resources.FindText(StaticTextResource.CardNames));
        Assert.Equal(110, text.Texts.Length);
        Assert.Equal(input.Package(host, input.Archives["card-artwork-names"]).Id, text.PackageId);
        Assert.Equal(text.Texts.ToArray(), Enumerable.Range(0, 110).Select(menu.CardName).ToArray());
        Assert.Equal(text.Texts.ToArray(), Enumerable.Range(0, 110).Select(triad.Name).ToArray());
        input.Select(host);
        Assert.Null(host.Resources.FindText(StaticTextResource.CardNames));
        Assert.Equal(originalMenu, Enumerable.Range(0, 110).Select(menu.CardName).ToArray());
        Assert.Equal(originalTriad, Enumerable.Range(0, 110).Select(triad.Name).ToArray());
    }

    [Fact]
    public void NativeMapPalettesChangeAndRestoreWithoutRecreatingWorldAssets()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        input.Select(host);
        using WorldAssets world = new(TestResources.GameDirectory, host.Resources);
        string[] original = Enumerable.Range(0, 2).Select(palette => Signature(world.ReadNativeSectionTexture(38, 11, palette))).ToArray();
        input.Select(host, "world-map");
        for (int palette = 0; palette < original.Length; palette++)
        {
            Assert.NotEqual(original[palette], Signature(world.ReadNativeSectionTexture(38, 11, palette)));
        }
        input.Select(host);
        Assert.Equal(original, Enumerable.Range(0, 2).Select(palette => Signature(world.ReadNativeSectionTexture(38, 11, palette))).ToArray());
    }

    [Fact]
    public void AlternateOutcomePageRefreshesTheCachedCompletedMatchArtwork()
    {
        using Inputs input = new();
        using GameResourceHost host = input.Host();
        input.Select(host);
        MenuArtworkProvider artwork = new(TestResources.GameDirectory, host.Resources);
        UiImageSource[] outcomes = Enumerable.Range(0, 3).Select(id => new UiImageSource(UiImageKind.TripleTriadOutcome, id)).ToArray();
        string[] original = outcomes.Select(outcome => Signature(artwork.Read(outcome))).ToArray();
        input.Select(host, "card-artwork-names");
        string[] defaults = outcomes.Select(outcome => Signature(artwork.Read(outcome))).ToArray();
        File.AppendAllText(input.Archives["card-artwork-names"] + ".ini", Environment.NewLine + File.ReadAllText(TestResources.FixturePath("junction-card-outcome-settings")));
        host.Mods.RequestRefresh();
        host.Pump(true);
        string[] alternate = outcomes.Select(outcome => Signature(artwork.Read(outcome))).ToArray();
        for (int id = 0; id < outcomes.Length; id++)
        {
            Assert.NotEqual(defaults[id], alternate[id]);
        }
        input.Select(host);
        Assert.Equal(original, outcomes.Select(outcome => Signature(artwork.Read(outcome))).ToArray());
    }

    [Fact]
    public void ExternalDeclaredOptionVariantsKeepSupportedResourcesAndValidTextTables()
    {
        using Inputs input = new();
        Dictionary<string, Dictionary<string, int>[]> cases = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>[]>>(File.ReadAllText(TestResources.FixturePath("junction-resource-option-cases")))!;
        using GameResourceHost host = input.Host();
        foreach ((string role, Dictionary<string, int>[] variants) in cases)
        {
            input.Select(host, role);
            foreach (Dictionary<string, int> options in variants)
            {
                string configuration = "enabled = 1" + Environment.NewLine + string.Join(Environment.NewLine, options.Select(option => $"option.{option.Key} = {option.Value}"));
                File.WriteAllText(input.Archives[role] + ".ini", configuration);
                host.Mods.RequestRefresh();
                host.Pump(true);
                ModPackage package = input.Package(host, input.Archives[role]);
                Assert.True(package.Status is ModEffectiveStatus.Active or ModEffectiveStatus.NoUsableEntries, $"{role}: {package.Status}; {configuration}");
                Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "ffnx-unsupported-target" or "junction-import-failed" or "Junction.InvalidTextResource");
            }
            Console.WriteLine($"junction-options=Validated role={role} cases={variants.Length}");
        }
    }

    private static string Signature(PixelImage image)
    {
        return $"{image.Width}x{image.Height}:" + Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan())));
    }

    private sealed class Inputs : IDisposable
    {
        private readonly Dictionary<string, byte[]?> configurations;
        private readonly string mods;

        public Inputs()
        {
            Archives = Roles.ToDictionary(role => role, role => TestResources.FixturePath("junction-" + role));
            string? directory = Path.GetDirectoryName(Archives.Values.First());
            while (directory is not null && !Path.GetFileName(directory).Equals("Junction", StringComparison.OrdinalIgnoreCase))
            {
                directory = Path.GetDirectoryName(directory);
            }
            Assert.NotNull(directory);
            mods = Directory.GetParent(directory)!.FullName;
            Assert.All(Archives.Values, archive => Assert.StartsWith(directory + Path.DirectorySeparatorChar, archive, StringComparison.OrdinalIgnoreCase));
            configurations = Archives.Values.ToDictionary(path => path + ".ini", path => File.Exists(path + ".ini") ? File.ReadAllBytes(path + ".ini") : null);
        }

        public Dictionary<string, string> Archives { get; }

        public GameResourceHost Host()
        {
            return new GameResourceHost(TestResources.GameDirectory, Path.Combine(mods, "test-application"), watch: false, modsDirectory: mods);
        }

        public ModPackage Package(GameResourceHost host, string archive)
        {
            return Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(archive, StringComparison.OrdinalIgnoreCase));
        }

        public void Select(GameResourceHost host, params string[] roles)
        {
            foreach ((string role, string archive) in Archives)
            {
                Assert.True(host.Mods.SetEnabled(Package(host, archive).Id, roles.Contains(role, StringComparer.Ordinal)).Success);
            }
            host.Pump(true);
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "Junction.InvalidTextResource");
        }

        public void Dispose()
        {
            foreach ((string path, byte[]? content) in configurations)
            {
                if (content is null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, content);
                }
            }
        }
    }
}
