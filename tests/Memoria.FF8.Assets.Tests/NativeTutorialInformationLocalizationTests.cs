using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialInformationLocalizationTests
{
    [Fact]
    public void UnicodeTargetsKeepConditionalVisibilityLinkOperandsNumbersAndLineRoles()
    {
        byte[] body = [0x0A, 0x40, 0x0B, 0x20, 0x21, .. FF8Encoding.CreateEuropean().GetBytes("Visible"), 2, 0x0A, 0x3F, .. FF8Encoding.CreateEuropean().GetBytes("Count "), 0x0A, 0xC0];
        NativeTutorialInformationNode node = new(0, ushort.MaxValue, 1, 1, "Synthetic", body.ToImmutableArray());
        NativeTutorialExamCatalog catalog = (NativeTutorialExamCatalog)Activator.CreateInstance(typeof(NativeTutorialExamCatalog), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ImmutableArray<NativeTutorialExamLevel>.Empty, ImmutableArray<string>.Empty, ImmutableArray.Create(node, node with { Id = 1 })], null)!;
        NativeTutorialExamProgress progress = new(0, 1, 1, InformationNumbers: new Dictionary<int, long> { [0xC0] = 123 });
        string source = NativeTutorialInformationText.Format(body, FF8Encoding.CreateEuropean());
        string translated = source.Replace("Visible", "可見 Українська", StringComparison.Ordinal).Replace("Count ", "數量 ", StringComparison.Ordinal);

        NativeTutorialInformationPage hidden = catalog.InformationPage(0, new HashSet<int>(), progress, (_, _) => translated);
        Assert.Empty(hidden.Links);
        Assert.Equal("數量 123", hidden.Text);
        NativeTutorialInformationPage visible = catalog.InformationPage(0, new HashSet<int> { 0 }, progress, (_, _) => translated);
        Assert.Equal("可見 Українська{Line}數量 123", visible.Text);
        Assert.Equal(new NativeTutorialInformationLink(1, "可見 Українська", 0), Assert.Single(visible.Links));
        Assert.Equal(body, node.Body);
        Assert.Contains("Visible", catalog.InformationPage(0, new HashSet<int> { 0 }, progress).Text);
        Assert.Equal(1, visible.Node.RightId);
        Assert.Throws<InvalidDataException>(() => catalog.InformationPage(0, new HashSet<int>(), progress, (_, _) => translated.Replace("2021", "2020", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("{Info Gate 40}A{Info Link 2021}B{Line}{Info Gate 3F}{Info Number C0}")]
    public void TargetCannotRewriteOrDropAnyNativeControl(string source)
    {
        Assert.True(NativeTutorialInformationText.ValidateTarget(source, source.Replace("A", "翻譯", StringComparison.Ordinal)));
        foreach (string token in new[] { "{Info Gate 40}", "{Info Link 2021}", "{Line}", "{Info Gate 3F}", "{Info Number C0}" })
        {
            Assert.False(NativeTutorialInformationText.ValidateTarget(source, source.Replace(token, "", StringComparison.Ordinal)));
        }
        Assert.False(NativeTutorialInformationText.ValidateTarget(source, source + "{Color Red}"));
        Assert.False(NativeTutorialInformationText.ValidateTarget(source, source + "\n"));
        Assert.False(NativeTutorialInformationText.ValidateTarget(source, source + "{Info Link 2022}"));
    }

    [Fact]
    public void SyntheticInformationBodiesExportAsStructuredTextWithResourceFingerprints()
    {
        byte[] body = [0x0A, 0x40, 0x0B, 0x20, 0x21, .. FF8Encoding.CreateEuropean().GetBytes("Synthetic link"), 2, 0x0A, 0x3F, 0x0A, 0xC0];
        byte[] title = FF8Encoding.CreateEuropean().GetBytes("Synthetic heading");
        int length = 8 + title.Length + 1 + body.Length + 1;
        byte[] block = new byte[(length + 3) & ~3];
        BinaryPrimitives.WriteUInt16LittleEndian(block, ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(2), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(4), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(6), checked((ushort)length));
        title.CopyTo(block, 8);
        body.CopyTo(block, 9 + title.Length);
        byte[] map = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(map, 1);
        byte[] content = [.. map, .. block];
        byte[] header = new byte[2048];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(127 * 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(127 * 8 + 4), checked((uint)map.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(128 * 8), checked((uint)map.Length + 1));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(128 * 8 + 4), checked((uint)block.Length));
        using InMemoryResourceSource resource = new(new("synthetic", GameDataEdition.Remastered, []));
        resource.Set(new("menu/mngrp.bin"), content, "bin");
        resource.Set(new("menu/mngrphd.bin"), header, "bin");

        var result = LocalizationSourceExporter.Read(resource, new MissingArchives(), "en", TestContext.Current.CancellationToken);

        Assert.True(result.Entries.Count > 0, string.Join("; ", result.Gaps.Where(gap => gap.Resource.Contains("mngrp", StringComparison.Ordinal)).Select(gap => gap.Reason)));
        var row = Assert.Single(result.Entries, row => row.Metadata.GetValueOrDefault("Role") == "Body");
        Assert.Equal("$en_mngrp_slot128_block0000_0001", row.Key);
        Assert.Equal(NativeTutorialInformationText.Format(body, FF8Encoding.CreateEuropean()), row.Text);
        Assert.Equal("information-body-v1", row.Metadata["Structure"]);
        Assert.Equal(64, row.Metadata["Source SHA256"]!.Length);
        Assert.Equal(64, row.Metadata["Companion SHA256"]!.Length);
        Assert.DoesNotContain(result.Gaps, gap => gap.Resource.Contains("slot128", StringComparison.Ordinal));
    }

    [Fact]
    public void JapaneseInformationPresentationUsesNodeEncodingAndPreservesNativeBody()
    {
        byte[] body = [.. FF8Encoding.CreateJapanese().GetBytes("あいうⅧ闘"), 0x0A, 0x3F];
        NativeTutorialInformationNode node = new(0, ushort.MaxValue, 0, 0, "日本語", body.ToImmutableArray()) { Japanese = true };
        NativeTutorialExamCatalog catalog = (NativeTutorialExamCatalog)Activator.CreateInstance(typeof(NativeTutorialExamCatalog), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ImmutableArray<NativeTutorialExamLevel>.Empty, ImmutableArray<string>.Empty, ImmutableArray.Create(node)], null)!;
        Assert.Equal("あいうⅧ闘", catalog.InformationPage(0, new HashSet<int>(), new(0, 1, 1)).Text);
        Assert.Equal(body, node.Body);
    }

    [Fact]
    public void JapaneseEscapesAndNativeControlOperandsRemainSeparate()
    {
        byte[] japanese = FF8Encoding.CreateJapanese().GetBytes("あいうⅧ闘");
        byte[] body = [.. japanese, 0x0A, 0x3F, 0x0B, 0x20, 0x21];
        Assert.Equal("あいうⅧ闘{Info Gate 3F}{Info Link 2021}", NativeTutorialInformationText.Format(body, FF8Encoding.CreateJapanese(), true));
        Assert.Throws<InvalidDataException>(() => NativeTutorialInformationText.Format(new byte[] { 0x0B, 0x20 }, FF8Encoding.CreateEuropean()));
    }

    private sealed class MissingArchives : IGameArchiveSource
    {
        public IFlArchive OpenArchive(string name) => throw new FileNotFoundException(name);
    }
}
