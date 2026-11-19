using System.Runtime.InteropServices;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class TextureBrowserTests
{
    [Fact]
    public void Aliases_share_one_selection_and_filtering_preserves_checked_targets()
    {
        TextureBrowserController browser = new(() => { }, _ => { });
        browser.SetRows([new("weapon/1", "Revolver", "Squall / Weapons", "Shared by battle and menu"), new("weapon/1", "Squall weapon alias", "Battle", "Same target"), new("field/1", "Library", "Fields", "Background")]);
        Assert.Equal(2, browser.Leaves.Count);
        ExpandAll(browser);
        SelectLeaf(browser, "weapon/1");
        Assert.Equal("weapon/1", browser.Selected!.Id);
        browser.Toggle();
        browser.Navigate(MenuInput.Tab);
        foreach (char character in "library")
        {
            browser.TextInput(character);
        }
        Assert.Equal("field/1", Assert.Single(browser.Rows.Where(row => !row.GroupNode)).Id);
        Assert.Contains("weapon/1", browser.Selection);
        browser.SetRows([new("weapon/1", "Revolver", "Squall / Weapons", "Shared"), new("field/1", "Library", "Fields", "Background")]);
        Assert.Contains("weapon/1", browser.Selection);
    }

    [Fact]
    public void Manual_matching_uses_same_catalog_without_changing_export_selection()
    {
        string? matched = null;
        TextureBrowserController browser = new(() => { }, _ => { }) { Matching = true, Match = id => matched = id };
        browser.SetRows([new("weapon/1", "Revolver", "Squall / Weapons", "Shared")]);
        ExpandAll(browser);
        SelectLeaf(browser, "weapon/1");
        browser.Toggle();
        Assert.Equal("weapon/1", matched);
        Assert.Empty(browser.Selection);
    }

    [Fact]
    public void Unknown_import_opens_matching_and_is_never_checked_silently()
    {
        string? requested = null;
        TextureBrowserController browser = new(() => { }, _ => { }) { Unmatched = id => requested = id };
        browser.SetRows([new("repaint.png", "Repaint", "Unknown", "Choose a target", false)]);
        ExpandAll(browser);
        SelectLeaf(browser, "repaint.png");
        browser.Toggle();
        Assert.Equal("repaint.png", requested);
        Assert.Empty(browser.Selection);
    }

    [Fact]
    public void Busy_cancel_requests_worker_cancellation_without_closing_browser()
    {
        bool closed = false;
        bool cancelled = false;
        TextureBrowserController browser = new(() => closed = true, _ => { }) { Busy = true, CancelWork = () => cancelled = true };
        browser.Cancel();
        Assert.True(cancelled);
        Assert.False(closed);
        browser.Busy = false;
        browser.Cancel();
        Assert.True(closed);
    }

    [Fact]
    public void Paging_bounds_rows_and_search_navigation_does_not_execute_commands()
    {
        int calls = 0;
        TextureBrowserController browser = new(() => { }, _ => calls++);
        browser.SetRows(Enumerable.Range(0, 100).Select(index => new TextureBrowserRow(index.ToString("D3"), "Texture " + index, "Group", "Detail")));
        ExpandAll(browser);
        browser.Select(0);
        browser.Navigate(MenuInput.PageDown);
        Assert.Equal(TextureBrowserController.RowsPerPage, browser.Index);
        browser.Navigate(MenuInput.Tab);
        browser.Navigate(MenuInput.Right);
        browser.Navigate(MenuInput.Confirm);
        Assert.False(browser.SearchFocused);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void View_routes_pointer_activation_to_rows_and_ignores_empty_rows()
    {
        TextureBrowserController browser = new(() => { }, _ => { });
        browser.SetRows([new("weapon/1", "Revolver", "Squall", "Shared")]);
        ExpandAll(browser);
        var view = browser.BuildView();
        view.Resize(960, 672);
        view.Pointer(100, 214, true);
        Assert.Contains("weapon/1", browser.Selection);
        view.Pointer(100, 214, false);
        view.Pointer(100, 270, true);
        Assert.Contains("weapon/1", browser.Selection);
    }

    [Fact]
    public void Group_selection_deduplicates_shared_targets_and_reports_partial_after_leaf_change()
    {
        TextureBrowserController browser = new(() => { }, _ => { });
        browser.SetRows([new("a", "Revolver", "Characters/Squall/Weapons", ""), new("a", "Alias", "Battle", ""), new("b", "Lionheart", "Characters/Squall/Weapons", ""), new("c", "Gloves", "Characters/Zell/Weapons", "")]);
        TextureBrowserRow root = Assert.Single(browser.Rows);
        Assert.True(root.GroupNode);
        browser.Toggle();
        Assert.Equal(3, browser.Selection.Count);
        Assert.Equal(2, browser.CheckState(root));
        ExpandAll(browser);
        SelectLeaf(browser, "b");
        browser.Toggle();
        Assert.Equal(1, browser.CheckState(root));
        browser.Select(0);
        browser.SetExpanded(false);
        Assert.Single(browser.Rows);
        Assert.Equal(2, browser.Selection.Count);
    }

    [Fact]
    public void Select_filtered_only_checks_matching_leaves_and_search_does_not_mutate_selection()
    {
        TextureBrowserController browser = new(() => { }, _ => { });
        browser.SetRows([new("a", "Revolver", "Characters/Squall/Weapons", ""), new("b", "Lionheart", "Characters/Squall/Weapons", "")]);
        browser.FocusSearch();
        foreach (char character in "Revolver")
        {
            browser.TextInput(character);
        }
        Assert.Empty(browser.Selection);
        browser.SelectFiltered();
        Assert.Equal("a", Assert.Single(browser.Selection));
        while (browser.Search.Length != 0)
        {
            browser.Backspace();
        }
        Assert.Equal("a", Assert.Single(browser.Selection));
        Assert.Equal(1, browser.CheckState(browser.Rows[0]));
    }

    [Fact]
    public void Large_catalog_search_advances_in_bounded_batches_and_publishes_latest_query()
    {
        TextureBrowserController browser = new(() => { }, _ => { });
        browser.SetRows(Enumerable.Range(0, 5000).Select(index => new TextureBrowserRow(index.ToString(), "Texture " + index, "Group", "")));
        browser.FocusSearch();
        foreach (char character in "4999")
        {
            browser.TextInput(character);
        }
        Assert.True(browser.Filtering);
        browser.AdvanceFiltering(32);
        Assert.True(browser.Filtering);
        while (browser.Filtering)
        {
            browser.AdvanceFiltering(128);
        }
        Assert.Equal("4999", Assert.Single(browser.Rows.Where(row => !row.GroupNode)).Id);
        Assert.Empty(browser.Selection);
    }

    [Fact]
    public void Native_multiple_file_callback_preserves_every_selected_unicode_path()
    {
        nint first = Marshal.StringToCoTaskMemUTF8("C:/art/雪.png");
        nint second = Marshal.StringToCoTaskMemUTF8("C:/art/field.ora");
        nint paths = Marshal.AllocHGlobal(IntPtr.Size * 3);
        try
        {
            Marshal.WriteIntPtr(paths, first);
            Marshal.WriteIntPtr(paths, IntPtr.Size, second);
            Marshal.WriteIntPtr(paths, IntPtr.Size * 2, 0);
            var result = SdlDesktopDialogs.ReadResult(paths, () => "", multiple: true);
            Assert.Equal("C:/art/雪.png", result.Path);
            Assert.Equal(new[] { "C:/art/雪.png", "C:/art/field.ora" }, result.Paths);
        }
        finally
        {
            Marshal.FreeCoTaskMem(first);
            Marshal.FreeCoTaskMem(second);
            Marshal.FreeHGlobal(paths);
        }
    }
    private static void SelectLeaf(TextureBrowserController browser, string id)
    {
        browser.Select(browser.Rows.ToList().FindIndex(row => row.Id == id));
    }

    private static void ExpandAll(TextureBrowserController browser)
    {
        for (int index = 0; index < browser.Rows.Count; index++)
        {
            if (browser.Rows[index].GroupNode)
            {
                browser.Select(index);
                browser.SetExpanded(true);
            }
        }
    }

}
