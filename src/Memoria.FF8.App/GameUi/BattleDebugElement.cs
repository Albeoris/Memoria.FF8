using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class BattleDebugElement : UiElement
{
    private readonly BattleDebugController controller;
    private int scroll;
    private const int VisibleRows = 16;
    private const float RowHeight = 29;

    public BattleDebugElement(BattleDebugController controller)
    {
        this.controller = controller;
        Id = "battle-debug";
        Focusable = true;
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Fill(Bounds, new UiColor(12, 12, 16));
        theme.Panel(canvas, new UiRect(18, 18, 924, 66));
        canvas.Text("DEBUG BATTLE", 28, new UiRect(36, 28, 860, 40), theme.Foreground);
        theme.Panel(canvas, new UiRect(18, 94, 626, 510));
        theme.Panel(canvas, new UiRect(656, 94, 286, 510));
        EnsureVisible();
        for (int index = scroll; index < Math.Min(controller.Rows.Count, scroll + VisibleRows); index++)
        {
            BattleDebugRow row = controller.Rows[index];
            float y = 110 + (index - scroll) * RowHeight;
            if (index == controller.SelectedIndex)
            {
                canvas.Fill(new UiRect(25, y, 612, RowHeight - 1), theme.Highlight);
                theme.Pointer(canvas, 30, y + 8);
            }
            canvas.Text(row.Label, 16, new UiRect(50, y + 4, 355, RowHeight), theme.Foreground);
            canvas.Text(row.Value, 16, new UiRect(412, y + 4, 220, RowHeight), theme.Foreground);
        }
        if (controller.Rows.Count > VisibleRows)
        {
            float height = 464 * VisibleRows / (float)controller.Rows.Count;
            float y = 110 + (464 - height) * scroll / (controller.Rows.Count - VisibleRows);
            canvas.Fill(new UiRect(638, y, 3, height), theme.Muted);
        }
        BattleDebugRow selected = controller.Rows[controller.SelectedIndex];
        canvas.Text(selected.Label, 21, new UiRect(674, 112, 248, 76), theme.Foreground);
        canvas.Text(selected.Help, 18, new UiRect(674, 208, 248, 238), theme.Foreground);
        canvas.Text("↑ ↓ Select\n← → Adjust / Expand\nConfirm: Edit / Start\nCancel: Return\nPgUp / PgDn: Scroll", 16, new UiRect(674, 446, 248, 140), theme.Muted);
        theme.Panel(canvas, new UiRect(18, 614, 924, 40));
        canvas.Text(controller.Message, 16, new UiRect(32, 623, 896, 30), theme.Foreground);
    }

    private void EnsureVisible()
    {
        scroll = Math.Clamp(scroll, Math.Max(0, controller.SelectedIndex - VisibleRows + 1), controller.SelectedIndex);
        scroll = Math.Min(scroll, Math.Max(0, controller.Rows.Count - VisibleRows));
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
        if (new UiRect(25, 110, 612, VisibleRows * RowHeight).Contains(x, y))
        {
            int index = scroll + (int)((y - 110) / RowHeight);
            if (index < controller.Rows.Count)
            {
                controller.Select(index, true);
            }
        }
    }
}
