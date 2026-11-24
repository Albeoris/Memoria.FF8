using System.Globalization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed record TargetScanData(string Title, string Name, string Description, int Level, long? Hp, long? MaximumHp, int Strength, int Vitality, int Speed, int Magic, int Spirit, int Evasion, IReadOnlyList<TargetScanResistance> Elements, IReadOnlyList<TargetScanResistance> Statuses);
internal readonly record struct TargetScanResistance(string Name, int? Icon, int Percent);

internal static class TargetScanScreen
{
    internal static readonly NativeStatusId[] Statuses =
    [
        NativeStatusId.Ko, NativeStatusId.Poison, NativeStatusId.Petrify, NativeStatusId.Blind, NativeStatusId.Silence,
        NativeStatusId.Berserk, NativeStatusId.Zombie, NativeStatusId.Sleep, NativeStatusId.Slow, NativeStatusId.Stop,
        NativeStatusId.Curse, NativeStatusId.Confuse, NativeStatusId.Drain
    ];
    internal const int HolyIcon = 295;
    internal const int ModelWidth = 437;
    internal const int ModelHeight = 385;

    public static TargetScanResistance Status(int index, int percent, Func<string, string, string>? localize)
    {
        NativeStatusId status = Statuses[index];
        return new(SystemTextCatalog.Translate(localize, BattleStatusLocalization.SemanticId(status), SystemTextCatalog.Defaults[BattleStatusLocalization.SemanticId(status)]), 272 + index, percent);
    }

    public static TargetScanResistance Eject(int percent, Func<string, string, string>? localize)
    {
        return new(SystemTextCatalog.Translate(localize, "battle.status.eject", "Eject"), HolyIcon, percent);
    }

    public static TargetScanResistance Element(int index, int percent, Func<string, string, string>? localize, string? nativeName = null)
    {
        string id = "junction.dashboard.elemental-" + index;
        string fallback = nativeName ?? SystemTextCatalog.Defaults[id];
        return new(SystemTextCatalog.Translate(localize, id, fallback), 288 + index, percent);
    }

