using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationCompositeFormatTests
{
    private static LocalizationRecord Row(string source, string target) => new() { Category = "System", File = "synthetic", Key = "$en_synthetic_0000", Source = source, Target = target };

    [Theory]
    [InlineData("Lv{0}", "Niveau {0}")]
    [InlineData("{0} / {1} / {0}", "{1} : {0} et {0}")]
    [InlineData("{0:D4} / {1:0.00}", "{1,12:0.00} : {0,-8:D4}")]
    [InlineData("{{{0}}}", "Valeur {{{0}}}")]
    [InlineData("Use {{Char Squall}} {0}", "Literal {{Char Irvine}} {0}")]
    public void ActualBclSyntaxAcceptsReorderingAlignmentFormatsAndEscapedLiterals(string source, string target)
    {
        LocalizationTextValidation.Validate(Row(source, target));
    }

    [Theory]
    [InlineData("Lv{0}", "Lv{99}")]
    [InlineData("Lv{0}", "Niveau")]
    [InlineData("Lv{0}", "{0} {0}")]
    [InlineData("{0} {1}", "{0}")]
    [InlineData("Lv{0}", "Lv{0")]
    [InlineData("Lv{0}", "Lv{0}}")]
    [InlineData("Lv{0}", "Lv{Color Red}{0}")]
    [InlineData("Plain", "{0}")]
    [InlineData("{0:D4}", "{0:D99999999}")]
    [InlineData("{0}", "{0,500000}")]
    [InlineData("{0}", "{999999}")]
    public void BackendRejectsDroppedExtraMalformedOrOversizedArguments(string source, string target)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Validate(new LocalizationProject { Manifest = new() { TargetLanguage = "test" }, Entries = [Row(source, target)] }));
        Assert.Contains("$en_synthetic_0000", error.Message);
    }

    [Fact]
    public void OversizedAlignmentValidationDoesNotAllocateTheRequestedOutput()
    {
        LocalizationRecord invalid = Row("{0}", "{0,500000}");
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(invalid));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(invalid));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, $"Bounded validation allocated {allocated} bytes.");
    }

    [Fact]
    public void CanonicalSystemOwnerCannotBeChangedByEditingSourceOrItsPresence()
    {
        LocalizationRecord row = new() { Category = "System", File = SystemTextCatalog.File, Key = SystemTextCatalog.Key("ui.buildmain.label.lv"), Source = "No arguments", SourcePresent = false, Target = "Lv{99}" };
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row));
        LocalizationTextValidation.Validate(row with { Target = "Niveau {0}" });
    }

    [Fact]
    public void NativeControlOwnedSystemTextUsesItsActualControlContract()
    {
        LocalizationTextValidation.Validate(Row("{Char Squall}{Line}Ready", "{Char Squall}{Line}Prêt"));
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(Row("{Char Squall}{Line}Ready", "{Char Irvine}{Line}Prêt")));
    }

    [Fact]
    public void VerifiedSystemInsertionKeepsEnglishOwnerIndependentOfScriptBase()
    {
        LocalizationSourceEntry source = SystemTextCatalog.SourceEntries().Single(entry => entry.Key == SystemTextCatalog.Key("ui.buildmain.label.lv"));
        LocalizationManifest manifest = new() { FieldScriptBaseLanguage = "jp", SourceLanguage = "jp", TargetLanguage = "ru", ReferenceLanguages = new Dictionary<string, string> { ["Reference [jp]"] = "jp", ["Reference [en]"] = "en" } };
        LocalizationRecord prepared = LocalizationProjectBuilder.CreateVerifiedRecord(source, manifest);
        Assert.False(prepared.SourcePresent);
        Assert.Null(prepared.Source);
        Assert.False(prepared.ReferencePresence["Reference [jp]"]);
        Assert.Equal(source.Text, prepared.References["Reference [en]"]);
        Assert.True(prepared.ExtensionData["equivalentKeys"].TryGetProperty("en", out _));
        Assert.False(prepared.ExtensionData["equivalentKeys"].TryGetProperty("jp", out _));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectBuilder.CreateVerifiedRecord(source with { File = "invented" }, manifest));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectBuilder.CreateVerifiedRecord(source with { Key = "$en_interface_system_invented" }, manifest));
        Assert.Throws<InvalidDataException>(() => LocalizationProjectBuilder.CreateVerifiedRecord(source with { Text = "Invented template" }, manifest));
    }
}
