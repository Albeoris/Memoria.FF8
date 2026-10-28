using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRecover93State([property: JsonRequired] ImmutableArray<byte>? DogControl, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] ImmutableArray<byte>? PublishedModelMatrix, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateAuxiliaryOffset, [property: JsonRequired] uint Global2594D88, [property: JsonRequired] uint Global259350C, [property: JsonRequired] NativeBattleRecover93NodeState Nodes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? ProjectedScratch { get; init; }
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecover93State? Recover93
    {
        get => effectState.Recover93;
        init => effectState = effectState with { Recover93 = value };
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecover93State? Reverse94
    {
        get => effectState.Reverse94;
        init => effectState = effectState with { Reverse94 = value };
    }
}
public sealed record NativeBattleRecover93Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93UploadModelTim : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93Sound(uint Descriptor, int First, int Second) : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93Voice(uint Descriptor, int First, int Second, int Third) : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93CommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93Contact(int Contact) : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93StageClearRgb : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93StageDepth(short Value) : NativeBattleRecover93Operation;
public sealed record NativeBattleRecover93ModelDrawStep(NativeBattleRecover93Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleRecover93ProgramStep(NativeBattleRecover93Frame Frame, NativeBattleModulePoolTask Task, int Remaining);
public sealed record NativeBattleRecover93NodesStep(NativeBattleRecover93Frame Frame, bool Complete);
public sealed record NativeBattleRecover93Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<int, ImmutableArray<byte>> ReadTargetWorld8, Func<NativeBattleRecover93Frame, uint, byte, NativeBattleVector> ReadOwnedBone, Func<NativeBattleRecover93Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleRecover93ModelDrawStep> DrawModel, Func<NativeBattleRecover93Frame, NativeBattleModulePoolTask, NativeBattleRecover93ProgramStep> AdvanceProgram, Func<NativeBattleRecover93Frame, uint, NativeBattleRecover93NodesStep> AdvanceNodes, Func<NativeBattleRecover93Frame, NativeBattleRecover93Operation, NativeBattleRecover93Frame> Execute);
