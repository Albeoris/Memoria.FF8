using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record ChocoboImportSource(string Path, string Label);
public sealed record ChocoboRewardRow(string Name, string Details, UiImageSource? Icon = null, bool Obtained = false, bool Exclusive = false);
public sealed record ChocoboImportReview(IReadOnlyList<string> Header, IReadOnlyList<ChocoboRewardRow> Items, bool CanImport, string? Reason = null, bool RequiresAssociationReview = false);
public sealed record ChocoboClaimReview(IReadOnlyList<ChocoboRewardRow> Items, bool CanClaim, string? Reason = null);

public sealed partial class MenuController
{
    private Func<IReadOnlyList<ChocoboImportSource>>? discoverChocoboSaves;
    private Func<string?>? browseChocoboSave;
    private Func<string, ChocoboImportReview>? previewChocoboImport;
    private Func<string>? commitChocoboImport;
    private Func<ChocoboClaimReview>? previewChocoboClaim;
    private Func<string>? commitChocoboClaim;
    private Func<ChocoboImportReview>? reviewChocoboAssociation;
    private IReadOnlyList<ChocoboImportSource> chocoboSources = [];
    private ChocoboImportReview? chocoboImport;
    private ChocoboClaimReview? chocoboClaim;

    public void ConnectChocoboWorld(Func<IReadOnlyList<ChocoboImportSource>> discover, Func<string?> browse, Func<string, ChocoboImportReview> preview, Func<string> commit, Func<ChocoboClaimReview> claimPreview, Func<string> claim, Func<ChocoboImportReview>? reviewAssociation = null)
    {
        discoverChocoboSaves = discover;
        browseChocoboSave = browse;
        previewChocoboImport = preview;
        commitChocoboImport = commit;
        previewChocoboClaim = claimPreview;
        commitChocoboClaim = claim;
        reviewChocoboAssociation = reviewAssociation;
    }

    private IEnumerable<MenuEntry> ChocoboWorldActions()
    {
        string mode = ConfigValue("Chocobo World");
        if (mode == "Native Import")
        {
            yield return new("chocobo-import", "Import Chocobo World…", "Preview a native save without changing the source file.", Action: new("chocobo-import-open"), Enabled: previewChocoboImport is not null);
        }
        if (mode == "Memoria")
        {
            yield return new("chocobo-claim", "Claim remaining exclusive items…", "The minigame is under development. Review the remaining exclusive items.", Action: new("chocobo-claim-open"), Enabled: previewChocoboClaim is not null);
        }
    }

