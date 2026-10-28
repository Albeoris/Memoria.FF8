namespace Memoria.FF8.Runtime;

internal static class NativeBattleActionTargetMask
{
    internal static (int Result, bool Unavailable) Resolve(int selected, int modifiers, int available, bool disableAutomaticReplacement)
    {
        int result;
        if ((modifiers & 4) != 0)
        {
            result = selected == 255 ? available : available & (selected <= 7 ? 7 : 248);
        }
        else
        {
            result = available & selected;
            if (result == 0 && disableAutomaticReplacement)
            {
                result = selected;
            }
            else if (result == 0 && selected != 0)
            {
                int side = available & (selected <= 7 ? 7 : 248);
                int shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted <<= 1;
                    result = side & shifted;
                }
                shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted >>= 1;
                    result = side & shifted;
                }
            }
        }
        bool unavailable = result == 0 && ((modifiers & 4) != 0 || !disableAutomaticReplacement);
        if (unavailable && (modifiers & 4) == 0)
        {
            result = selected;
        }
        return (result, unavailable);
    }
}
