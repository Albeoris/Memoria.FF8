using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record ChocoboImportSource(string Path, string Label)
{
    public ChocoboPresentationText? LabelText { get; init; }
}
public sealed record ChocoboRewardRow(string Name, string Details, UiImageSource? Icon = null, bool Obtained = false, bool Exclusive = false)
{
    public ChocoboPresentationText? NameText { get; init; }
    public ChocoboPresentationText? DetailsText { get; init; }
}
public sealed record ChocoboImportReview(IReadOnlyList<string> Header, IReadOnlyList<ChocoboRewardRow> Items, bool CanImport, string? Reason = null, bool RequiresAssociationReview = false)
{
    public IReadOnlyList<ChocoboPresentationText>? HeaderText { get; init; }
    public ChocoboPresentationText? ReasonText { get; init; }
}
public sealed record ChocoboClaimReview(IReadOnlyList<ChocoboRewardRow> Items, bool CanClaim, string? Reason = null)
{
    public ChocoboPresentationText? ReasonText { get; init; }
}

public sealed partial class MenuController
{
    private Func<IReadOnlyList<ChocoboImportSource>>? discoverChocoboSaves;
    private Func<Task<string?>>? browseChocoboSave;
    private Func<string, ChocoboImportReview>? previewChocoboImport;
    private Func<string>? commitChocoboImport;
    private Func<ChocoboClaimReview>? previewChocoboClaim;
    private Func<string>? commitChocoboClaim;
    private Func<ChocoboImportReview>? reviewChocoboAssociation;
    private IReadOnlyList<ChocoboImportSource> chocoboSources = [];
    private ChocoboImportReview? chocoboImport;
    private ChocoboClaimReview? chocoboClaim;
    private Func<ChocoboPresentationText?>? readChocoboResultText;

    public void ConnectChocoboWorld(Func<IReadOnlyList<ChocoboImportSource>> discover, Func<string?> browse, Func<string, ChocoboImportReview> preview, Func<string> commit, Func<ChocoboClaimReview> claimPreview, Func<string> claim, Func<ChocoboImportReview>? reviewAssociation = null, Func<ChocoboPresentationText?>? lastResultText = null)
    {
        ConnectChocoboWorldAsync(discover, () => Task.FromResult(browse()), preview, commit, claimPreview, claim, reviewAssociation, lastResultText);
    }

    public void ConnectChocoboWorldAsync(Func<IReadOnlyList<ChocoboImportSource>> discover, Func<Task<string?>> browse, Func<string, ChocoboImportReview> preview, Func<string> commit, Func<ChocoboClaimReview> claimPreview, Func<string> claim, Func<ChocoboImportReview>? reviewAssociation = null, Func<ChocoboPresentationText?>? lastResultText = null)
    {
        discoverChocoboSaves = discover;
        browseChocoboSave = browse;
        previewChocoboImport = preview;
        commitChocoboImport = commit;
        previewChocoboClaim = claimPreview;
        commitChocoboClaim = claim;
        reviewChocoboAssociation = reviewAssociation;
        readChocoboResultText = lastResultText;
    }

    private string ChocoboSourceLabel(ChocoboImportSource source) => source.LabelText?.Format(SystemText) ?? source.Label;
    private string ChocoboRewardName(ChocoboRewardRow row) => row.NameText?.Format(SystemText) ?? row.Name;
    private string ChocoboRewardDetails(ChocoboRewardRow row) => row.DetailsText?.Format(SystemText) ?? row.Details;
    private string? ChocoboImportReason(ChocoboImportReview? review) => review?.ReasonText?.Format(SystemText) ?? review?.Reason;
    private string? ChocoboClaimReason(ChocoboClaimReview? review) => review?.ReasonText?.Format(SystemText) ?? review?.Reason;
    private IReadOnlyList<string> ChocoboHeader(ChocoboImportReview? review) => review?.HeaderText is { } text ? text.Select(row => row.Format(SystemText)).ToArray() : review?.Header ?? [];

    private IEnumerable<MenuEntry> ChocoboWorldActions()
    {
        string mode = ConfigValue("Chocobo World");
        if (mode == "Native Import")
        {
            yield return new("chocobo-import", SystemText("ui.chocoboworldactions.entry.label.import.chocobo.world", "Import Chocobo World…"), SystemText("ui.chocoboworldactions.entry.help.preview.a.native.save.without.changing.the.source", "Preview a native save without changing the source file."), Action: new("chocobo-import-open"), Enabled: previewChocoboImport is not null);
        }
        if (mode == "Memoria")
        {
            yield return new("chocobo-claim", SystemText("ui.chocoboworldactions.entry.label.claim.remaining.exclusive.items", "Claim remaining exclusive items…"), SystemText("ui.chocoboworldactions.entry.help.the.minigame.is.under.development.review.the.remaining", "The minigame is under development. Review the remaining exclusive items."), Action: new("chocobo-claim-open"), Enabled: previewChocoboClaim is not null);
        }
    }

