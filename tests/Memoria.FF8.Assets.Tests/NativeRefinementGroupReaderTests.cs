using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRefinementGroupReaderTests
{
    [Fact]
    public void GroupRecordsUseFirstMatchingCardIdentityAndLeaveMissingCardUnavailable()
    {
        MngrpFilePair files = Groups();
        int recordOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(files.Header.AsSpan(192 * 8)) - 1;
        files.Content[recordOffset + 27 * 8 + 5] = 24;
        files.Content[recordOffset + 27 * 8 + 7] = 198;
        MngrpArchive archive = MngrpArchive.Read(files, FF8Encoding.CreateEuropean(FF8TextLanguage.EN));
        Assert.Equal(110, NativeRefinementGroupReader.ReadTables(archive)[4].Length);
        var recipes = NativeRefinementGroupReader.ReadCardRefinements(archive);
        Assert.Equal(109, recipes.Length);
        Assert.DoesNotContain(recipes, recipe => recipe.CardId == 27);
        Assert.Equal(1, recipes.Single(recipe => recipe.CardId == 24).ItemId);
        Assert.Equal(198, NativeRefinementGroupReader.ReadCardRefinements(archive, new("Synthetic", 4, 27, 27)).Single().ItemId);
        string[] descriptions = NativeRefinementGroupReader.ReadCardDescriptions(archive);
        Assert.Equal("", descriptions[27]);
        Assert.NotEmpty(descriptions[24]);
        files.Content[recordOffset + 27 * 8 + 5] = 110;
        Assert.Throws<InvalidDataException>(() => NativeRefinementGroupReader.ReadTables(MngrpArchive.Read(files, FF8Encoding.CreateEuropean(FF8TextLanguage.EN))));
    }

    [Fact]
    public void SourceHashesCoverFullRecordAndCompanionTextBytes()
    {
        MngrpFilePair files = Groups();
        string[] original = NativeRefinementGroupReader.ReadTableHashes(files.Content, files.Header).ToArray();
        int textOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(files.Header.AsSpan(200 * 8)) - 1;
        files.Content[textOffset]++;
        string[] selected = NativeRefinementGroupReader.ReadTableHashes(files.Content, files.Header).ToArray();
        Assert.Equal(original[..4], selected[..4]);
        Assert.NotEqual(original[4], selected[4]);
        int recordOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(files.Header.AsSpan(188 * 8)) - 1;
        files.Content[recordOffset + 7]++;
        Assert.NotEqual(original[0], NativeRefinementGroupReader.ReadTableHashes(files.Content, files.Header)[0]);
    }

    [Theory]
    [InlineData("menu-data-a", 3, 110)]
    [InlineData("menu-data-b", 2, 109)]
    [InlineData("menu-data-c", 3, 110)]
    public void OptionalSelectedGroupsDriveNativeBalanceAndCardAvailability(string fixture, int magicOutput, int availableCards)
    {
        string catalog = TestResources.FixturePath(fixture);
        Dictionary<string, string> files = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog))!;
        byte[] Read(string name) => File.ReadAllBytes(Path.GetFullPath(files[name], Path.GetDirectoryName(catalog)!));
        byte[] content = Read("mngrp.bin");
        byte[] header = Read("mngrphd.bin");
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources:
        [
            new EncodedResource(new ResourceKey("menu/mngrp.bin"), "bin", 1, "fixture", content),
            new EncodedResource(new ResourceKey("menu/mngrphd.bin"), "bin", 1, "fixture", header)
        ]).Commit();
        using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        Assert.Equal(magicOutput, definitions.Tables[0][20].Output);
        Assert.Equal(new NativeRefinementRecipe(3, 1, 126, 1, 10), definitions.Tables[1][0]);
        Assert.Equal(10, definitions.Tables[4][77].OutputCount);
        Assert.Equal(NativeRefinementGroupReader.ReadTableHashes(content, header).ToArray(), definitions.TableHashes.ToArray());
        NativeRefinementDefinitions restored = JsonSerializer.Deserialize<NativeRefinementDefinitions>(JsonSerializer.Serialize(definitions))!;
        Assert.Equal(definitions.TableHashes.ToArray(), restored.TableHashes.ToArray());
        TripleTriadCatalog cards = TripleTriadCatalog.Load(TestResources.GameDirectory, resources);
        Assert.Equal(availableCards, cards.Refinements.Length);
        Assert.Equal(10, cards.Refinements.Single(recipe => recipe.CardId == 77).ItemCount);
        if (availableCards == 109)
        {
            Assert.Equal(-1, restored.FindRecipeIndex(115, 27));
            Assert.DoesNotContain(cards.Refinements, recipe => recipe.CardId == 27);
            Assert.Equal(24, restored.FindRecipeIndex(115, 24));
        }
    }

    private static MngrpFilePair Groups()
    {
        byte[] content = new byte[10 * 2048];
        byte[] header = new byte[256 * 8];
        int[] counts = [102, 143, 10, 12, 110];
        for (int kind = 0; kind < 5; kind++)
        {
            int recordOffset = kind * 2048;
            int textOffset = (5 + kind) * 2048;
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan((188 + kind) * 8), (uint)recordOffset + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan((188 + kind) * 8 + 4), 2048);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan((196 + kind) * 8), (uint)textOffset + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan((196 + kind) * 8 + 4), 2048);
            for (int row = 0; row < counts[kind]; row++)
            {
                Span<byte> record = content.AsSpan(recordOffset + row * 8, 8);
                BinaryPrimitives.WriteUInt16LittleEndian(record, (ushort)(row * 2));
                BinaryPrimitives.WriteUInt16LittleEndian(record[2..], 1);
                record[4] = 1;
                record[5] = kind == 4 ? (byte)row : (byte)(row + 1);
                record[6] = 1;
                record[7] = 1;
                content[textOffset + row * 2] = 0x45;
            }
        }
        return new(content, header);
    }
}
