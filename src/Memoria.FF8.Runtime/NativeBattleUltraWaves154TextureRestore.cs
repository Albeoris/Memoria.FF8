using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltraWaves154TextureRestore
{
    public static NativeBattleEffectMemory Copy(NativeBattleEffectMemory memory, ImmutableArray<byte> data)
    {
        NativeBattleUltraWaves154State state = memory.UltraWaves154 ?? throw new NotSupportedException("Native Ultra Waves texture restore requires its private executable destination.");
        NativeBattleStickyWeb153State web = memory.StickyWeb153 ?? throw new NotSupportedException("Native Ultra Waves A3 destination overlaps the retained neighboring process bytes; explicitly migrate the standalone owner.");
        if (data.IsDefault || data.Length < 0x18004 || data.Length > 0x18004 + web.Globals.Length)
        {
            throw new InvalidDataException("Native A3 texture exceeds its reached mapped Ultra Waves and neighboring executable span.");
        }
        byte[] neighboring = web.Globals.ToArray();
        data.AsSpan().Slice(0x18004).CopyTo(neighboring);
        return memory with { UltraWaves154 = state with { Packets = state.Packets.Write(0, data.AsSpan()) }, StickyWeb153 = web with { Globals = neighboring.ToImmutableArray() } };
    }
}
