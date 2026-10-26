using System.Collections.Immutable;
using System.Reflection;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class NativeInformationLocalizationTests
{
    [Fact]
    public void OpenInformationReadsFreshUnicodeBodyAndLinkLabelsWithoutChangingNativeNodes()
    {
        byte[] body = [0x0B, 0x20, 0x21, .. FF8Encoding.CreateEuropean().GetBytes("Synthetic link"), 2, .. FF8Encoding.CreateEuropean().GetBytes("Synthetic prose")];
        NativeTutorialInformationNode node = new(0, ushort.MaxValue, 1, 1, "Synthetic heading", body.ToImmutableArray()) { LocalizationFile = "mngrp_slot128_block0000" };
        NativeTutorialExamCatalog catalog = (NativeTutorialExamCatalog)Activator.CreateInstance(typeof(NativeTutorialExamCatalog), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ImmutableArray<NativeTutorialExamLevel>.Empty, ImmutableArray<string>.Empty, ImmutableArray.Create(node, node with { Id = 1 })], null)!;
        MenuController menu = new();
        string generation = "第一";
        menu.ConfigureNativeTutorialLocalization((file, ordinal, source) => ordinal == 0 ? generation + "標題" : source.Replace("Synthetic link", generation + "連結", StringComparison.Ordinal).Replace("Synthetic prose", generation + "本文", StringComparison.Ordinal));
        menu.ConnectNativeTutorialExam(catalog, () => new(0, 1, 1));
        menu.Execute(new("native-exam-open", "information"));

        NativeTutorialInformationPage first = menu.CurrentNativeInformationPage!;
        Assert.Equal("第一標題", first.Node.Title);
        Assert.Equal("第一連結", Assert.Single(first.Links).Label);
        Assert.Equal("第一連結{Line}第一本文", first.Text);
        generation = "第二";
        NativeTutorialInformationPage second = menu.CurrentNativeInformationPage!;
        Assert.Equal("第二連結{Line}第二本文", second.Text);
        Assert.Equal(first.Links[0].TargetId, second.Links[0].TargetId);
        Assert.Equal(first.Links[0].LineIndex, second.Links[0].LineIndex);
        Assert.Equal(body, catalog.Information[0].Body);
        Assert.Equal("Synthetic heading", catalog.Information[0].Title);
        Assert.Equal("第一連結{Line}第一本文", first.Text);
    }
}
