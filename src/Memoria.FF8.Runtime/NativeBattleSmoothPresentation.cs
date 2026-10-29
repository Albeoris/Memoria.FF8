namespace Memoria.FF8.Runtime;

public static class NativeBattleSmoothPresentation
{
    public static bool Supports(ushort? effect)
    {
        return effect is 2 or 3 or 6 or 90 or 95 or 103 or 104 or 116 or 142 or 143 or 144 or 185 or 191 or 199 or 201 or 202 or 203 or 204 or 205 or 206 or 278 or 291 or 325;
    }
}
