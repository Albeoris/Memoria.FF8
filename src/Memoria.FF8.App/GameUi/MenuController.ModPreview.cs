using System.Diagnostics;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private ModPreviewResourcesProfile? modPreviewProfile;
    private CancellationTokenSource? modPreviewCancellation;
    private Task<ModPreviewDocument>? modPreviewOpening;
    private Task<ModPreviewComparison>? modPreviewComparing;
    private ModPreviewDocument? modPreviewDocument;
    private ModPreviewEntry? modPreviewSelected;
    private ModPreviewComparison? modPreviewResult;
    private string modPreviewMessage = "";
    private bool modPreviewExpanded;
    private bool modPreviewUseItem;
    private CatalogPreviewPresentation.WorldComparison? modPreviewWorld;
    private ModPreviewTreeNode? modPreviewTreeSelection;
    private string modPreviewFocus = "preview-tree";
    private float modPreviewYaw;
    private float modPreviewTextOffset;
    private int modPreviewTextRow;
    private float modPreviewPose;
    private LocalizationModPreview? modPreviewLocalization;
    private PlayerAction? queuedPreviewAction;
    private long previewSceneUpdated;
    private int? modPreviewTreeOffset;
    private readonly HashSet<ModPreviewTreeNode> modPreviewOpenFolders = [];

    internal void ConnectModPreview(ModPreviewResourcesProfile profile, string? mediaTools = null, bool mediaAudioEnabled = true, PcmAudioOutput? audioOutput = null)
    {
        modPreviewProfile = profile;
        modPreviewMediaTools = mediaTools;
        modPreviewMediaAudioEnabled = mediaAudioEnabled;
        modPreviewMediaAudioOutput = audioOutput;
    }

    private void RegisterModPreviewScreen()
    {
        Register(new()
        {
            Id = "mods-preview",
            Title = "Mods · Preview changes",
            Help = "Compare with the original game. No changes are applied to your game.",
            Entries = _ => [],
            BuildContent = (_, panel) => panel.Children.Add(new ModPreviewElement(this))
        });
    }

    private ModPackage? PreviewInstalledPackage(RemoteModEntry entry)
    {
        return entry.Official is { } official
            ? modService?.View.Packages.FirstOrDefault(view => view.Package.Id.Equals(official.RuntimePackageId, StringComparison.OrdinalIgnoreCase))?.Package
            : AssociatedMod(entry)?.Package;
    }

    private bool HasModPreview(RemoteModEntry entry) => modPreviewProfile is not null && (EntryJob(entry)?.CanPreview == true || PreviewInstalledPackage(entry) is not null);

    private bool HasInstalledModPreview(ModPackage package) => modLibrary is not null && modPreviewProfile is not null && ModPreviewDocument.CanOpenInstalled(package);

    private void OpenModPreview(RemoteModEntry entry)
    {
        OpenModPreview(entry, PreviewInstalledPackage(entry));
    }

    private void OpenModPreview(RemoteModEntry? entry, ModPackage? package)
    {
        CloseModPreview();
        modPreviewExpanded = false;
        modPreviewTreeOffset = null;
        modPreviewFocus = "preview-tree";
        modPreviewMessage = "Reading package changes…";
        modPreviewCancellation = new();
        modPreviewOpening = modLibrary!.OpenPreviewAsync(entry, package, modPreviewCancellation.Token);
        Push("mods-preview");
    }

    private void SelectModPreview(ModPreviewEntry selection)
    {
        if (modPreviewSelected == selection)
        {
            return;
        }
        CancelModPreviewWork();
        modPreviewSelected = selection;
        modPreviewWorld?.Dispose();
        modPreviewWorld = null;
        modPreviewYaw = 0.4f;
        modPreviewPose = 0;
        EndModMediaPreview();
        EndLocalizationPreview();
        modPreviewUseItem = false;
        modPreviewTextOffset = 0;
        modPreviewTextRow = 0;
        modPreviewResult = null;
        modPreviewMessage = "Comparing with the original game…";
        modPreviewCancellation = new();
        if (selection.IsMedia)
        {
            modPreviewMediaOpening = OpenModPreviewMediaAsync(modPreviewDocument!, selection, modPreviewProfile!, modPreviewCancellation.Token);
        }
        else if (selection.Id == ModPreviewDocument.LocalizationSceneId)
        {
            modPreviewLocalization = new(modPreviewProfile!, modPreviewDocument!.PackageCatalog, modPreviewMediaTools, modService?.View.Packages.Select(view => view.Package).ToArray());
            modPreviewComparing = modPreviewLocalization.RenderAsync(null, modPreviewCancellation.Token);
        }
        else
        {
            modPreviewComparing = selection.Id == ModPreviewDocument.WorldSceneId
                ? (modPreviewWorld = new(modPreviewProfile!, modPreviewDocument!.PackageCatalog)).RenderAsync(0, 0, modPreviewCancellation.Token)
                : CatalogPreviewPresentation.CompareAsync(modPreviewDocument!, selection, modPreviewProfile!, modPreviewCancellation.Token);
        }
    }

    private void MovePreviewWorld(float east, float south)
    {
        if (modPreviewWorld is null || modPreviewComparing is not null || modPreviewResult is null)
        {
            return;
        }
        modPreviewComparing = modPreviewWorld.RenderAsync(east, south, modPreviewCancellation!.Token);
    }

    private void RotatePreviewModels(float yaw, float seconds)
    {
        if (modPreviewResult is not { } current || modPreviewComparing is not null)
        {
            return;
        }
        modPreviewYaw = MathF.IEEERemainder(modPreviewYaw + yaw, MathF.Tau);
        modPreviewPose += seconds;
        float angle = modPreviewYaw;
        float pose = modPreviewPose;
        modPreviewComparing = Task.Run(() => CatalogPreviewPresentation.DrawModels(current, angle, pose), modPreviewCancellation!.Token);
    }

    private void MoveModPreviewText(int direction)
    {
        if (modPreviewResult is not { TextRows.Count: > 1 } result)
        {
            return;
        }
        int next = Math.Clamp(modPreviewTextRow + direction, 0, result.TextRows.Count - 1);
        if (next != modPreviewTextRow)
        {
            modPreviewTextRow = next;
            modPreviewResult = result.SelectTextRow(next);
            modPreviewTextOffset = 0;
            _view = null;
        }
    }

    private void SendLocalizationAction(PlayerAction action)
    {
        if (modPreviewLocalization is null)
        {
            return;
        }
        queuedPreviewAction = action;
        UpdateLocalizationPreview();
    }

    private void UpdateLocalizationPreview()
    {
        if (modPreviewLocalization is not { } scene || modPreviewComparing is not null || modPreviewResult is null)
        {
            return;
        }
        PlayerAction? action = queuedPreviewAction;
        if (action is null && modPreviewExpanded && Stopwatch.GetElapsedTime(previewSceneUpdated).TotalMilliseconds >= 66)
        {
            action = new(PlayerActionKind.Wait, Frames: 2, WaitWhilePlayable: true);
        }
        if (action is not null)
        {
            queuedPreviewAction = null;
            previewSceneUpdated = Stopwatch.GetTimestamp();
            modPreviewComparing = scene.RenderAsync(action, modPreviewCancellation!.Token);
        }
    }

    private void EndLocalizationPreview()
    {
        modPreviewLocalization?.Dispose();
        modPreviewLocalization = null;
        queuedPreviewAction = null;
        previewSceneUpdated = Stopwatch.GetTimestamp();
    }

    private void PollModPreview()
    {
        if (ScreenId != "mods-preview")
        {
            return;
        }
        PollModMedia();
        if (modPreviewOpening is { IsCompleted: true } opening)
        {
            modPreviewOpening = null;
            if (opening.IsCompletedSuccessfully)
            {
                modPreviewDocument = opening.Result;
                IReadOnlyList<ModPreviewTreeNode> path = modPreviewDocument.Tree.InitialPath;
                if (path.LastOrDefault() is { Entry: { } first } leaf)
                {
                    modPreviewOpenFolders.UnionWith(path.Take(path.Count - 1));
                    modPreviewTreeSelection = leaf;
                    modPreviewFocus = "preview-tree";
                    SelectModPreview(first);
                }
                else
                {
                    modPreviewMessage = "No changes are available for comparison.";
                }
            }
            else
            {
                modPreviewMessage = opening.Exception?.GetBaseException().Message ?? "The preview was cancelled.";
            }
            _view = null;
        }
        if (modPreviewComparing is { IsCompleted: true } comparing)
        {
            bool rebuild = modPreviewLocalization is null || modPreviewResult is null;
            modPreviewComparing = null;
            if (comparing.IsCompletedSuccessfully)
            {
                modPreviewResult = comparing.Result;
                modPreviewMessage = "";
            }
            else
            {
                modPreviewMessage = comparing.Exception?.GetBaseException().Message ?? "The comparison was cancelled.";
                EndLocalizationPreview();
                rebuild = true;
            }
            if (rebuild)
            {
                _view = null;
            }
        }
        UpdateLocalizationPreview();
    }

    private bool NavigateModPreview(MenuInput input)
    {
        if (input is MenuInput.Cancel or MenuInput.Actions)
        {
            if (modPreviewExpanded)
            {
                modPreviewExpanded = false;
                modPreviewFocus = "preview-tree";
                _view = null;
            }
            else
            {
                Back();
            }
            return true;
        }
        if (modPreviewExpanded && modPreviewLocalization is not null && input == MenuInput.Alternate)
        {
            SendLocalizationAction(new(PlayerActionKind.Cancel));
            return true;
        }
        if (modPreviewExpanded && modPreviewLocalization is not null && input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right)
        {
            float horizontal = input == MenuInput.Left ? -1 : input == MenuInput.Right ? 1 : 0;
            float vertical = input == MenuInput.Up ? -1 : input == MenuInput.Down ? 1 : 0;
            SendLocalizationAction(new(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical, Frames: 8));
            return true;
        }
        if (modPreviewExpanded && modPreviewWorld is not null && input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right)
        {
            MovePreviewWorld(input == MenuInput.Left ? -900 : input == MenuInput.Right ? 900 : 0, input == MenuInput.Up ? -900 : input == MenuInput.Down ? 900 : 0);
            return true;
        }
        if (input is MenuInput.FocusLeft or MenuInput.FocusRight or MenuInput.ReverseTab)
        {
            UiView view = BuildView();
            UiElement[] targets = FocusableDescendants(view.Root)
                .Where(element => element.Visible && element.Enabled && !element.Bounds.IsEmpty).ToArray();
            if (targets.Length != 0)
            {
                UiElement? target;
                if (input == MenuInput.ReverseTab)
                {
                    int index = Array.IndexOf(targets, view.FocusedElement);
                    target = targets[(Math.Max(0, index) + targets.Length - 1) % targets.Length];
                }
                else
                {
                    string id = input == MenuInput.FocusLeft ? "preview-tree" : "preview-action-0";
                    target = targets.FirstOrDefault(element => element.Id == id)
                        ?? targets.FirstOrDefault(element => element.Id == "preview-timeline");
                }
                if (target is not null)
                {
                    view.Focus(target);
                }
            }
            return true;
        }
        UiKey? key = input switch
        {
            MenuInput.Up => UiKey.Up,
            MenuInput.Down => UiKey.Down,
            MenuInput.Left => UiKey.Left,
            MenuInput.Right => UiKey.Right,
            MenuInput.PageUp => UiKey.PageUp,
            MenuInput.PageDown => UiKey.PageDown,
            MenuInput.Confirm => UiKey.Confirm,
            MenuInput.Tab => UiKey.Tab,
            _ => null
        };
        if (key is { } navigation)
        {
            UiView view = BuildView();
            if (input is MenuInput.Up or MenuInput.Down && view.FocusedElement is UiScrollPanel { Id: "preview-before-text" or "preview-after-text" } panel && panel.MaximumOffset > 0)
            {
                panel.ScrollBy((input == MenuInput.Down ? 36 : -36) * TextScale);
            }
            else
            {
                view.Key(navigation);
            }
        }
        return true;
    }

    private void ScrollModPreview(int x, int y, int delta)
    {
        UiView view = BuildView();
        var point = view.ToDesignPoint(x, y);
        if (FocusableDescendants(view.Root).OfType<UiScrollPanel>().FirstOrDefault(panel => panel.Visible && panel.Bounds.Contains(point.X, point.Y)) is { } scroll)
        {
            scroll.ScrollBy(-delta / 120f * 48 * TextScale);
            return;
        }
        if (FocusableDescendants(view.Root).OfType<UiList>().FirstOrDefault(list => list.Id == "preview-tree" && list.Visible && list.Bounds.Contains(point.X, point.Y)) is { } tree)
        {
            tree.ScrollOffset -= Math.Sign(delta) * 3;
            modPreviewTreeOffset = tree.ScrollOffset;
        }
    }

    private void CancelModPreviewWork()
    {
        modPreviewCancellation?.Cancel();
        modPreviewCancellation?.Dispose();
        modPreviewCancellation = null;
        ObserveAbandonedPreview(modPreviewOpening);
        ObserveAbandonedPreview(modPreviewComparing);
        modPreviewOpening = null;
        modPreviewComparing = null;
    }

    private static void ObserveAbandonedPreview(Task? task)
    {
        _ = task?.ContinueWith(static done => _ = done.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void CloseModPreview()
    {
        CancelModPreviewWork();
        EndModMediaPreview();
        EndLocalizationPreview();
        modPreviewWorld?.Dispose();
        modPreviewWorld = null;
        modPreviewDocument = null;
        modPreviewSelected = null;
        modPreviewTreeSelection = null;
        modPreviewOpenFolders.Clear();
        modPreviewResult = null;
    }

    private sealed class ModPreviewElement : UiElement
    {
        private readonly MenuController menus;
        private readonly UiList tree;
        private readonly UiButton expand;
        private readonly UiButton back;
        private readonly UiButton use;
        private readonly PreviewMediaTimeline timeline;
        private readonly UiScrollPanel beforeText;
        private readonly UiScrollPanel afterText;
        private readonly List<UiButton> interaction = [];
        private readonly List<ModPreviewTreeNode> rows = [];
        private UiRect comparison;

        public ModPreviewElement(MenuController menus)
        {
            this.menus = menus;
            List<string> labels = [];
            AddRows(menus.modPreviewDocument?.Tree.Branches ?? [], 0);
            tree = new()
            {
                Id = "preview-tree",
                Visible = !menus.modPreviewExpanded && rows.Count > 0,
                Items = labels,
                RowHeight = 28 * menus.TextScale,
                FontSize = 16 * menus.TextScale,
                AutoRowHeight = false,
                KeepSelectionVisibleOnDraw = false,
                TextFlow = UiTextFlow.SingleLine,
                SelectedIndex = menus.modPreviewTreeSelection is { } selected ? rows.IndexOf(selected) : 0,
                Confirmed = ToggleGroup,
                SelectionChanged = index =>
                {
                    menus.modPreviewTreeOffset = tree!.ScrollOffset;
                    menus.modPreviewTreeSelection = rows[index];
                    if (rows[index].Entry is { } entry)
                    {
                        menus.SelectModPreview(entry);
                    }
                }
            };
            expand = Button("preview-expand", menus.modPreviewExpanded ? "Show change list" : "Expand comparison", () =>
            {
                menus.modPreviewExpanded = !menus.modPreviewExpanded;
                menus.modPreviewFocus = menus.modPreviewExpanded
                    ? menus.modPreviewLocalization is null ? "preview-expand" : "preview-action-0"
                    : "preview-tree";
                menus._view = null;
            });
            expand.Visible = menus.modPreviewDocument?.Entries.Count > 0;
            back = Button("preview-back", "Back", menus.Back);
            use = Button("preview-use", "Use item", () => menus.modPreviewUseItem = true);
            use.Visible = menus.modPreviewResult?.After.CanUseItem == true;
            beforeText = TextPanel("preview-before-text", menus.modPreviewResult?.Before.Text ?? "");
            afterText = TextPanel("preview-after-text", menus.modPreviewResult?.After.Text ?? "");
            Children.Add(beforeText);
            Children.Add(afterText);
            TrackFocus(tree);
            Children.Add(tree);
            Children.Add(expand);
            Children.Add(back);
            Children.Add(use);
            timeline = new(menus) { Id = "preview-timeline", Focusable = true, Visible = menus.modPreviewMedia?.Playback.DurationSeconds > 0 };
            TrackFocus(timeline);
            Children.Add(timeline);
            if (menus.modPreviewResult is not null && menus.modPreviewWorld is not null)
            {
                AddControl("West", () => menus.MovePreviewWorld(-900, 0));
                AddControl("North", () => menus.MovePreviewWorld(0, -900));
                AddControl("South", () => menus.MovePreviewWorld(0, 900));
                AddControl("East", () => menus.MovePreviewWorld(900, 0));
            }
            else if (menus.modPreviewMedia?.Playback is { DurationSeconds: > 0 } media)
            {
                AddControl(media.IsPlaying ? "Pause" : media.PositionSeconds >= media.DurationSeconds ? "Replay" : "Play", menus.ToggleModMediaPlayback);
                if (menus.modPreviewMediaAudioEnabled && (media.Before.HasAudio || media.After.HasAudio))
                {
                    AddControl(media.ListenAfter ? "Sound · After" : "Sound · Before", menus.ToggleModMediaSound);
                }
            }
            else if (menus.modPreviewResult is not null && menus.modPreviewLocalization is not null)
            {
                AddControl("Interact", () => menus.SendLocalizationAction(new(PlayerActionKind.Confirm)));
                AddControl("Scene Cancel", () => menus.SendLocalizationAction(new(PlayerActionKind.Cancel)));
            }
            else if (menus.modPreviewResult is { TextRows.Count: > 1 } text)
            {
                AddControl("Previous line", () => menus.MoveModPreviewText(-1));
                AddControl("Next line", () => menus.MoveModPreviewText(1));
                interaction[0].Enabled = menus.modPreviewTextRow > 0;
                interaction[1].Enabled = menus.modPreviewTextRow + 1 < text.TextRows.Count;
            }
            else if (menus.modPreviewResult is { Before.Model: not null } or { After.Model: not null })
            {
                AddControl("Rotate left", () => menus.RotatePreviewModels(-0.35f, 0));
                AddControl("Next pose", () => menus.RotatePreviewModels(0, 0.15f));
                AddControl("Rotate right", () => menus.RotatePreviewModels(0.35f, 0));
            }

            void AddRows(IReadOnlyList<ModPreviewTreeNode> branches, int depth)
            {
                string indent = new(' ', depth * 4);
                foreach (ModPreviewTreeNode branch in branches)
                {
                    rows.Add(branch);
                    bool open = menus.modPreviewOpenFolders.Contains(branch);
                    string marker = branch.Entry is null ? open ? "▾ " : "▸ " : "  ";
                    labels.Add(indent + marker + branch.Label);
                    if (open)
                    {
                        AddRows(branch.Children, depth + 1);
                    }
                }
            }

            void AddControl(string label, Action click)
            {
                UiButton button = Button("preview-action-" + interaction.Count, label, click);
                interaction.Add(button);
                Children.Add(button);
            }
        }

        private void ToggleGroup(int index)
        {
            ModPreviewTreeNode row = rows[index];
            if (row.Entry is not null)
            {
                return;
            }
            if (!menus.modPreviewOpenFolders.Remove(row))
            {
                menus.modPreviewOpenFolders.Add(row);
            }
            menus._view = null;
        }

        private UiScrollPanel TextPanel(string id, string text)
        {
            UiScrollPanel panel = new() { Id = id, Visible = text.Length != 0, OffsetChanged = offset => menus.modPreviewTextOffset = offset };
            TrackFocus(panel);
            panel.Children.Add(new UiLabel { Text = text, TextFlow = UiTextFlow.Wrap, FontSize = 16 * menus.TextScale });
            return panel;
        }

        private UiButton Button(string id, string title, Action click)
        {
            UiButton button = new() { Id = id, Text = title, Click = click, FontSize = 16 * menus.TextScale, DrawBackground = true };
            TrackFocus(button);
            return button;
        }

        private void TrackFocus(UiElement element)
        {
            element.FocusChanged = focused =>
            {
                if (focused)
                {
                    menus.modPreviewFocus = element.Id!;
                }
            };
        }

        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            UiRect inner = bounds.Inset(10);
            float footer = 30 * menus.TextScale;
            float top = inner.Y + 33 * menus.TextScale;
            float timelineHeight = timeline.Visible ? footer + 6 : 0;
            float height = Math.Max(0, inner.Bottom - footer - top - 8 - timelineHeight);
            float sidebar = tree.Visible ? Math.Min(inner.Width * 0.32f, 335 * menus.TextScale) : 0;
            if (menus.modPreviewTreeOffset is null)
            {
                tree.ScrollOffset = 0;
            }
            tree.Arrange(new(inner.X, top, Math.Max(0, sidebar - 8), height));
            if (tree.Visible)
            {
                if (menus.modPreviewTreeOffset is { } savedOffset)
                {
                    tree.ScrollOffset = savedOffset;
                }
                menus.modPreviewTreeOffset = tree.ScrollOffset;
            }
            comparison = new(inner.X + sidebar, top, Math.Max(0, inner.Width - sidebar), height);
            timeline.Arrange(new(inner.X, comparison.Bottom + 4, inner.Width, timeline.Visible ? footer : 0));
            back.Arrange(new(inner.X, inner.Bottom - footer, 95 * menus.TextScale, footer));
            expand.Arrange(new(inner.Right - 180 * menus.TextScale, inner.Bottom - footer, 180 * menus.TextScale, footer));
            use.Arrange(new(comparison.X + comparison.Width * 0.5f + 10, comparison.Bottom - footer - 8, 112 * menus.TextScale, footer));
            float usable = Math.Max(0, expand.Bounds.X - back.Bounds.Right - 16);
            float width = Math.Min((timeline.Visible ? 144 : 120) * menus.TextScale, usable / Math.Max(1, interaction.Count));
            for (int index = 0; index < interaction.Count; index++)
            {
                interaction[index].Arrange(new(back.Bounds.Right + 8 + index * width, expand.Bounds.Y, Math.Max(0, width - 4), footer));
            }
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            float scale = menus.TextScale;
            canvas.Text(menus.modPreviewDocument?.Name ?? "Preview changes", 21 * scale, new(Bounds.X + 10, Bounds.Y + 8, Math.Max(0, Bounds.Width - 20), 28 * scale), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
            if (menus.modPreviewResult is not { } result)
            {
                canvas.Text(menus.modPreviewMessage, 17 * scale, comparison.Inset(12), theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
                return;
            }
            string status = menus.modPreviewMessage;
            if (status.Length == 0 && result.TextRows.Count > 1)
            {
                status = $"Line {menus.modPreviewTextRow + 1} of {result.TextRows.Count}";
            }
            if (status.Length != 0)
            {
                canvas.Text(status, 14 * scale, new(comparison.X, Bounds.Y + 8, comparison.Width, 26 * scale), theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            }
            float width = Math.Max(0, (comparison.Width - 8) / 2);
            DrawSide("Before", result.Before, new(comparison.X, comparison.Y, width, comparison.Height), beforeText, 0);
            DrawSide("After", result.After, new(comparison.Right - width, comparison.Y, width, comparison.Height), afterText, 1);

            void DrawSide(string title, ModPreviewSide side, UiRect rect, UiScrollPanel scroll, int slot)
            {
                theme.Panel(canvas, rect);
                UiRect area = rect.Inset(10);
                canvas.Text(title, 18 * scale, new(area.X, area.Y, area.Width, 25 * scale), theme.Foreground, UiAlignment.Start, UiTextFlow.SingleLine);
                bool reveal = !side.CanUseItem || menus.modPreviewUseItem;
                CatalogMediaSide? media = menus.ModMediaSide(slot);
                var image = media?.Image ?? side.Image;
                float textHeight = image is null ? area.Height - 31 * scale : 60 * scale;
                UiRect body = new(area.X, area.Y + 31 * scale, area.Width, Math.Max(0, textHeight));
                UiLabel label = (UiLabel)scroll.Children[0];
                if (scroll.Bounds != body || label.Text != side.Text)
                {
                    label.Text = side.Text;
                    scroll.Visible = side.Text.Length != 0;
                    float offset = menus.modPreviewTextOffset;
                    scroll.Arrange(body);
                    float content = MeasureCatalogText(canvas, side.Text, 16 * scale, Math.Max(1, body.Width - scroll.ScrollbarWidth));
                    scroll.ContentHeight = content;
                    scroll.Offset = offset;
                }
                if (image is not null && reveal)
                {
                    float top = area.Y + 31 * scale + textHeight;
                    float bottom = side.CanUseItem ? use.Bounds.Y - 8 : area.Bottom;
                    canvas.RasterImage(image, new(area.X, top, area.Width, Math.Max(0, bottom - top)), slot, media?.ImageRevision ?? 0);
                }
            }
        }
    }
}
