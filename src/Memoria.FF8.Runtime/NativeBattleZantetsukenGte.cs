using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleZantetsukenGte
{
    public static NativeBattleEffectMemory LoadPlane(NativeBattleEffectMemory memory, ReadOnlySpan<byte> plane8)
    {
        if (plane8.Length != 8)
        {
            throw new InvalidDataException("Zantetsuken requires its complete packed clipping plane.");
        }
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedLightingMatrix? current = lighting.LightMatrix;
        current?.Validate();
        byte[] bytes = current?.Bytes.ToArray() ?? new byte[20];
        bool[] known = current?.Known.ToArray() ?? new bool[20];
        plane8.CopyTo(bytes);
        known.AsSpan(0, 8).Fill(true);
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown()).WriteLane(0, BinaryPrimitives.ReadInt16LittleEndian(plane8[6..]));
        return memory with { ScriptedLighting = lighting with { LightMatrix = new(bytes.ToImmutableArray(), known.ToImmutableArray()), BackColor = back } };
    }

    public static NativeBattleEffectMemory EvaluatePlane(NativeBattleEffectMemory memory, int slot)
    {
        return NativeBattleLightBackTransform.Transform(memory, slot);
    }
}
