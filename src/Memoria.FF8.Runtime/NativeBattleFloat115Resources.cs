using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFloat115Resources(NativeBattleGfAnimationSource Glyph, NativeBattleGfAnimationSource Upper, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleVector DepthPoint);
public sealed record NativeBattleFloat115State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleFloat115State? Float115 { get; init; }
}
