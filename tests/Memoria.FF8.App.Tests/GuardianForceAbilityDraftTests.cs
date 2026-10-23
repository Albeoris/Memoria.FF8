using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityDraftTests
{
    private static GuardianForceAbilityDraft Draft(GuardianForceAbilityPatchDocument? document = null) => new(Enumerable.Range(0, 16).Select(_ => ImmutableArray.Create(new GuardianForceAbilityTemplate(1, false, 10, LearningPriority: 0), new GuardianForceAbilityTemplate(2, false, 20, LearningPriority: 1), new GuardianForceAbilityTemplate(3, true, 30, LearningPriority: 2))).ToImmutableArray(), Enumerable.Range(0, 116).Select(id => (byte)(id is >= 1 and <= 3 ? id * 10 : id)).ToImmutableArray(), document ?? new() { DocumentId = "draft" });

    [Fact]
    public void ReturningSavedGuardedContributionToInheritedValueRemovesItsNetOperation()
    {
        GuardianForceAbilityPatchDocument saved = new()
        {
            DocumentId = "saved",
            Operations = [new() { Id = "state", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.SetInitialState, Ability = 1, ExpectedInitialLearned = false, InitialLearned = true }]
        };
        var draft = Draft(saved);
        draft.Toggle(0, 1);
        Assert.Empty(draft.ExportDocument.Operations);
        Assert.True(draft.Dirty);
        draft.Undo(0, 1);
        Assert.Single(draft.ExportDocument.Operations);
        Assert.False(draft.Dirty);
    }
    [Fact]
    public void OneShotResetsIncludeOnlyExplicitNetStartingStateEdits()
    {
        var draft = Draft();
        draft.Append(0, 4);
        Assert.Empty(draft.EditedStartingStates);
        draft.Toggle(0, 4);
        Assert.Equal(new GuardianForceAbilityReset(0, 4, true), Assert.Single(draft.EditedStartingStates));
        draft.Toggle(0, 1);
        Assert.Equal(2, draft.EditedStartingStates.Length);
        draft.Toggle(0, 1);
        Assert.Single(draft.EditedStartingStates);
        draft.Accept(draft.ExportDocument);
        Assert.Empty(draft.EditedStartingStates);
        draft.Toggle(0, 4);
        Assert.Equal(new GuardianForceAbilityReset(0, 4, false), Assert.Single(draft.EditedStartingStates));
        draft.Undo(0, 4);
        Assert.Empty(draft.EditedStartingStates);
    }
    [Fact]
    public void ReplacementChainsExportFinalIntentAndReturnToInputIsClean()
    {
        var draft = Draft();
        draft.Replace(0, 1, 4);
        draft.Replace(0, 4, 5);
        Assert.Single(draft.ExportDocument.Operations);
        Assert.Equal(1, draft.ExportDocument.Operations[0].Ability);
        Assert.Equal(5, draft.ExportDocument.Operations[0].Replacement);
        draft.Replace(0, 5, 1);
        Assert.Empty(draft.ExportDocument.Operations);
        Assert.False(draft.Dirty);
        draft.Append(0, 6);
        draft.Replace(0, 6, 7);
        Assert.Single(draft.ExportDocument.Operations);
        Assert.Equal(GuardianForceAbilityOperationKind.EnsureAbility, draft.ExportDocument.Operations[0].Kind);
        Assert.Equal(7, draft.ExportDocument.Operations[0].Ability);
    }
    [Fact]
    public void TogglingAndSwappingBackRemovesInteractionHistoryButPreservesOtherEdits()
    {
        var draft = Draft();
        draft.Toggle(0, 1);
        draft.Append(1, 4);
        draft.Toggle(0, 1);
        draft.Swap(0, 1, 3);
        draft.Swap(0, 1, 3);
        Assert.Single(draft.ExportDocument.Operations);
        Assert.Equal(1, draft.ExportDocument.Operations[0].GuardianForce);
        Assert.False(draft.RowDirty(0, 1));
        Assert.True(draft.RowDirty(1, 4));
        draft.Undo(1, 4);
        Assert.False(draft.Dirty);
        Assert.Empty(draft.ExportDocument.Operations);
    }

    [Fact]
    public void AddedStartingStateIsFoldedIntoCreatorAndAcceptClearsUndoAndDirty()
    {
        var draft = Draft();
        draft.Append(0, 4);
        draft.Toggle(0, 4);
        var document = draft.ExportDocument;
        Assert.Single(document.Operations);
        Assert.Equal(GuardianForceAbilityOperationKind.EnsureAbility, document.Operations[0].Kind);
        Assert.True(document.Operations[0].InitialLearned);
        draft.Accept(document);
        Assert.False(draft.Dirty);
        Assert.False(draft.RowDirty(0, 4));
        draft.Undo(0, 4);
        Assert.Equal(document.Operations, draft.ExportDocument.Operations);
    }

    [Fact]
    public void ReplacementKeepsLogicalHandleAndUndoPreservesUnrelatedCommands()
    {
        var draft = Draft();
        string handle = draft.Handle(0, 1);
        draft.Replace(0, 1, 4);
        Assert.Equal(handle, draft.Handle(0, 4));
        draft.Append(1, 5);
        draft.Toggle(0, 4);
        draft.Undo(0, 4);
        Assert.False(draft.Preview.Templates[0].Single(entry => entry.Ability == 4).InitialLearned);
        draft.Undo(0, 4);
        Assert.Equal(handle, draft.Handle(0, 1));
        Assert.Contains(draft.Preview.Templates[1], entry => entry.Ability == 5);
        Assert.Single(draft.ExportDocument.Operations);
    }

    [Fact]
    public void SwapDependsOnBothCreatorsAndUndoIsAtomic()
    {
        var draft = Draft();
        draft.Append(0, 4);
        draft.Append(0, 5);
        draft.Swap(0, 4, 5);
        Assert.Equal(2, draft.ExportDocument.Operations[^1].Producers.Length);
        draft.Append(1, 6);
        draft.Undo(0, 4);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.Contains(draft.Preview.Templates[1], entry => entry.Ability == 6);
        draft.Undo(0, 4);
        Assert.DoesNotContain(draft.Preview.Templates[0], entry => entry.Ability == 4);
        Assert.Contains(draft.Preview.Templates[0], entry => entry.Ability == 5);
    }

    [Fact]
    public void RestoreInheritedIsUndoableAndRetainsUnrelatedHistory()
    {
        GuardianForceAbilityPatchDocument saved = new()
        {
            DocumentId = "saved",
            Operations = [new() { Id = "replace", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.ReplaceAbility, Ability = 1, Replacement = 4 }]
        };
        var draft = Draft(saved);
        draft.Append(1, 5);
        draft.RestoreInherited(0, 4);
        Assert.Contains(draft.Preview.Templates[0], entry => entry.Ability == 1);
        Assert.True(draft.Dirty);
        draft.Undo(0, 1);
        Assert.Contains(draft.Preview.Templates[0], entry => entry.Ability == 4);
        draft.Undo(1, 5);
        Assert.False(draft.Dirty);
    }

    [Fact]
    public void ReopenedContributionRemovalCanBeSavedAndFurtherEdited()
    {
        var draft = Draft();
        draft.Append(0, 4);
        draft.Accept(draft.ExportDocument);
        draft.RestoreInherited(0, 4);
        Assert.Empty(draft.ExportDocument.Operations);
        draft.Accept(draft.ExportDocument);
        Assert.False(draft.Dirty);
        draft.Append(0, 5);
        Assert.True(draft.Dirty);
        Assert.Single(draft.ExportDocument.Operations);
    }
}