    private void RegisterChocoboWorldScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-sources",
            Title = "Import Chocobo World",
            Help = "Select a detected save or browse for a file. Source files are read only.",
            Entries = _ => [.. chocoboSources.Select(source => new MenuEntry(source.Path, ChocoboSourceLabel(source), source.Path, Action: new("chocobo-import-preview", source.Path))), new("browse", SystemText("ui.registerchocoboworldscreens.entry.label.browse", "Browse…"), SystemText("ui.registerchocoboworldscreens.entry.help.choose.any.native.chocobo.world.save.file", "Choose any native Chocobo World save file."), Action: new("chocobo-import-browse")), new("cancel", SystemText("ui.registerchocoboworldscreens.entry.label.cancel", "Cancel"), SystemText("ui.registerchocoboworldscreens.entry.help.return.without.importing", "Return without importing."), Action: new("menu-back"))],
            Details = _ => chocoboSources.Count == 0 ? [SystemText("ui.registerchocoboworldscreens.details.no.saves.found.use.browse.to.choose.a", "No saves found. Use Browse to choose a file.")] : [SystemText("ui.registerchocoboworldscreens.details.detected.saves", "Detected saves"), SelectedEntry?.Help ?? ""]
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-import-preview",
            Title = "Chocobo World · Preview",
            Help = "Review the incoming items and progression, then confirm Import.",
            Entries = _ => [new("cancel", SystemText("ui.registerchocoboworldscreens.entry.label.cancel", "Cancel"), SystemText("ui.registerchocoboworldscreens.entry.help.leave.the.source.and.game.unchanged", "Leave the source and game unchanged."), Action: new("menu-back")), .. chocoboImport?.RequiresAssociationReview == true ? new[] { new MenuEntry("review-association", SystemText("ui.registerchocoboworldscreens.entry.label.review.world.association", "Review world association…"), SystemText("ui.registerchocoboworldscreens.entry.help.this.source.belongs.to.a.different.world.explicit", "This source belongs to a different world. Explicit review is required."), Action: new("chocobo-association-open")) } : Array.Empty<MenuEntry>(), new("import", SystemText("ui.registerchocoboworldscreens.entry.label.import", "Import"), ChocoboImportReason(chocoboImport) ?? SystemText("ui.registerchocoboworldscreens.entry.help.commit.this.unchanged.preview", "Commit this unchanged preview."), Action: new("chocobo-import-commit"), Enabled: chocoboImport?.CanImport == true)],
            Details = _ => [.. ChocoboHeader(chocoboImport), .. chocoboImport?.Items.Select(item => ChocoboRewardName(item) + " · " + ChocoboRewardDetails(item)) ?? [], ChocoboImportReason(chocoboImport) ?? ""],
            BuildContent = (menus, root) => menus.BuildChocoboImportPreview(root)
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-association-review",
            Title = "Review Chocobo World association",
            Help = "This source belongs to a different world. Review the identities before allowing this source.",
            Entries = _ => [new("no", SystemText("ui.registerchocoboworldscreens.entry.label.no", "No"), SystemText("ui.registerchocoboworldscreens.entry.help.keep.the.current.association.and.return.to.preview", "Keep the current association and return to preview."), Action: new("menu-back")), new("use-source", SystemText("ui.registerchocoboworldscreens.entry.label.use.this.source", "Use this source"), SystemText("ui.registerchocoboworldscreens.entry.help.authorize.this.foreign.source.for.the.preview.import", "Authorize this foreign source for the preview. Import still requires confirmation."), Action: new("chocobo-association-confirm"), Enabled: reviewChocoboAssociation is not null)],
            Details = _ => [.. ChocoboHeader(chocoboImport), ChocoboImportReason(chocoboImport) ?? SystemText("ui.registerchocoboworldscreens.details.the.selected.source.has.a.different.world.identity", "The selected source has a different world identity."), SystemText("ui.registerchocoboworldscreens.details.the.external.source.file.will.remain.unchanged", "The external source file will remain unchanged.")]
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-claim",
            Title = "Memoria Chocobo World",
            Help = "This minigame is under development.",
            Entries = _ => chocoboClaim?.Items.All(item => item.Obtained) != false
                ? [new("close", SystemText("ui.registerchocoboworldscreens.entry.label.close", "Close"), SystemText("ui.registerchocoboworldscreens.entry.help.all.exclusive.items.have.already.been.obtained", "All exclusive items have already been obtained."), Action: new("menu-back"))]
                : [new("no", SystemText("ui.registerchocoboworldscreens.entry.label.no", "No"), SystemText("ui.registerchocoboworldscreens.entry.help.leave.inventory.and.acquisition.history.unchanged", "Leave inventory and acquisition history unchanged."), Action: new("menu-back")), new("yes", SystemText("ui.registerchocoboworldscreens.entry.label.yes", "Yes"), ChocoboClaimReason(chocoboClaim) ?? SystemText("ui.registerchocoboworldscreens.entry.help.receive.all.remaining.exclusive.items.once", "Receive all remaining exclusive items once."), Action: new("chocobo-claim-commit"), Enabled: chocoboClaim?.CanClaim == true)],
            Details = _ => [SystemText("ui.registerchocoboworldscreens.details.this.minigame.is.under.development", "This minigame is under development."), chocoboClaim?.Items.All(item => item.Obtained) != false ? SystemText("ui.registerchocoboworldscreens.details.all.exclusive.items.have.already.been.obtained", "All exclusive items have already been obtained.") : SystemText("ui.registerchocoboworldscreens.details.would.you.like.to.receive.all.remaining.chocobo", "Would you like to receive all remaining Chocobo World-exclusive items now?"), .. chocoboClaim?.Items.Select(item => ChocoboRewardName(item) + (item.Obtained ? SystemText("chocobo.item.obtained", " · Obtained") : " · " + ChocoboRewardDetails(item))) ?? [], ChocoboClaimReason(chocoboClaim) ?? ""],
            BuildContent = (menus, root) => menus.BuildChocoboClaim(root)
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-result",
            Title = "Chocobo World · Result",
            Help = "The external source file has not been changed.",
            Entries = _ => [new("close", SystemText("ui.registerchocoboworldscreens.entry.label.close", "Close"), SystemText("ui.registerchocoboworldscreens.entry.help.return.to.chocobo.world.settings", "Return to Chocobo World settings."), Action: new("menu-back"))],
            Details = _ => [Message]
        });
    }

    private void BuildChocoboImportPreview(UiPanel root)
    {
        UiGrid content = Grid([2.3f, 4.5f, 1.4f], [1], 8);
        Place(content, Panel(SystemText("ui.buildchocoboimportpreview.panel.source.and.progression", "Source and progression"), new UiList
        {
            Id = "chocobo-progression",
            Items = [.. ChocoboHeader(chocoboImport), ChocoboImportReason(chocoboImport) ?? ""],
            Paginated = true,
            TextFlow = UiTextFlow.Wrap,
            FontSize = 18 * TextScale,
            RowHeight = 30 * TextScale
        }), 0);
        IReadOnlyList<ChocoboRewardRow> rows = (chocoboImport?.Items ?? []).OrderByDescending(row => row.Exclusive).ToArray();
        Place(content, Panel(SystemText("ui.buildchocoboimportpreview.panel.incoming.items.tab.to.inspect.arrows.to.scroll", "Incoming items · Tab to inspect / arrows to scroll"), new UiList
        {
            Id = "chocobo-items",
            Items = rows.Count == 0 ? [SystemText("ui.buildchocoboimportpreview.items.no.new.rewards", "No new rewards.")] : rows.Select(row => (row.Exclusive ? SystemText("chocobo.import.exclusive", "Exclusive · ") : SystemText("chocobo.import.ordinary", "Ordinary · ")) + ChocoboRewardName(row) + "\n" + ChocoboRewardDetails(row)).ToList(),
            Images = rows.Select(row => row.Icon).ToList(),
            Paginated = true,
            TextFlow = UiTextFlow.Wrap,
            FontSize = 19 * TextScale,
            RowHeight = 52 * TextScale
        }), 1);
        Place(content, Panel(null, Commands(30, 22)), 2);
        root.Children.Add(content);
    }

    private void BuildChocoboClaim(UiPanel root)
    {
        UiAnchorPanel anchor = new();
        UiGrid content = Grid([2, 5, 1.5f], [1], 12);
        bool all = chocoboClaim?.Items.All(item => item.Obtained) != false;
        Place(content, new UiList
        {
            Id = "chocobo-claim-message",
            Items = [FormatSystemText("ui.buildchocoboclaim.items.this.minigame.is.under.development.template", "This minigame is under development.\n{0}", (all ? SystemText("ui.buildchocoboclaim.items.all.exclusive.items.have.already.been.obtained", "All exclusive items have already been obtained.") : SystemText("ui.buildchocoboclaim.items.would.you.like.to.receive.all.remaining.chocobo", "Would you like to receive all remaining Chocobo World-exclusive items now?"))), .. ChocoboClaimReason(chocoboClaim) is { Length: > 0 } reason ? new[] { reason } : Array.Empty<string>()],
            Paginated = true,
            TextFlow = UiTextFlow.Wrap,
            FontSize = 20 * TextScale,
            RowHeight = 42 * TextScale,
            ShowSelection = false
        }, 0);
        IReadOnlyList<ChocoboRewardRow> rows = chocoboClaim?.Items ?? [];
        UiGrid items = Grid(Enumerable.Repeat(1f, Math.Max(1, rows.Count)).ToArray(), [1], 5);
        for (int index = 0; index < rows.Count; index++)
        {
            ChocoboRewardRow item = rows[index];
            UiGrid row = Grid([1], [0.08f, 0.92f], 8);
            if (item.Icon is { } icon)
            {
                Place(row, new UiImage { Source = icon }, 0, 0);
            }
            Place(row, Label(FormatSystemText("ui.buildchocoboclaim.label.obtained.template", "{0}{1}", ChocoboRewardName(item), (item.Obtained ? SystemText("chocobo.item.obtained", " · Obtained") : "\n" + ChocoboRewardDetails(item))), 18, UiTextFlow.Wrap), 0, 1);
            Place(items, row, index);
        }
        Place(content, items, 1);
        Place(content, Commands(28, 22), 2);
        UiPanel panel = Panel(SystemText("ui.buildchocoboclaim.panel.memoria.chocobo.world", "Memoria Chocobo World"), content);
        panel.Anchors = new UiAnchors(0.08f, 0.06f, 0.92f, 0.94f);
        anchor.Children.Add(panel);
        root.Children.Add(anchor);
    }

    public void ShowChocoboClaim()
    {
        if (previewChocoboClaim is null)
        {
            return;
        }
        chocoboClaim = previewChocoboClaim();
        if (IsOpen)
        {
            Push("chocobo-claim");
        }
        else
        {
            Open("chocobo-claim");
        }
        Select(0, Entries.Count);
    }

    private async void BrowseChocoboSaveAsync()
    {
        try
        {
            if (browseChocoboSave is not null && await browseChocoboSave() is { } path)
            {
                Execute(new("chocobo-import-preview", path));
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Message = error.Message;
        }
        _view = null;
    }

    private bool ExecuteChocoboWorldAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("chocobo-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            switch (action.Kind)
            {
                case "chocobo-import-open":
                    chocoboSources = discoverChocoboSaves?.Invoke() ?? [];
                    Push("chocobo-sources");
                    break;
                case "chocobo-import-browse":
                    BrowseChocoboSaveAsync();
                    break;
                case "chocobo-import-preview":
                    chocoboImport = previewChocoboImport?.Invoke(action.SubjectId);
                    Push("chocobo-import-preview");
                    Select(0, Entries.Count);
                    break;
                case "chocobo-association-open" when ScreenId == "chocobo-import-preview" && chocoboImport?.RequiresAssociationReview == true:
                    Push("chocobo-association-review");
                    Select(0, Entries.Count);
                    break;
                case "chocobo-association-confirm" when ScreenId == "chocobo-association-review" && reviewChocoboAssociation is not null:
                    chocoboImport = reviewChocoboAssociation();
                    Back();
                    Select(0, Entries.Count);
                    break;
                case "chocobo-claim-open":
                    ShowChocoboClaim();
                    break;
                case "chocobo-import-commit" when ScreenId == "chocobo-import-preview" && chocoboImport?.CanImport == true:
                    string importMessage = commitChocoboImport?.Invoke() ?? SystemText("ui.executechocoboworldaction.importmessage.import.is.unavailable", "Import is unavailable.");
                    chocoboImport = null;
                    Back();
                    Push("chocobo-result");
                    SetSystemMessage(() => readChocoboResultText?.Invoke()?.Format(SystemText) ?? importMessage);
                    _liveData = null;
                    break;
                case "chocobo-claim-commit" when ScreenId == "chocobo-claim" && chocoboClaim?.CanClaim == true:
                    string claimMessage = commitChocoboClaim?.Invoke() ?? SystemText("ui.executechocoboworldaction.claimmessage.claim.is.unavailable", "Claim is unavailable.");
                    chocoboClaim = null;
                    Back();
                    Push("chocobo-result");
                    SetSystemMessage(() => readChocoboResultText?.Invoke()?.Format(SystemText) ?? claimMessage);
                    _liveData = null;
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Push("chocobo-result");
            SetSystemMessage(() => ChocoboPresentationText.FromException(exception).Format(SystemText));
        }
        return true;
    }
}
