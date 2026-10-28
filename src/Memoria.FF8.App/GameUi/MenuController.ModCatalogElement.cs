using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private sealed class ModCatalogElement : UiElement
    {
        private readonly MenuController menus;
        private readonly ModLibrarySnapshot snapshot;
        private readonly bool downloads;
        private readonly RemoteModEntry[] entries;
        private readonly ModLibraryJob[] jobs;
        private readonly Dictionary<string, UiElement> controls = new();
        private readonly CatalogTable table;
        private UiRect inspector;
        private UiRect status;
        private UiRect activity;
        private int selected;
        private int first;
        private int pageSize = 1;
        private float scale;
        private float rowHeight;
        private bool wide;
        private int Count => downloads ? jobs.Length : entries.Length;
        private RemoteModEntry? Entry => downloads ? Job?.Entry : entries.ElementAtOrDefault(selected) ?? menus.SelectedCatalog;
        private ModLibraryJob? Job => downloads ? jobs.ElementAtOrDefault(selected) : Entry is { } entry ? menus.EntryJob(entry) : null;

        public ModCatalogElement(MenuController menus)
        {
            this.menus = menus;
            snapshot = menus.modLibrarySnapshot!;
            downloads = menus.IsDownloads;
            entries = menus.FilteredCatalog();
            jobs = snapshot.Jobs.OrderBy(job => JobGroup(job)).ThenBy(job => job.Entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(job => job.Id).ToArray();
            selected = downloads ? Array.FindIndex(jobs, job => job.Id == menus.selectedDownloadId) : Array.FindIndex(entries, entry => CatalogIdentity(entry) == menus.selectedCatalogId);
            if (selected < 0 && (downloads || menus.selectedCatalogId is null))
            {
                selected = 0;
            }
            if (Count > 0 && selected >= 0)
            {
                Select(selected, false);
            }
            foreach ((string id, string title, string screen) in new[] { ("installed-tab", "Installed", "mods"), ("catalog-tab", "Catalog", "mods-catalog"), ("downloads-tab", "Downloads", "mods-downloads"), ("create-tab", "Create", "mods-create") })
            {
                Button(id, title, () => menus.Push(screen));
            }
            Button("library-refresh", snapshot.Catalog.IsRefreshing ? "Refreshing…" : "Refresh", () => menus.modLibrary!.RefreshCatalog());
            controls["library-refresh"].Enabled = !snapshot.Catalog.IsRefreshing;
            Button("library-search", menus.catalogSearch.Length == 0 ? "Search mods…" : menus.catalogSearch, () => menus.Push("mods-catalog-search"));
            string[] categories = menus.CatalogCategories;
            Choice("library-category", categories, Math.Max(0, Array.IndexOf(categories, menus.catalogCategory)), value => menus.catalogCategory = categories[value]);
            Choice("library-filter", CatalogFilters, menus.catalogFilter, value => menus.catalogFilter = value);
            Button("library-clear", "Clear filters", () =>
            {
                menus.catalogSearch = "";
                menus.catalogCategory = "All categories";
                menus.catalogFilter = 0;
                menus._view = null;
            });
            table = new CatalogTable(this) { Id = "commands", Focusable = true };
            Track(table);
            controls.Add("commands", table);
            Children.Add(table);
            Button("mod-inspector", "Details", OpenInspector);
            Button("library-list", "← List", () =>
            {
                menus.catalogInspectorOpen = false;
                menus.modFocusedControlId = "commands";
                menus._view = null;
            });
            Button("library-primary", PrimaryLabel(), Primary);
            controls["library-primary"].Enabled = CanPrimary();
            Button("library-secondary", Job?.CanOpenDownloadPage == true ? "Open download page" : Job?.CanPause == true ? "Pause" : Job?.CanResume == true ? "Resume" : "More details", Secondary);
            Button("library-cancel", Job?.CanInstall == true ? "Remove cached download" : "Cancel job", () =>
            {
                if (Job is { CanCancel: true } job)
                {
                    menus.modLibrary!.Cancel(job.Id);
                }
            });
            Button("library-more", "Full details", () => menus.Push("mods-library-details"));
            Button("library-prev", "← Previous", () => Move(-1, true));
            Button("library-next", "Next →", () => Move(1, true));
            Button("library-activity", ActivityLabel(), () => menus.Push("mods-downloads"));
            UiProgressBar progress = new() { Id = "library-progress", Min = 0, Max = 1, Value = Job?.TotalBytes is > 0 ? (float)((double)Job.ReceivedBytes / Job.TotalBytes.Value) : 0 };
            controls.Add("library-progress", progress);
            Children.Add(progress);
        }

        private static int JobGroup(ModLibraryJob job) => job.Error is not null || job.CanInstall || job.CanResume || job.CanRetry ? 0 : job.Phase == "Installed" ? 3 : job.Phase == "Queued" ? 2 : 1;
        private static string JobGroupLabel(ModLibraryJob job) => JobGroup(job) switch { 0 => "Needs attention", 1 => "Active", 2 => "Queued", _ => "Recent" };
        private string ActivityLabel()
        {
            ModLibraryJob? active = snapshot.Jobs.FirstOrDefault(job => job.Phase != "Installed");
            return active is null ? "Downloads · No pending work" : "Downloads · " + active.Entry.Name + " · " + active.Phase;
        }

        private string PrimaryLabel()
        {
            if (Job is { } job)
            {
                return job.CanInstall ? "Install" : job.CanRetry ? "Retry" : job.CanResume ? "Resume" : downloads ? "Full details" : "View download";
            }
            if (Entry is { } update && menus.CatalogUpdateAvailable(update))
            {
                return "Download update & check";
            }
            return Entry is { } entry && menus.ManagedMod(entry) is { Pending: true } ? "View pending change" : Entry is { } local && (menus.AssociatedMod(local) is not null || menus.ManagedMod(local) is not null) ? "View installed mod" : "Download & check";
        }

        private bool CanPrimary() => Entry is not null && (Job is not null || menus.AssociatedMod(Entry) is not null || menus.ManagedMod(Entry) is not null || snapshot.TargetSupported && menus.CatalogEntryListed(Entry) && Entry.Links.Count > 0);
        private void Primary()
        {
            if (!CanPrimary())
            {
                return;
            }
            if (Job is { } job)
            {
                if (job.CanInstall)
                {
                    menus.modLibrary!.Install(job.Id);
                }
                else if (job.CanRetry)
                {
                    menus.modLibrary!.Retry(job.Id);
                }
                else if (job.CanResume)
                {
                    menus.modLibrary!.Resume(job.Id);
                }
                else if (!downloads)
                {
                    menus.selectedDownloadId = job.Id;
                    menus.Push("mods-downloads");
                }
                else
                {
                    menus.Push("mods-library-details");
                }
            }
            else if (Entry is { } entry)
            {
                if (menus.ManagedMod(entry) is { Pending: true })
                {
                    menus.Push("mods-library-details");
                }
                else if (menus.AssociatedMod(entry) is { } local && !menus.CatalogUpdateAvailable(entry))
                {
                    menus.SelectMod(local.Package.Id);
                    menus.Push("mods");
                }
                else if (menus.ManagedMod(entry) is not null && !menus.CatalogUpdateAvailable(entry))
                {
                    menus.Push("mods-library-details");
                }
                else
                {
                    menus.modLibrary!.Queue(entry);
                }
            }
        }

        private void Secondary()
        {
            if (Job is { CanOpenDownloadPage: true } external)
            {
                menus.modLibrary!.OpenDownloadPage(external.Id);
            }
            else if (Job is { CanPause: true } pause)
            {
                menus.modLibrary!.Pause(pause.Id);
            }
            else if (Job is { CanResume: true } resume)
            {
                menus.modLibrary!.Resume(resume.Id);
            }
            else
            {
                menus.Push("mods-library-details");
            }
        }

        private void OpenInspector()
        {
            menus.catalogInspectorOpen = true;
            menus.modFocusedControlId = "library-primary";
            menus._view = null;
        }

        private void Select(int index, bool invalidate = true)
        {
            selected = Math.Clamp(index, 0, Math.Max(0, Count - 1));
            if (downloads && jobs.ElementAtOrDefault(selected) is { } job)
            {
                menus.selectedDownloadId = job.Id;
            }
            else if (entries.ElementAtOrDefault(selected) is { } entry)
            {
                menus.selectedCatalogId = CatalogIdentity(entry);
                menus.retainedCatalogEntry = entry;
                menus.selectedDownloadId = null;
            }
            if (invalidate)
            {
                menus._view = null;
            }
        }

        private void Move(int direction, bool page)
        {
            if (Count > 0)
            {
                int next = page ? Math.Min(Count - 1, Math.Clamp(selected / pageSize + direction, 0, (Count - 1) / pageSize) * pageSize + selected % pageSize) : selected + direction;
                Select(next);
            }
        }

        private void Track(UiElement element)
        {
            element.FocusChanged = focused =>
            {
                if (focused)
                {
                    menus.modFocusedControlId = element.Id!;
                }
            };
        }

        private void Button(string id, string text, Action action)
        {
            UiButton button = new() { Id = id, Text = text, Click = action, DrawBackground = true, TextInset = 8, FontSize = 16 * menus.TextScale };
            Track(button);
            controls.Add(id, button);
            Children.Add(button);
        }

        private void Choice(string id, string[] items, int index, Action<int> change)
        {
            UiChoiceRow choice = new() { Id = id, Items = items, SelectedIndex = index, LabelWidthRatio = 0, TextInset = 0, RightInset = 0, MaxVisibleItems = 1, FontSize = 16 * menus.TextScale, SelectionChanged = value =>
            {
                change(value);
                menus._view = null;
            } };
            Track(choice);
            controls.Add(id, choice);
            Children.Add(choice);
        }

        private void Place(string id, float x, float y, float width, float height) => controls[id].Arrange(new(x, y, Math.Max(0, width), Math.Max(0, height)));

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            scale = menus.TextScale;
            if (menus._view is { ViewportWidth: > 0 and < 940 } view)
            {
                scale *= Math.Max(1, view.DesignWidth / view.ViewportWidth);
            }
            foreach (UiElement element in controls.Values)
            {
                if (element is UiButton button)
                {
                    button.FontSize = 16 * scale;
                }
                else if (element is UiChoiceRow choice)
                {
                    choice.FontSize = 16 * scale;
                }
            }
            float gap = 8;
            float control = 28 * scale;
            float x = bounds.X + 12;
            float width = Math.Max(0, bounds.Width - 24);
            float right = x + width;
            float top = bounds.Y + 8;
            wide = width >= 940 * scale;
            menus.catalogNarrow = !wide;
            float tabWidth = Math.Min(width / 4 - gap, 140 * scale);
            int tab = 0;
            foreach (string id in new[] { "installed-tab", "catalog-tab", "downloads-tab", "create-tab" })
            {
                Place(id, x + tab++ * (tabWidth + gap), top, tabWidth, control);
            }
            top += control + gap;
            Place("library-refresh", right - 112 * scale, top, 112 * scale, control);
            status = new(x, top, Math.Max(0, width - 120 * scale), control);
            top += control + gap;
            bool compact = width < 700 * scale;
            foreach (string id in new[] { "library-search", "library-category", "library-filter", "library-clear" })
            {
                controls[id].Visible = !downloads && (wide || !menus.catalogInspectorOpen);
            }
            if (!downloads && (wide || !menus.catalogInspectorOpen))
            {
                float searchWidth = compact ? width - 100 * scale : width * 0.4f;
                Place("library-search", x, top, searchWidth - gap, control);
                Place("library-clear", compact ? x + searchWidth : right - 108 * scale, top, compact ? width - searchWidth : 108 * scale, control);
                if (compact)
                {
                    top += control + gap;
                }
                float categoryWidth = compact ? (width - gap) / 2 : (width - searchWidth - 108 * scale - gap * 2) / 2;
                float categoryX = compact ? x : x + searchWidth;
                Place("library-category", categoryX, top, categoryWidth, control);
                Place("library-filter", categoryX + categoryWidth + gap, top, categoryWidth, control);
                top += control + gap;
            }
            float footer = control * 2 + gap * 3 + 23 * scale;
            float contentHeight = Math.Max(0, bounds.Bottom - top - footer);
            float listWidth = wide ? (width - gap) * 0.42f : width;
            bool showInspector = wide || menus.catalogInspectorOpen;
            inspector = wide ? new(x + listWidth + gap, top, width - listWidth - gap, contentHeight) : new(x, top, width, contentHeight);
            table.Arrange(new(x, top, listWidth, contentHeight));
            menus.modTableBounds = table.Bounds;
            controls["commands"].Visible = wide || !showInspector;
            rowHeight = 64 * scale;
            pageSize = Math.Max(1, (int)(contentHeight / rowHeight));
            first = Math.Max(0, selected) / pageSize * pageSize;
            foreach (string id in new[] { "library-primary", "library-secondary", "library-more", "library-cancel" })
            {
                controls[id].Visible = showInspector;
            }
            controls["library-cancel"].Visible = showInspector && Job?.CanCancel == true;
            float actionWidth = (inspector.Width - gap) / 2;
            Place("library-primary", inspector.X, inspector.Bottom - control * 2 - gap, actionWidth, control);
            Place("library-secondary", inspector.X + actionWidth + gap, inspector.Bottom - control * 2 - gap, actionWidth, control);
            Place("library-more", inspector.X, inspector.Bottom - control, actionWidth, control);
            Place("library-cancel", inspector.X + actionWidth + gap, inspector.Bottom - control, actionWidth, control);
            controls["library-progress"].Visible = showInspector && Job is { Phase: "Downloading", TotalBytes: > 0 };
            Place("library-progress", inspector.X, inspector.Bottom - control * 2 - gap - 10 * scale, inspector.Width, 5 * scale);
            float pagerY = top + contentHeight + gap;
            Place("mod-inspector", x, pagerY, Math.Min(90 * scale, width * 0.2f), control);
            Place("library-list", x, pagerY, Math.Min(90 * scale, width * 0.2f), control);
            controls["library-list"].Visible = !wide && showInspector;
            controls["mod-inspector"].Visible = !controls["library-list"].Visible;
            Place("library-prev", right - Math.Min(width * 0.45f, 250 * scale), pagerY, Math.Min(width * 0.2f, 110 * scale), control);
            Place("library-next", right - Math.Min(width * 0.2f, 100 * scale), pagerY, Math.Min(width * 0.2f, 100 * scale), control);
            controls["library-prev"].Enabled = first > 0;
            controls["library-next"].Enabled = first + pageSize < Count;
            activity = new(x, pagerY + control + gap, width, control);
            Place("library-activity", activity.X, activity.Y, activity.Width, activity.Height);
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            string freshness = snapshot.Catalog.IsRefreshing ? "Refreshing catalog…" : snapshot.Catalog.Error is { } sourceError ? sourceError : snapshot.Catalog.FetchedAt is null ? "Catalog not loaded · Refresh" : snapshot.Catalog.IsStale ? "Saved catalog" : "Catalog up to date";
            if (snapshot.Catalog.FetchedAt is { } fetched)
            {
                freshness += " · " + fetched.ToLocalTime().ToString("g");
            }
            canvas.Text(snapshot.TargetLabel + " · " + freshness, 14 * scale, status, theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
            string activeTab = downloads ? "downloads-tab" : "catalog-tab";
            UiRect tab = controls[activeTab].Bounds;
            canvas.Fill(new(tab.X, tab.Bottom - 2, tab.Width, 2), theme.Foreground);
            if (wide || menus.catalogInspectorOpen)
            {
                canvas.PushClip(inspector);
                try
                {
                    float available = Math.Max(0, inspector.Height - 64 * scale - 16);
                    float previewHeight = available >= 120 * scale ? Math.Min(80 * scale, available * 0.22f) : 0;
                    UiRect preview = new(inspector.X + 10, inspector.Y + 8, Math.Max(0, inspector.Width - 20), previewHeight);
                    if (previewHeight > 0 && !canvas.ModPreview(Entry?.PreviewUrl ?? "", preview))
                    {
                        canvas.Fill(preview, theme.BorderDark);
                        canvas.Text(Entry?.Category is { Length: > 0 } category ? category + " · Preview unavailable" : "Package preview", 15 * scale, preview.Inset(8), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
                    }
                    if (previewHeight == 0)
                    {
                        canvas.ModPreview("", preview);
                    }
                    UiRect textBounds = new(inspector.X + 10, preview.Bottom + 8, Math.Max(0, inspector.Width - 20), Math.Max(0, available - previewHeight - 8));
                    string detail = Entry is null ? "Select a mod to see its details." : Entry.Name + "\n" + Entry.Author + "\n" + (Job?.Phase ?? menus.CatalogStatus(Entry)) + "\n" + Entry.Description + "\n\nVersion " + Entry.Version + " · " + Entry.Category + "\n" + Entry.Edition + " · " + Entry.Language + "\nSource: " + Entry.SourceId + "\n" + (Job is { } job ? DownloadProgress(job) + "\n" + job.Details + (job.Error is null ? "" : "\nNeeds attention · Full details") : "Download checks package support before installation.") + (snapshot.Error is null ? "" : "\nA library operation needs attention · Full details");
                    canvas.Text(detail, 17 * scale, textBounds, theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
                }
                finally
                {
                    canvas.PopClip();
                }
            }
            UiRect previous = controls["library-prev"].Bounds;
            canvas.Text($"{(Count == 0 ? 0 : first + 1)}–{Math.Min(Count, first + pageSize)} of {Count} · {first / pageSize + 1}/{Math.Max(1, (Count + pageSize - 1) / pageSize)}", 14 * scale, new(Bounds.X + 110 * scale, previous.Y, Math.Max(0, previous.X - Bounds.X - 118 * scale), previous.Height), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
            canvas.Text($"{menus.CurrentBindingText(ControlAction.Confirm)} Details · {menus.CurrentBindingText(ControlAction.Cancel)} Back · {menus.CurrentBindingText(ControlAction.UiTab)} Focus", 13 * scale, new(Bounds.X + 12, activity.Bottom + 4, Math.Max(0, Bounds.Width - 24), 23 * scale), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
        }

        private sealed class CatalogTable(ModCatalogElement owner) : UiElement
        {
            protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
            {
                canvas.PushClip(Bounds);
                try
                {
                    for (int offset = 0; offset < owner.pageSize && owner.first + offset < owner.Count; offset++)
                    {
                        int index = owner.first + offset;
                        RemoteModEntry entry = owner.downloads ? owner.jobs[index].Entry : owner.entries[index];
                        UiRect row = new(Bounds.X, Bounds.Y + offset * owner.rowHeight, Bounds.Width, owner.rowHeight);
                        if (index == owner.selected)
                        {
                            theme.Selection(canvas, row);
                        }
                        theme.RowSeparator(canvas, new(row.X, row.Bottom - 1, row.Width, 1));
                        canvas.Text(entry.Name, 17 * owner.scale, new(row.X + 8, row.Y + 4, Math.Max(0, row.Width - 16), row.Height * 0.45f), theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
                        string caption = owner.downloads ? JobGroupLabel(owner.jobs[index]) + " · " + owner.jobs[index].Phase : entry.Author + " · " + entry.Category + " · " + owner.menus.CatalogStatus(entry);
                        canvas.Text(caption, 13 * owner.scale, new(row.X + 8, row.Y + row.Height * 0.5f, Math.Max(0, row.Width - 16), row.Height * 0.4f), theme.Muted, UiAlignment.Center, UiTextFlow.SingleLine);
                    }
                    if (owner.Count == 0)
                    {
                        string empty = owner.downloads ? "No downloads. Browse Catalog to download and check a package." : owner.snapshot.Catalog.Entries.Count > 0 ? "No matching mods · Clear filters" : "Catalog unavailable · Refresh to retry. Installed mods remain available.";
                        canvas.Text(empty, 17 * owner.scale, Bounds.Inset(8), theme.Muted, UiAlignment.Start, UiTextFlow.Wrap);
                    }
                }
                finally
                {
                    canvas.PopClip();
                }
            }

            public override bool Key(UiKey key)
            {
                if (key is UiKey.Up or UiKey.Down or UiKey.Left or UiKey.Right or UiKey.PageUp or UiKey.PageDown)
                {
                    owner.Move(key is UiKey.Up or UiKey.Left or UiKey.PageUp ? -1 : 1, key is UiKey.Left or UiKey.Right or UiKey.PageUp or UiKey.PageDown);
                    return true;
                }
                if (key == UiKey.Confirm)
                {
                    owner.OpenInspector();
                    return true;
                }
                return false;
            }

            public override void Pointer(float x, float y)
            {
                if (!Bounds.Contains(x, y))
                {
                    return;
                }
                int offset = (int)((y - Bounds.Y) / owner.rowHeight);
                if (offset < owner.pageSize && owner.first + offset < owner.Count)
                {
                    owner.menus.modFocusedControlId = "commands";
                    owner.Select(owner.first + offset);
                }
            }
        }
    }
}
