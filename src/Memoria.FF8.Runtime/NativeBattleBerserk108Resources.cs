using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBerserk108Resources(NativeBattleGfAnimationSource Burst, ImmutableArray<byte> Glow, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<uint> PartFlags, ImmutableArray<uint> RayColors, ImmutableArray<byte> TextureWindows, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleBerserk108State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleBerserk108State? Berserk108 { get; init; }
}
