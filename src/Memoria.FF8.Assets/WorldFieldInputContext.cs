namespace Memoria.FF8.Assets;

/// <summary>Native546100/FF20 reads559240's current/prior low WORD input and its per-callback activity publication.</summary>
public readonly record struct WorldFieldInputContext(uint CurrentButtons, uint? PreviousButtons, bool? Activity, int? Axis0, int? Axis1, int? Axis2, int? Axis3, bool PreviewOverride = false)
{
    public bool? Matches(ushort operand)
    {
        if (PreviewOverride)
        {
            return true;
        }
        if (Activity == true)
        {
            return false;
        }
        uint mask = operand;
        uint current = CurrentButtons & mask;
        bool? edge = current == 0 ? false : PreviousButtons is uint previous ? (current & (CurrentButtons ^ previous)) != 0 : null;
        bool? input = edge;
        if (operand == ushort.MaxValue)
        {
            bool? analog = false;
            foreach (int? axis in new[] { Axis0, Axis1, Axis2, Axis3 })
            {
                if (axis is not int raw)
                {
                    analog = analog == true ? true : null;
                    continue;
                }
                int centered = raw == -1 ? 0 : unchecked((sbyte)(raw + 0x80));
                if (Math.Abs(centered) > 45)
                {
                    analog = true;
                }
            }
            input = edge == true || analog == true ? true : edge == false && analog == false ? false : null;
        }
        if (input == false)
        {
            return false;
        }
        return input == true && Activity == false ? true : null;
    }
}
