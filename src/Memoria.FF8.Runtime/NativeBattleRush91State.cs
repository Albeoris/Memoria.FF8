using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRush91State([property: JsonRequired] ImmutableArray<byte>? DogControl, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] ImmutableArray<byte>? PublishedModelMatrix, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateAuxiliaryOffset, [property: JsonRequired] uint Global259EE98, [property: JsonRequired] uint Global259BE2C, [property: JsonRequired] NativeBattleRecover93NodeState Nodes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? ProjectedScratch { get; init; }
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRush91State? Rush91 { get; init; }
}
public sealed record NativeBattleRush91Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleRush91Operation;
public sealed record NativeBattleRush91UploadModelTim : NativeBattleRush91Operation;
public sealed record NativeBattleRush91Sound(uint Descriptor, int First, int Second) : NativeBattleRush91Operation;
public sealed record NativeBattleRush91CommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleRush91Operation;
public sealed record NativeBattleRush91Sprite(NativeBattleModulePoolTask Task) : NativeBattleRush91Operation;
public sealed record NativeBattleRush91Mesh(NativeBattleModulePoolTask Task) : NativeBattleRush91Operation;
public sealed record NativeBattleRush91Contact(int Contact) : NativeBattleRush91Operation;
public sealed record NativeBattleRush91ModelDrawStep(NativeBattleRush91Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleRush91ProgramStep(NativeBattleRush91Frame Frame, NativeBattleModulePoolTask Task, int Remaining);
public sealed record NativeBattleRush91NodesStep(NativeBattleRush91Frame Frame, bool Complete);
public sealed record NativeBattleRush91OverlayStep(NativeBattleRush91Frame Frame, int Result);
public sealed record NativeBattleRush91Inputs(uint OverlayPause2795914, NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleRush91Frame, uint, byte, NativeBattleVector> ReadOwnedBone, Func<NativeBattleRush91Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleRush91ModelDrawStep> DrawModel, Func<NativeBattleRush91Frame, NativeBattleModulePoolTask, NativeBattleRush91ProgramStep> AdvanceProgram, Func<NativeBattleRush91Frame, uint, NativeBattleRush91NodesStep> AdvanceNodes, Func<NativeBattleRush91Frame, uint, uint, NativeBattleRush91OverlayStep> AdvanceOverlay, Func<NativeBattleRush91Frame, NativeBattleRush91Operation, NativeBattleRush91Frame> Execute);
