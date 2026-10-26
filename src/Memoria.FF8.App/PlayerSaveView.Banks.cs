using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class PlayerSaveView
{
    internal static UiGrid BuildBanks(int selectedBank, bool quick = false, float textScale = 1, PlayerSaveMenu? menu = null)
    {
        UiGrid banks = new() { Columns = quick ? 1 : 3, Rows = 1, Gap = 6 };
        for (int bank = 0; bank < (quick ? 1 : PlayerSaveStore.BankCount); bank++)
        {
            UiButton tab = new() { Id = "save-bank-" + bank, GridColumn = bank, DrawBackground = false, HighlightSelection = false, PointerSize = 0 };
            tab.Children.Add(new SaveBankPlaque(bank, bank == selectedBank, quick, textScale, menu));
            banks.Children.Add(tab);
        }
        return banks;
    }

    private sealed class SaveBankPlaque(int bank, bool active, bool quick, float textScale, PlayerSaveMenu? menu) : UiElement
    {
        private string SystemText(string id, string original) => menu?.SystemText(id, original) ?? original;

        private string FormatSystemText(string id, string original, params object?[] values) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, values);

        public override UiElement? HitTest(float x, float y)
        {
            return null;
        }

        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            theme.Panel(canvas, Bounds);
            canvas.Fill(Bounds.Inset(4), active ? new UiColor(70, 75, 85) : new UiColor(36, 38, 43));
            UiColor foreground = active ? theme.Foreground : theme.Muted;
            float iconWidth = 63;
            UiRect icon = new(Bounds.X + 12, Bounds.Y + 6, iconWidth, Bounds.Height - 12);
            if (bank == 0)
            {
                canvas.Image(new UiImageSource(UiImageKind.SilverCrest, 0), icon);
            }
            else
            {
                string numeral = bank == 1 ? "I" : "II";
                canvas.Text(numeral, 42 * textScale, new UiRect(icon.X + (bank == 1 ? 20 : 10), icon.Y, icon.Width, icon.Height), foreground, "Times New Roman");
            }
            float textLeft = Bounds.X + iconWidth + 22;
            float rowHeight = Math.Max(0, Bounds.Height - 12) / 2;
            UiRect label = new(textLeft, Bounds.Y + 6, Math.Max(0, Bounds.Right - textLeft - 10), rowHeight);
            string name = bank == 0 ? SystemText("save.bank.memoria", "Memoria") : SystemText("save.bank.original", "Original");
            string details = (active ? SystemText("save.bank.active", "ACTIVE  ·  ") : "") + (quick ? SystemText("save.bank.quick", "Quick saves") : FormatSystemText("save.bank.slots", "{0} slots", PlayerSaveStore.GetBankSlotCount(bank)));
            canvas.Text(name, 24 * textScale, label, foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            UiRect capacity = new(textLeft, label.Bottom, label.Width, rowHeight);
            canvas.Text(details, 15 * textScale, capacity, foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            if (active)
            {
                canvas.Fill(new UiRect(Bounds.X + 4, Bounds.Bottom - 6, Bounds.Width - 8, 3), new UiColor(222, 228, 239));
            }
        }
    }
}
