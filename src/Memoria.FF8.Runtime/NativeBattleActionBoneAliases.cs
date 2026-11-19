using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleActionBoneAliases
{
    public static NativeBattleActionMatrix ReadMatrix(ImmutableArray<NativeBattleActionMatrix> bones, short selector, ImmutableArray<byte> aliases = default, ImmutableArray<NativeBattleActionMatrix> frames = default)
    {
        short physical = Resolve(aliases, selector);
        if ((uint)physical >= (uint)bones.Length)
        {
            throw new InvalidDataException("The action matrix selector exceeds its current physical actor skeleton.");
        }
        NativeBattleActionMatrix matrix = bones[physical];
        if (aliases.IsDefaultOrEmpty || frames.IsDefaultOrEmpty || selector >= 240)
        {
            return matrix;
        }
        if (frames.Length != aliases.Length)
        {
            throw new InvalidDataException("Source action bone frames do not match their source selector directory.");
        }
        NativeBattleActionMatrix converted = NativeBattleGfMatrices.Compose56C2F0(matrix, frames[selector]);
        return converted with { X = matrix.X, Y = matrix.Y, Z = matrix.Z, Scale = matrix.Scale };
    }

    public static short Resolve(ImmutableArray<byte> aliases, short selector)
    {
        if (aliases.IsDefaultOrEmpty || selector >= 240)
        {
            return selector;
        }
        if (selector < 0 || selector >= aliases.Length || aliases[selector] == byte.MaxValue)
        {
            throw new NotSupportedException($"The transferred action reaches unmapped source bone {selector}.");
        }
        return aliases[selector];
    }
}