    public static void Draw(IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale, TargetScanData data, Func<string, string, string>? localize, Action<UiRect>? drawModel = null)
    {
        if (layout.Bounds.IsEmpty)
        {
            return;
        }
        float scale = layout.Scale;
        UiColor gold = new(255, 220, 96);
        UiColor rule = new(125, 155, 179, 150);
        string Text(string id, string fallback) => SystemTextCatalog.Translate(localize, id, fallback);
        string Format(string id, string fallback, params object?[] args) => SystemTextCatalog.Format(Text(id, fallback), fallback, args);

        theme.Panel(canvas, layout.Rect(8, 8, 944, 58));
        Label(data.Title, 22, 20, 14, 139, 44);
        Label(data.Name, 34, 173, 14, 340, 44);
        Label(Format("battle.scan.level", "Lv {0}", data.Level), 23, 524, 14, 136, 44);
        Label(Format("battle.scan.hp", "{0} {1} / {2}", Text("stat.hp", "HP"), data.Hp?.ToString(CultureInfo.CurrentCulture) ?? "???", data.MaximumHp?.ToString(CultureInfo.CurrentCulture) ?? "???"), 23, 673, 10, 266, 33);
        if (data.Hp is long hp && data.MaximumHp is > 0)
        {
            UiRect gauge = layout.Rect(673, 47, 260, 7);
            canvas.Fill(gauge, new(31, 62, 82));
            canvas.Fill(gauge with { Width = gauge.Width * Math.Clamp(hp / (float)data.MaximumHp.Value, 0, 1) }, new(61, 212, 233));
        }

        UiRect model = layout.Rect(8, 74, ModelWidth, ModelHeight);
        drawModel?.Invoke(model);
        Horizontal(8, 445, 74);
        canvas.Line(model.X, model.Y, model.X, model.Bottom, rule, Math.Max(1, scale));
        canvas.Line(model.Right, model.Y, model.Right, model.Bottom, rule, Math.Max(1, scale));
        theme.Panel(canvas, layout.Rect(8, 459, 437, 205));
        UiRect description = layout.Rect(22, 470, 409, 92);
        float descriptionSize = Math.Min(24, 19 * textScale) * scale;
        float descriptionHeight = canvas.MeasureText(data.Description, descriptionSize, description.Width).Height;
        if (descriptionHeight > description.Height)
        {
            descriptionSize *= description.Height / descriptionHeight;
        }
        canvas.Text(data.Description, descriptionSize, description, theme.Foreground, UiAlignment.Start, UiTextFlow.Wrap);
        Label(Text("junction.dashboard.stats", "Stats"), 22, 22, 561, 409, 27);
        Horizontal(22, 431, 589);
        Stat("strength", "Str", data.Strength, 0, 0);
        Stat("vitality", "Vit", data.Vitality, 1, 0);
        Stat("speed", "Spd", data.Speed, 2, 0);
        Stat("magic", "Mag", data.Magic, 0, 1);
        Stat("spirit", "Spr", data.Spirit, 1, 1);
        Stat("evasion", "Eva", data.Evasion, 2, 1);

        ResistancePanel(Text("junction.dashboard.elements", "Elements"), data.Elements, 74, 226, highlightWeakness: true);
        ResistancePanel(Text("battle.scan.status-resistance", "Status resistance"), data.Statuses, 309, 355, highlightWeakness: false);

        void Stat(string stat, string fallback, int value, int column, int row)
        {
            float x = 22 + column * 137;
            float y = 591 + row * 32;
            Label(Text("stat." + stat, fallback), 19, x, y, 72, 31);
            Label(stat == "evasion" ? Percent(value) : value.ToString(CultureInfo.CurrentCulture), 19, x + 72, y, 54, 31, alignment: UiAlignment.End);
        }

        void ResistancePanel(string title, IReadOnlyList<TargetScanResistance> entries, float y, float height, bool highlightWeakness)
        {
            theme.Panel(canvas, layout.Rect(454, y, 498, height));
            Label(title, 24, 469, y + 8, 468, 35);
            Horizontal(469, 937, y + 45);
            int rows = (entries.Count + 1) / 2;
            if (rows == 0)
            {
                Label("???", 22, 469, y + 49, 468, 35);
                return;
            }
            float rowHeight = Math.Min(42, (height - 61) / rows);
            for (int index = 0; index < entries.Count; index++)
            {
                TargetScanResistance entry = entries[index];
                float x = 469 + index / rows * 237;
                float top = y + 50 + index % rows * rowHeight;
                bool weak = highlightWeakness && entry.Percent < 0;
                if (weak)
                {
                    canvas.Fill(layout.Rect(x, top, 230, rowHeight), new(104, 78, 20, 175));
                }
                if (entry.Icon is int icon)
                {
                    canvas.Image(new(UiImageKind.Icon, icon), layout.Rect(x + 3, top + 5, 24, rowHeight - 10));
                }
                Label(entry.Name, 20, x + 34, top, 129, rowHeight, weak ? gold : null);
                Label(Percent(entry.Percent), 20, x + 163, top, 63, rowHeight, weak ? gold : null, UiAlignment.End);
                Horizontal(x, x + 230, top + rowHeight);
            }
        }

        void Horizontal(float left, float right, float y)
        {
            UiRect line = layout.Rect(left, y, right - left, 1);
            canvas.Line(line.X, line.Y, line.Right, line.Y, rule, Math.Max(1, scale));
        }

        void Label(string text, float size, float x, float y, float width, float height, UiColor? color = null, UiAlignment alignment = UiAlignment.Start)
        {
            float pixels = Math.Min(size * textScale, height * 0.82f) * scale;
            UiSize measured = canvas.MeasureText(text, pixels, float.PositiveInfinity);
            if (measured.Width > width * scale)
            {
                pixels *= width * scale / measured.Width;
                measured = canvas.MeasureText(text, pixels, float.PositiveInfinity);
            }
            float offset = alignment == UiAlignment.End ? Math.Max(0, width - measured.Width / scale) : 0;
            canvas.Text(text, pixels, layout.Rect(x + offset, y, width - offset, height), color ?? theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
        }
    }

    private static string Percent(int value) => (value / 100m).ToString("P0", CultureInfo.CurrentCulture);
}
