using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class SceneInspectorElement : UiElement
{
    internal static UiRect PreviewBounds => new(18, 160, 370, 370);
    private readonly SceneInspectorController controller;
    private static readonly UiRect FilterBounds = new(420, 190, 218, 30);
    private static readonly UiRect PreviousPageBounds = new(420, 464, 58, 26);
    private static readonly UiRect NextPageBounds = new(872, 464, 58, 26);
    private static readonly UiRect PreviousDetailBounds = new(796, 502, 30, 23);
    private static readonly UiRect NextDetailBounds = new(898, 502, 30, 23);
    private static readonly UiRect ResetBounds = new(420, 612, 156, 28);
    private static readonly UiRect BackBounds = new(812, 612, 118, 28);
    private const float RowTop = 237;
    private const float RowHeight = 27;

    public SceneInspectorElement(SceneInspectorController controller)
    {
        this.controller = controller;
        Id = "scene-inspector";
        Focusable = true;
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, new UiRect(408, 18, 534, 646));
        canvas.Text(controller.SystemText("inspector.ui.sceneinspectorelement.drawself.scene.inspector", "SCENE INSPECTOR"), 23, new UiRect(426, 30, 488, 30), theme.Foreground);
        string field = controller.Snapshot is { } snapshot ? controller.FormatSystemText("inspector.ui.sceneinspectorelement.drawself.field.frame", "{0} \u00B7 field {1} \u00B7 frame {2}", snapshot.FieldName, snapshot.FieldId, snapshot.Frame) : controller.SystemText("inspector.ui.sceneinspectorelement.drawself.no.field.scene.is.loaded", "No field scene is loaded");
        Label(canvas, field, 15, new UiRect(426, 66, 488, 22), theme.Muted);
        foreach (SceneInspectorKind kind in Enum.GetValues<SceneInspectorKind>())
        {
            Button(canvas, theme, TabBounds(kind), controller.KindLabel(kind), controller.Kind == kind);
        }
        string explanation = controller.Kind == SceneInspectorKind.Event ? controller.SystemText("inspector.ui.sceneinspectorelement.drawself.suspends.one.script.in.place.other.scripts", "Suspends one script in place. Other scripts waiting for it may remain blocked.") : controller.Kind == SceneInspectorKind.Object ? controller.SystemText("inspector.ui.sceneinspectorelement.drawself.object.visibility.affects.the.preview.its.event", "Object visibility affects the preview. Its event scripts continue independently.") : controller.SystemText("inspector.ui.sceneinspectorelement.drawself.toggle.field.background.layers.and.preview.their", "Toggle field background layers and preview their composition.");
        canvas.Text(explanation, 14, new UiRect(426, 145, 488, 39), theme.Foreground);
        Button(canvas, theme, FilterBounds, controller.FormatSystemText("inspector.filter.label", "Filter: {0}", controller.FilterLabel), controller.Focus == SceneInspectorFocus.Filter);
        canvas.Text(controller.FormatSystemText("inspector.ui.sceneinspectorelement.drawself.entries", "{0} entries", controller.Entries.Count), 14, new UiRect(658, 196, 260, 22), theme.Muted);
        theme.Panel(canvas, new UiRect(420, 230, 510, 230));
        int first = controller.Page * SceneInspectorController.RowsPerPage;
        for (int index = first; index < Math.Min(first + SceneInspectorController.RowsPerPage, controller.Entries.Count); index++)
        {
            SceneInspectorEntry entry = controller.Entries[index];
            float y = RowTop + (index - first) * RowHeight;
            bool selected = index == controller.SelectedIndex;
            if (selected)
            {
                canvas.Fill(new UiRect(426, y, 498, RowHeight - 1), controller.Focus == SceneInspectorFocus.Entries ? theme.Highlight : new UiColor(90, 90, 90));
                theme.Pointer(canvas, 430, y + 8, 8);
            }
            Label(canvas, entry.Name, 15, new UiRect(452, y + 4, 360, 21), entry.Enabled ? theme.Foreground : theme.Muted);
            Button(canvas, theme, new UiRect(844, y + 1, 72, 24), entry.Enabled ? controller.SystemText("inspector.ui.sceneinspectorelement.drawself.on", "On") : controller.SystemText("inspector.ui.sceneinspectorelement.drawself.off", "Off"), false, entry.Enabled);
        }
        if (controller.Entries.Count == 0)
        {
            canvas.Text(controller.SystemText("inspector.ui.sceneinspectorelement.drawself.no.entries.match.this.tab.and.filter", "No entries match this tab and filter."), 16, new UiRect(440, 268, 466, 66), theme.Muted);
        }
        Button(canvas, theme, PreviousPageBounds, "<", false, controller.Page > 0);
        canvas.Text(controller.FormatSystemText("inspector.ui.sceneinspectorelement.drawself.page.mouse.wheel.scrolls", "Page {0} / {1} \u00B7 mouse wheel scrolls", controller.Page + 1, controller.PageCount), 13, new UiRect(490, 470, 372, 21), theme.Foreground);
        Button(canvas, theme, NextPageBounds, ">", false, controller.Page + 1 < controller.PageCount);
        theme.Panel(canvas, new UiRect(420, 498, 510, 108));
        Label(canvas, controller.Selected?.Name ?? controller.SystemText("inspector.ui.sceneinspectorelement.drawself.details", "Details"), 15, new UiRect(432, 505, 352, 23), theme.Foreground);
        Button(canvas, theme, PreviousDetailBounds, "<", false, controller.DetailPage > 0);
        canvas.Text($"{controller.DetailPage + 1}/{controller.DetailPageCount}", 12, new UiRect(835, 508, 59, 18), theme.Muted);
        Button(canvas, theme, NextDetailBounds, ">", false, controller.DetailPage + 1 < controller.DetailPageCount);
        int line = 0;
        foreach (string detail in controller.VisibleDetails)
        {
            Label(canvas, detail, 14, new UiRect(432, 530 + line * 17, 484, 18), theme.Foreground);
            line++;
        }
        Button(canvas, theme, ResetBounds, controller.SystemText("inspector.ui.sceneinspectorelement.drawself.reset.all", "Reset all"), controller.Focus == SceneInspectorFocus.Reset, controller.Snapshot is not null);
        Button(canvas, theme, BackBounds, controller.SystemText("inspector.ui.sceneinspectorelement.drawself.back", "Back"), controller.Focus == SceneInspectorFocus.Back);
        Label(canvas, controller.Message, 12, new UiRect(424, 643, 505, 18), theme.Foreground);
        canvas.Text(controller.SystemText("inspector.ui.sceneinspectorelement.drawself.complete.field.preview", "COMPLETE FIELD PREVIEW"), 18, new UiRect(18, 122, 370, 29), theme.Foreground);
        theme.Panel(canvas, new UiRect(18, 550, 370, 106));
        canvas.Text(controller.SystemText("inspector.ui.sceneinspectorelement.drawself.field.preview.paused", "FIELD PREVIEW · PAUSED"), 16, new UiRect(32, 562, 344, 25), theme.Foreground);
        canvas.Text(controller.SystemText("inspector.ui.sceneinspectorelement.drawself.left.right.tabs.up.down.select.confirm", "Left / Right: tabs · Up / Down: select\nConfirm: toggle · Page Up / Down: list\nTab: filter / reset / back · Cancel: back"), 13, new UiRect(32, 590, 344, 57), theme.Foreground);
    }

    public override bool Key(UiKey key)
    {
        MenuInput? input = key switch
        {
            UiKey.Up => MenuInput.Up,
            UiKey.Down => MenuInput.Down,
            UiKey.Left => MenuInput.Left,
            UiKey.Right => MenuInput.Right,
            UiKey.Confirm => MenuInput.Confirm,
            UiKey.PageUp => MenuInput.PageUp,
            UiKey.PageDown => MenuInput.PageDown,
            _ => null
        };
        if (input is null)
        {
            return false;
        }
        controller.Navigate(input.Value);
        return true;
    }

    public override void Pointer(float x, float y)
    {
        foreach (SceneInspectorKind kind in Enum.GetValues<SceneInspectorKind>())
        {
            if (TabBounds(kind).Contains(x, y))
            {
                controller.SetKind(kind);
                return;
            }
        }
        if (FilterBounds.Contains(x, y))
        {
            controller.SetFocus(SceneInspectorFocus.Filter);
            controller.CycleFilter();
        }
        else if (ResetBounds.Contains(x, y))
        {
            controller.SetFocus(SceneInspectorFocus.Reset);
            controller.ResetAll();
        }
        else if (BackBounds.Contains(x, y))
        {
            controller.Close();
        }
        else if (PreviousPageBounds.Contains(x, y) && controller.Page > 0)
        {
            controller.Select((controller.Page - 1) * SceneInspectorController.RowsPerPage);
        }
        else if (NextPageBounds.Contains(x, y) && controller.Page + 1 < controller.PageCount)
        {
            controller.Select((controller.Page + 1) * SceneInspectorController.RowsPerPage);
        }
        else if (PreviousDetailBounds.Contains(x, y))
        {
            controller.ChangeDetailPage(-1);
        }
        else if (NextDetailBounds.Contains(x, y))
        {
            controller.ChangeDetailPage(1);
        }
        else if (new UiRect(426, RowTop, 498, SceneInspectorController.RowsPerPage * RowHeight).Contains(x, y))
        {
            int index = controller.Page * SceneInspectorController.RowsPerPage + (int)((y - RowTop) / RowHeight);
            if (index < controller.Entries.Count)
            {
                controller.Select(index);
                if (x >= 844)
                {
                    controller.ToggleSelected();
                }
            }
        }
    }

    private static UiRect TabBounds(SceneInspectorKind kind)
    {
        return new UiRect(420 + (int)kind * 172, 101, 166, 34);
    }

    private static void Button(IUiCanvas canvas, UiTheme theme, UiRect bounds, string label, bool selected, bool enabled = true)
    {
        canvas.Fill(bounds, selected ? theme.Highlight : new UiColor(50, 50, 54));
        canvas.Line(bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, selected ? theme.BorderLight : theme.BorderDark, 1);
        Label(canvas, label, 14, new UiRect(bounds.X + 10, bounds.Y + 5, bounds.Width - 18, bounds.Height - 6), enabled ? theme.Foreground : theme.Muted);
    }

    private static void Label(IUiCanvas canvas, string label, float size, UiRect bounds, UiColor color)
    {
        canvas.PushClip(bounds);
        try
        {
            canvas.Text(label, size, bounds, color);
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
