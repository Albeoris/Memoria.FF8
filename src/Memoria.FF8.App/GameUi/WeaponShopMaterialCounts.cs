using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class WeaponShopMaterialCounts(IReadOnlyList<MenuMaterial> materials)
{
    public UiSize Measure(IUiCanvas canvas, int index, float fontSize)
    {
        var columns = MeasureColumns(canvas, fontSize);
        return new UiSize(columns.Owned + columns.Separator + columns.Required, columns.Height);
    }

    public void Draw(IUiCanvas canvas, int index, float fontSize, UiRect bounds, UiColor foreground)
    {
        MenuMaterial material = materials[index];
        var columns = MeasureColumns(canvas, fontSize);
        float width = columns.Owned + columns.Separator + columns.Required;
        float scale = width > 0 ? Math.Min(1, bounds.Width / width) : 1;
        float ownedWidth = columns.Owned * scale;
        float separatorWidth = columns.Separator * scale;
        float requiredWidth = columns.Required * scale;
        float x = bounds.Right - ownedWidth - separatorWidth - requiredWidth;
        UiColor ownedColor = material.Owned < material.Required ? new UiColor(240, 96, 96) : new UiColor(112, 232, 144);
        float size = fontSize * scale;
        string owned = material.Owned.ToString(CultureInfo.InvariantCulture);
        float ownedTextWidth = Math.Min(ownedWidth, canvas.MeasureText(owned, size, float.PositiveInfinity).Width);
        float separatorTextWidth = Math.Min(separatorWidth, canvas.MeasureText("/", size, float.PositiveInfinity).Width);
        canvas.Text(owned, size, new UiRect(x + ownedWidth - ownedTextWidth, bounds.Y, ownedTextWidth, bounds.Height), ownedColor, UiAlignment.Center, UiTextFlow.SingleLine);
        canvas.Text("/", size, new UiRect(x + ownedWidth + (separatorWidth - separatorTextWidth) / 2, bounds.Y, separatorTextWidth, bounds.Height), foreground, UiAlignment.Center, UiTextFlow.SingleLine);
        canvas.Text(material.Required.ToString(CultureInfo.InvariantCulture), size, new UiRect(x + ownedWidth + separatorWidth, bounds.Y, requiredWidth, bounds.Height), foreground, UiAlignment.Center, UiTextFlow.SingleLine);
    }

    private (float Owned, float Separator, float Required, float Height) MeasureColumns(IUiCanvas canvas, float fontSize)
    {
        float owned = 0;
        float required = 0;
        UiSize separator = canvas.MeasureText(" / ", fontSize, float.PositiveInfinity);
        float height = separator.Height;
        foreach (MenuMaterial material in materials)
        {
            UiSize ownedSize = canvas.MeasureText(material.Owned.ToString(CultureInfo.InvariantCulture), fontSize, float.PositiveInfinity);
            UiSize requiredSize = canvas.MeasureText(material.Required.ToString(CultureInfo.InvariantCulture), fontSize, float.PositiveInfinity);
            owned = Math.Max(owned, ownedSize.Width);
            required = Math.Max(required, requiredSize.Width);
            height = Math.Max(height, Math.Max(ownedSize.Height, requiredSize.Height));
        }
        return (owned, separator.Width, required, height);
    }
}
