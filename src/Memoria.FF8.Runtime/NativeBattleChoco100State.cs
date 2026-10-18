using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100State([property: JsonRequired] NativeBattleChoco97Context? Context, [property: JsonRequired] NativeBattleChoco97NodeState Nodes, [property: JsonRequired] NativeBattleChoco100IoState? Io, [property: JsonRequired] bool IoScriptKnownCold, [property: JsonRequired] NativeBattleChoco97IoBanks? PendingIoBanks, [property: JsonRequired] NativeBattleGfAnimationSource? ProgramSource, [property: JsonRequired] bool ProgramSourceKnownCold, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectOwnedModelState> Models, [property: JsonRequired] NativeBattleMoogleCameraState? Camera, [property: JsonRequired] ImmutableArray<byte> MorphScratch, [property: JsonRequired] ImmutableArray<byte> ProjectedScratch, [property: JsonRequired] uint? FreezePrimary, [property: JsonRequired] uint? FreezeSecondary, [property: JsonRequired] uint? NextAllocation, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateBufferAliasOffset)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco100SecondarySettings? SecondarySettings { get; init; }

    public static NativeBattleChoco100State FromColdProcess() => new(null, NativeBattleChoco97NodeState.FromColdProcess(), null, true, new(0x19D5018, 0x22300, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x22300)), null, true, ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty, null, new byte[0x120].ToImmutableArray(), new byte[0xFE0].ToImmutableArray(), null, null, null, null, null) { SecondarySettings = NativeBattleChoco100SecondarySettings.FromColdProcess() };

    public void Validate()
    {
        SecondarySettings?.Validate();
        Context?.Validate();
        Nodes.Validate();
        Camera?.Validate();
        Io?.Validate();
        PendingIoBanks?.Validate();
        if (Io is not null && PendingIoBanks is not null || Io is null && (!IoScriptKnownCold || PendingIoBanks is null) || Io is not null && (IoScriptKnownCold || Context is null))
        {
            throw new NotSupportedException("Native Choco100 requires exactly one actual IO bank and its current retained script/context.");
        }
        NativeBattleChoco97IoBanks banks = Io?.Banks ?? PendingIoBanks!;
        if (banks.BaseAddress != 0x19D5018 || banks.Length != 0x22300)
        {
            throw new InvalidDataException("Native Choco100 external banks contradict their actual physical span.");
        }
        if (ProgramSource is { } source && (source.Address != 0x1663154 || source.Data.IsDefault || source.Data.Length != 0x2884) || ProgramSource is null && !ProgramSourceKnownCold)
        {
            throw new NotSupportedException("Native Choco100 requires its complete retained static program source.");
        }
        if (Models is null || Models.Count > 2 || Models.Any(pair => pair.Key is not (0x2565438 or 0x2565578) || pair.Value is null || pair.Value.TaskAddress != pair.Key || pair.Value.ResourceAddress is not (0x1656DE4 or 0x19E8678)))
        {
            throw new InvalidDataException("Native Choco100 model decoder owners must match their two physical task slots.");
        }
        if (MorphScratch.IsDefault || MorphScratch.Length != 0x120 || ProjectedScratch.IsDefault || ProjectedScratch.Length != 0xFE0 || ViewCopy is { } view && (view.IsDefault || view.Length != 32))
        {
            throw new InvalidDataException("Native Choco100 shared scratch contradicts its physical extent.");
        }
        if (FreezePrimary.HasValue != FreezeSecondary.HasValue || PrivateBufferAliasOffset is int alias && (uint)alias >= NativeBattleEffectArena.Length || NextAllocation is uint next && (next < NativeBattleEffectNodeMemory.ArenaAddress || (ulong)next > (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length))
        {
            throw new InvalidDataException("Native Choco100 freeze or arena pointers contradict their actual owners.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco100State? Choco100 { get; init; }
}

public sealed record NativeBattleChoco100Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleChoco100FileRequest(NativeBattleChoco100IoRequest Request) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco100ModelDrawStep(NativeBattleChoco100Frame Frame, NativeBattleEffectOwnedModelDraw Drawing)
{
    public NativeBattleModulePoolTask Task => Drawing.Task;
}
public sealed record NativeBattleChoco100ProgramStep(NativeBattleChoco100Frame Frame, NativeBattleModulePoolTask Task, bool ReadyToComplete);
public sealed record NativeBattleChoco100NodesStep(NativeBattleChoco100Frame Frame, bool Complete);
public sealed record NativeBattleChoco100TaskStep(NativeBattleChoco100Frame Frame, NativeBattleModulePoolTask Task, bool ReadyToComplete);
public sealed record NativeBattleChoco100VibrationStep(NativeBattleChoco100Frame Frame, short Handle);
public sealed record NativeBattleChoco100Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleChoco100Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleChoco100ModelDrawStep> DrawModel, Func<NativeBattleChoco100Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco100ProgramStep> AdvanceProgram, Func<NativeBattleChoco100Frame, uint, NativeBattleChoco100NodesStep> AdvanceNodes, Func<NativeBattleChoco100Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco100TaskStep> AdvanceAux, Func<NativeBattleChoco100Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco100TaskStep> AdvanceSecondary, Func<NativeBattleChoco100Frame, uint, NativeBattleModulePoolTask, NativeBattleChoco100TaskStep> AdvanceSmall, Func<NativeBattleChoco100Frame, NativeBattleChoco100VibrationStep> StartVibration, Func<NativeBattleChoco100Frame, NativeBattleChoco97Operation, NativeBattleChoco100Frame> Execute);
