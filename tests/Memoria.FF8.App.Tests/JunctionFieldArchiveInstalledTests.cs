using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionFieldArchiveInstalledTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedArchivePublishesCompleteFrozenDirectoryAndLogic(bool archive)
    {
        using PackageFixture fixture = new(archive);
        using GameResourceHost host = fixture.Open();
        Evidence(archive ? "initial-archive.json" : "initial-folder.json", new { packages = host.Mods.Catalog.Packages, diagnostics = host.Mods.Diagnostics });
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == fixture.PackageId).Status);
        AssertOnlyExpectedPackages(host, fixture.PackageId);
        Assert.All(host.Mods.Diagnostics.Where(item => item.PackageId == fixture.PackageId), item => Assert.Equal("Junction.MissingFieldColors", item.Code));
        Assert.True(JunctionEncodedResources.TryPrepare(host.Mods.Catalog, out IReadOnlyList<EncodedResource> encoded, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(diagnostics);
        EncodedResource[] selected = encoded.Where(resource => fixture.Expected.ContainsKey(resource.Key.Path)).ToArray();
        Assert.Equal(fixture.Expected.Count, selected.Length);
        using IResourceSource original = GameDataSourceFactory.Open(fixture.GameDirectory);
        using GameAssets assets = new(fixture.GameDirectory, resources: host.Resources);
        List<object> observations = [];
        foreach (ArchiveCase group in fixture.Archives)
        {
            ResourceKey scope = new($"field/{group.Field}");
            foreach ((string key, byte[] bytes) in fixture.Expected.Where(pair => pair.Key.StartsWith(scope.Path + "/", StringComparison.Ordinal) || pair.Key.StartsWith(scope.Path + ".", StringComparison.Ordinal)))
            {
                EncodedResource resource = Assert.Single(selected, item => item.Key.Path == key);
                Assert.Equal(scope, resource.ExclusiveScope);
                Assert.Equal(Path.GetExtension(key)[1..], resource.Content.Format);
                Assert.Equal(Hash(bytes), resource.Fingerprint);
                Assert.StartsWith(fixture.PackageId + ":", resource.Content.Provenance);
                Assert.Equal(bytes, host.Resources.Read(new(key)));
                using Stream stream = resource.Content.OpenRead();
                using MemoryStream copy = new();
                stream.CopyTo(copy);
                Assert.Equal(bytes, copy.ToArray());
                if (!group.OriginalArchivePresent)
                {
                    Assert.Null(original.Find(new(key)));
                }
            }
            IReadOnlyList<FieldArchiveMember> directory = FieldArchiveSnapshot.Read(group.Field, fixture.Expected[$"field/{group.Field}.fl"], fixture.Expected[$"field/{group.Field}.fi"], fixture.Expected[$"field/{group.Field}.fs"]);
            Assert.Equal(group.Members.Length, directory.Count);
            foreach (FieldArchiveMember member in directory)
            {
                MemberCase expected = Assert.Single(group.Members, item => item.Member == member.Name);
                Assert.Equal(expected.Length, member.Data.Length);
                Assert.Equal(expected.Sha256, Hash(member.Data));
                Assert.Equal(member.Data, host.Resources.Read(new(expected.Canonical)));
            }
            ModDiagnostic warning = Assert.Single(host.Mods.Diagnostics, item => item.PackageId == fixture.PackageId && item.Code == "Junction.MissingFieldColors" && item.RelativePath == group.PathPrefix + ".fs");
            Assert.NotNull(warning.RelativePath);
            Assert.Null(original.Find(new($"field/{group.Field}/{group.Field}.pcb")));
            FieldAssets native = assets.LoadField(group.NativeFieldId);

            Assert.NotNull(native.NativeRegistration);
            Assert.NotNull(native.NativeRegistration.Loader);
            Assert.Null(native.NativeRegistration.Loader.Colors);
            FieldLogicResources logic = assets.ReadFieldLogicResources(group.Field);
            Assert.False(logic.HasColorCorrections);
            Assert.Equal(group.NativeFieldId, logic.Id);
            Assert.Equal(fixture.Expected[$"field/{group.Field}/{group.Field}.jsm"], logic.Program);
            JsmResourceValidationResult validation = JsmResourceValidation.Validate(logic.Program, logic.Messages.Count);
            Assert.Empty(validation.Errors);
            observations.Add(new { group.Field, logic.Id, members = directory.Count, texts = logic.Messages.Count, validation.DynamicMessageReferences, validation.UnprovenStackOperations, validation.UnreachableUnsupportedOperations });
        }
        Evidence(archive ? "accepted-archive.json" : "accepted-folder.json", new { archive, selected = selected.Select(item => new { item.Key.Path, item.Content.Format, item.Content.Provenance, item.Fingerprint, scope = item.ExclusiveScope?.Path }), observations, diagnostics = host.Mods.Diagnostics, scope = "Archive/resource acquisition and field logic only; no gameplay or save conversion." });
    }

    [Fact]
    public void MalformedArchiveRetainsAcquiredPublicationAndOwningDiagnostics()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        ArchiveCase group = fixture.Archives[0];
        ResourceKey member = new(group.Members[0].Canonical);
        byte[] acquired = host.Resources.Read(member);
        long generation = host.Resources.Generation;
        long catalogGeneration = host.Mods.Catalog.Generation;
        FieldSceneMods sceneMods = host.SceneMods;
        string index = fixture.CopyPath(group.PathPrefix + ".fi");
        File.WriteAllBytes(index, [1]);
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Equal(catalogGeneration, host.Mods.Catalog.Generation);
        Assert.Same(sceneMods, host.SceneMods);
        foreach ((string key, byte[] bytes) in fixture.Expected)
        {
            Assert.Equal(bytes, host.Resources.Read(new(key)));
        }
        Assert.Equal(acquired, host.Resources.Read(member));
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource" && item.PackageId == fixture.PackageId && item.RelativePath is not null);
        Assert.All(host.Mods.Diagnostics.Where(item => item.Code == "Junction.InvalidEncodedResource"), item => Assert.Equal(fixture.PackageId, item.PackageId));
        string output = fixture.CopyPath("rejected-import.json");
        Assert.Throws<InvalidDataException>(() => SaveTransferCommand.Run([fixture.NativeSave, "--output", output, "--game-dir", fixture.GameDirectory, "--mods-dir", fixture.ModsDirectory], import: true));
        Assert.False(File.Exists(output));
        Evidence("malformed-archive-rollback.json", new { group.Field, acquired = member.Path, generation, atomicRollback = true, diagnostics = host.Mods.Diagnostics });
    }

    [Fact]
    public void PresentMalformedColorMemberRejectsUpdateInsteadOfUsingAbsencePolicy()
    {
        using PackageFixture fixture = new();
        using GameResourceHost host = fixture.Open();
        ArchiveCase group = fixture.Archives[0];
        ResourceKey key = new(group.Members[0].Canonical);
        byte[] previous = host.Resources.Read(key);
        long generation = host.Resources.Generation;
        long catalogGeneration = host.Mods.Catalog.Generation;
        string prefix = fixture.CopyPath(group.PathPrefix);
        byte[] index = File.ReadAllBytes(prefix + ".fi");
        byte[] content = File.ReadAllBytes(prefix + ".fs");
        byte[] changedIndex = new byte[index.Length + 12];
        index.CopyTo(changedIndex, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(changedIndex.AsSpan(index.Length), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(changedIndex.AsSpan(index.Length + 4), checked((uint)content.Length));
        string[] labels = File.ReadAllLines(prefix + ".fl");
        string label = labels.First(line => !string.IsNullOrWhiteSpace(line));
        int separator = Math.Max(label.LastIndexOf('/'), label.LastIndexOf('\\'));
        string colorLabel = label[..(separator + 1)] + group.Field + ".pcb";
        File.WriteAllText(prefix + ".fl", string.Join("\n", labels.Where(line => !string.IsNullOrWhiteSpace(line)).Append(colorLabel)) + "\n", Encoding.ASCII);
        File.WriteAllBytes(prefix + ".fi", changedIndex);
        File.WriteAllBytes(prefix + ".fs", content.Append((byte)0).ToArray());
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.Equal(catalogGeneration, host.Mods.Catalog.Generation);
        Assert.Equal(previous, host.Resources.Read(key));
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidFieldResource" && item.PackageId == fixture.PackageId && item.RelativePath is not null);
        Evidence("malformed-present-colors.json", new { group.Field, atomicRollback = true, diagnostics = host.Mods.Diagnostics });
    }
    [Fact]
    public void SuppliedNativeSaveImportsInitializesAndRestartsStandaloneAfterOneWait()
    {
        string? evidenceRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(evidenceRoot), "Configure an external evidence directory for the separately named native-save checkpoint.");
        using PackageFixture fixture = new();
        string run = Path.Combine(Path.GetFullPath(evidenceRoot!), "FieldArchives", "entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        string mods = Path.Combine(run, "Mods");
        string package = Path.Combine(mods, "Junction", "candidate");
        fixture.CopyPackage(package);
        string imported = Path.Combine(run, "native-entry-imported.json");
        string checkpoint = Path.Combine(run, "native-entry-one-wait.json");
        string sourceHash = Hash(File.ReadAllBytes(fixture.NativeSave));
        string phase = "native-save import and validated field initialization";
        try
        {
            Assert.Equal(0, SaveTransferCommand.Run([fixture.NativeSave, "--output", imported, "--game-dir", fixture.GameDirectory, "--mods-dir", mods], import: true));
            phase = "fresh host acquisition and one ordinary wait";
            GameSession session = new(GameSession.Load(imported));
            Assert.Contains(fixture.Archives, group => group.NativeFieldId == session.State.FieldId && group.Field == session.State.FieldName);
            long initialFrame;
            long continuedFrame;
            JsonElement expectedEvents;
            using (GameResourceHost host = new(fixture.GameDirectory, watch: false, modsDirectory: mods))
            using (GameAssets assets = new(fixture.GameDirectory, resources: host.Resources, modelOverrides: host))
            using (GameWorld world = new(session, fixture.GameDirectory, resources: host.Resources, validateWorldPresentation: false, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host))
            {
                Assert.True(world.EnsureField());
                Assert.Null(world.Events.LastFault);
                foreach ((string key, byte[] bytes) in fixture.Expected)
                {
                    Assert.Equal(bytes, host.Resources.Read(new(key)));
                }
                initialFrame = world.Events.Frame;
                world.Apply(new PlayerAction(PlayerActionKind.Wait));
                Assert.Null(world.Events.LastFault);
                continuedFrame = world.Events.Frame;
                Assert.Equal(initialFrame + 1, continuedFrame);
                expectedEvents = world.Events.Capture();
                session.SetEventState(expectedEvents);
                session.Save(checkpoint, compact: true);
            }
            phase = "standalone fresh-host restart";
            GameSession restored = new(GameSession.Load(checkpoint));
            using (GameResourceHost host = new(fixture.GameDirectory, watch: false, modsDirectory: mods))
            using (GameAssets assets = new(fixture.GameDirectory, resources: host.Resources, modelOverrides: host))
            using (GameWorld world = new(restored, fixture.GameDirectory, resources: host.Resources, validateWorldPresentation: false, resolveGameFile: host.ResolveGameFile, sharedAssets: assets, readSceneMods: () => host.SceneMods, modelOverrides: host))
            {
                Assert.True(world.EnsureField());
                Assert.Equal(continuedFrame, world.Events.Frame);
                Assert.True(JsonElement.DeepEquals(expectedEvents, world.Events.Capture()));
            }
            string resume = $"dotnet \"{typeof(GameWorld).Assembly.Location}\" --game-dir \"{fixture.GameDirectory}\" --mods-dir \"{mods}\" --state \"{checkpoint}\"";
            File.WriteAllText(Path.Combine(run, "continuation.json"), JsonSerializer.Serialize(new { source = fixture.NativeSave, sourceHash, imported, checkpoint, session.State.FieldId, session.State.FieldName, initialFrame, continuedFrame, ordinaryWaits = 1, resumedWithoutAdvancing = true, resume, mods }, JsonOptions));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(run, "unsupported-boundary.json"), JsonSerializer.Serialize(new { phase, source = fixture.NativeSave, sourceHash, imported = File.Exists(imported) ? imported : null, checkpoint = File.Exists(checkpoint) ? checkpoint : null, exception = exception.ToString(), policy = "No native state or instruction changed to bypass this boundary." }, JsonOptions));
            throw;
        }
        finally
        {
            Assert.Equal(sourceHash, Hash(File.ReadAllBytes(fixture.NativeSave)));
        }
    }
    [Fact]
    public void ShadowedArchiveSaveAttachmentDoesNotKeepThePackageActive()
    {
        using PackageFixture fixture = new();
        fixture.AddPackageCopy("shadowed");
        using GameResourceHost host = fixture.Open();
        Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(package => package.Id == fixture.PackageId).Status);
        Assert.Equal(ModEffectiveStatus.NoUsableEntries, host.Mods.Catalog.Packages.Single(package => package.Id == "Junction/shadowed").Status);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.FieldGroupConflict" && diagnostic.PackageId == "Junction/shadowed");
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.MissingFieldColors" && diagnostic.PackageId == "Junction/shadowed");
        foreach (ArchiveCase field in fixture.Archives)
        {
            Assert.StartsWith(fixture.PackageId + ":", host.Resources.Get(new($"field/{field.Field}/{field.Field}.jsm")).Provenance);
        }
        host.Mods.RequestRefresh();
        host.Pump(true);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.FieldGroupConflict" && diagnostic.PackageId == "Junction/shadowed");
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.MissingFieldColors" && diagnostic.PackageId == fixture.PackageId);
    }

    private static void AssertOnlyExpectedPackages(GameResourceHost host, string packageId)
    {
        string bundledRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "BundledMods")) + Path.DirectorySeparatorChar;
        Assert.All(host.Mods.Catalog.Packages, package => Assert.True(package.Id == packageId || Path.GetFullPath(package.RootDirectory).StartsWith(bundledRoot, StringComparison.OrdinalIgnoreCase), $"Unexpected package '{package.Id}' from '{package.RootDirectory}'."));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Evidence(string name, object value)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(root))
        {
            string directory = Path.Combine(root, "FieldArchives");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, JsonOptions));
        }
    }

    private sealed record ArchiveCatalog(ArchiveCase[] Archives);
    private sealed record ArchiveCase(string Field, int NativeFieldId, string PathPrefix, bool OriginalArchivePresent, MemberCase[] Members);
    private sealed record MemberCase(string Member, string Canonical, int Length, string Sha256);

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-field-archive-" + Guid.NewGuid().ToString("N"));
        private readonly IsolatedFieldGame game;
        private readonly string destination;
        private readonly string sourceDirectory;
        private readonly string sourceArchive;
        private readonly string sourceArchiveHash;
        private readonly Dictionary<string, string> sourceHashes = new(StringComparer.OrdinalIgnoreCase);
        public string PackageId { get; }
        public string GameDirectory => game.GameDirectory;
        public string ModsDirectory => root;
        public ArchiveCase[] Archives { get; }
        public string NativeSave { get; }
        public Dictionary<string, byte[]> Expected { get; } = new(StringComparer.OrdinalIgnoreCase);

        public PackageFixture(bool archive = false)
        {
            string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_ARCHIVE_FIXTURE");
            Assert.SkipUnless(File.Exists(catalogPath) && Directory.Exists(TestResources.GameDirectory), "Configure the optional field archive fixture catalog and original game data.");
            string fixtureRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
            Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath!))!;
            NativeSave = Path.GetFullPath(catalog["native-field-entry-save"], fixtureRoot);
            sourceHashes.Add(NativeSave, Hash(File.ReadAllBytes(NativeSave)));
            sourceDirectory = Path.GetFullPath(catalog["junction-field-archives-extracted"], fixtureRoot);
            sourceArchive = Path.GetFullPath(catalog["junction-field-archives"], fixtureRoot);
            sourceArchiveHash = Hash(File.ReadAllBytes(sourceArchive));
            Archives = JsonSerializer.Deserialize<ArchiveCatalog>(File.ReadAllText(Path.GetFullPath(catalog["junction-field-archive-catalog"], fixtureRoot)), JsonOptions)!.Archives;
            Assert.NotEmpty(Archives);
            PackageId = archive ? "Junction/archive.iroj" : "Junction/candidate";
            destination = Path.Combine(root, archive ? "working-package" : "Junction/candidate");
            foreach (string path in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                sourceHashes.TryAdd(path, Hash(File.ReadAllBytes(path)));
                string copy = CopyPath(Path.GetRelativePath(sourceDirectory, path));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(path, copy);
            }
            string configuration;
            if (archive)
            {
                string target = Path.Combine(root, "Junction", "archive.iroj");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(sourceArchive, target);
                configuration = target + ".ini";
            }
            else
            {
                configuration = CopyPath("mod.ini");
            }
            File.WriteAllText(configuration, "enabled = 1");
            foreach (ArchiveCase group in Archives)
            {
                foreach (string extension in new[] { ".fl", ".fi", ".fs" })
                {
                    Expected.Add($"field/{group.Field}{extension}", File.ReadAllBytes(CopyPath(group.PathPrefix + extension)));
                }
                foreach (MemberCase member in group.Members)
                {
                    string decoded = Path.Combine(fixtureRoot, "Decoded", group.Field, member.Member);
                    byte[] bytes = File.ReadAllBytes(decoded);
                    Assert.Equal(member.Length, bytes.Length);
                    Assert.Equal(member.Sha256, Hash(bytes));
                    Expected.Add(member.Canonical, bytes);
                    sourceHashes.TryAdd(decoded, member.Sha256);
                }
            }
            game = new IsolatedFieldGame();
        }

        public string CopyPath(string relative) => Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
        public void AddPackageCopy(string name)
        {
            string copy = Path.Combine(root, "Junction", name);
            CopyPackage(copy);
            string manifest = Path.Combine(copy, "mod.xml");
            System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(manifest);
            System.Xml.Linq.XElement? identifier = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("ID", StringComparison.OrdinalIgnoreCase));
            if (identifier is not null)
            {
                identifier.Value = Guid.NewGuid().ToString();
                document.Save(manifest);
            }
        }
        public void CopyPackage(string target)
        {
            foreach (string path in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
            {
                string copy = Path.Combine(target, Path.GetRelativePath(destination, path));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(path, copy);
            }
        }

        public GameResourceHost Open() => new(GameDirectory, watch: false, modsDirectory: root);

        public void Dispose()
        {
            try
            {
                Assert.Equal(sourceArchiveHash, Hash(File.ReadAllBytes(sourceArchive)));
                foreach ((string path, string expected) in sourceHashes)
                {
                    Assert.Equal(expected, Hash(File.ReadAllBytes(path)));
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
