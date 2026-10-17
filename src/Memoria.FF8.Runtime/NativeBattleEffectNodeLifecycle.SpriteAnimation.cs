using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal static void AdvanceSpriteAnimation(NativeBattleEffectNodeMemory memory, int node, sbyte resourceKind)
    {
        if (resourceKind is < 0 or > 2)
        {
            return;
        }
        sbyte phase = unchecked((sbyte)(memory.Byte(node + 0x1D0) + 1));
        memory.Byte(node + 0x1D0, unchecked((byte)phase));
        if (resourceKind == 2 && phase > unchecked((sbyte)memory.Byte(node + 0x1D4)))
        {
            sbyte repeats = unchecked((sbyte)memory.Byte(node + 0x1D5));
            if (repeats > 0)
            {
                memory.Byte(node + 0x1D5, unchecked((byte)(repeats - 1)));
                phase = unchecked((sbyte)memory.Byte(node + 0x1D3));
                memory.Byte(node + 0x1D0, unchecked((byte)phase));
            }
        }
        if (phase > unchecked((sbyte)memory.Byte(node + 0x1D1)))
        {
            memory.Byte(node + 0x1D0, 0);
            if (resourceKind != 1)
            {
                memory.Byte(node + 0x1D2, 1);
            }
        }
    }
}
