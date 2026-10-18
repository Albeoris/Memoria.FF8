using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSearchState([property: JsonRequired] ImmutableArray<byte>? DogControl, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] ImmutableArray<byte>? PublishedModelMatrix, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateAuxiliaryOffset, [property: JsonRequired] uint Global2598F18, [property: JsonRequired] uint Global2596610)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? ProjectedScratch { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSearchState? Search { get; init; }
}

public sealed record NativeBattleSearchFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleSearchOperation;
public sealed record NativeBattleSearchBindTargetResource(int Actor) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchSelectActorSequence(int Actor, byte Sequence) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchUploadModelTim : NativeBattleSearchOperation;
public sealed record NativeBattleSearchSound(uint Descriptor, int First, int Second) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchVoice(uint Descriptor, int First, int Second, int Third) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchSprite(NativeBattleModulePoolTask Task) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchMesh(NativeBattleModulePoolTask Task) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchCommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchContact(int Contact) : NativeBattleSearchOperation;
public sealed record NativeBattleSearchModelDrawStep(NativeBattleSearchFrame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleSearchProgramStep(NativeBattleSearchFrame Frame, NativeBattleModulePoolTask Task, int Remaining);
public sealed record NativeBattleSearchInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<int, ImmutableArray<byte>> ReadTargetWorld8, Func<NativeBattleSearchFrame, uint, byte, NativeBattleVector> ReadOwnedBone, Func<NativeBattleSearchFrame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleSearchModelDrawStep> DrawModel, Func<NativeBattleSearchFrame, NativeBattleModulePoolTask, NativeBattleSearchProgramStep> AdvanceProgram, Func<NativeBattleSearchFrame, NativeBattleSearchOperation, NativeBattleSearchFrame> Execute);
