using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public static class NativeWorldParticleTile
{
    public static (int Offset, int Size) Dimensions(ushort scale, short halfSize)
    {
        int half = unchecked(scale * halfSize) >> 13;
        short size = unchecked((short)(2 * half));
        return (-half, size == 0 ? 8 : size);
    }
}
