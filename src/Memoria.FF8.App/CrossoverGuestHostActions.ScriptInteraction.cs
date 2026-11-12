using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions : IFf7BattleGuestScriptActions
{
    public Ff7GuestScriptInteraction DescribeScriptInteraction(byte command, ushort ability, int targetSlot)
    {
        return ClassifyScriptInteraction(command, ability, targetSlot);
    }

    internal static Ff7GuestScriptInteraction ClassifyScriptInteraction(byte command, ushort ability, int targetSlot)
    {
        if ((ability & 0xFF00) == DrawStockKey)
        {
            ValidateScriptDrawSource(targetSlot);
            return new(byte.MaxValue, ushort.MaxValue, targetSlot);
        }
        if (IsDrawCast(ability))
        {
            int source = DrawSource(ability);
            ValidateScriptDrawSource(source);
            return new(2, ushort.MaxValue, source, HasRecipientEffect: true);
        }
        byte originCommand = ability > byte.MaxValue ? (byte)(ability >> 8) : command;
        ushort originAction = ability > byte.MaxValue ? (byte)ability : ability;
        if (originCommand == 2 && originAction == 50)
        {
            return new(6, ushort.MaxValue, InspectionOnly: true);
        }
        return originCommand switch
        {
            1 => new(1, 0),
            2 => new(2, ushort.MaxValue),
            3 => new(3, ushort.MaxValue),
            4 => new(4, originAction == 9 ? (ushort)5 : ushort.MaxValue),
            _ => new(byte.MaxValue, ushort.MaxValue)
        };
    }

    private static void ValidateScriptDrawSource(int source)
    {
        if (source is < 4 or > 9)
        {
            throw new InvalidDataException("An imported Draw interaction requires an enemy actor source.");
        }
    }
}
