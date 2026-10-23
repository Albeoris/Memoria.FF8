using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityEditorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-gf-editor-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MouseCatalogAndAddAppendWithoutMutatingThePlaythrough()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        ClickAbility(fixture.Menu, 4, catalog: true);
        Assert.False(Draft(fixture.Menu).Dirty);
        Click(fixture.Menu, "Add");
        Assert.Equal(4, Draft(fixture.Menu).Preview.Templates[0][^1].Ability);
        Assert.Equal(3, Field<int>(fixture.Menu, "guardianRowCursor"));
        Click(fixture.Menu, "Save mod");
        GuardianForceAbilityPatchOperation operation = Assert.Single(fixture.Saved!.Operations);
        Assert.Equal(4, operation.Ability);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void ClickingADirtyRowSelectsWithoutUndoingIt()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Alternate);
        string edited = JsonSerializer.Serialize(Draft(fixture.Menu).ExportDocument);
        ClickAbility(fixture.Menu, 2, catalog: false);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianRowCursor"));
        Assert.Equal(edited, JsonSerializer.Serialize(Draft(fixture.Menu).ExportDocument));
        Assert.Equal("rows", Field<string>(fixture.Menu, "guardianFocus"));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void PointerCheckboxTogglesExactlyOnceWithoutChangingCurrentProgress()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        UiRect name = Render(fixture.Menu).Texts.Where(text => text.Value == "Ability2").MaxBy(text => text.Bounds.X).Bounds;
        ClickAt(fixture.Menu, name.Right + 34 * fixture.Menu.TextScale, name.Y + name.Height / 2);
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][1].InitialLearned);
        Assert.Single(Draft(fixture.Menu).ExportDocument.Operations);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(Draft(fixture.Menu).Dirty);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void PreviewIsReadOnlyAndRestoresTheEditorViewState()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusLeft);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Alternate);
        int catalogCursor = Field<int>(fixture.Menu, "guardianCatalogCursor");
        int rowCursor = Field<int>(fixture.Menu, "guardianRowCursor");
        string document = JsonSerializer.Serialize(Draft(fixture.Menu).ExportDocument);
        string playthrough = JsonSerializer.Serialize(fixture.Session.State);
        Click(fixture.Menu, "Preview");
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("Read-only", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Render(fixture.Menu).Texts, text => text.Value is "Add" or "Replace" or "Swap" or "Undo" or "Save mod");
        fixture.Menu.Navigate(MenuInput.Alternate);
        fixture.Menu.Navigate(MenuInput.PageRight);
        fixture.Menu.Navigate(MenuInput.Down);
        Assert.Equal(document, JsonSerializer.Serialize(Draft(fixture.Menu).ExportDocument));
        Assert.Equal(playthrough, JsonSerializer.Serialize(fixture.Session.State));
        Click(fixture.Menu, "Back to editor");
        Assert.Equal(0, Field<int>(fixture.Menu, "guardianSubject"));
        Assert.Equal(catalogCursor, Field<int>(fixture.Menu, "guardianCatalogCursor"));
        Assert.Equal(rowCursor, Field<int>(fixture.Menu, "guardianRowCursor"));
    }

    [Fact]
    public void TargetsIncludeDisabledPackagesAndAllSixteenGfsWithoutAcquiringThem()
    {
        string package = Path.Combine(root, "Mods", "Memoria", "disabled");
        Directory.CreateDirectory(Path.Combine(package, "gameplay"));
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0\n");
        File.WriteAllBytes(Path.Combine(package, GuardianForceAbilityMods.PayloadPath), GuardianForceAbilityMods.Serialize(new()
        {
            DocumentId = "disabled-document"
        }));
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Fixture fixture = Create(runtime);
        fixture.Menu.Execute(new("gf-editor-target", "Memoria/disabled"));
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("disabled", StringComparison.Ordinal));
        fixture.Menu.Navigate(MenuInput.PageLeft);
        Assert.Equal(15, Field<int>(fixture.Menu, "guardianSubject"));
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("Not acquired", StringComparison.Ordinal));
        Assert.Equal(0, fixture.Session.CharacterVitals!.GuardianForces[15].Flags & 1);
        for (int id = 0; id < 16; id++)
        {
            fixture.Menu.Navigate(MenuInput.PageRight);
            Assert.Equal(id, Field<int>(fixture.Menu, "guardianSubject"));
        }
    }

    [Fact]
    public void DisabledPackageThisModOnlyPreviewIncludesItsContributionIndependentlyOfCurrentSetup()
    {
        string package = Path.Combine(root, "Mods", "Memoria", "disabled");
        Directory.CreateDirectory(Path.Combine(package, "gameplay"));
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0\n");
        File.WriteAllBytes(Path.Combine(package, GuardianForceAbilityMods.PayloadPath), GuardianForceAbilityMods.Serialize(new()
        {
            DocumentId = "disabled-document",
            Operations = [new() { Id = "add-fourth", GuardianForce = 0, Ability = 4, Kind = GuardianForceAbilityOperationKind.EnsureAbility, InitialLearned = false }]
        }));
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Fixture fixture = Create(runtime);
        fixture.Menu.Execute(new("gf-editor-target", "Memoria/disabled"));
        string playthrough = JsonSerializer.Serialize(fixture.Session.State);
        Click(fixture.Menu, "Preview");
        Assert.True(Field<bool>(fixture.Menu, "guardianWithoutLayer"));
        Assert.DoesNotContain(Render(fixture.Menu).Texts, text => text.Value == "Ability4");
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "mode:0"));
        Assert.DoesNotContain(Render(fixture.Menu).Texts, text => text.Value == "Ability4");
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "mode:1"));
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value == "Ability4");
        Assert.True(Field<bool>(fixture.Menu, "guardianWithoutLayer"));
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "mode:2"));
        Assert.DoesNotContain(Render(fixture.Menu).Texts, text => text.Value == "Ability4");
        Assert.Equal(playthrough, JsonSerializer.Serialize(fixture.Session.State));
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void RebuildingTheViewRetainsRowFocusAndUndoKeepsSessionPure()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        Render(fixture.Menu);
        fixture.Menu.InvalidateLiveData();
        Render(fixture.Menu);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][1].InitialLearned);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(Draft(fixture.Menu).Dirty);
        Click(fixture.Menu, "Save mod");
        Assert.Empty(fixture.Saved!.Operations);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void FocusedSaveOwnsConfirmWhileADirtyRowRemainsSelected()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Render(fixture.Menu);
        for (int remaining = 40; remaining > 0 && Field<string>(fixture.Menu, "guardianFocus") != "save"; remaining--)
        {
            fixture.Menu.Navigate(MenuInput.Tab);
        }
        Assert.Equal("save", Field<string>(fixture.Menu, "guardianFocus"));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Single(fixture.Saved!.Operations);
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][0].InitialLearned);
    }

    [Theory]
    [InlineData(MenuInput.Right)]
    [InlineData(MenuInput.PageDown)]
    public void ListPagingPreservesTheSlotAndClampsTheFinalPage(MenuInput nextPage)
    {
        Fixture fixture = Create(source: Source(29));
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        Render(fixture.Menu);
        SetField(fixture.Menu, "guardianRowsPerColumn", 4);
        SetField(fixture.Menu, "guardianColumns", 2);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(nextPage);
        Assert.Equal(10, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(nextPage);
        Assert.Equal(18, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(nextPage);
        Assert.Equal(26, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(nextPage);
        Assert.Equal(28, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(nextPage == MenuInput.Right ? MenuInput.Left : MenuInput.PageUp);
        Assert.Equal(20, Field<int>(fixture.Menu, "guardianRowCursor"));
    }

    [Fact]
    public void SwitchColumnMovesWithinThePageAndClampsTheShorterColumn()
    {
        Fixture fixture = Create(source: Source(6));
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        Render(fixture.Menu);
        SetField(fixture.Menu, "guardianRowsPerColumn", 4);
        SetField(fixture.Menu, "guardianColumns", 2);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.SwitchColumn);
        Assert.Equal(5, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(MenuInput.SwitchColumn);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianRowCursor"));
        SetField(fixture.Menu, "guardianColumns", 1);
        fixture.Menu.Navigate(MenuInput.SwitchColumn);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianRowCursor"));
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void CatalogPagesIndependentlyAndBoundaryInputsKeepListFocus()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        Render(fixture.Menu);
        SetField(fixture.Menu, "guardianCatalogCapacity", 5);
        fixture.Menu.Navigate(MenuInput.FocusLeft);
        fixture.Menu.Navigate(MenuInput.Left);
        fixture.Menu.Navigate(MenuInput.Up);
        Assert.Equal(0, Field<int>(fixture.Menu, "guardianCatalogCursor"));
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Right);
        Assert.Equal(6, Field<int>(fixture.Menu, "guardianCatalogCursor"));
        Assert.Equal(0, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(MenuInput.PageUp);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianCatalogCursor"));
    }

    [Fact]
    public void SearchOwnsArrowsAndCancelThenEmptyCatalogConsumesNavigation()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusLeft);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "search"));
        Assert.True(fixture.Menu.EnterGuardianSearchCharacter('Z'));
        fixture.Menu.Navigate(MenuInput.Left);
        Assert.Equal(0, Field<int>(fixture.Menu, "guardianSearchCaret"));
        Assert.True(fixture.Menu.EnterGuardianSearchCharacter('Q'));
        Assert.Equal("QZ", Field<string>(fixture.Menu, "guardianSearch"));
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.False(fixture.Menu.IsGuardianSearchEditing);
        foreach (MenuInput input in new[] { MenuInput.Left, MenuInput.Right, MenuInput.Up, MenuInput.Down, MenuInput.PageUp, MenuInput.PageDown })
        {
            fixture.Menu.Navigate(input);
            Assert.Equal("catalog", Field<string>(fixture.Menu, "guardianFocus"));
            Assert.Equal(0, Field<int>(fixture.Menu, "guardianCatalogCursor"));
        }
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("No abilities match", StringComparison.Ordinal));
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void ArmedReplaceConfirmTakesPrecedenceOverDirtyRowUndo()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Alternate);
        ClickAbility(fixture.Menu, 4, catalog: true);
        Click(fixture.Menu, "Replace");
        ClickAbility(fixture.Menu, 2, catalog: false);
        Assert.Equal(new[] { 1, 2, 3 }, Draft(fixture.Menu).Preview.Templates[0].Select(entry => entry.Ability));
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][1].InitialLearned);
        Assert.Equal("rows", Field<string>(fixture.Menu, "guardianFocus"));
        Assert.Equal("replace", Field<string?>(fixture.Menu, "guardianPending"));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { 1, 4, 3 }, Draft(fixture.Menu).Preview.Templates[0].Select(entry => entry.Ability));
        Assert.Null(Field<string?>(fixture.Menu, "guardianPending"));
    }

    [Fact]
    public void ArmedSwapConfirmTakesPrecedenceOverDirtyRowUndoAndFollowsAnchor()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Alternate);
        fixture.Menu.Navigate(MenuInput.Up);
        Click(fixture.Menu, "Swap");
        ClickAbility(fixture.Menu, 2, catalog: false);
        Assert.Equal(new[] { 1, 2, 3 }, Draft(fixture.Menu).Preview.Templates[0].Select(entry => entry.Ability));
        Assert.Equal("rows", Field<string>(fixture.Menu, "guardianFocus"));
        Assert.Equal("swap", Field<string?>(fixture.Menu, "guardianPending"));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { 2, 1, 3 }, Draft(fixture.Menu).Preview.Templates[0].Select(entry => entry.Ability));
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][0].InitialLearned);
        Assert.Equal(1, Field<int>(fixture.Menu, "guardianRowCursor"));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { 1, 2, 3 }, Draft(fixture.Menu).Preview.Templates[0].Select(entry => entry.Ability));
        Assert.True(Draft(fixture.Menu).Preview.Templates[0][1].InitialLearned);
    }

    [Fact]
    public void PagingAnArmedSwapPreservesItsAnchorAndDoesNotMutateUntilConfirm()
    {
        Fixture fixture = Create(source: Source(29));
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        Click(fixture.Menu, "Swap");
        SetField(fixture.Menu, "guardianRowsPerColumn", 4);
        SetField(fixture.Menu, "guardianColumns", 2);
        fixture.Menu.Navigate(MenuInput.Right);
        Assert.NotNull(Field<string?>(fixture.Menu, "guardianPending"));
        Assert.Equal(8, Field<int>(fixture.Menu, "guardianRowCursor"));
        Assert.False(Draft(fixture.Menu).Dirty);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal(9, Draft(fixture.Menu).Preview.Templates[0][0].Ability);
        Assert.Equal(1, Draft(fixture.Menu).Preview.Templates[0][8].Ability);
        Assert.Equal(8, Field<int>(fixture.Menu, "guardianRowCursor"));
        Assert.Equal(0, Draft(fixture.Menu).Preview.Templates[0][8].LearningPriority);
    }

    [Fact]
    public void ArmedSwapSurvivesAViewRebuildAndDisablesStartingStateEdits()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        Click(fixture.Menu, "Swap");
        fixture.Menu.InvalidateLiveData();
        Render(fixture.Menu);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.NotNull(Field<string?>(fixture.Menu, "guardianPending"));
        Assert.False(Draft(fixture.Menu).Dirty);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Null(Field<string?>(fixture.Menu, "guardianPending"));
        Assert.Equal("rows", Field<string>(fixture.Menu, "guardianFocus"));
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Theory]
    [InlineData(MenuInput.Cancel)]
    [InlineData(MenuInput.FocusLeft)]
    [InlineData(MenuInput.PageRight)]
    [InlineData(MenuInput.Actions)]
    public void LeavingTargetSelectionCancelsThePendingOperation(MenuInput input)
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        Click(fixture.Menu, "Swap");
        Assert.NotNull(Field<string?>(fixture.Menu, "guardianPending"));
        fixture.Menu.Navigate(input);
        Assert.Null(Field<string?>(fixture.Menu, "guardianPending"));
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void ApplyingAPackageDraftRetainsItsDirtyCheckpointAndUndoHistory()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        fixture.Menu.Navigate(MenuInput.Actions);
        Click(fixture.Menu, "Apply to this playthrough");
        Assert.Equal("gf-ability-review", fixture.Menu.ScreenId);
        fixture.Menu.Execute(new("gf-editor-apply"));
        Assert.True(Draft(fixture.Menu).Dirty);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(Draft(fixture.Menu).Dirty);
        Assert.Null(fixture.Saved);
    }

    [Fact]
    public void ApplyReviewRejectsAChangedPlaythrough()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        fixture.Menu.Navigate(MenuInput.Actions);
        Click(fixture.Menu, "Apply to this playthrough");
        Assert.Equal("gf-ability-review", fixture.Menu.ScreenId);
        fixture.Session.SetCharacterVitals(fixture.Session.CharacterVitals! with
        {
            DreamEnabled = true
        });
        string before = JsonSerializer.Serialize(fixture.Session.State);
        fixture.Menu.Execute(new("gf-editor-apply"));
        Assert.Contains("stale", fixture.Menu.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("gf-ability-review", fixture.Menu.ScreenId);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void SavingAtAMateriallyDifferentLayerPositionRequiresReviewBeforeRebasing()
    {
        GuardianForceAbilityPatchDocument preceding = new()
        {
            DocumentId = "preceding",
            Operations = [new() { Id = "learn-second", GuardianForce = 0, Ability = 2, Kind = GuardianForceAbilityOperationKind.SetInitialState, InitialLearned = true }]
        };
        Fixture fixture = Create(layers: [new("Z-layer", preceding)]);
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        GuardianForceAbilityDraft draft = Draft(fixture.Menu);
        Assert.True(draft.Preview.Templates[0][1].InitialLearned);
        string document = JsonSerializer.Serialize(draft.ExportDocument);
        string playthrough = JsonSerializer.Serialize(fixture.Session.State);
        Assert.True(fixture.Menu.ReviewGuardianPackagePlacement("Zz-candidate", draft.ExportDocument));
        Assert.False(fixture.Menu.ReviewGuardianPackagePlacement("A-candidate", draft.ExportDocument));
        Assert.Equal("mods-gf-placement-review", fixture.Menu.ScreenId);
        Assert.Equal(document, JsonSerializer.Serialize(draft.ExportDocument));
        Assert.True(draft.Preview.Templates[0][1].InitialLearned);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.True(draft.Preview.Templates[0][1].InitialLearned);
        Assert.False(fixture.Menu.ReviewGuardianPackagePlacement("A-candidate", draft.ExportDocument));
        fixture.Menu.Execute(new("gf-editor-placement-accept"));
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.True(draft.Dirty);
        Assert.True(draft.Preview.Templates[0][0].InitialLearned);
        Assert.False(draft.Preview.Templates[0][1].InitialLearned);
        Assert.True(fixture.Menu.ReviewGuardianPackagePlacement("A-candidate", draft.ExportDocument));
        Assert.Null(fixture.Saved);
        Assert.Equal(playthrough, JsonSerializer.Serialize(fixture.Session.State));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(draft.Dirty);
    }

    [Fact]
    public void CancellingDirtyNavigationDoesNotResumeItAfterALaterApply()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "local"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        string documentId = Draft(fixture.Menu).ExportDocument.DocumentId;
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        Assert.Equal("gf-ability-dirty", fixture.Menu.ScreenId);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.True(Draft(fixture.Menu).Dirty);
        Click(fixture.Menu, "Apply changes");
        fixture.Menu.Execute(new("gf-editor-apply"));
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.True(Field<bool>(fixture.Menu, "guardianLocal"));
        Assert.Equal(documentId, Draft(fixture.Menu).ExportDocument.DocumentId);
        Assert.False(Draft(fixture.Menu).Dirty);
    }

    [Fact]
    public void RestoreInheritedReviewsThenCreatesANewUndoableDraftEdit()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        GuardianForceAbilityDraft draft = Draft(fixture.Menu);
        draft.Toggle(0, 1);
        draft.Accept(draft.ExportDocument);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "tool:4"));
        Assert.Equal("mods-gf-restore-review", fixture.Menu.ScreenId);
        Assert.False(draft.Dirty);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.True(draft.Preview.Templates[0][0].InitialLearned);
        fixture.Menu.Navigate(MenuInput.Actions);
        fixture.Menu.Execute(new("gf-editor-command", "tool:4"));
        fixture.Menu.Execute(new("gf-editor-restore-accept"));
        Assert.True(draft.Dirty);
        Assert.False(draft.Preview.Templates[0][0].InitialLearned);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.False(draft.Dirty);
        Assert.True(draft.Preview.Templates[0][0].InitialLearned);
    }

    [Fact]
    public void UndoFromAChangedDependentRowReviewsAdditionalCommandsAndRestoresTheRelationship()
    {
        NativeCharacterVitals source = Source();
        var templates = source.MenuDefinitions.Templates.SetItem(0, source.MenuDefinitions.Templates[0].SetItem(2, source.MenuDefinitions.Templates[0][2] with { PrerequisiteAbility = 1 }));
        source = source with { MenuDefinitions = source.MenuDefinitions with { Templates = templates, SourceTemplates = templates } };
        Fixture fixture = Create(source: source);
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        GuardianForceAbilityDraft draft = Draft(fixture.Menu);
        draft.Replace(0, 1, 4);
        draft.Toggle(0, 4);
        Assert.Equal(4, draft.Preview.Templates[0][2].PrerequisiteAbility);
        Assert.True(draft.RowDirty(0, 3));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        fixture.Menu.Navigate(MenuInput.Down);
        string before = JsonSerializer.Serialize(draft.ExportDocument);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal("mods-gf-undo-review", fixture.Menu.ScreenId);
        Assert.Equal(before, JsonSerializer.Serialize(draft.ExportDocument));
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal(before, JsonSerializer.Serialize(draft.ExportDocument));
        fixture.Menu.Navigate(MenuInput.Confirm);
        fixture.Menu.Execute(new("gf-editor-undo-accept"));
        Assert.Equal(new[] { 1, 2, 3 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.Equal(1, draft.Preview.Templates[0][2].PrerequisiteAbility);
        Assert.False(draft.Dirty);
    }

    [Fact]
    public void ReplacementSwapToggleAndRowUndoKeepStableIdentityAndLearningPriority()
    {
        NativeCharacterVitals source = Source();
        GuardianForceAbilityDraft draft = new(source.ResolveGuardianForceTemplates(), source.MenuDefinitions.AbilityPoints, new()
        {
            DocumentId = "selected"
        });
        draft.Replace(0, 1, 4);
        draft.Toggle(0, 4);
        draft.Swap(0, 4, 3);
        Assert.Equal(new[] { 3, 2, 4 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.True(draft.Preview.Templates[0][2].InitialLearned);
        Assert.Equal(0, draft.Preview.Templates[0][2].LearningPriority);
        draft.Undo(0, 4);
        Assert.Equal(new[] { 4, 2, 3 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        draft.Undo(0, 4);
        Assert.False(draft.Preview.Templates[0][0].InitialLearned);
        draft.Undo(0, 4);
        Assert.Equal(new[] { 1, 2, 3 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.False(draft.Dirty);
        Assert.Empty(draft.ExportDocument.Operations);
    }

    private Fixture Create(IModService? mods = null, NativeCharacterVitals? source = null, ImmutableArray<GuardianForceAbilityPatchLayer> layers = default)
    {
        source ??= Source();
        GameSession session = new(new GameState { CharacterVitals = source });
        Fixture fixture = new(new MenuController(() => new MenuData()), session);
        if (mods is not null)
        {
            fixture.Menu.ConnectMods(mods);
        }
        fixture.Menu.ConnectGuardianForceEditor(() => new(source, session.CharacterVitals, layers.IsDefault ? [] : layers, [], []), id => "GF" + id, id => "Ability" + id, id => "Description" + id, action => action.ToString(),
            (document, resets, explicitResets) => new(session.PreviewGuardianForceAbilities(GuardianForceAbilityComposer.Compose(source.ResolveGuardianForceTemplates(), [new("Selected", document)], source.MenuDefinitions.AbilityPoints).Templates), document, resets),
            review => { session.ApplyGuardianForceAbilities(review.Preview); return "Applied"; },
            (document, package, independent) => { fixture.Saved = document; return null; });
        fixture.Menu.Open("gf-ability-targets");
        return fixture;
    }

    private sealed class Fixture(MenuController menu, GameSession session)
    {
        public MenuController Menu { get; } = menu;
        public GameSession Session { get; } = session;
        public GuardianForceAbilityPatchDocument? Saved
        {
            get; set;
        }
    }

    internal static NativeCharacterVitals Source(int abilityCount = 3)
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = Enumerable.Range(0, 16).Select(_ => Enumerable.Range(1, abilityCount).Select(ability => new GuardianForceAbilityTemplate(ability, false, 20, LearningPriority: ability - 1)).ToImmutableArray()).ToImmutableArray();
        return new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new('0', 64),
            KernelSha256 = new('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new(10, 10), 0, new(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(id => new NativeGuardianForceVital(0, (byte)(id == 15 ? 0 : 1), 10, new(0, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), Enumerable.Repeat((byte)20, 116).ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray())
            {
                Templates = templates,
                SourceTemplates = templates
            },
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
    }

    private static RecordingCanvas Render(MenuController menu)
    {
        UiView view = menu.BuildView();
        view.Resize(1280, 896);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private static GuardianForceAbilityDraft Draft(MenuController menu) => Field<GuardianForceAbilityDraft>(menu, "guardianDraft");

    private static T Field<T>(MenuController menu, string name) => (T)typeof(MenuController).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(menu)!;

    private static void SetField<T>(MenuController menu, string name, T value) => typeof(MenuController).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(menu, value);

    private static void ClickAbility(MenuController menu, int ability, bool catalog)
    {
        RecordingCanvas canvas = Render(menu);
        var matching = canvas.Texts.Where(text => text.Value.TrimStart('*', ' ', '\u2194') == "Ability" + ability).OrderBy(text => text.Bounds.X).ToArray();
        Assert.NotEmpty(matching);
        UiRect bounds = catalog ? matching[0].Bounds : matching[^1].Bounds;
        ClickAt(menu, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    private static void ClickAt(MenuController menu, float x, float y)
    {
        UiView view = menu.BuildView();
        view.Pointer(x, y, true);
        view.Pointer(x, y, false);
    }

    private static void Click(MenuController menu, string label)
    {
        RecordingCanvas canvas = Render(menu);
        UiRect bounds = Assert.Single(canvas.Texts, text => text.Value == label).Bounds;
        UiView view = menu.BuildView();
        float x = bounds.X + bounds.Width / 2;
        float y = bounds.Y + bounds.Height / 2;
        view.Pointer(x, y, true);
        view.Pointer(x, y, false);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Value, UiRect Bounds)> Texts { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => new(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Texts.Add((text, bounds));
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => Text(text, fontSize, bounds, color);
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
