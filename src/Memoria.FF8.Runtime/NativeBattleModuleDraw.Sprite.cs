using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleDraw
{
    /// <summary>571C80 receiptWORD36; ordinary570BC0 sprites publish3.</summary>
    public ushort SpriteFlags { get; init; } = 3;
    public uint SpriteColor { get; init; } = 0x808080;
    public ImmutableArray<byte> SpritePaletteOffsets { get; init; } = [];
    /// <summary>Optional native world-space3x3 matrix, before the original camera composition.</summary>
    public ImmutableArray<short> SpriteWorldRotation { get; init; } = [];
}
