using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public static class NativeBattleModuleSpriteSetup
{
    public static NativeBattleEffectMemory Setup571BC0(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 camera, NativeBattleModuleAnchor anchor, short scale, short depth)
    {
        return Setup571BC0(memory, camera, new NativeBattleVector(anchor.X, anchor.Y, anchor.Z), anchor.W, scale, depth);
    }

    public static NativeBattleEffectMemory Setup571BC0(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 camera, NativeBattleVector position, short? sourceW, short scale, short depth)
    {
        NativeBattlePackedMatrix32 billboard = memory.ModuleSpriteBillboard21DFED0 ?? throw new NotSupportedException("The sprite billboard requires its actual retained packed matrix.");
        billboard.Validate();
        camera.Validate();
        byte[] bytes = billboard.Bytes.ToArray();
        bool[] known = billboard.Known.ToArray();
        WriteWord(0, scale);
        WriteWord(8, scale);
        WriteWord(16, scale);
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("The sprite billboard requires its retained geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("The sprite billboard requires its retained geometry data registers."));
        step = step with { Geometry = LoadPackedMatrix(step.Geometry, camera) };
        step = NativeBattleGeometry.LoadVector(step, 0, position, sourceW);
        step = NativeBattleGeometry.Transform(step);
        int x = step.Colors.Mac[0] ?? throw new InvalidDataException("The sprite billboard requires its transformed X MAC.");
        int y = step.Colors.Mac[1] ?? throw new InvalidDataException("The sprite billboard requires its transformed Y MAC.");
        int z = step.Colors.Mac[2] ?? throw new InvalidDataException("The sprite billboard requires its transformed Z MAC.");
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        WriteDword(20, unchecked(x + (unchecked(direction.X * depth) >> 12)));
        WriteDword(24, unchecked(y + (unchecked(direction.Y * depth) >> 12)));
        WriteDword(28, unchecked(z + (unchecked(direction.Z * depth) >> 12)));
        billboard = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        step = step with { Geometry = LoadPackedMatrix(step.Geometry, billboard) };
        return memory with { ModuleSpriteBillboard21DFED0 = billboard, GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };

        void WriteWord(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
            known[offset] = true;
            known[offset + 1] = true;
        }

        void WriteDword(int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
            for (int index = offset; index < offset + 4; index++)
            {
                known[index] = true;
            }
        }
    }

    public static NativeBattlePackedMatrix32 CaptureCurrentMatrix(NativeBattleGeometryRegisters geometry)
    {
        geometry.Validate();
        byte[] bytes = new byte[32];
        bool[] known = new bool[32];
        for (int index = 0; index < 20; index++)
        {
            if (geometry.Rotation20[index] is { } value)
            {
                bytes[index] = value;
                known[index] = true;
            }
        }
        for (int component = 0; component < 3; component++)
        {
            if (geometry.Translation[component] is { } value)
            {
                int offset = 20 + component * 4;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
                for (int index = offset; index < offset + 4; index++)
                {
                    known[index] = true;
                }
            }
        }
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleGeometryRegisters LoadPackedMatrix(NativeBattleGeometryRegisters geometry, NativeBattlePackedMatrix32 packed)
    {
        NativeBattleActionMatrix matrix = packed.ReadMatrix();
        geometry = NativeBattleGeometry.LoadMatrix(geometry, matrix, true, true, null);
        byte?[] rotation = new byte?[20];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = packed.Known[index] ? packed.Bytes[index] : null;
        }
        return geometry with { Rotation20 = rotation.ToImmutableArray() };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePackedMatrix32? ModuleSpriteBillboard21DFED0 { get; init; }
}
