using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFullCure280Resources(NativeBattleGfAnimationSource Layout, NativeBattleModuleResource Spark, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleFullCure280State([property: JsonRequired] NativeBattleActionMatrix Billboard, [property: JsonRequired] ushort EntryRandom)
{
    public static NativeBattleFullCure280State FromColdProcess() => new(new([0, 0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 4096), 0);

    public void Validate()
    {
        if (Billboard is null)
        {
            throw new InvalidDataException("Native Full-cure requires its retained billboard matrix.");
        }
        Billboard.Validate();
    }
}

public sealed record NativeBattleFullCure280Execution([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFullCure280State? FullCure280
    {
        get => effectState.FullCure280;
        init => effectState = effectState with { FullCure280 = value };
    }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFullCure280Execution? FullCure280 { get; init; }
}

public static class NativeBattleFullCure280Geometry
{
    public static int Flash(short counter) => counter < 8 ? counter << 8 : counter <= 57 ? 2048 : (65 - counter) << 8;

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard, NativeBattleVector anchor)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        int x;
        int y;
        int z;
        if ((node.Header & 0x1000) != 0)
        {
            (int baseX, int baseY, int baseZ) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (int localX, int localY, int localZ) = NativeBattleGfMatrices.Rotate56C3D0(billboard, offset);
            x = unchecked(baseX + localX);
            y = unchecked(baseY + localY);
            z = unchecked(baseZ + localZ);
            matrix = NativeBattleGfMatrices.Multiply56C090(billboard, matrix);
        }
        else if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector translated = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, translated);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }
}
