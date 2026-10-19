using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    internal static UiGrid BuildPlayerSaveBanks(int selectedBank)
    {
        UiGrid banks = new() { Columns = 3, Rows = 1, Gap = 6 };
        for (int bank = 0; bank < PlayerSaveStore.BankCount; bank++)
        {
            UiButton tab = new() { Id = "save-bank-" + bank, GridColumn = bank, DrawBackground = false, HighlightSelection = false, PointerSize = 0 };
            tab.Children.Add(new SaveBankPlaque(bank, bank == selectedBank));
            banks.Children.Add(tab);
        }
        return banks;
    }

    private sealed class SaveBankPlaque(int bank, bool active) : UiElement
    {
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
                canvas.Text(numeral, 42, new UiRect(icon.X + (bank == 1 ? 20 : 10), icon.Y, icon.Width, icon.Height), foreground, "Times New Roman");
            }
            float textLeft = Bounds.X + iconWidth + 22;
            UiRect label = new(textLeft, Bounds.Y + 7, Math.Max(0, Bounds.Right - textLeft - 10), 31);
            canvas.Text(bank == 0 ? "Memoria" : "Original", 24, label, foreground);
            UiRect capacity = new(textLeft, Bounds.Y + 37, label.Width, 23);
            canvas.Text((active ? "ACTIVE  ·  " : "") + PlayerSaveStore.GetBankSlotCount(bank) + " slots", 15, capacity, foreground);
            if (active)
            {
                canvas.Fill(new UiRect(Bounds.X + 4, Bounds.Bottom - 6, Bounds.Width - 8, 3), new UiColor(222, 228, 239));
            }
        }
    }
}
