using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38Zones
{
    public static byte Read8D9A80(ImmutableArray<byte> map, short x, short z, short halfX, short halfZ, short offsetZ)
    {
        if (map.IsDefault || map.Length != 1024 || halfX < 16 || halfZ < 16)
        {
            throw new InvalidDataException("Native Quake zone lookup requires its original1024-byte map and nonzero signed cell divisors.");
        }
        short nx = unchecked((short)-x);
        short nz = unchecked((short)(z - offsetZ));
        nx = (short)Math.Clamp((int)nx, -halfX, halfX - 1);
        nz = (short)Math.Clamp((int)nz, -halfZ, halfZ - 1);
        short column = unchecked((short)(unchecked((short)(halfX + nx)) / (halfX / 16)));
        short row = unchecked((short)(unchecked((short)(halfZ + nz)) / (halfZ / 16)));
        int index = checked(row * 32 + column);
        if ((uint)index >= (uint)map.Length)
        {
            throw new InvalidDataException("Native Quake zone lookup leaves its original table.");
        }
        return map[index];
    }
}
