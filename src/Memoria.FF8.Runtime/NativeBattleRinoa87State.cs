using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleRinoa87State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa87IoState? Io { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa87SceneMemory? Scene { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectOwnedModelState? BodyModel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleActionMatrix>? BodyBones { get; init; }
    public ushort? TargetRotationPadding18 { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> BodyProjected { get; init; }
    public int? EmitterOffset { get; init; }
    public int? GeometryOffset { get; init; }
    public int Counter25B4B4C { get; init; }
    public int Counter25AECD4 { get; init; }
    public ushort Word25B4B08 { get; init; }
    public ushort Word25B4630 { get; init; }
    public ushort Word25AECD0 { get; init; }
    public ushort Word25B4640 { get; init; }
    public ushort Word25B4444 { get; init; }
    public ushort Word25ACEB8 { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa87State? Rinoa87 { get; init; }
}
