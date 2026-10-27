using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99State([property: JsonRequired] NativeBattleChoco97Context? Context, [property: JsonRequired] NativeBattleChoco97NodeState Nodes, [property: JsonRequired] NativeBattleChoco99IoState? Io, [property: JsonRequired] bool IoScriptKnownCold, [property: JsonRequired] NativeBattleChoco97IoBanks? PendingIoBanks, [property: JsonRequired, JsonConverter(typeof(NativeBattleOwnedGfAnimationSource.Converter))] NativeBattleGfAnimationSource? ProgramSource, [property: JsonRequired] bool ProgramSourceKnownCold, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] NativeBattleMoogleCameraState? Camera, [property: JsonRequired] ImmutableArray<byte> MorphScratch, [property: JsonRequired] ImmutableArray<byte>? ProjectedScratch, [property: JsonRequired] uint? FreezePrimary, [property: JsonRequired] uint? FreezeSecondary, [property: JsonRequired] uint? NextAllocation, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateBufferAliasOffset)
{
    public static NativeBattleChoco99State FromColdProcess() => new(null, NativeBattleChoco97NodeState.FromColdProcess(), null, true, new(0x19D3018, 0x1F560, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F560)), null, true, ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty, null, new byte[0x760].ToImmutableArray(), null, null, null, null, null, null);

    public void Validate()
    {
        Context?.Validate();
        Nodes.Validate();
        Camera?.Validate();
        Io?.Validate();
        PendingIoBanks?.Validate();
        if (Io is not null && PendingIoBanks is not null || Io is null && (!IoScriptKnownCold || PendingIoBanks is null) || Io is not null && (IoScriptKnownCold || Context is null))
        {
            throw new NotSupportedException("Native Choco99 requires exactly one actual IO bank and its current retained script/context.");
        }
        NativeBattleChoco97IoBanks banks = Io?.Banks ?? PendingIoBanks!;
        if (banks.BaseAddress != 0x19D3018 || banks.Length != 0x1F560)
        {
            throw new InvalidDataException("Native Choco99 external banks contradict their actual physical span.");
        }
        if (ProgramSource is { } source && (source.Address != 0x166DC58 || source.IsDefault || source.Length != 0x2884) || ProgramSource is null && !ProgramSourceKnownCold)
        {
            throw new NotSupportedException("Native Choco99 requires its complete retained static program source.");
        }
        if (Models is null || Models.Count > 2 || Models.Any(pair => pair.Key is not (0x256D750 or 0x256D890) || pair.Value is null || pair.Value.TaskAddress != pair.Key || pair.Value.ResourceAddress != 0x1665B08))
        {
            throw new InvalidDataException("Native Choco99 model decoder owners must match their two physical task slots.");
        }
        if (MorphScratch.IsDefault || MorphScratch.Length != 0x760 || ProjectedScratch is { } projected && (projected.IsDefault || projected.Length != 0xCD0) || ViewCopy is { } view && (view.IsDefault || view.Length != 32))
        {
            throw new InvalidDataException("Native Choco99 shared scratch contradicts its physical extent.");
        }
        if (FreezePrimary.HasValue != FreezeSecondary.HasValue || PrivateBufferAliasOffset is int alias && (uint)alias >= NativeBattleEffectArena.Length || NextAllocation is uint next && (next < NativeBattleEffectNodeMemory.ArenaAddress || (ulong)next > (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length))
        {
            throw new InvalidDataException("Native Choco99 freeze or arena pointers contradict their actual owners.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco99State? Choco99 { get; init; }
}

public sealed record NativeBattleChoco99Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleChoco99FileRequest(NativeBattleChoco99IoRequest Request) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco99ViewTranslationY(short Y) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco99ModelDrawStep(NativeBattleChoco99Frame Frame, NativeBattleEffectOwnedModelDraw Drawing)
{
    public NativeBattleModulePoolTask Task => Drawing.Task;
}
public sealed record NativeBattleChoco99ProgramStep(NativeBattleChoco99Frame Frame, NativeBattleModulePoolTask Task, bool ReadyToComplete);
public sealed record NativeBattleChoco99NodesStep(NativeBattleChoco99Frame Frame, bool Complete);
public sealed record NativeBattleChoco99TaskStep(NativeBattleChoco99Frame Frame, NativeBattleModulePoolTask Task, bool ReadyToComplete);
public sealed record NativeBattleChoco99OverlayStep(NativeBattleChoco99Frame Frame, int Result);
public sealed record NativeBattleChoco99VibrationStep(NativeBattleChoco99Frame Frame, short Handle);
public sealed record NativeBattleChoco99Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleChoco99Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleChoco99ModelDrawStep> DrawModel, Func<NativeBattleChoco99Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco99ProgramStep> AdvanceProgram, Func<NativeBattleChoco99Frame, uint, NativeBattleChoco99NodesStep> AdvanceNodes, Func<NativeBattleChoco99Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco99TaskStep> AdvanceAux, Func<NativeBattleChoco99Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco99TaskStep> AdvanceSecondary, Func<NativeBattleChoco99Frame, uint, NativeBattleChoco99OverlayStep> AdvanceOverlay, Func<NativeBattleChoco99Frame, NativeBattleChoco99VibrationStep> StartVibration, Func<NativeBattleChoco99Frame, NativeBattleChoco97Operation, NativeBattleChoco99Frame> Execute);
