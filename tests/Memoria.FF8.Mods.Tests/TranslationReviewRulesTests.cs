using Memoria.FF8.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class TranslationReviewRulesTests
{
    private const string FieldKey = "$en_fixture_0000";

    [Theory]
    [InlineData("Zell{Line}(Check the lock (again).){End}", "{Char Zell}{Line}(Проверь замок (ещё раз).){End}")]
    [InlineData("{X12Y12}Zell{Line}(Check the lock.){End}", "{X12Y12}{Char Zell}{Line}（Проверь замок.）{End}")]
    [InlineData("Sephie{Line}”Check the lock.“{End}", "{Char Selphie}{Line}«Проверь замок».{End}")]
    public void NativeSpeakerContractsAcceptVerifiedThoughtsAndAlias(string source, string target)
    {
        LocalizationTextValidation.Validate(new() { Category = "Fields", Key = FieldKey, Source = source, Target = target });
        Assert.Empty(TranslationReviewRules.Review(target, source, "Fields", FieldKey, fix: false).Diagnostics);
    }

    [Theory]
    [InlineData("Zell{Line}(Check the lock.){End}", "{Char Selphie}{Line}(Проверь замок.){End}")]
    [InlineData("Zell{Line}(Check the lock.){End}", "{Char Zell}{Line}Проверь замок.{End}")]
    [InlineData("Zell{Line}(Check the lock.){End}", "{Char Zell}{Line}(Проверь замок.{End}")]
    [InlineData("Zell{Line}(Check the lock.){End}", "{Char Zell}{Line}(Проверь замок.) Следующая строка.{End}")]
    [InlineData("Zell{Line}(Check the lock.) Continue.{End}", "{Char Zell}{Line}(Проверь замок.){End}")]
    [InlineData("Mechanic{Line}(Check the lock.){End}", "{Char Zell}{Line}(Проверь замок.){End}")]
    [InlineData("Zell{Line}(Check the lock.){End}", "{Char Zell}{Line}({Char Squall}, проверь замок.){End}")]
    [InlineData("Sephie{Line}”Check the lock.“{End}", "{Char Rinoa}{Line}«Проверь замок».{End}")]
    [InlineData("Sephis{Line}”Check the lock.“{End}", "{Char Selphie}{Line}«Проверь замок».{End}")]
    [InlineData("Sephie{Line}”Check the lock.“{End}", "{Char Selphie}{Line}«{Char Selphie}, проверь замок».{End}")]
    public void NativeSpeakerContractsRejectWrongIdentityAndUnprovenInsertion(string source, string target)
    {
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(new() { Category = "Fields", Key = FieldKey, Source = source, Target = target }));
        TranslationReviewResult reviewed = TranslationReviewRules.Review(target, source, "Fields", FieldKey, fix: true);
        Assert.DoesNotContain(reviewed.Diagnostics, diagnostic => diagnostic.Rule == "speaker-character" && diagnostic.Fixable);
    }

    [Fact]
    public void ReviewCanonicalizesVerifiedThoughtLabel()
    {
        const string source = "Zell{Line}(Check the lock.){End}";
        const string target = "Зелл{Line}(Проверь замок.){End}";
        var names = new Dictionary<string, string> { ["Zell"] = "Зелл" };
        TranslationReviewResult result = TranslationReviewRules.Review(target, source, "Fields", FieldKey, fix: true, characterNames: names);
        Assert.Equal("{Char Zell}{Line}(Проверь замок.){End}", result.Target);
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Rule == "speaker-character" && diagnostic.Fixed);
    }

    [Fact]
    public void NormalizesKnownSpeakerAndNativeReversedQuotes()
    {
        const string source = "{X12Y12}Zell{Line}”Did you show Squall the wooden kite?“{End}";
        const string target = "{X12Y12}Зелл{Line}Ты показал Скволлу деревянного змея?{End}";
        var names = new Dictionary<string, string> { ["Zell"] = "Зелл" };

        TranslationReviewResult lint = TranslationReviewRules.Review(target, source, "Fields", FieldKey, false, names);
        Assert.Equal(target, lint.Target);
        Assert.Equal(2, lint.Diagnostics.Count);
        Assert.All(lint.Diagnostics, diagnostic =>
        {
            Assert.True(diagnostic.Fixable);
            Assert.False(diagnostic.Fixed);
        });

        TranslationReviewResult fix = TranslationReviewRules.Review(target, source, "Fields", FieldKey, true, names);
        Assert.Equal("{X12Y12}{Char Zell}{Line}«Ты показал Скволлу деревянного змея?»{End}", fix.Target);
        Assert.All(fix.Diagnostics, diagnostic => Assert.True(diagnostic.Fixed));
        Assert.Empty(TranslationReviewRules.Review(fix.Target, source, "Fields", FieldKey, true, names).Diagnostics);
        LocalizationTextValidation.Validate(new() { Category = "Fields", Key = FieldKey, Source = source, Target = fix.Target });
    }

    [Fact]
    public void PreservesLiteralTitlesAndReportsUntranslatedNpcLabels()
    {
        const string source = "Sorceress Edea{Line}”The lantern needs a green ribbon.“{Next}Biggs{Line}”Take the spare one!“";
        const string target = "Волшебница Эдея{Line}“Фонарю нужна зелёная лента.”{Next}Biggs{Line}Возьми запасную!";
        TranslationReviewResult result = Review(source, target);

        Assert.Equal("Волшебница Эдея{Line}«Фонарю нужна зелёная лента».{Next}Biggs{Line}«Возьми запасную!»", result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "speaker-untranslated" && !diagnostic.Fixable);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Rule == "speaker-character");
    }

    [Fact]
    public void DistinguishesSpanningQuotesFromNarrationAndPreservesInnerQuotation()
    {
        const string source = "”It was called ‘A’{Next}before I came.“{Next}He mentioned “B” in the report.";
        const string target = "Это называлось „А“{Next}до моего приезда.{Next}Он упомянул «Б» в отчёте.";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("«Это называлось „А“{Next}до моего приезда».{Next}Он упомянул «Б» в отчёте.", result.Target);
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Fixed);
    }

    [Theory]
    [InlineData("Техник{Line}Найдите отвёртку{Line}на верхней полке и{Next}проверьте защёлку.")]
    [InlineData("Техник{Line}(Защёлка?!{Line} Медная{Next}{Line} защёлка?!)")]
    public void LeavesUnfinishedCrossPageSentencesForManualReview(string target)
    {
        const string source = "Technician{Line}”Look on the top shelf.“{Next}”Check the copper latch.“";
        TranslationReviewResult result = Review(source, target);

        Assert.Equal(target, result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "dialogue-quotes" && !diagnostic.Fixable);
    }

    [Fact]
    public void UsesChoicePrefixAndProtectsOptionIndentation()
    {
        const string source = "{F1L3B1C1}”Which basket shall we bring?“{Line}  The red one{Line} Where’s the wicker basket?{Line}  The green one{End}";
        const string target = "{F1L3B1C1}Какую корзину возьмём?{Line}  Красную{Line} Where’s the wicker basket?{Line}  Зелёную{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("{F1L3B1C1}«Какую корзину возьмём?»{Line}  Красную{Line} Where’s the wicker basket?{Line}  Зелёную{End}", result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "choice-untranslated" && !diagnostic.Fixable);
    }

    [Fact]
    public void FixesOnlySimpleContextualResponsesAndFlagsOtherUntranslatedOptions()
    {
        const string source = "{F1L6B1C1}Ready to inspect the dial?{Line}Yes{Line}{Color Red}NO{Color White}{Line}Inspect copper dial (First panel){Line}...Maybe I’ll call tomorrow{Line}Yeah{Line}Juniper Pavilion{End}";
        const string target = "{F1L6B1C1}Готовы осмотреть шкалу?{Line}Yes{Line}{Color Red}NO{Color White}{Line}Inspect copper dial (First panel){Line}...Maybe I’ll call tomorrow{Line}Yeah{Line}Juniper Pavilion{End}";
        const string expected = "{F1L6B1C1}Готовы осмотреть шкалу?{Line}Да{Line}{Color Red}НЕТ{Color White}{Line}Inspect copper dial (First panel){Line}...Maybe I’ll call tomorrow{Line}Yeah{Line}Juniper Pavilion{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal(expected, result.Target);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Rule == "choice-untranslated" && diagnostic.Fixed));
        Assert.Equal(3, result.Diagnostics.Count(diagnostic => diagnostic.Rule == "choice-untranslated" && !diagnostic.Fixable));
        Assert.Equal(expected, Review(source, result.Target).Target);
        Assert.Equal(source, Review(source, source).Target);
    }

    [Theory]
    [InlineData("{F2L4B4C2}Путь?{Line}Идём.{Line}Да{Line}Нет{Line}Позже")]
    [InlineData("Путь?{Line}Идём.{Line}Да{Line}Нет{Line}Позже")]
    public void ReportsUnprovedInitialAndCancellationOptionSemantics(string target)
    {
        const string source = "{F2L4B2C4}Which way?{Line}Let's go.{Line}Yes{Line}No{Line}Later";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal(target, result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "choice-layout" && !diagnostic.Fixable);
    }

    [Fact]
    public void FixesSpacingOnlyWithSupportingLayout()
    {
        const string source = "{X12Y12}Hello,{Line}friend!{Line}    Fine ...";
        const string target = "{X12Y12}Привет ,{Line} друг !{Line}    Хорошо ...";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("{X12Y12}Привет,{Line}друг!{Line}    Хорошо ...", result.Target);
        Assert.All(result.Diagnostics, diagnostic => Assert.True(diagnostic.Fixed));
        Assert.Empty(Review(source, result.Target).Diagnostics);
    }

    [Fact]
    public void FixesMultipleLeadingSpacesAndPunctuationNextToNameControls()
    {
        const string source = "{X12Y12}Stamp the envelope,{Line}{Char Squall}, then post it.{Line}”The mailbox is behind the willow.“";
        const string target = "{X12Y12}Поставь марку,{Line}{Char Squall} , затем отправь письмо.{Line}   «Ящик стоит за ивой ».{End}";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("{X12Y12}Поставь марку,{Line}{Char Squall}, затем отправь письмо.{Line}«Ящик стоит за ивой».{End}", result.Target);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Rule == "punctuation-space" && diagnostic.Fixed));
        Assert.Empty(Review(source, result.Target).Diagnostics);
    }

    [Fact]
    public void ReattachesPunctuationAtAProvenBreakWithoutMovingTheBreak()
    {
        const string source = "Zell{Line}”Find {Char Squall}{Line} to return the silver key.“{Next}”Then check the lock.“";
        const string target = "Zell{Line}Найди {Char Squall}{Line} , чтобы вернуть серебряный ключ.{Next}Затем проверь замок.";

        TranslationReviewResult result = Review(source, target);

        Assert.Equal("{Char Zell}{Line}«Найди {Char Squall},{Line}чтобы вернуть серебряный ключ».{Next}«Затем проверь замок».", result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "line-stranded-punctuation" && diagnostic.Fixed);
    }

    [Fact]
    public void FixesUnambiguousTagSpellingAndOperand()
    {
        TranslationReviewResult result = Review("Out of {Var 1}!{End}", "Осталось {var 2}!{End}");

        Assert.Equal("Осталось {Var 1}!{End}", result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "tag-malformed" && diagnostic.Fixed);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "tag-incorrect" && diagnostic.Fixed);
    }

    [Theory]
    [InlineData("Call {Char Squall}.", "Позови {Char Squal}.", "Позови {Char Squall}.")]
    [InlineData("Go.{End}", "Иди.{End", "Иди.{End}")]
    public void FixesSingleCharacterTagErrorsOnlyWithMatchingReference(string source, string target, string expected)
    {
        TranslationReviewResult result = Review(source, target);

        Assert.Equal(expected, result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == "tag-malformed" && diagnostic.Fixed);
        Assert.Empty(Review(source, result.Target).Diagnostics);
    }

    [Theory]
    [InlineData("«Доброго дня».{End}", "«Доброго дня».{End}")]
    [InlineData("«Доброго дня.»{End}", "«Доброго дня».{End}")]
    [InlineData("“Доброго дня”.{End}", "«Доброго дня».{End}")]
    [InlineData("Доброго дня.{End}", "«Доброго дня».{End}")]
    [InlineData("Доброго дня…{End}", "«Доброго дня…»{End}")]
    [InlineData("Доброго дня...{End}", "«Доброго дня...»{End}")]
    [InlineData("Доброго дня!{End}", "«Доброго дня!»{End}")]
    [InlineData("Доброго дня?{End}", "«Доброго дня?»{End}")]
    [InlineData("В 1670 г.{End}", "«В 1670 г.»{End}")]
    [InlineData("В 1670 гг.{End}", "«В 1670 гг.»{End}")]
    public void UsesRussianDialogueBoundaryPunctuation(string target, string expected)
    {
        const string reference = "”Good day.“{End}";
        TranslationReviewResult result = Review(reference, target);

        Assert.Equal(expected, result.Target);
        Assert.Empty(Review(reference, result.Target).Diagnostics);
    }

    [Theory]
    [InlineData("Angelo", "Angelo", "{Name Angelo}")]
    [InlineData("{Char Angelo2}", "Angelo", "{Char Angelo2}")]
    [InlineData("Boko", "Boko", "{Name Boko}")]
    [InlineData("Griever", "Griever", "{Name Griever}")]
    [InlineData("{Char UnknownCharacter3}", "Griever", "{Char UnknownCharacter3}")]
    [InlineData("Quezacotl", "Quetzacotl", "{Name Quetzacotl}")]
    [InlineData("Mog", "Mog", "{Char Mog}")]
    public void UsesResolvedSpeakerCatalogAndPreservesSourceTagAliases(string sourceSpeaker, string targetSpeaker, string expectedSpeaker)
    {
        string source = sourceSpeaker + "{Line}”Bring the shortest ladder.“";
        TranslationReviewResult result = Review(source, targetSpeaker + "{Line}Принеси самую короткую лестницу.");

        Assert.Equal(expectedSpeaker + "{Line}«Принеси самую короткую лестницу».", result.Target);
        LocalizationTextValidation.Validate(new() { Category = "Fields", Key = FieldKey, Source = source, Target = result.Target });
    }

    [Fact]
    public void UsesOnlyAttestedNpcLabelTranslationsForMissingBoundaries()
    {
        const string source = "Sundial Keeper{Line}”A plank is missing from the bench.“";
        const string target = "Смотритель часов{Line}У скамьи не хватает доски.";
        Assert.False(TranslationReviewRules.TryGetGlossaryWitness(target, source, out _, out _));
        Assert.True(TranslationReviewRules.TryGetGlossaryWitness("Смотритель часов{Line}«У скамьи не хватает доски».", source, out string english, out string russian));
        Assert.Equal("Sundial Keeper", english);
        Assert.Equal("Смотритель часов", russian);
        Assert.Equal(target, Review(source, target).Target);

        var names = new Dictionary<string, string> { [english] = russian };
        TranslationReviewResult result = TranslationReviewRules.Review("Sundial Keeper{Line}У скамьи не хватает доски.", source, "Fields", FieldKey, true, names);
        Assert.Equal("Смотритель часов{Line}«У скамьи не хватает доски».", result.Target);
        Assert.Empty(TranslationReviewRules.Review(result.Target, source, "Fields", FieldKey, true, names).Diagnostics);
    }

    [Theory]
    [InlineData("Use {Var 1} and {Var 2}.", "Используй {Var 1} и {Var 1}.", "tag-missing")]
    [InlineData("Call {Char Zell}.", "Позови Зелла.", "tag-missing")]
    [InlineData("Any text.", "Привет {Something New}.", "tag-unknown")]
    [InlineData("Any text.", "Привет {Glyph 203}.", "tag-malformed")]
    [InlineData("Any text.", "Привет {Char NotACharacter}.", "tag-malformed")]
    [InlineData("Any text.", "Привет {X1Y2}.", "tag-malformed")]
    public void ReportsAmbiguousControlsWithoutChangingText(string source, string target, string rule)
    {
        TranslationReviewResult result = Review(source, target);

        Assert.Equal(target, result.Target);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Rule == rule && !diagnostic.Fixable);
    }

    [Fact]
    public void HandlesOwnedInformationAndCompositeControls()
    {
        const string information = "{Info Gate 40}Текст{Info Link 2021}";
        Assert.Empty(TranslationReviewRules.Review(information, "{Info Gate 40}Guide{Info Link 2021}", "Information and SeeD exam", "entry", true).Diagnostics);
        Assert.Contains(TranslationReviewRules.Review(information, "{Info Link 2021}Guide{Info Gate 40}", "Information and SeeD exam", "entry", true).Diagnostics,
            diagnostic => diagnostic.Rule == "tag-contract" && !diagnostic.Fixable);
        Assert.Empty(TranslationReviewRules.Review("Версия {0}", "Version {0}", "System", "entry", true).Diagnostics);
        Assert.Contains(TranslationReviewRules.Review("Версия {1}", "Version {0}", "System", "entry", true).Diagnostics,
            diagnostic => diagnostic.Rule == "tag-contract" && !diagnostic.Fixable);
    }

    [Theory]
    [InlineData("Test.{End}", "Текст.{End}{End}", "Текст.{End}")]
    [InlineData("Test.{End}{End}", "Текст.{End}{End}{End}", "Текст.{End}{End}")]
    [InlineData("Test.", "Текст.{End}{End}", "Текст.{End}{End}")]
    [InlineData("Test.{End}", "Текст.", "Текст.")]
    [InlineData("Test.{End}", "Текст.{End}{Line}Да.{End}", "Текст.{End}{Line}Да.{End}")]
    [InlineData("Test.{End}", "{Invalid}{End}{End}", "{Invalid}{End}{End}")]
    public void RemovesOnlyReferenceBoundDuplicateTerminalEnds(string source, string target, string expected)
    {
        TranslationReviewResult result = Review(source, target);

        Assert.Equal(expected, result.Target);
        Assert.Equal(expected != target, result.Diagnostics.Any(diagnostic => diagnostic.Rule == "tag-terminal-duplicate" && diagnostic.Fixed));
    }

    [Theory]
    [InlineData("{Invalid}{End}")]
    [InlineData("{X12Y12}{Invalid}{Color White}{Line}{Next}{End}")]
    public void SkipsEditorialReviewForIntentionalInvalidMarkerOnlyRows(string target)
    {
        TranslationReviewResult result = Review("”Wait here.“{End}", target);

        Assert.Equal(target, result.Target);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void KeepsControlValidationForInvalidMarkerOnlyRows()
    {
        const string malformed = "{Invalid}{End broken}{Line}";
        const string missing = "{Invalid}{End}";

        TranslationReviewResult syntax = Review("”Wait here.“{End}", malformed);
        TranslationReviewResult contract = Review("”You have {Var 1}.“{End}", missing);

        Assert.Equal(malformed, syntax.Target);
        Assert.Equal("tag-malformed", Assert.Single(syntax.Diagnostics).Rule);
        Assert.Equal(missing, contract.Target);
        Assert.Equal("tag-missing", Assert.Single(contract.Diagnostics).Rule);
    }

    [Theory]
    [InlineData("Laguna{Line}”Wait here.“", "{Char Ward}{Line}«Жди здесь.»")]
    [InlineData("Laguna{Line}”Wait here.“", "Лагуна{Line}«Жди {Char Laguna} здесь.»")]
    [InlineData("Laguna{Line}”Wait here.“", "{Char Laguna}{Line}«Жди {Char Laguna} здесь.»")]
    [InlineData("Laguna{Line}A short biography.", "{Char Laguna}{Line}Краткая биография.")]
    [InlineData("Sorceress Edea{Line}”Wait here.“", "{Char Edea}{Line}«Жди здесь.»")]
    public void RuntimeDoesNotRelaxWrongIdentityOrBodyControls(string source, string target)
    {
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(new LocalizationRecord { Category = "Fields", Key = FieldKey, Source = source, Target = target }));
    }

    private static TranslationReviewResult Review(string source, string target)
    {
        return TranslationReviewRules.Review(target, source, "Fields", FieldKey, true);
    }
}
