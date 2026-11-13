namespace Memoria.FF8.Runtime;

public static class NativeBattleReflectionRules
{
    public static bool CanReflect(byte command, byte attackFlags, byte attackType)
    {
        return command != 247 && (attackFlags & 0x10) != 0 && attackType is 2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33;
    }
}
