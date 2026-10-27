using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionTranslationResourceInstalledTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Fact]
    public void EveryConfiguredSelectionPublishesActualArchiveBytesToNativeConsumers()
    {
        string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRANSLATION_FIXTURES");
        Assert.SkipUnless(File.Exists(catalogPath), "Configure an optional native translation fixture catalog.");
        FixtureCatalog fixture = JsonSerializer.Deserialize<FixtureCatalog>(File.ReadAllText(catalogPath!), JsonOptions)!;
        Assert.NotEmpty(fixture.Values);
        string catalogRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
        string package = Path.GetFullPath(fixture.Package, catalogRoot);
        string originalHash = FileHash(package);
        string root = Path.Combine(Path.GetTempPath(), "ff8-translation-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string archive = Path.Combine(mods, "Junction", "translation.iroj");
        string application = Path.Combine(root, "Application");
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        Directory.CreateDirectory(application);
        File.Copy(package, archive);
        string lower = Path.Combine(mods, "Junction", "zz-shadow");
        Directory.CreateDirectory(Path.Combine(lower, "menu"));
        File.WriteAllText(Path.Combine(lower, "mod.xml"), "<ModInfo><Name>Lower resource fixture</Name></ModInfo>");
        File.WriteAllText(Path.Combine(lower, "mod.ini"), "enabled = 1");
        File.WriteAllBytes(Path.Combine(lower, "kernel.bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(lower, "menu", "mngrp.bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(lower, "menu", "mngrphd.bin"), new byte[16]);
        if (fixture.ScenePackage is not null)
        {
            string scene = Path.GetFullPath(fixture.ScenePackage, catalogRoot);
            foreach (string file in Directory.EnumerateFiles(scene, "*", SearchOption.AllDirectories))
            {
                string copy = Path.Combine(mods, "Memoria", "scene-fixture", Path.GetRelativePath(scene, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy);
            }
            File.WriteAllText(Path.Combine(mods, "Memoria", "scene-fixture", "mod.ini"), "enabled = 1");
        }
        List<string> failures = [];
        List<object> selections = [];
        using IsolatedFieldGame game = new();
        try
        {
            foreach (int value in fixture.Values)
            {
                List<object> observations = [];
                ModDiagnostic[] diagnostics = [];
                ModDiagnostic[] restartDiagnostics = [];
                try
                {
                    File.WriteAllText(archive + ".ini", $"enabled = 1{Environment.NewLine}option.{fixture.Option} = {value}");
                    JunctionModPackage imported = JunctionModImporter.Open(archive, new Dictionary<string, int> { [fixture.Option] = value });
                    ModFile[] files = imported.Files.Select(file => new ModFile("Junction/translation.iroj", ModFormat.Junction, file.RelativePath, 1, () => Read(file.OpenRead()))).ToArray();
                    ModCatalogSnapshot expectedCatalog = new(1, [], files);
                    Assert.True(JunctionEncodedResources.TryPrepare(expectedCatalog, out var expectedResources, out var expectedDiagnostics), JsonSerializer.Serialize(expectedDiagnostics));
                    Dictionary<ResourceKey, byte[]> expected = expectedResources.ToDictionary(resource => resource.Key, resource => Read(resource.Content.OpenRead()));
                    using GameResourceHost host = new(game.GameDirectory, application, watch: false, modsDirectory: mods);
                    diagnostics = host.Mods.Diagnostics.ToArray();
                    WriteEvidence(value, new { stage = "prepared", packages = host.Mods.Catalog.Packages, diagnostics });
                    Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, item => item.Id == "Junction/translation.iroj").Status);
                    Assert.DoesNotContain(host.Mods.Catalog.Packages, item => item.Status == ModEffectiveStatus.ReloadFailure);
                    Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource");
                    Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.ResourceGroupConflict" && item.PackageId == "Junction/zz-shadow");
                    using IResourceSource original = GameDataSourceFactory.Open(game.GameDirectory);
                    using GameAssets assets = new(game.GameDirectory, resources: host.Resources);
                    using BattleAssets battle = new(game.GameDirectory, host.Resources);
                    LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
                    foreach ((ResourceKey key, byte[] bytes) in expected.Where(pair => JunctionEncodedResources.FieldGroup(pair.Key) is null))
                    {
                        Assert.Equal(bytes, host.Resources.Read(key));
                        observations.Add(new { key = key.Path, hash = Hash(bytes), bytes = bytes.Length });
                    }
                    byte[] Expected(string path) => expected.GetValueOrDefault(new ResourceKey(path)) ?? Read(original.Find(new ResourceKey(path))!.OpenRead());
                    FF8Encoding encoding = FF8Encoding.CreateEuropean();
                    KernelData kernel = KernelData.ReadFromArray(Expected("main/kernel.bin"), encoding);
                    Assert.Equal(kernel.MagicSpells[1].Name, menu.MagicName(1));
                    Assert.Equal(kernel.Weapons[0].Name, menu.WeaponName(0));
                    Assert.Equal(kernel.BattleItems[1].Name, menu.ItemName(1));
                    Assert.Equal(kernel.MiscTexts, battle.ReadNativeGlobalBattleMessages());
                    MngrpArchive textArchive = MngrpArchive.Read(Expected("menu/mngrp.bin"), Expected("menu/mngrphd.bin"), encoding);
                    MngrpFilePair roundtrip = textArchive.Write();
                    Assert.Equal(Expected("menu/mngrp.bin"), roundtrip.Content);
                    Assert.Equal(Expected("menu/mngrphd.bin"), roundtrip.Header);
                    MngrpTextRecordSection cards = textArchive.GetSection<MngrpTextRecordSection>(192);
                    Assert.Equal(LiveMenuCatalog.FormatText(cards.Records[0].Text.Value), menu.CardDescription(0));
                    foreach (var pair in expected.Where(pair => NativeDataResourceFiles.Classify(pair.Key) == NativeDataResourceKind.Enemy))
                    {
                        byte[] bytes = pair.Value;
                        int model = int.Parse(pair.Key.Path.AsSpan(10, 3));
                        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
                        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 28 : 4));
                        int nameEnd = bytes.AsSpan(information, 24).IndexOf((byte)0);
                        Assert.Equal(encoding.GetString(bytes.AsSpan(information, nameEnd)), battle.ReadEnemyName(model));
                        Assert.Equal(EnemyTexts(bytes), battle.ReadEnemyAi(model, 0).Texts);
                    }
                    using (WorldAssets world = new(game.GameDirectory, resources: host.Resources))
                    {
                        byte[] bytes = Expected("world/wmsetus.obj");
                        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(13 * 4));
                        int table = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section));
                        int textEnd = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14 * 4));
                        for (int id = 0; id < table / 4; id++)
                        {
                            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + id * 4));
                            if (offset == 0 && id == table / 4 - 1)
                            {
                                break;
                            }
                            int end = bytes.AsSpan(section + offset, textEnd - section - offset).IndexOf((byte)0);
                            Assert.Equal(bytes.AsSpan(section + offset, end + 1).ToArray(), world.ReadWorldMessageBytes(id));
                            Assert.Equal(encoding.GetString(bytes.AsSpan(section + offset, end)), world.ReadWorldMessage(id));
                        }
                        Assert.Equal(new WorldMapLocations(bytes).GetPlaceId(0, 0), menu.LocationId(new GameState { Scene = SceneKind.WorldMap, World = new WorldState() }));
                    }
                    Assert.True(JunctionTextResources.TryPrepare(expectedCatalog, out var texts, out var textDiagnostics), JsonSerializer.Serialize(textDiagnostics));
                    foreach (StaticTextResource table in texts)
                    {
                        Assert.Equal(table.Texts, host.Resources.FindText(table.Key)!.Texts);
                        if (table.Key == StaticTextResource.BattleScans)
                        {
                            NativeBattleScanTextResources scan = assets.ReadNativeScanTextResources();
                            for (int index = 0; index < table.Texts.Length; index++)
                            {
                                int offset = scan.DescriptionOffsets[index];
                                int length = scan.DescriptionBytes.AsSpan()[offset..].IndexOf((byte)0);
                                Assert.Equal(table.Texts[index], encoding.GetString(scan.DescriptionBytes.AsSpan(offset, length)));
                            }
                        }
                        if (table.Key == StaticTextResource.DrawPointMessages)
                        {
                            NativeDrawPointCatalog draw = assets.ReadNativeDrawPointCatalog();
                            Assert.Equal(table.Texts.Take(8), draw.CommonMessages);
                            Assert.Equal(table.Texts[7], draw.NoEligibleMessage);
                            for (int index = 0; index < 8; index++)
                            {
                                byte[] encoded = encoding.GetBytes(table.Texts[index]);
                                Assert.Equal(encoded.Append((byte)0), draw.CommonMessageBytes[index]);
                            }
                        }
                        for (int index = 0; index < table.Texts.Length; index++)
                        {
                            Assert.Equal(table.Texts[index], host.Resources.ReadText(table.Key, index));
                        }
                    }
                    List<object> fields = [];
                    foreach (var group in expected.Where(pair => JunctionEncodedResources.FieldGroup(pair.Key) is not null).GroupBy(pair => JunctionEncodedResources.FieldGroup(pair.Key)!))
                    {
                        var errors = host.Mods.Diagnostics.Where(diagnostic => diagnostic.Code == "Junction.InvalidFieldResource" && (diagnostic.Message.StartsWith(group.Key + ":", StringComparison.Ordinal) || diagnostic.Message.StartsWith(group.Key + ",", StringComparison.Ordinal))).ToArray();
                        if (errors.Length > 0)
                        {
                            foreach (var pair in group)
                            {
                                ResourceContent? installed = original.Find(pair.Key);
                                if (installed is null)
                                {
                                    Assert.Throws<FileNotFoundException>(() => host.Resources.Read(pair.Key));
                                }
                                else
                                {
                                    Assert.Equal(Read(installed.OpenRead()), host.Resources.Read(pair.Key));
                                }
                            }
                            fields.Add(new { field = group.Key, accepted = false, errors });
                            continue;
                        }
                        foreach (var pair in group)
                        {
                            Assert.Equal(pair.Value, host.Resources.Read(pair.Key));
                        }
                        FieldLogicResources logic = assets.ReadFieldLogicResources(group.Key);
                        Assert.Equal(MsdFile.Read(Expected($"field/{group.Key}/{group.Key}.msd"), encoding).Texts, logic.Messages);
                        fields.Add(new { field = group.Key, accepted = true, messages = logic.Messages.Count });
                    }
                    foreach (OwnedFieldScenePatch patch in host.SceneMods.Patches)
                    {
                        FieldLogicResources logic = assets.ReadFieldLogicResources(patch.Patch.FieldName);
                        Assert.Equal(Expected($"field/{logic.Name}/{logic.Name}.jsm"), logic.Program);
                        GameState state = new() { Scene = SceneKind.Field, FieldId = logic.Id, FieldName = logic.Name };
                        using GameWorld scene = new(new GameSession(state), game.GameDirectory, resources: host.Resources, sharedAssets: assets, readSceneMods: () => host.SceneMods);
                        var inspection = scene.ReadSceneInspection()!;
                        Assert.Contains(inspection.Entries, entry => entry.Details.Any(detail => detail.Contains(patch.PackageId, StringComparison.Ordinal)));
                        Assert.Equal(logic.Program, scene.Field!.Jsm);
                    }
                    if (value == fixture.Values[0])
                    {
                        byte[] before = host.Resources.Read(new ResourceKey("main/kernel.bin"));
                        Assert.True(fixture.Values.Length > 1);
                        File.WriteAllText(archive + ".ini", $"enabled = 1{Environment.NewLine}option.{fixture.Option} = {fixture.Values[1]}");
                        host.Mods.Refresh();
                        Assert.False(host.Pump(true));
                        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "resource-reload-required");
                        Assert.Equal(before, host.Resources.Read(new ResourceKey("main/kernel.bin")));
                        restartDiagnostics = host.Mods.Diagnostics.ToArray();
                    }
                    selections.Add(new { value, success = true, observations, fields, diagnostics, restartDiagnostics });
                    WriteEvidence(value, selections[^1]);
                }
                catch (Exception exception)
                {
                    failures.Add($"Selection {value}: {exception}");
                    selections.Add(new { value, success = false, error = exception.ToString(), observations, diagnostics, restartDiagnostics });
                    WriteEvidence(value, selections[^1]);
                }
            }
            Assert.Equal(originalHash, FileHash(package));
            Assert.Equal(originalHash, FileHash(archive));
            WriteEvidence(null, new { archiveHash = originalHash, selections, scope = "Native resource component acceptance; no gameplay callbacks or story route advanced." });
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
        finally
        {
            Assert.Equal(originalHash, FileHash(package));
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string[] EnemyTexts(byte[] bytes)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 32 : 8));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 36 : 12));
        int pointers = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 8));
        int text = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 12));
        List<string> values = [];
        for (int index = pointers; index < text; index += 2)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index));
            if (offset == ushort.MaxValue)
            {
                values.Add("");
                continue;
            }
            int start = text + offset;
            int length = bytes.AsSpan(start, end - start).IndexOf((byte)0);
            values.Add(FF8Encoding.CreateEuropean().GetString(bytes.AsSpan(start, length)));
        }
        return values.ToArray();
    }

    private static byte[] Read(Stream stream)
    {
        using (stream)
        {
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteEvidence(int? value, object evidence)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRANSLATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, value is int selection ? $"selection-{selection}.json" : "acceptance.json"), JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private sealed record FixtureCatalog(string Package, string Option, int[] Values, string? ScenePackage);
}
