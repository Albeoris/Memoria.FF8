using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Localization.Authoring;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public sealed class FieldMessageTargetSyntax : ILocalizationTargetSyntax
{
    public IReadOnlyList<string> ValidateCandidate(LocalizationRecord row)
    {
        try
        {
            LocalizationTextValidation.Validate(row);
            if (row.Category == "Fields" && !string.IsNullOrEmpty(row.Target))
            {
                _ = LocalizationFieldPreparation.Prepare(row);
            }
            return [];
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or NotSupportedException)
        {
            return [exception.Message];
        }
    }

    public LocalizationTargetParts Parse(string? text)
    {
        text ??= "";
        if (!FieldMessageContext.TryParseTag(0, text, out FieldMessageContext? context, out int length))
        {
            return new(text, null, null, null);
        }
        string prefix = text[..length];
        int w = prefix.IndexOf('W');
        int f = prefix.IndexOf('F');
        int positionEnd = w >= 0 ? w : f >= 0 ? f : length - 1;
        int sizeEnd = f >= 0 ? f : length - 1;
        return new(text[length..], context!.Position is null ? null : prefix[1..positionEnd], context.Size is null ? null : prefix[w..sizeEnd], context.Choices is null ? null : prefix[f..^1], prefix)
        {
            BodySpan = new(length, text.Length - length),
            PositionSpan = context.Position is null ? null : new(1, positionEnd - 1),
            SizeSpan = context.Size is null ? null : new(w, sizeEnd - w),
            ChoicesSpan = context.Choices is null ? null : new(f, length - f - 1)
        };
    }
    public string Compose(LocalizationTargetParts parts)
    {
        string tag = (parts.Position ?? "") + (parts.Size ?? "") + (parts.Choices ?? "");
        string result = tag.Length == 0 ? parts.Body : "{" + tag + "}" + parts.Body;
        if (tag.Length != 0 && !FieldMessageContext.TryParseTag(0, result, out _, out _))
        {
            throw new InvalidDataException("Invalid field presentation prefix.");
        }
        return result;
    }
    public LocalizationTargetValue SetOrigin(LocalizationTargetValue target, string source, int x, int y)
    {
        return SetOrigin(target, source, "", x, y);
    }

    public LocalizationTargetValue SetOrigin(LocalizationTargetValue target, string source, string nativeBase, int x, int y)
    {
        LocalizationTargetParts parts = Parse(target.Text);
        if (string.IsNullOrEmpty(target.Text))
        {
            parts = Parse(nativeBase) with { Body = Parse(source).Body };
            if (parts.Body.Length == 0)
            {
                throw new InvalidDataException("A position edit requires a nonempty source or authored body.");
            }
        }
        string tag = new FieldMessageContext(0, new FieldMessagePosition(x, y)).FormatTag();
        return new(true, Compose(parts with { Position = tag[1..^1] }));
    }

    public LocalizationTargetValue ResetOrigin(LocalizationTargetValue target)
    {
        if (string.IsNullOrEmpty(target.Text))
        {
            return target;
        }
        LocalizationTargetParts parts = Parse(target.Text);
        return target with { Text = Compose(parts with { Position = null }) };
    }
}
