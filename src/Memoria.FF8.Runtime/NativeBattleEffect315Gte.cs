using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect315Gte
{
    public static NativeBattleEffectMemory LoadRotation45DE50(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        return memory with { ScriptedLighting = lighting with { LightMatrix = new(packed.Bytes.Take(20).ToImmutableArray(), packed.Known.Take(20).ToImmutableArray()) } };
    }

    public static NativeBattleEffectMemory LoadAlternateTranslation64DDA0(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedBackColor back = lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown();
        for (int lane = 0; lane < 3; lane++)
        {
            int offset = 20 + lane * 4;
            if (packed.Known.AsSpan().Slice(offset, 4).ToArray().All(value => value))
            {
                back = back.WriteLane(lane, BinaryPrimitives.ReadInt32LittleEndian(packed.Bytes.AsSpan().Slice(offset, 4)));
            }
            else
            {
                back = back with { Known = (back.Known ?? [true, true, true]).SetItem(lane, false) };
            }
        }
        return memory with { ScriptedLighting = lighting with { BackColor = back } };
    }

    public static NativeBattleEffectMemory LoadTranslation64DD70(NativeBattleEffectMemory memory, NativeBattleEffectArena point12)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 requires its current ordinary translation owner.");
        return memory with { GeometryRegisters = geometry with { Translation = Enumerable.Range(0, 3).Select(lane => (int?)BinaryPrimitives.ReadInt32LittleEndian(point12.Read(lane * 4, 4))).ToImmutableArray() } };
    }

    public static NativeBattleEffectMemory SetDiagonal64DE00(NativeBattleEffectMemory memory, ushort scale)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 requires its current rotation owner.");
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, scale);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), scale);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), scale);
        return memory with { GeometryRegisters = geometry with { Rotation20 = bytes.Select(value => (byte?)value).ToImmutableArray() } };
    }

    public static NativeBattleEffectMemory MacToTranslation64DE40(NativeBattleEffectMemory memory)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 requires its current ordinary translation owner.");
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 requires its current MAC registers.");
        if (colors.Mac.Any(value => value is null))
        {
            throw new NotSupportedException("Effect315 cannot publish unobserved MAC translation DWORDs.");
        }
        return memory with { GeometryRegisters = geometry with { Translation = colors.Mac } };
    }

    public static NativeBattleEffectMemory LoadVertex64DEE0(NativeBattleEffectMemory memory, NativeBattleEffectArena point6)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 requires its current arithmetic registers."));
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(point6.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point6.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point6.Read(4, 2)));
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, 0);
        return memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }

    public static NativeBattleEffectArena StoreIr64DF10(NativeBattleEffectMemory memory)
    {
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 requires its current IR registers.");
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, [0, 0]);
        for (int lane = 0; lane < 3; lane++)
        {
            int value = colors.Ir[lane] ?? throw new NotSupportedException("Effect315 requires its current transformed IR coordinate.");
            point = point.Write(lane * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        return point;
    }

    public static NativeBattleEffectMemory TransformAlternate4607E0(NativeBattleEffectMemory memory) => NativeBattleLightBackTransform.Transform(memory, 0);
}
