using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionFieldResourceInstalledTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedFieldGroupsReachHostLogicResourcesAndRetainPresentationMods(bool archive)
    {
        using PackageFixture fixture = new(archive);
        using GameResourceHost host = fixture.Open();
        using IResourceSource original = GameDataSourceFactory.Open(fixture.GameDirectory);
        using GameAssets assets = new(fixture.GameDirectory, resources: host.Resources);
        Evidence(archive ? "field-resource-initial-host-archive.json" : "field-resource-initial-host.json", new { expectedResources = fixture.Expected.Count, diagnostics = host.Mods.Diagnostics, packages = host.Mods.Catalog.Packages });
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == fixture.PackageId).Status);
        string bundledRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "BundledMods")) + Path.DirectorySeparatorChar;
        Assert.All(host.Mods.Catalog.Packages, package => Assert.True(package.Id == fixture.PackageId || Path.GetFullPath(package.RootDirectory).StartsWith(bundledRoot, StringComparison.OrdinalIgnoreCase), $"Unexpected package '{package.Id}' from '{package.RootDirectory}'."));
        Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidFieldResource");
        Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource");
        List<object> observations = [];
        foreach (FieldGroup group in fixture.Groups)
        {
            foreach (ResourceCase resource in group.Resources.Where(resource => resource.Role == "runtime-resource"))
            {
                byte[] bytes = host.Resources.Read(new(resource.Canonical));
                Assert.Equal(resource.Sha256, Hash(bytes));
                Assert.Equal(fixture.Expected[resource.Canonical], bytes);
            }
            FieldLogicResources logic = assets.ReadFieldLogicResources(group.Field);
            Assert.Equal(group.Field, logic.Name);
            string prefix = $"field/{group.Field}/{group.Field}";
            Assert.Equal(Expected(prefix + ".jsm"), logic.Program);
            MsdFile texts = MsdFile.Read(Expected(prefix + ".msd"), FF8Encoding.CreateEuropean());
            Assert.Equal(texts.Texts, logic.Messages);
            Assert.Equal(JsonSerializer.Serialize(FieldInformation.Read(Expected(prefix + ".inf"))), JsonSerializer.Serialize(logic.Information));
            JsmProgram program = JsmProgram.Read(logic.Program);
            JsmResourceValidationResult validation = JsmResourceValidation.Validate(logic.Program, logic.Messages.Count);
            Assert.Empty(validation.Errors);
            observations.Add(new { group.Field, logic.Id, program.Operations.Count, objects = program.Objects.Count, texts = logic.Messages.Count, program = Hash(logic.Program), validation.DynamicMessageReferences, validation.UnprovenStackOperations, validation.UnreachableUnsupportedOperations, selected = group.Resources.Where(resource => resource.Role == "runtime-resource").Select(resource => new { resource.Canonical, resource.Sha256 }) });
        }
        OwnedFieldScenePatch[] presentation = host.SceneMods.Patches.Where(patch => patch.PackageId == "Memoria/Depixelation").ToArray();
        Assert.NotEmpty(presentation);
        foreach (OwnedFieldScenePatch owned in presentation)
        {
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Memoria.InvalidFieldResource" && item.PackageId == owned.PackageId);
            FieldLogicResources logic = assets.ReadFieldLogicResources(owned.Patch.FieldName);
            Assert.Equal(owned.Patch.FieldId, logic.Id);
            Assert.Equal(Expected($"field/{logic.Name}/{logic.Name}.jsm"), logic.Program);
        }
        Evidence(archive ? "field-resource-installed-archive.json" : "field-resource-installed.json", new { archive, selectedResources = fixture.Expected.Count, fields = fixture.Groups.Length, observations, presentation = presentation.Select(patch => new { patch.PackageId, patch.RelativePath, patch.Patch.FieldName }), diagnostics = host.Mods.Diagnostics, scope = "Host resource and native field-logic acquisition; no gameplay state advanced." });

        byte[] Expected(string key) => fixture.Expected.TryGetValue(key, out byte[]? value) ? value : Read(original, new(key));
    }

    [Fact]
    public void AcquiredFieldResourcesRemainFrozenAndRequireRestartForChangedBytes()
    {
        using PackageFixture fixture = new();
        ResourceCase selected = fixture.Groups.SelectMany(group => group.Resources).First(resource => resource.Format == "msd");
        ResourceKey key = new(selected.Canonical);
        using GameResourceHost host = fixture.Open();
        byte[] before = host.Resources.Read(key);
        host.Mods.Refresh();
        Assert.True(host.Pump(true));
        Assert.Equal(before, host.Resources.Read(key));
        long generation = host.Resources.Generation;
        long catalogGeneration = host.Mods.Catalog.Generation;
        FieldSceneMods sceneMods = host.SceneMods;
        MsdFile table = MsdFile.Read(before, FF8Encoding.CreateEuropean());
        table.Texts[0] = "Updated field resource{End}";
        byte[] changed = table.Write();
        Assert.NotEqual(Hash(before), Hash(changed));
        File.WriteAllBytes(fixture.CopyPath(selected.Source), changed);
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Equal(catalogGeneration, host.Mods.Catalog.Generation);
        Assert.Same(sceneMods, host.SceneMods);
        Assert.Equal(before, host.Resources.Read(key));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "resource-reload-required");
        using (GameResourceHost restarted = fixture.Open())
        {
            Assert.Equal(changed, restarted.Resources.Read(key));
        }
        fixture.SetEnabled(false);
        using GameResourceHost disabled = fixture.Open();
        using IResourceSource source = GameDataSourceFactory.Open(fixture.GameDirectory);
        Assert.Equal(Read(source, key), disabled.Resources.Read(key));
        Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(disabled.Mods.Catalog.Packages, package => package.Id == fixture.PackageId).Status);
        Evidence("field-resource-restart.json", new { selected.Canonical, original = Hash(before), changed = Hash(changed), frozen = true, restartRequired = true, restartedUsesChangedBytes = true, disabledUsesInstalledOriginal = true, diagnostics = host.Mods.Diagnostics });
    }

    [Fact]
    public void MalformedSelectedTextReferencesRejectTheAtomicUpdateWithOwningDiagnostics()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        using GameAssets assets = new(fixture.GameDirectory, resources: host.Resources);
        ResourceCase? selected = null;
        FieldLogicResources? selectedLogic = null;
        foreach (FieldGroup group in fixture.Groups)
        {
            ResourceCase? text = group.Resources.FirstOrDefault(resource => resource.Format == "msd");
            if (text is null)
            {
                continue;
            }
            FieldLogicResources logic = assets.ReadFieldLogicResources(group.Field);
            if (logic.Messages.Count > 1 && JsmResourceValidation.Validate(logic.Program, 1).Errors.Count > 0)
            {
                selected = text;
                selectedLogic = logic;
                break;
            }
        }
        Assert.NotNull(selected);
        Assert.NotNull(selectedLogic);
        long generation = host.Resources.Generation;
        long catalogGeneration = host.Mods.Catalog.Generation;
        FieldSceneMods sceneMods = host.SceneMods;
        ResourceKey key = new(selected.Canonical);
        byte[] previous = host.Resources.Read(key);
        MsdFile invalid = new(FF8Encoding.CreateEuropean());
        invalid.Texts.Add("Only message{End}");
        File.WriteAllBytes(fixture.CopyPath(selected.Source), invalid.Write());
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Equal(catalogGeneration, host.Mods.Catalog.Generation);
        Assert.Same(sceneMods, host.SceneMods);
        Assert.Equal(previous, host.Resources.Read(key));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidFieldResource" && diagnostic.PackageId == fixture.PackageId && diagnostic.RelativePath is not null);
        Assert.All(host.Mods.Diagnostics.Where(diagnostic => diagnostic.Code == "Junction.InvalidFieldResource"), diagnostic => Assert.Equal(fixture.PackageId, diagnostic.PackageId));
        Evidence("field-resource-invalid-text.json", new { selected.Canonical, field = selectedLogic.Name, sourceTexts = selectedLogic.Messages.Count, candidateTexts = 1, atomicRollback = true, diagnostics = host.Mods.Diagnostics });
    }

    private static byte[] Read(IResourceSource source, ResourceKey key)
    {
        ResourceContent content = source.Find(key) ?? throw new FileNotFoundException($"Installed resource '{key}' is unavailable.");
        using Stream input = content.OpenRead();
        using MemoryStream output = new();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Evidence(string name, object value)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(root))
        {
            string directory = Path.Combine(root, "FieldResources");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, JsonOptions));
        }
    }

    private sealed record GroupCatalog(FieldGroup[] Groups);
    private sealed record FieldGroup(string Field, ResourceCase[] Resources);
    private sealed record ResourceCase(string Canonical, string Source, string Format, int Bytes, string Sha256, string Role);

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-field-resource-" + Guid.NewGuid().ToString("N"));
        private readonly string destination;
        private readonly string sourceDirectory;
        private readonly IsolatedFieldGame game;
        private readonly string sourceArchive;
        private readonly string sourceArchiveHash;
        private readonly string configurationPath;
        public string PackageId { get; }
        public string GameDirectory => game.GameDirectory;
        public FieldGroup[] Groups { get; }
        public Dictionary<string, byte[]> Expected { get; } = new(StringComparer.OrdinalIgnoreCase);

        public PackageFixture(bool archive = false)
        {
            string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_REPLACEMENT_FIXTURE");
            Assert.SkipUnless(File.Exists(catalogPath) && Directory.Exists(TestResources.GameDirectory), "Configure the optional field replacement fixture catalog and original game data.");
            Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath!))!;
            string fixtureRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
            sourceDirectory = Path.GetFullPath(catalog["junction-field-groups-extracted"], fixtureRoot);
            sourceArchive = Path.GetFullPath(catalog["junction-field-groups"], fixtureRoot);
            sourceArchiveHash = Hash(File.ReadAllBytes(sourceArchive));
            string groupPath = Path.GetFullPath(catalog["junction-field-group-catalog"], fixtureRoot);
            Groups = JsonSerializer.Deserialize<GroupCatalog>(File.ReadAllText(groupPath), JsonOptions)!.Groups;
            Assert.NotEmpty(Groups);
            PackageId = archive ? "Junction/archive.iroj" : "Junction/candidate";
            destination = archive ? Path.Combine(root, "working-package") : Path.Combine(root, "Junction", "candidate");
            string archivePath = Path.Combine(root, "Junction", "archive.iroj");
            configurationPath = archive ? archivePath + ".ini" : Path.Combine(destination, "mod.ini");
            foreach (string path in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string copy = CopyPath(Path.GetRelativePath(sourceDirectory, path));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(path, copy);
            }
            if (archive)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                File.Copy(sourceArchive, archivePath);
            }
            SetEnabled(true);
            foreach (ResourceCase resource in Groups.SelectMany(group => group.Resources).Where(resource => resource.Role == "runtime-resource"))
            {
                byte[] bytes = File.ReadAllBytes(CopyPath(resource.Source));
                Assert.Equal(resource.Bytes, bytes.Length);
                Assert.Equal(resource.Sha256, Hash(bytes));
                Expected.Add(resource.Canonical, bytes);
            }
            Assert.NotEmpty(Expected);
            game = new IsolatedFieldGame();
        }

        public string CopyPath(string relative) => Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
        public GameResourceHost Open() => new(GameDirectory, watch: false, modsDirectory: root);
        public void SetEnabled(bool value) => File.WriteAllText(configurationPath, value ? "enabled = 1" : "enabled = 0");

        public void Dispose()
        {
            try
            {
                Assert.Equal(sourceArchiveHash, Hash(File.ReadAllBytes(sourceArchive)));
                foreach (ResourceCase resource in Groups.SelectMany(group => group.Resources).Where(resource => resource.Role == "runtime-resource"))
                {
                    Assert.Equal(resource.Sha256, Hash(File.ReadAllBytes(Path.Combine(sourceDirectory, resource.Source.Replace('/', Path.DirectorySeparatorChar)))));
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

/// <summary>Isolates optional installed-resource checks from the installed Mods directory.</summary>
internal sealed class IsolatedFieldGame : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-field-game-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> links = [];
    private readonly Dictionary<string, string> sourceHashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool externallySupplied;
    private bool disposed;

    public string GameDirectory => root;

    public IsolatedFieldGame()
    {
        string sourceDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(sourceDirectory), "Configure the optional original game data.");
        using IResourceSource source = GameDataSourceFactory.Open(sourceDirectory);
        Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "Native Classic field replacement checks require the Classic source profile.");
        string data = Path.Combine(sourceDirectory, "Data");
        string executable = Path.Combine(sourceDirectory, "FF8_EN.exe");
        Assert.SkipUnless(Directory.Exists(data) && File.Exists(executable), "The original Classic Data directory and native executable are required.");
        string? configured = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_ISOLATED_GAME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            root = Path.GetFullPath(configured);
            externallySupplied = true;
            Assert.True(Directory.Exists(Path.Combine(root, "Data")));
            Assert.False(Directory.Exists(Path.Combine(root, "Mods")), "The optional isolated game directory must not contain installed mods.");
            Assert.NotEqual(Path.GetFullPath(sourceDirectory), root);
            foreach (string filename in new[] { "FF8_EN.exe", "AF3DN.P", "AF4DN.P" })
            {
                string original = Path.Combine(sourceDirectory, filename);
                if (File.Exists(original))
                {
                    string expected = FileHash(original);
                    string copy = Path.Combine(root, filename);
                    Assert.Equal(expected, FileHash(copy));
                    sourceHashes.Add(original, expected);
                    sourceHashes.Add(copy, expected);
                }
            }
            return;
        }
        Directory.CreateDirectory(root);
        try
        {
            LinkDirectory(data, "Data");
            foreach (string directory in new[] { "Extraction", "DEMASTER_EXP" })
            {
                string path = Path.Combine(sourceDirectory, directory);
                if (Directory.Exists(path))
                {
                    LinkDirectory(path, directory);
                }
            }
            foreach (string filename in new[] { "FF8_EN.exe", "AF3DN.P", "AF4DN.P" })
            {
                string path = Path.Combine(sourceDirectory, filename);
                if (File.Exists(path))
                {
                    sourceHashes.Add(path, FileHash(path));
                    File.Copy(path, Path.Combine(root, filename));
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Dispose();
            Assert.Skip($"The optional isolated game instance requires directory symbolic links: {exception.Message}");
        }
    }

    private void LinkDirectory(string source, string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateSymbolicLink(path, source);
        links.Add(path);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        try
        {
            foreach ((string path, string expected) in sourceHashes)
            {
                Assert.Equal(expected, FileHash(path));
            }
        }
        finally
        {
            foreach (string link in links)
            {
                Directory.Delete(link, false);
            }
            if (!externallySupplied && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string FileHash(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }
}
