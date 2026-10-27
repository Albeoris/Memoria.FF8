using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeExecutableTextOverlayTests
{
    [Fact]
    public void ScanOverlayPreservesModelIdsControlsAndCapturedOriginalResources()
    {
        NativeBattleScanTextResources original = ScanOriginal();
        Assert.Same(original, NativeBattleScanTextResourcesReader.ApplyDescriptions(original, null));
        string[] messages = Enumerable.Range(0, 160).Select(id => $"Description {id}").ToArray();
        messages[87] = "Selected{Line}{Color Red}description";
        NativeBattleScanTextResources selected = NativeBattleScanTextResourcesReader.ApplyDescriptions(original, Text(StaticTextResource.BattleScans, messages));
        Assert.Equal(original.KernelMiscOffsets, selected.KernelMiscOffsets);
        Assert.Equal(original.KernelMiscBytes, selected.KernelMiscBytes);
        Assert.Equal(original.KernelMiscDirectoryFileOffset, selected.KernelMiscDirectoryFileOffset);
        Assert.Equal(original.KernelMiscTextFileOffset, selected.KernelMiscTextFileOffset);
        Assert.Equal("Original{End}", ScanText(original, 87));
        for (int id = 0; id < 160; id++)
        {
            Assert.Equal(messages[id] + "{End}", ScanText(selected, id));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextResourcesReader.ApplyDescriptions(original, Text(StaticTextResource.BattleScans, messages.Take(159))));
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextResourcesReader.ApplyDescriptions(original, Text(StaticTextResource.BattleScans, Enumerable.Repeat(new string('A', 600), 160))));
    }

    [Fact]
    public void DrawOverlayPreservesNativeIdentityAndPublishesSelectedBytesAndHashes()
    {
        NativeDrawPointCatalog original = DrawOriginal();
        Assert.Same(original, NativeDrawPointCatalogReader.ApplyMessages(original, null));
        string[] messages = Enumerable.Range(0, 9).Select(id => $"Selected {id}").ToArray();
        messages[2] = "Found{Line}{Name 32}";
        NativeDrawPointCatalog selected = NativeDrawPointCatalogReader.ApplyMessages(original, Text(StaticTextResource.DrawPointMessages, messages));
        Assert.Equal(original.ExecutableHash, selected.ExecutableHash);
        Assert.Equal(original.KernelHash, selected.KernelHash);
        Assert.Equal(original.PointBytes, selected.PointBytes);
        Assert.Equal(original.CommandIds, selected.CommandIds);
        Assert.Equal(original.MagicNames, selected.MagicNames);
        Assert.Equal("Original 7", original.NoEligibleMessage);
        Assert.Equal("Selected 7", selected.NoEligibleMessage);
        Assert.NotEqual(original.CommonMessageHash, selected.CommonMessageHash);
        Assert.Equal("Found{Line}Magic 6", selected.FormatMessage(2, 6));
        for (int id = 0; id < 8; id++)
        {
            Assert.Equal(messages[id], selected.Message(id));
            byte[] bytes = selected.CommonMessageBytes[id].ToArray();
            Assert.Equal(messages[id] + "{End}", FF8Encoding.CreateEuropean().GetString(bytes));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), selected.MessageHash(id));
        }
        Assert.Throws<InvalidDataException>(() => NativeDrawPointCatalogReader.ApplyMessages(original, Text(StaticTextResource.DrawPointMessages, messages.Take(8))));
    }

    [Fact]
    public void NewMaterializationsUseCurrentPublicationWhileCapturedBanksStayImmutable()
    {
        using ClassicGameDataSource source = new(TestResources.GameDirectory);
        ResourceService resources = new(source);
        using GameAssets game = new(TestResources.GameDirectory, resources: resources);
        NativeBattleScanTextResources scanOriginal = game.ReadNativeScanTextResources();
        NativeDrawPointCatalog drawOriginal = game.ReadNativeDrawPointCatalog();
        Assert.Same(scanOriginal, game.ReadNativeScanTextResources());
        Assert.Same(drawOriginal, game.ReadNativeDrawPointCatalog());
        string[] descriptions = Enumerable.Range(0, 160).Select(id => $"Selected description {id}").ToArray();
        string[] messages = Enumerable.Range(0, 9).Select(id => $"Selected message {id}").ToArray();
        resources.Prepare(null, [Text(StaticTextResource.BattleScans, descriptions), Text(StaticTextResource.DrawPointMessages, messages)]).Commit();
        NativeBattleScanTextResources scanSelected = game.ReadNativeScanTextResources();
        NativeDrawPointCatalog drawSelected = game.ReadNativeDrawPointCatalog();
        Assert.Equal(descriptions[87] + "{End}", ScanText(scanSelected, 87));
        Assert.Equal(messages[7], drawSelected.NoEligibleMessage);
        Assert.Same(scanSelected, game.ReadNativeScanTextResources());
        Assert.Same(drawSelected, game.ReadNativeDrawPointCatalog());
        Assert.NotEqual(ScanText(scanOriginal, 87), ScanText(scanSelected, 87));
        Assert.NotEqual(drawOriginal.CommonMessageHash, drawSelected.CommonMessageHash);
        resources.Prepare(null, []).Commit();
        Assert.Equal(ScanText(scanOriginal, 87), ScanText(game.ReadNativeScanTextResources(), 87));
        Assert.Equal(drawOriginal.NoEligibleMessage, game.ReadNativeDrawPointCatalog().NoEligibleMessage);
        Assert.Equal(descriptions[87] + "{End}", ScanText(scanSelected, 87));
        Assert.Equal(messages[7], drawSelected.NoEligibleMessage);
    }

    private static StaticTextResource Text(ResourceKey key, IEnumerable<string> messages) => new(key, messages.ToImmutableArray(), "selected", "text.msd");

    private static string ScanText(NativeBattleScanTextResources resources, int model)
    {
        uint pointer = resources.DescriptionPointer(model);
        List<byte> bytes = [];
        byte value;
        do
        {
            value = resources.ReadDescriptionByte(pointer++);
            bytes.Add(value);
        }
        while (value != 0);
        return FF8Encoding.CreateEuropean().GetString(bytes.ToArray());
    }

    private static NativeBattleScanTextResources ScanOriginal() => new(Enumerable.Repeat((ushort)0, 160).ToImmutableArray(), FF8Encoding.CreateEuropean().GetBytes("Original{End}").ToImmutableArray())
    {
        KernelMiscOffsets = [(ushort)0],
        KernelMiscBytes = [0],
        KernelMiscDirectoryFileOffset = 228,
        KernelMiscTextFileOffset = 230
    };

    private static NativeDrawPointCatalog DrawOriginal()
    {
        ImmutableArray<string> messages = Enumerable.Range(0, 8).Select(id => $"Original {id}").ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> bytes = messages.Select(text => FF8Encoding.CreateEuropean().GetBytes(text + "{End}").ToImmutableArray()).ToImmutableArray();
        ImmutableArray<string> hashes = bytes.Select(value => Convert.ToHexString(SHA256.HashData(value.AsSpan()))).ToImmutableArray();
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('0', 64), new byte[256].ToImmutableArray(), new byte[19].ToImmutableArray(), messages[7], hashes[7])
        {
            CommonMessages = messages,
            CommonMessageBytes = bytes,
            CommonMessageHashes = hashes,
            MagicNames = Enumerable.Range(0, 57).Select(id => $"Magic {id}").ToImmutableArray()
        };
    }
}
