using System.Globalization;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

public static class NativeMagicMenuProjection
{
    public static bool IsAction(MenuAction action) => action.Kind is "magic-use" or "magic-transfer";

    public static PlayerAction PlayerAction(MenuAction action)
    {
        if (!IsAction(action))
        {
            throw new ArgumentException("Unsupported magic menu input.", nameof(action));
        }
        string[] spell = action.SubjectId.Split('-');
        if (spell.Length != 3 || spell[0] != "magic" || !action.TargetId.StartsWith("character-", StringComparison.Ordinal))
        {
            throw new ArgumentException("Magic input lost its explicit saved stock or target identity.", nameof(action));
        }
        int quantity = action.Kind == "magic-use" ? 1 : action.Value == "All" ? 100 : int.Parse(action.Value, NumberStyles.None, CultureInfo.InvariantCulture);
        PlayerAction result = new(action.Kind == "magic-use" ? PlayerActionKind.UseFieldMagic : PlayerActionKind.TransferMagic, quantity, $"{spell[1]}:{spell[2]}:{action.TargetId[10..]}");
        NativeMagicMenuInput.Validate(result);
        return result;
    }
}
