using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationGeneratedDrawPointTests
{
    [Fact]
    public void CommonTemplateKeepsControlsAndUsesFrozenPartyWithoutFieldOrdinalLookup()
    {
        NativeDrawPointCatalog catalog = Catalog();
        NativeDrawPointInteraction interaction = Interaction(catalog) with { Message = 5, Selection = 1 };
        string key = LocalizationSourceExporter.IndexedKey("en", "drawpoints_world", 5);
        string translated = "Translated " + catalog.Message(5);
        LocalizationCatalog localization = new(new Dictionary<string, LocalizationValue>
        {
            [key] = new(translated, "selected", "project"),
            [FieldLocalizationKey.Create("en", "fixture", 0)] = new("WRONG FIELD ROW", "other", "other")
        });
        List<string> queried = [];
        int[] party = [0, 1, 255];
        RecreateGame.GeneratedAuthoringDialogue presentation = RecreateGame.CreateGeneratedDrawPointPresentation(interaction, catalog, null, party, "en", (identity, source) =>
        {
            queried.Add(identity);
            return localization.ResolveText(identity, source);
        }, 7);
        party[1] = 0;
        Assert.Equal(key, Assert.Single(queried));
        Assert.Equal("Draw points", presentation.Binding.Category);
        Assert.Contains("{Name 32}", presentation.Binding.Template);
        Assert.Contains("{Char Squall}", presentation.Binding.Template);
        Assert.Contains("{Var 55}", presentation.Binding.Template);
        Assert.Contains("{Char Zell}", presentation.Binding.Text);
        Assert.Contains("7 Frozen magic", presentation.Binding.Text);
        Assert.DoesNotContain("WRONG FIELD", presentation.Binding.Text);
        Assert.Equal(presentation.Binding.Text, presentation.FormatTemplate(translated));
        Assert.Throws<InvalidDataException>(() => presentation.FormatTemplate(translated.Replace("{Name 32}", "Frozen magic", StringComparison.Ordinal)));
        Assert.Equal("{Char Squall} took {Var 55} {Name 32}.", catalog.CommonMessages[5]);
        Assert.Equal(new string('A', 64), catalog.CommonMessageHash);
        Assert.All(catalog.CommonMessageHashes, hash => Assert.Equal(new string('A', 64), hash));
        Assert.All(catalog.CommonMessageBytes, bytes => Assert.Equal(new byte[] { 0 }, bytes));
        Assert.All(catalog.PointBytes, value => Assert.Equal((byte)6, value));
    }

    [Fact]
    public void UnchangedCommonTemplatePreservesNativeDialogueReferenceAndPlayerOwnership()
    {
        NativeDrawPointCatalog catalog = Catalog();
        NativeDrawPointInteraction interaction = Interaction(catalog) with { Message = 5 };
        DialogueState native = new(6, -1, catalog.FormatMessage(5, 6, 0), 0, 0);
        RecreateGame.GeneratedAuthoringDialogue presentation = RecreateGame.CreateGeneratedDrawPointPresentation(interaction, catalog, null, [0, 255, 255], "en", LocalizationCatalog.Empty.ResolveText, 7);
        Assert.Same(native, presentation.Project(native));
        Assert.Equal(-1, native.MessageId);
        Assert.Contains("{Var 55}", native.Text);
    }

    [Fact]
    public void SelectionRequiresFiveNativeLinesPerRowControlsAndJunctionPadding()
    {
        string source = Catalog().Message(4);
        RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, "Translated " + source, "key");
        RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, "", "key");
        Assert.Throws<InvalidDataException>(() => RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, source + "{Line}extra", "key"));
        Assert.Throws<InvalidDataException>(() => RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, source.Replace(":  ", ": ", StringComparison.Ordinal), "key"));
        Assert.Throws<InvalidDataException>(() => RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, source.Replace("{Color Disabled}", "", StringComparison.Ordinal), "key"));
        string[] lines = source.Split("{Line}");
        lines[2] = lines[2].Replace("{Var 53}", "{Var 54}", StringComparison.Ordinal);
        lines[3] = lines[3].Replace("{Var 54}", "{Var 53}", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => RecreateGame.ValidateGeneratedDrawPointTemplate(4, source, string.Join("{Line}", lines), "key"));
    }

    [Fact]
    public void SourceBankMismatchFailsBeforeFormatting()
    {
        NativeDrawPointCatalog catalog = Catalog();
        NativeDrawPointInteraction interaction = Interaction(catalog) with { CommonMessageHash = new string('B', 64) };
        Assert.Throws<InvalidDataException>(() => RecreateGame.CreateGeneratedDrawPointPresentation(interaction, catalog, null, [], "en", LocalizationCatalog.Empty.ResolveText));
    }

    [Fact]
    public void InstalledActiveDrawPointLocalizesOnlyPresentationAndPreservesAuthenticatedBank()
    {
        string game = TestResources.GameDirectory;
        string path = TestResources.FixturePath("App.Tests.LocalizationGeneratedDrawPointTests.Active");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(path));
        GameSession session = new(GameSession.Load(path));
        using GameResourceHost resources = new(game, watch: false);
        using GameWorld world = new(session, game, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, readSceneMods: () => resources.SceneMods, modelOverrides: resources);
        world.EnsureField();
        DialogueState native = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(-1, native.MessageId);
        NativeDrawPointInteraction interaction = Assert.IsType<NativeDrawPointInteraction>(world.Events.Scene.DrawPointInteraction);
        NativeDrawPointCatalog catalog = world.Events.ReadNativeDrawPointCatalog!();
        string key = LocalizationSourceExporter.IndexedKey(resources.Resources.Source.Language, "drawpoints_world", interaction.Message);
        string target = "日本語 " + catalog.Message(interaction.Message);
        resources.Resources.PrepareLocalization(new LocalizationCatalog(new Dictionary<string, LocalizationValue>
        {
            [key] = new(target, "selected", "project"),
            [LocalizationSourceExporter.IndexedKey(resources.Resources.Source.Language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), interaction.PointByte & 63)] = new("Localized magic", "selected", "project"),
            [FieldLocalizationKey.Create(resources.Resources.Source.Language, world.Field!.Name, 0)] = new("WRONG FIELD ROW", "other", "other")
        })).Commit("Generated common-text presentation test");
        JsonElement before = world.Events.Capture();
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        foreach ((string field, object value) in new (string, object)[] { ("session", session), ("resourceHost", resources), ("world", world) })
        {
            typeof(RecreateGame).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);
        }
        object?[] arguments = [native, null];
        Assert.True((bool)typeof(RecreateGame).GetMethod("TryGeneratedAuthoringDialogue", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, arguments)!);
        RecreateGame.GeneratedAuthoringDialogue presentation = Assert.IsType<RecreateGame.GeneratedAuthoringDialogue>(arguments[1]);
        DialogueState display = presentation.Project(native);
        Assert.StartsWith("日本語 ", display.Text);
        Assert.Contains("Localized magic", display.Text);
        Assert.DoesNotContain("WRONG FIELD ROW", display.Text);
        Assert.Equal(native.Operation, display.Operation);
        Assert.Equal(native.FirstChoice, display.FirstChoice);
        Assert.Equal(native.LastChoice, display.LastChoice);
        Assert.Equal(native.InitialChoice, display.InitialChoice);
        Assert.Equal(native.CancelChoice, display.CancelChoice);
        Assert.Contains("{Name 32}", presentation.Binding.Template);
        Assert.Equal(catalog.Message(interaction.Message), presentation.Binding.Source);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(interaction.CommonMessageHash, catalog.CommonMessageHash);
    }

    private static NativeDrawPointInteraction Interaction(NativeDrawPointCatalog catalog) => new(331, new string('A', 64), 425, 5, 2, 42, 1, 14, 13, 6, 0, 1, 2, 5, catalog.CommonMessageHash) { Message = 2, EligibleCombatSlots = 3 };
    private static NativeDrawPointCatalog Catalog()
    {
        string[] messages = Enumerable.Range(0, 8).Select(index => "Message " + index).ToArray();
        messages[2] = "Found {Name 32}.";
        messages[4] = "Who?{Line}Cancel{Line}{Color Disabled}{Char Squall}:  {Var 53}{Line}{Color Disabled}{Char Squall}:  {Var 54}{Line}{Color Disabled}{Char Squall}:  {Var 55}";
        messages[5] = "{Char Squall} took {Var 55} {Name 32}.";
        messages[7] = "No eligible character.";
        return new(new string('A', 64), new string('B', 64), Enumerable.Repeat((byte)6, 256).ToImmutableArray(), Enumerable.Repeat((byte)0, 19).ToImmutableArray(), messages[7], new string('A', 64))
        {
            CommonMessages = messages.ToImmutableArray(),
            CommonMessageHashes = Enumerable.Repeat(new string('A', 64), 8).ToImmutableArray(),
            CommonMessageBytes = Enumerable.Range(0, 8).Select(_ => ImmutableArray.Create((byte)0)).ToImmutableArray(),
            MagicNames = Enumerable.Range(0, 57).Select(index => index == 6 ? "Frozen magic" : "Magic " + index).ToImmutableArray()
        };
    }
}
