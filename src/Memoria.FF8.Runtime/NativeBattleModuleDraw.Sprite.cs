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
    /// <summary>5710D0's camera-space translation DWORDs, added after571BC0 normalizes and applies depth displacement.</summary>
    public int SpriteCameraXOffset { get; init; }
    public int SpriteCameraYOffset { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? SpriteYScale { get; init; }
}
