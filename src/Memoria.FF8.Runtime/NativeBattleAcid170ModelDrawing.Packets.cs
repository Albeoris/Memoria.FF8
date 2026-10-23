using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170ModelDrawing
{
    private static NativeBattleEffectMemory LoadPlaneControls(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary)
    {
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedLightingMatrix retained = lighting.LightMatrix ?? new(new byte[20].ToImmutableArray(), Enumerable.Repeat(false, 20).ToImmutableArray());
        byte[] bytes = retained.Bytes.ToArray();
        bool[] known = retained.Known.ToArray();
        temporary.Read(0x74, 8).CopyTo(bytes, 0);
        Array.Fill(known, true, 0, 8);
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown()).WriteLane(0, S16(temporary, 0x7A));
        return memory with { ScriptedLighting = lighting with { LightMatrix = new(bytes.ToImmutableArray(), known.ToImmutableArray()), BackColor = back } };
    }

    private static (NativeBattleEffectArena Temporary, NativeBattleEffectArena Scratch) DeformVertex(NativeBattleEffectArena temporary, NativeBattleEffectArena scratch, int offset, int plane)
    {
        int distance = unchecked(S16(temporary, 0x7A) + (plane >> 12));
        temporary = Dword(temporary, 0x38, unchecked((uint)distance));
        if (distance <= 0)
        {
            return (temporary, scratch);
        }
        short x = S16(scratch, offset);
        short y = S16(scratch, offset + 2);
        short z = S16(scratch, offset + 4);
        int dx = unchecked((short)(x - S16(temporary, 0x7C)));
        int dy = unchecked((short)(y - S16(temporary, 0x7E)));
        int dz = unchecked((short)(z - S16(temporary, 0x80)));
        temporary = temporary.Write(0x54, BitConverter.GetBytes((short)dx)).Write(0x56, BitConverter.GetBytes((short)dy)).Write(0x58, BitConverter.GetBytes((short)dz));
        int squared = unchecked(dx * dx + dy * dy + dz * dz);
        int radius = unchecked((int)U32(temporary, 0x40));
        if (squared >= radius)
        {
            return (temporary, scratch);
        }
        squared = Math.Max(squared, 640000);
        int ratio = unchecked(squared << 12) / radius;
        int scaled = unchecked(ratio * unchecked((int)U32(temporary, 0x3C))) >> 9;
        int vertical = unchecked(unchecked((int)U32(temporary, 0x30)) - plane) / unchecked((int)U32(temporary, 0x34));
        int amount = unchecked(scaled * vertical) >> 12;
        if (amount < 0)
        {
            amount = unchecked(-amount);
        }
        temporary = Dword(temporary, 0x38, unchecked((uint)amount));
        short movedY = unchecked((short)(y + (short)unchecked((amount >> 1) + amount)));
        scratch = scratch.Write(offset + 2, BitConverter.GetBytes(movedY));
        if (movedY <= 0)
        {
            return (temporary, scratch);
        }
        scratch = scratch.Write(offset + 2, new byte[2]);
        if (U16(temporary, 0x8A) == 0)
        {
            return (temporary, scratch);
        }
        short horizontalX = unchecked((short)(x - S16(temporary, 0x84)));
        short horizontalZ = unchecked((short)(z - S16(temporary, 0x88)));
        var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(horizontalX, 0, horizontalZ);
        short nx = unchecked((short)normal.X);
        short nz = unchecked((short)normal.Z);
        temporary = temporary.Write(0x54, BitConverter.GetBytes(nx)).Write(0x56, new byte[2]).Write(0x58, BitConverter.GetBytes(nz));
        scratch = scratch.Write(offset, BitConverter.GetBytes(unchecked((short)(x + (unchecked(nx * amount) >> 12))))).Write(offset + 4, BitConverter.GetBytes(unchecked((short)(z + (unchecked(nz * amount) >> 12)))));
        return (temporary, scratch);
    }
}
