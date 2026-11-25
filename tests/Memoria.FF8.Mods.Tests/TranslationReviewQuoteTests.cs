using Memoria.FF8.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class TranslationReviewQuoteTests
{
    [Theory]
    [InlineData("«Стойте!{Next}Я не закончил».{End}")]
    [InlineData("«???{Next}Я не закончил?»{End}")]
    public void PreservesOneAuthoredUtteranceAcrossPages(string target)
    {
        const string source = "”Wait!“{Next}”I’m not finished.“{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal(target, result.Target);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Rule == "dialogue-quotes");
    }

    [Fact]
    public void ReportsAnAuthoredQuotationThatCrossesProvenSpeakerLabels()
    {
        const string source = "Zell{Line}”Wait!“{Next}Quistis{Line}”I’m not finished.“{End}";
        const string target = "{Char Zell}{Line}«Стойте!{Next}{Char Quistis}{Line}Я не закончила».{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal(target, result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "dialogue-quotes" && !diagnostic.Fixable);
    }

    [Fact]
    public void StillRepairsMissingQuotesOnASeparatePage()
    {
        const string source = "”Wait!“{Next}”I’m not finished.“{End}";
        const string target = "«Стойте!»{Next}Я не закончил.{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("«Стойте!»{Next}«Я не закончил».{End}", result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "dialogue-quotes" && diagnostic.Fixed);
    }

    private static TranslationReviewResult Review(string source, string target)
    {
        return TranslationReviewRules.Review(target, source, "Fields", "$en_fixture_0000", true);
    }
}
