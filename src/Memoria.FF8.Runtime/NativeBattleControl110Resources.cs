using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleControl110Resources(ImmutableArray<byte> Stream, ImmutableArray<byte> Ring, ImmutableArray<byte> Debris, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<byte> StaticFrame, uint Sound);
public sealed record NativeBattleControl110State([property: JsonRequired] ImmutableArray<byte>? Vertices)
{
    public static NativeBattleControl110State FromColdProcess() => new(Vertices: null);
}
public sealed record NativeBattleControl110EntryState([property: JsonRequired] int BufferOffset);
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleControl110State? Control110 { get; init; }
}
public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleControl110EntryState? Control110 { get; init; }
}
