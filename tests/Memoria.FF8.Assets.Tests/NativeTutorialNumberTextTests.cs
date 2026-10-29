using System.Collections.Immutable;
using System.Reflection;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialNumberTextTests
{
    [Theory]
    [InlineData(null, "—")]
    [InlineData(12345L, "12345")]
    public void InformationNumberUsesReadableFallbackOnlyWhenValueIsUnavailable(long? value, string expected)
    {
        NativeTutorialInformationNode node = new(0, ushort.MaxValue, 0, 0, "Report", ImmutableArray.Create<byte>(0x0A, 0xC0)) { Language = FF8TextLanguage.EN };
        NativeTutorialExamCatalog catalog = (NativeTutorialExamCatalog)Activator.CreateInstance(typeof(NativeTutorialExamCatalog), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ImmutableArray<NativeTutorialExamLevel>.Empty, ImmutableArray<string>.Empty, ImmutableArray.Create(node), FF8TextLanguage.EN], null)!;
        NativeTutorialExamProgress progress = new(0, 1, 1, InformationNumbers: value is long number ? new Dictionary<int, long> { [0xC0] = number } : null);

        Assert.Equal(expected, catalog.InformationPage(0, new HashSet<int>(), progress).Text);
    }
}
