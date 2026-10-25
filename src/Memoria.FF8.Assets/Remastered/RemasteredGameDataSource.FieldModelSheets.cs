using System.Globalization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private ImageBinding? FieldModelImageBinding(TextureBuildContext context)
    {
        if (context.Layout != "classic-field-model" || context.NativeSheet is not { } sheet || sheet.Palette != 0 || context.Key.Palette != 0 || sheet.CanvasWidth != 128 || sheet.CanvasHeight != 128 || sheet.X != 0 || sheet.Y != 0 || sheet.Width != context.LogicalWidth || sheet.Height != context.LogicalHeight)
        {
            return null;
        }
        string[] owner = sheet.Owner.Path.Split('/');
        string[] member = sheet.Subresource.Split('/');
        if (owner.Length != 4 || owner[0] != "field" || owner[1] != "model" || owner[2] is not ("main_chr" or "second_chr") || owner[3].Length != 4 || !char.IsAsciiLetter(owner[3][0]) || owner[3].Skip(1).Any(character => !char.IsAsciiDigit(character)) || member.Length is not (2 or 3) || member[0] != "row" || !int.TryParse(member[1], NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row < 0 || member[1] != row.ToString(CultureInfo.InvariantCulture) || member.Length == 3 && member[2] != "alpha")
        {
            return null;
        }
        string name = owner[3];
        if (name[0] == 'c')
        {
            name = "n" + name[1..];
        }
        string suffix = member.Length == 3 ? "_alpha" : "";
        string stem = $"{name}_{row}{suffix}.png";
        string preferred = "textures/field.fs/field_hd_new/" + stem;
        string fallback = "textures/field.fs/field_hd/" + stem;
        string? path = Contains(preferred) ? preferred : Contains(fallback) ? fallback : null;
        return path is null ? null : new ImageBinding(path, true);
    }
}