    private void RegisterChocoboWorldScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-sources",
            Title = "Import Chocobo World",
            Help = "Select a detected save or browse for a file. Source files are read only.",
            Entries = _ => [.. chocoboSources.Select(source => new MenuEntry(source.Path, source.Label, source.Path, Action: new("chocobo-import-preview", source.Path))), new("browse", "Browse…", "Choose any native Chocobo World save file.", Action: new("chocobo-import-browse")), new("cancel", "Cancel", "Return without importing.", Action: new("menu-back"))],
            Details = _ => chocoboSources.Count == 0 ? ["No saves found. Use Browse to choose a file."] : ["Detected saves", SelectedEntry?.Help ?? ""]
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-import-preview",
            Title = "Chocobo World · Preview",
            Help = "Review the incoming items and progression, then confirm Import.",
            Entries = _ => [new("cancel", "Cancel", "Leave the source and game unchanged.", Action: new("menu-back")), .. chocoboImport?.RequiresAssociationReview == true ? new[] { new MenuEntry("review-association", "Review world association…", "This source belongs to a different world. Explicit review is required.", Action: new("chocobo-association-open")) } : Array.Empty<MenuEntry>(), new("import", "Import", chocoboImport?.Reason ?? "Commit this unchanged preview.", Action: new("chocobo-import-commit"), Enabled: chocoboImport?.CanImport == true)],
            Details = _ => [.. chocoboImport?.Header ?? [], .. chocoboImport?.Items.Select(item => item.Name + " · " + item.Details) ?? [], chocoboImport?.Reason ?? ""],
            BuildContent = (menus, root) => menus.BuildChocoboImportPreview(root)
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-association-review",
            Title = "Review Chocobo World association",
            Help = "This source belongs to a different world. Review the identities before allowing this source.",
            Entries = _ => [new("no", "No", "Keep the current association and return to preview.", Action: new("menu-back")), new("use-source", "Use this source", "Authorize this foreign source for the preview. Import still requires confirmation.", Action: new("chocobo-association-confirm"), Enabled: reviewChocoboAssociation is not null)],
            Details = _ => [.. chocoboImport?.Header ?? [], chocoboImport?.Reason ?? "The selected source has a different world identity.", "The external source file will remain unchanged."]
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-claim",
            Title = "Memoria Chocobo World",
            Help = "This minigame is under development.",
            Entries = _ => chocoboClaim?.Items.All(item => item.Obtained) != false
                ? [new("close", "Close", "All exclusive items have already been obtained.", Action: new("menu-back"))]
                : [new("no", "No", "Leave inventory and acquisition history unchanged.", Action: new("menu-back")), new("yes", "Yes", chocoboClaim?.Reason ?? "Receive all remaining exclusive items once.", Action: new("chocobo-claim-commit"), Enabled: chocoboClaim?.CanClaim == true)],
            Details = _ => ["This minigame is under development.", chocoboClaim?.Items.All(item => item.Obtained) != false ? "All exclusive items have already been obtained." : "Would you like to receive all remaining Chocobo World-exclusive items now?", .. chocoboClaim?.Items.Select(item => item.Name + (item.Obtained ? " · Obtained" : " · " + item.Details)) ?? [], chocoboClaim?.Reason ?? ""],
            BuildContent = (menus, root) => menus.BuildChocoboClaim(root)
        });
        Register(new MenuScreenDefinition
        {
            Id = "chocobo-result",
            Title = "Chocobo World · Result",
            Help = "The external source file has not been changed.",
            Entries = _ => [new("close", "Close", "Return to Chocobo World settings.", Action: new("menu-back"))],
            Details = _ => [Message]
        });
    }

    private void BuildChocoboImportPreview(UiPanel root)
    {
        UiGrid content = Grid([2.3f, 4.5f, 1.4f], [1], 8);
        Place(content, Panel("Source and progression", new UiList
        {
            Id = "chocobo-progression",
            Items = [.. chocoboImport?.Header ?? [], chocoboImport?.Reason ?? ""],
            Paginated = true,
            TextFlow = UiTextFlow.Wrap,
            FontSize = 18 * TextScale,
            RowHeight = 30 * TextScale
        }), 0);
        IReadOnlyList<ChocoboRewardRow> rows = (chocoboImport?.Items ?? []).OrderByDescending(row => row.Exclusive).ToArray();
        Place(content, Panel("Incoming items · Tab to inspect / arrows to scroll", new UiList
        {
            Id = "chocobo-items",
            Items = rows.Count == 0 ? ["No new rewards."] : rows.Select(row => (row.Exclusive ? "Exclusive · " : "Ordinary · ") + row.Name + "\n" + row.Details).ToList(),
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
            Items = ["This minigame is under development.\n" + (all ? "All exclusive items have already been obtained." : "Would you like to receive all remaining Chocobo World-exclusive items now?"), .. chocoboClaim?.Reason is { Length: > 0 } reason ? new[] { reason } : Array.Empty<string>()],
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
            Place(row, Label(item.Name + (item.Obtained ? " · Obtained" : "\n" + item.Details), 18, UiTextFlow.Wrap), 0, 1);
            Place(items, row, index);
        }
        Place(content, items, 1);
        Place(content, Commands(28, 22), 2);
        UiPanel panel = Panel("Memoria Chocobo World", content);
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
                    if (browseChocoboSave?.Invoke() is { } path)
                    {
                        Execute(new("chocobo-import-preview", path));
                    }
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
                    string importMessage = commitChocoboImport?.Invoke() ?? "Import is unavailable.";
                    chocoboImport = null;
                    Back();
                    Push("chocobo-result");
                    Message = importMessage;
                    _liveData = null;
                    break;
                case "chocobo-claim-commit" when ScreenId == "chocobo-claim" && chocoboClaim?.CanClaim == true:
                    string claimMessage = commitChocoboClaim?.Invoke() ?? "Claim is unavailable.";
                    chocoboClaim = null;
                    Back();
                    Push("chocobo-result");
                    Message = claimMessage;
                    _liveData = null;
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Push("chocobo-result");
            Message = exception.Message;
        }
        return true;
    }
}
