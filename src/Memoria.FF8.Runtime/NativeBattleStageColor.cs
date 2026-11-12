using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A uniform arena color transition; target RGB uses native low-byte-red channel order.</summary>
public readonly record struct NativeBattleStageColor(short Depth, uint TargetRgb)
{
    public static NativeBattleStageColor ReadUniform(ImmutableArray<NativeBattleStageGroup> groups)
    {
        if (groups.IsDefault || groups.Length != 4)
        {
            throw new NotSupportedException("The host requires the original four arena color groups.");
        }
        NativeBattleStageGroup first = groups[0];
        if (groups.Any(group => group.ColorDepth != first.ColorDepth || group.ColorFlags != first.ColorFlags))
        {
            throw new NotSupportedException("The host cannot apply different color transitions to individual arena groups.");
        }
        if ((first.ColorFlags & 0xFF000000u) != 0)
        {
            throw new NotSupportedException("The arena color request includes native rendering flags that the host cannot represent.");
        }
        return new(first.ColorDepth, first.ColorFlags);
    }
}
