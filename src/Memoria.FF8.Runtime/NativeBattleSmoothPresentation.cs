namespace Memoria.FF8.Runtime;

public static class NativeBattleSmoothPresentation
{
    public static bool Supports(ushort? effect)
    {
        return effect is 2 or 3 or 6 or 95 or 116 or 144 or 185 or 201 or 203 or 205 or 278 or 291 or 325;
    }
}
