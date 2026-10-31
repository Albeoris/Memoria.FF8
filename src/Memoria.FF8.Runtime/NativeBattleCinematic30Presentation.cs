namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematic30Presentation
{
    internal static bool Owns(ushort effect) => effect is 248 or 250 or 253 or 254 or 255 or 261 or 262 or 263 or 264;

    internal static bool OwnsRibbon(ushort effect) => effect is 250 or 253 or 254 or 255;
}
