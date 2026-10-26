using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class ModelBrowserElement : UiElement
{
    private readonly ModelBrowserController controller;
    private static readonly UiRect SearchBounds = new(440, 94, 490, 34);
    private static readonly UiRect BackBounds = new(828, 622, 102, 30);
    private static readonly UiRect VariantBounds = new(18, 540, 390, 30);
    private static readonly UiRect PreviousBounds = new(440, 540, 62, 30);
    private static readonly UiRect NextBounds = new(868, 540, 62, 30);
    private static readonly UiRect ViewBounds = new(440, 580, 178, 30);
    private static readonly UiRect CommandsBounds = new(440, 540, 178, 30);
    private const float RowTop = 146;
    private const float RowHeight = 31;

    public ModelBrowserElement(ModelBrowserController controller)
    {
        this.controller = controller;
        Id = "model-browser";
        Focusable = true;
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, new UiRect(10, 10, 940, 66));
        Label(canvas, controller.SystemText("inspector.ui.modelbrowserelement.drawself.model.browser", "MODEL BROWSER"), 24, new UiRect(24, 20, 700, 28), theme.Foreground);
        theme.Panel(canvas, new UiRect(10, 530, 940, 132));
        if (!controller.AssetsOpen)
        {
            Button(canvas, theme, VariantBounds, controller.SystemText("inspector.ui.modelbrowserelement.drawself.variant", "Variant: ") + (controller.CurrentVariant?.DisplayName ?? controller.SystemText("inspector.ui.modelbrowserelement.drawself.unavailable", "Unavailable")));
        }
        Button(canvas, theme, BackBounds, controller.Mode == ModelBrowserMode.Catalog ? controller.SystemText("inspector.ui.modelbrowserelement.drawself.close", "Close") : controller.SystemText("inspector.ui.modelbrowserelement.drawself.back", "Back"));
        Label(canvas, controller.Message, 13, new UiRect(24, 578, 384, 32), theme.Foreground);
        Label(canvas, controller.Hints, 12, new UiRect(24, 619, 790, 36), theme.Muted);
        if (controller.Mode == ModelBrowserMode.Catalog)
        {
            DrawCatalog(canvas, theme);
        }
        else
        {
            if (controller.AssetsOpen)
            {
                DrawAssets(canvas, theme);
            }
            else
            {
                DrawViewer(canvas, theme);
            }
        }
        if (controller.Mode == ModelBrowserMode.Commands)
        {
            theme.Panel(canvas, new UiRect(252, 236, 456, 166));
            Label(canvas, controller.SystemText("inspector.ui.modelbrowserelement.drawself.model.commands", "MODEL COMMANDS"), 19, new UiRect(270, 250, 420, 30), theme.Foreground);
            Button(canvas, theme, CommandBounds(0), controller.SystemText("inspector.ui.modelbrowserelement.drawself.export.to.blender", "Export to Blender"), controller.CommandIndex == 0);
            Button(canvas, theme, CommandBounds(1), controller.SystemText("inspector.ui.modelbrowserelement.drawself.import.from.blender.as.mod", "Import from Blender (as mod)"), controller.CommandIndex == 1);
        }
    }

    private void DrawCatalog(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, new UiRect(426, 84, 516, 440));
        Button(canvas, theme, SearchBounds, controller.Search.Length == 0 ? controller.SystemText("inspector.ui.modelbrowserelement.drawcatalog.search.models.and.supported", "Search models (* and ? supported)") : controller.Search, controller.SearchFocused);
        int first = controller.Page * ModelBrowserController.RowsPerPage;
        for (int index = first; index < Math.Min(first + ModelBrowserController.RowsPerPage, controller.Entries.Count); index++)
        {
            ModelBrowserEntry entry = controller.Entries[index];
            Button(canvas, theme, new UiRect(440, RowTop + (index - first) * RowHeight, 490, RowHeight - 2), entry.Name + "  ·  " + entry.Category, index == controller.SelectedIndex);
        }
        if (controller.Entries.Count == 0)
        {
            Label(canvas, controller.SystemText("inspector.ui.modelbrowserelement.drawcatalog.no.models.match.this.search", "No models match this search."), 16, new UiRect(450, 160, 468, 60), theme.Muted);
        }
        Label(canvas, controller.Selected?.Name ?? controller.SystemText("inspector.ui.modelbrowserelement.drawcatalog.model.preview", "MODEL PREVIEW"), 18, new UiRect(18, 92, 390, 30), theme.Foreground);
        Button(canvas, theme, PreviousBounds, "<");
        Button(canvas, theme, NextBounds, ">");
        Label(canvas, controller.FormatSystemText("inspector.ui.modelbrowserelement.drawcatalog.models.page", "{0} models \u00B7 Page {1} / {2}", controller.Entries.Count, controller.Page + 1, controller.PageCount), 14, new UiRect(512, 546, 348, 22), theme.Foreground);
        Button(canvas, theme, ViewBounds, controller.SystemText("inspector.ui.modelbrowserelement.drawcatalog.view.selected.model", "View selected model"));
        string details = string.Join(" · ", controller.Selected?.Details?.Take(2) ?? []);
        Label(canvas, details, 12, new UiRect(634, 582, 294, 28), theme.Muted);
    }

    private void DrawViewer(IUiCanvas canvas, UiTheme theme)
    {
        Label(canvas, (controller.Selected?.Name ?? controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.model", "Model")) + $" · {controller.SelectedIndex + 1}/{controller.Entries.Count} · " + controller.CurrentVariant?.DisplayName, 16, new UiRect(24, 53, 900, 23), theme.Foreground);
        Button(canvas, theme, CommandsBounds, controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.commands", "Commands"));
        Button(canvas, theme, new UiRect(628, 540, 92, 30), controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.prev", "Prev"));
        Button(canvas, theme, new UiRect(730, 540, 92, 30), controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.next", "Next"));
        Button(canvas, theme, new UiRect(832, 540, 46, 30), "+");
        Button(canvas, theme, new UiRect(886, 540, 44, 30), "-");
        string[] labels = ["<", ">", controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.up", "Up"), controller.SystemText("inspector.ui.modelbrowserelement.drawviewer.down", "Down")];
        for (int index = 0; index < labels.Length; index++)
        {
            Button(canvas, theme, RotateBounds(index), labels[index]);
        }
        Label(canvas, controller.FormatSystemText("inspector.ui.modelbrowserelement.drawviewer.rotate.zoom.x", "Rotate \u00B7 Zoom {0:0.00}x", controller.Zoom), 13, new UiRect(736, 586, 194, 23), theme.Muted);
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
            UiKey.Tab => MenuInput.Tab,
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
        if (controller.AssetsOpen)
        {
            AssetsPointer(x, y);
            return;
        }
        if (controller.Mode == ModelBrowserMode.Commands)
        {
            for (int index = 0; index < 2; index++)
            {
                if (CommandBounds(index).Contains(x, y))
                {
                    controller.ExecuteCommand(index);
                    return;
                }
            }
            if (BackBounds.Contains(x, y))
            {
                controller.Cancel();
            }
            return;
        }
        if (BackBounds.Contains(x, y))
        {
            controller.Cancel();
        }
        else if (VariantBounds.Contains(x, y))
        {
            controller.CycleVariant();
        }
        else if (controller.Mode == ModelBrowserMode.Catalog)
        {
            CatalogPointer(x, y);
        }
        else
        {
            ViewerPointer(x, y);
        }
    }

    private void CatalogPointer(float x, float y)
    {
        controller.SetSearchFocused(SearchBounds.Contains(x, y));
        if (SearchBounds.Contains(x, y))
        {
            return;
        }
        if (ViewBounds.Contains(x, y) || controller.PreviewBounds.Contains(x, y))
        {
            controller.OpenViewer();
        }
        else if (PreviousBounds.Contains(x, y) && controller.Page > 0)
        {
            controller.Select((controller.Page - 1) * ModelBrowserController.RowsPerPage);
        }
        else if (NextBounds.Contains(x, y) && controller.Page + 1 < controller.PageCount)
        {
            controller.Select((controller.Page + 1) * ModelBrowserController.RowsPerPage);
        }
        else if (new UiRect(440, RowTop, 490, ModelBrowserController.RowsPerPage * RowHeight).Contains(x, y))
        {
            int index = controller.Page * ModelBrowserController.RowsPerPage + (int)((y - RowTop) / RowHeight);
            if (index < controller.Entries.Count)
            {
                bool alreadySelected = index == controller.SelectedIndex;
                controller.Select(index);
                if (alreadySelected)
                {
                    controller.OpenViewer();
                }
            }
        }
    }

    private void ViewerPointer(float x, float y)
    {
        if (CommandsBounds.Contains(x, y))
        {
            controller.OpenCommands();
        }
        else if (new UiRect(628, 540, 92, 30).Contains(x, y))
        {
            controller.StepModel(-1);
        }
        else if (new UiRect(730, 540, 92, 30).Contains(x, y))
        {
            controller.StepModel(1);
        }
        else if (new UiRect(832, 540, 46, 30).Contains(x, y))
        {
            controller.ZoomBy(1.1f);
        }
        else if (new UiRect(886, 540, 44, 30).Contains(x, y))
        {
            controller.ZoomBy(1 / 1.1f);
        }
        else
        {
            MenuInput[] rotations = [MenuInput.Left, MenuInput.Right, MenuInput.Up, MenuInput.Down];
            for (int index = 0; index < rotations.Length; index++)
            {
                if (RotateBounds(index).Contains(x, y))
                {
                    controller.Navigate(rotations[index]);
                    return;
                }
            }
        }
    }

    private static UiRect CommandBounds(int index)
    {
        return new UiRect(270, 290 + index * 48, 420, 38);
    }

    private static UiRect RotateBounds(int index)
    {
        return new UiRect(440 + index * 73, 580, 64, 30);
    }

    private static void Button(IUiCanvas canvas, UiTheme theme, UiRect bounds, string label, bool selected = false)
    {
        canvas.Fill(bounds, selected ? theme.Highlight : new UiColor(50, 50, 54));
        canvas.Line(bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, theme.BorderLight, 1);
        Label(canvas, label, 14, new UiRect(bounds.X + 8, bounds.Y + 6, bounds.Width - 16, bounds.Height - 6), theme.Foreground);
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
