using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityLocalizationTests
{
    [Theory]
    [InlineData("none", "Undo")]
    [InlineData("add", "Undo add")]
    [InlineData("replace", "Undo replace")]
    [InlineData("state", "Undo starting state")]
    [InlineData("swap", "Undo swap with Ability2")]
    public void UndoAndReviewReadNewGenerationWithoutChangingDraftCommands(string action, string expected)
    {
        NativeCharacterVitals source = GuardianForceAbilityEditorTests.Source();
        GuardianForceAbilityDraft draft = new(source.ResolveGuardianForceTemplates(), source.MenuDefinitions.AbilityPoints, new() { DocumentId = "fixture" });
        switch (action)
        {
            case "add":
                draft.Append(0, 4);
                break;
            case "replace":
                draft.Replace(0, 1, 4);
                break;
            case "state":
                draft.Toggle(0, 1);
                break;
            case "swap":
                draft.Swap(0, 1, 2);
                break;
        }
        int ability = action is "add" or "replace" ? 4 : 1;
        string before = JsonSerializer.Serialize(draft.ExportDocument);
        string generation = "第一";
        string Localize(string id, string fallback) => generation + " " + fallback;
        string Name(int id) => "Ability" + id;
        Assert.Equal(expected, draft.UndoDescription(0, ability, Name));
        Assert.StartsWith("第一 ", draft.UndoDescription(0, ability, Name, Localize));
        Assert.All(draft.UndoReview(0, ability, Name, Localize), line => Assert.StartsWith("第一 ", line));
        generation = "第二";
        Assert.StartsWith("第二 ", draft.UndoDescription(0, ability, Name, Localize));
        Assert.All(draft.UndoReview(0, ability, Name, Localize), line => Assert.StartsWith("第二 ", line));
        Assert.Equal(before, JsonSerializer.Serialize(draft.ExportDocument));
    }

    [Fact]
    public void InheritedReviewAndPreviewConsequencesKeepEnglishFallbackAndSavedOwners()
    {
        NativeCharacterVitals source = GuardianForceAbilityEditorTests.Source();
        GameSession session = new(new GameState { CharacterVitals = source });
        string before = JsonSerializer.Serialize(session.State);
        GuardianForceAbilityDraft draft = new(source.ResolveGuardianForceTemplates(), source.MenuDefinitions.AbilityPoints, new() { DocumentId = "fixture" });
        draft.Toggle(0, 1);
        string generation = "第一";
        string Localize(string id, string fallback) => generation + " " + fallback;
        string Name(int id) => "Ability" + id;
        Assert.All(draft.RestoreInheritedReview(0, 1, Name, Localize), line => Assert.StartsWith("第一 ", line));
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(draft.Preview.Templates, resets: [new(0, 1, true)]);
        Assert.NotEmpty(preview.Consequences);
        Assert.Equal(preview.Consequences, preview.FormatConsequences());
        Assert.Equal(preview.Consequences.Length, preview.ConsequenceTexts.Length);
        Assert.All(preview.FormatConsequences(Localize), line => Assert.StartsWith("第一 ", line));
        generation = "第二";
        Assert.All(preview.FormatConsequences(Localize), line => Assert.StartsWith("第二 ", line));
        Assert.All(draft.RestoreInheritedReview(0, 1, Name, Localize), line => Assert.StartsWith("第二 ", line));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.DoesNotContain("ConsequenceTexts", JsonSerializer.Serialize(preview));
        GuardianForceAbilityOperationOutcome outcome = Assert.Single(draft.Preview.Outcomes);
        Assert.Equal(outcome.Message, outcome.FormatMessage());
        Assert.StartsWith("第二 ", outcome.FormatMessage(Localize));
        Assert.DoesNotContain("MessageText", JsonSerializer.Serialize(outcome));
    }
}
