using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogle96State([property: JsonRequired] ImmutableArray<byte>? DogControl, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] NativeBattleMoogle96NodeState Nodes, [property: JsonRequired] NativeBattleMoogleCameraState? Camera, [property: JsonRequired] NativeBattleGfAnimationSource? ProgramSource, [property: JsonRequired] bool ProgramSourceKnownCold, [property: JsonRequired] NativeBattleMoogle96IoState? Io, [property: JsonRequired] bool IoScriptKnownCold, [property: JsonRequired] uint? Global257F724, [property: JsonRequired] int? Global257CF24, [property: JsonRequired] uint? NextAllocation, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateBufferAliasOffset, [property: JsonRequired] ImmutableArray<byte>? ProjectedScratch)
{
    public static NativeBattleMoogle96State FromColdProcess() => new(null, [], NativeBattleMoogle96NodeState.FromColdProcess(), null, null, true, null, true, null, null, null, null, null, null);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMoogle96State? Moogle96 { get; init; }
}
public sealed record NativeBattleMoogle96Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96Sound(uint Descriptor, int First, int Second) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96Contact(int Contact) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96CommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96CameraPublish(NativeBattleMoogleCameraStep Step) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96StageFade(short? Depth, bool ClearRgb, bool Clear0200, bool TransformFlagsByte1) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96EnemyFlags(int Actor, ushort Flags0) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96EnemyFade(int Actor, ushort Flags0, uint Color40, byte Alpha7) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96MusicGain(short Value) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96StopVibration(short Handle) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96DirectorModeE1 : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96TextureCopy(NativeBattleTextureCopy Copy) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96FileRequest(NativeBattleMoogle96IoRequest Request) : NativeBattleMoogle96Operation;
public sealed record NativeBattleMoogle96ModelDrawStep(NativeBattleMoogle96Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleMoogle96ProgramStep(NativeBattleMoogle96Frame Frame, NativeBattleModulePoolTask Task, int Remaining);
public sealed record NativeBattleMoogle96NodesStep(NativeBattleMoogle96Frame Frame, bool Complete);
public sealed record NativeBattleMoogle96VibrationStep(NativeBattleMoogle96Frame Frame, short Handle);
public sealed record NativeBattleMoogle96Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, uint TransitionColor, Func<NativeBattleMoogle96Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleMoogle96ModelDrawStep> DrawModel, Func<NativeBattleMoogle96Frame, NativeBattleModulePoolTask, NativeBattleMoogle96ProgramStep> AdvanceProgram, Func<NativeBattleMoogle96Frame, uint, NativeBattleMoogle96NodesStep> AdvanceNodes, Func<NativeBattleMoogle96Frame, NativeBattleMoogle96VibrationStep> StartVibration, Func<NativeBattleMoogle96Frame, NativeBattleMoogle96Operation, NativeBattleMoogle96Frame> Execute);
