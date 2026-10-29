namespace Memoria.FF8.Runtime;

public static class NativeBattleSmoothPresentation
{
    public static bool Supports(ushort? effect)
    {
        return effect is 1 or 2 or 3 or 4 or 6 or 21 or 22 or 24 or 25 or 28 or 32 or 33 or 38 or 39 or 90 or 95 or 102 or 103 or 104 or 105 or 106 or 107 or 109 or 111 or 116 or 118 or 119 or 121 or 122 or 123 or 124 or 125 or 142 or 143 or 144 or 146 or 147 or 148 or 149 or 175 or 185 or 191 or 199 or 201 or 202 or 203 or 204 or 205 or 206 or 222 or 278 or 291 or 325;
    }
}
