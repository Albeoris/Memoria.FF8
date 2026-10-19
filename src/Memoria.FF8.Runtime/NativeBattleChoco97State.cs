using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97State([property: JsonRequired] NativeBattleChoco97Context? Context, [property: JsonRequired] NativeBattleChoco97NodeState Nodes, [property: JsonRequired] NativeBattleChoco97IoState? Io, [property: JsonRequired] bool IoScriptKnownCold, [property: JsonRequired] NativeBattleChoco97IoBanks? PendingIoBanks, [property: JsonRequired] NativeBattleGfAnimationSource? ProgramSource, [property: JsonRequired] bool ProgramSourceKnownCold, [property: JsonRequired] NativeBattleEffectOwnedModelState? Model, [property: JsonRequired] NativeBattleMoogleCameraState? Camera, [property: JsonRequired] ImmutableArray<byte> MorphScratch, [property: JsonRequired] ImmutableArray<byte>? ProjectedScratch, [property: JsonRequired] uint? FreezePrimary, [property: JsonRequired] uint? FreezeSecondary, [property: JsonRequired] uint? NextAllocation, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateBufferAliasOffset)
{
    public static NativeBattleChoco97State FromColdProcess() => new(null, NativeBattleChoco97NodeState.FromColdProcess(), null, true, new(0x19D1018, 0x23600, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x23600)), null, true, null, null, new byte[0x3C0].ToImmutableArray(), null, null, null, null, null, null);

    public void Validate()
    {
        Context?.Validate();
        Nodes.Validate();
        Camera?.Validate();
        Io?.Validate();
        PendingIoBanks?.Validate();
        if (Io is not null && PendingIoBanks is not null || Io is null && (!IoScriptKnownCold || PendingIoBanks is null))
        {
            throw new NotSupportedException("Native Choco97 requires exactly one current external IO bank owner.");
        }
        if (Io is not null && (IoScriptKnownCold || Context is null))
        {
            throw new NotSupportedException("Native Choco97 initialized IO requires its retained script and target context.");
        }
        NativeBattleChoco97IoBanks banks = Io?.Banks ?? PendingIoBanks!;
        if (banks.BaseAddress > 0x19DB018 || (ulong)banks.BaseAddress + (uint)banks.Length < 0x19EEA80)
        {
            throw new InvalidDataException("Native Choco97 IO banks contradict their actual physical address span.");
        }
        if (ProgramSource is { } source && (source.Address != 0x168F8BC || source.Data.IsDefault || source.Data.Length != 0x2884) || ProgramSource is null && !ProgramSourceKnownCold)
        {
            throw new NotSupportedException("Native Choco97 requires its complete retained static program source.");
        }
        if (MorphScratch.IsDefault || MorphScratch.Length != 0x3C0 || ProjectedScratch is { } projected && (projected.IsDefault || projected.Length != 0xCD0) || ViewCopy is { } view && (view.IsDefault || view.Length != 32))
        {
            throw new InvalidDataException("Native Choco97 scratch or copied view contradicts its physical extent.");
        }
        if (Model is { ResourceAddress: not 0x168354C } || FreezePrimary.HasValue != FreezeSecondary.HasValue || PrivateBufferAliasOffset is int alias && (uint)alias >= NativeBattleEffectArena.Length || NextAllocation is uint next && (next < NativeBattleEffectNodeMemory.ArenaAddress || (ulong)next > (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length))
        {
            throw new InvalidDataException("Native Choco97 retained model, freeze or arena pointers contradict their owners.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco97State? Choco97 { get; init; }
}

public sealed record NativeBattleChoco97Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97Sound(uint Descriptor, uint Flags, byte Pan) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97Contact(int Contact) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97CommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97CameraPublish(NativeBattleMoogleCameraStep Step) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97StageDepth(short? Depth, bool ClearRgb) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97TextureCopy(NativeBattleTextureCopy Copy) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97FileRequest(NativeBattleChoco97IoRequest Request) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97VibrationStop(short Handle) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco97ModelDrawStep(NativeBattleChoco97Frame Frame, NativeBattleEffectOwnedModelDraw Drawing)
{
    public NativeBattleModulePoolTask Task => Drawing.Task;
}
public sealed record NativeBattleChoco97ProgramStep(NativeBattleChoco97Frame Frame, NativeBattleModulePoolTask Task, int? Remaining);
public sealed record NativeBattleChoco97NodesStep(NativeBattleChoco97Frame Frame, bool Complete);
public sealed record NativeBattleChoco97SpriteDrawStep(NativeBattleChoco97Frame Frame, NativeBattleModulePoolTask Task, bool Complete);
public sealed record NativeBattleChoco97VibrationStep(NativeBattleChoco97Frame Frame, short Handle);
public sealed record NativeBattleChoco97Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleChoco97Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleChoco97ModelDrawStep> DrawModel, Func<NativeBattleChoco97Frame, NativeBattleModulePoolTask, NativeBattleChoco97ProgramStep> AdvanceProgram, Func<NativeBattleChoco97Frame, uint, NativeBattleChoco97NodesStep> AdvanceNodes, Func<NativeBattleChoco97Frame, NativeBattleModulePoolTask, NativeBattleChoco97SpriteDrawStep> AdvanceSprite, Func<NativeBattleChoco97Frame, NativeBattleChoco97VibrationStep> StartVibration, Func<NativeBattleChoco97Frame, NativeBattleChoco97Operation, NativeBattleChoco97Frame> Execute);
