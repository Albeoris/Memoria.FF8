using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveOriginalExportCorpusTests(ITestOutputHelper output)
{
    private const string DirectoryResource = "App.Tests.GameplaySaveOriginalExportCorpusTests.Directory";

    [Fact]
    public void Every_supplied_component_save_exports_to_a_valid_original_save_without_changing_its_source()
    {
        string directory = CorpusDirectory();
        string gameDirectory = TestResources.GameDirectory;
        List<string> failures = [];
        int audited = 0;
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            using (FileStream candidate = File.OpenRead(path))
            {
                if (!GameplaySaveCodec.HasSignature(candidate))
                {
                    continue;
                }
            }
            audited++;
            string name = Path.GetRelativePath(directory, path);
            byte[] sourceHash = HashFile(path);
            try
            {
                Audit(path, gameDirectory);
                output.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                failures.Add(name + ": " + exception);
                output.WriteLine("FAIL " + name + ": " + exception.Message);
            }
            finally
            {
                if (!sourceHash.AsSpan().SequenceEqual(HashFile(path)))
                {
                    failures.Add(name + ": source file changed during the read-only export audit.");
                }
            }
        }
        output.WriteLine($"Audited {audited} component saves.");
        Assert.True(audited > 0, "The supplied corpus directory contains no component saves.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, failures));
    }

    private static void Audit(string path, string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        GameplaySaveComponentDocument document;
        using (FileStream source = File.OpenRead(path))
        {
            document = GameplaySaveCodec.Read(source);
        }
        string documentBefore = JsonSerializer.Serialize(document);
        GameState state = GameplaySaveSessionLoader.Restore(document, gameDirectory, sharedAssets: assets);
        Assert.Equal(documentBefore, JsonSerializer.Serialize(document));
        string stateBefore = JsonSerializer.Serialize(state);
        byte[] exported = NativePcSaveConverter.Export(state);
        Assert.Equal(stateBefore, JsonSerializer.Serialize(state));
        NativePcSaveData decoded = NativePcSaveCodec.Decode(exported);
        Assert.Equal(NativePcSaveData.BlockSize, decoded.Block.Length);
        Assert.Equal(decoded.Block, NativePcSaveCodec.Decode(NativePcSaveCodec.Encode(decoded)).Block);
        GameState imported = NativePcSaveConverter.Import(exported, assets);
        Assert.Equal(state.Scene, imported.Scene);
        Assert.Equal(state.FieldId, imported.FieldId);
        Assert.Equal(state.Economy!.Gil, imported.Economy!.Gil);
        Assert.Equal(state.Economy.Items, imported.Economy.Items);
        Assert.Equal(state.Playtime!.Seconds, imported.Playtime!.Seconds);
        Assert.Equal(state.CharacterVitals!.CombatParty, imported.CharacterVitals!.CombatParty);
        for (int row = 0; row < state.CharacterVitals.Records.Length; row++)
        {
            Assert.Equal(state.CharacterVitals.Records[row].Experience, imported.CharacterVitals.Records[row].Experience);
            if (!state.CharacterVitals.MagicSystem.RequiresMemoriaFormat)
            {
                Assert.Equal(ActiveMagicStocks(state.CharacterVitals.Records[row]), ActiveMagicStocks(imported.CharacterVitals.Records[row]));
            }
        }
        Assert.Equal(TotalMagicStocks(state.CharacterVitals), TotalMagicStocks(imported.CharacterVitals));
        GameSession resumed = new(imported);
        using GameWorld continuation = new(resumed, gameDirectory, sharedAssets: assets);
        continuation.EnsureField();
        PlayerActionResult result = continuation.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        Assert.True(result.Accepted, result.Detail);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Null(continuation.Events.LastFault);
        Assert.Equal(stateBefore, JsonSerializer.Serialize(state));
        Assert.Equal(documentBefore, JsonSerializer.Serialize(document));
    }

    private static NativeMagicStock[] ActiveMagicStocks(NativeCharacterVital character)
    {
        return character.MagicInventory.Where(stock => stock.SpellId != 0 && stock.Count != 0).OrderBy(stock => stock.SpellId).ToArray();
    }

    private static (byte SpellId, int Count)[] TotalMagicStocks(NativeCharacterVitals vitals)
    {
        return vitals.Records.SelectMany(character => character.MagicInventory).Concat(vitals.MagicSystem.RecordZeroExtension).Where(stock => stock.SpellId != 0 && stock.Count != 0).GroupBy(stock => stock.SpellId).Select(group => (SpellId: group.Key, Count: group.Sum(stock => stock.Count))).OrderBy(stock => stock.SpellId).ToArray();
    }

    private static string CorpusDirectory()
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(catalog) && File.Exists(catalog), "Set MEMORIA_FF8_TEST_FIXTURE_CATALOG to an external fixture catalog.");
        Dictionary<string, string> resources = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("The external fixture catalog must contain a JSON object.");
        Assert.SkipUnless(resources.TryGetValue(DirectoryResource, out string? directory) && !string.IsNullOrWhiteSpace(directory), $"The optional external fixture catalog has no resource '{DirectoryResource}'.");
        string resolved = Path.GetFullPath(directory!, TestResources.FixtureDirectory);
        Assert.True(Directory.Exists(resolved), "The supplied component save corpus directory is unavailable.");
        return resolved;
    }

    private static byte[] HashFile(string path)
    {
        using FileStream source = File.OpenRead(path);
        return SHA256.HashData(source);
    }
}
