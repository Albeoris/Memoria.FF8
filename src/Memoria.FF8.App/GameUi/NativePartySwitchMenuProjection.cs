using System.Globalization;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

public static class NativePartySwitchMenuProjection
{
    public static PlayerAction PlayerAction(MenuAction action)
    {
        int Row(string id) => id.StartsWith("character-", StringComparison.Ordinal) && int.TryParse(id.AsSpan(10), NumberStyles.None, CultureInfo.InvariantCulture, out int row) ? row : throw new ArgumentException("Switch requires a physical character identifier.", nameof(action));
        PlayerAction result = new(PlayerActionKind.SwitchPartyMembers, Row(action.SubjectId), Quantity: Row(action.TargetId));
        NativePartySwitchInput.Validate(result);
        return result;
    }
}
