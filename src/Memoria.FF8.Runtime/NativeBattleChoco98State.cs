using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98State([property: JsonRequired] NativeBattleChoco97Context? Context, [property: JsonRequired] NativeBattleChoco97NodeState Nodes, [property: JsonRequired] NativeBattleChoco98IoState? Io, [property: JsonRequired] bool IoScriptKnownCold, [property: JsonRequired] NativeBattleChoco97IoBanks? PendingIoBanks, [property: JsonRequired] NativeBattleGfAnimationSource? ProgramSource, [property: JsonRequired] bool ProgramSourceKnownCold, [property: JsonRequired] NativeBattleEffectOwnedModelState? Model, [property: JsonRequired] NativeBattleMoogleCameraState? Camera, [property: JsonRequired] ImmutableArray<byte> MorphScratch, [property: JsonRequired] ImmutableArray<byte>? ProjectedScratch, [property: JsonRequired] uint? FreezePrimary, [property: JsonRequired] uint? FreezeSecondary, [property: JsonRequired] uint? NextAllocation, [property: JsonRequired] ImmutableArray<byte>? ViewCopy, [property: JsonRequired] int? PrivateBufferAliasOffset)
{
    public static NativeBattleChoco98State FromColdProcess() => new(null, NativeBattleChoco97NodeState.FromColdProcess(), null, true, new(0x19DB018, 0x13184, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x13184)), null, true, null, null, new byte[0x168].ToImmutableArray(), null, null, null, null, null, null);

    public void Validate()
    {
        Context?.Validate();
        Nodes.Validate();
        Camera?.Validate();
        Io?.Validate();
        PendingIoBanks?.Validate();
        if (Io is not null && PendingIoBanks is not null || Io is null && (!IoScriptKnownCold || PendingIoBanks is null))
        {
            throw new NotSupportedException("Native Choco98 requires exactly one current external IO bank owner.");
        }
        if (Io is not null && (IoScriptKnownCold || Context is null))
        {
            throw new NotSupportedException("Native Choco98 initialized IO requires its retained script and target context.");
        }
        NativeBattleChoco97IoBanks banks = Io?.Banks ?? PendingIoBanks!;
        if (banks.BaseAddress != 0x19DB018 || banks.Length != 0x13184)
        {
            throw new InvalidDataException("Native Choco98 IO banks contradict their actual physical address span.");
        }
        if (ProgramSource is { } source && (source.Address != 0x1680B9C || source.Data.IsDefault || source.Data.Length != 0x2884) || ProgramSource is null && !ProgramSourceKnownCold)
        {
            throw new NotSupportedException("Native Choco98 requires its complete retained static program source.");
        }
        if (MorphScratch.IsDefault || MorphScratch.Length != 0x168 || ProjectedScratch is { } projected && (projected.IsDefault || projected.Length != 0xCD0) || ViewCopy is { } view && (view.IsDefault || view.Length != 32))
        {
            throw new InvalidDataException("Native Choco98 scratch or copied view contradicts its physical extent.");
        }
        if (Model is { ResourceAddress: not 0x167482C } || FreezePrimary.HasValue != FreezeSecondary.HasValue || PrivateBufferAliasOffset is int alias && (uint)alias >= NativeBattleEffectArena.Length || NextAllocation is uint next && (next < NativeBattleEffectNodeMemory.ArenaAddress || (ulong)next > (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length))
        {
            throw new InvalidDataException("Native Choco98 retained model, freeze or arena pointers contradict their owners.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco98State? Choco98
    {
        get => effectState.Choco98;
        init => effectState = effectState with { Choco98 = value };
    }
}

public sealed record NativeBattleChoco98Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleChoco98FileRequest(NativeBattleChoco98IoRequest Request) : NativeBattleChoco97Operation;
public sealed record NativeBattleChoco98ModelDrawStep(NativeBattleChoco98Frame Frame, NativeBattleEffectOwnedModelDraw Drawing)
{
    public NativeBattleModulePoolTask Task => Drawing.Task;
}
public sealed record NativeBattleChoco98ProgramStep(NativeBattleChoco98Frame Frame, NativeBattleModulePoolTask Task, int? Remaining);
public sealed record NativeBattleChoco98NodesStep(NativeBattleChoco98Frame Frame, bool Complete);
public sealed record NativeBattleChoco98SpriteDrawStep(NativeBattleChoco98Frame Frame, NativeBattleModulePoolTask Task, bool Complete);
public sealed record NativeBattleChoco98VibrationStep(NativeBattleChoco98Frame Frame, short Handle);
public sealed record NativeBattleChoco98Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix View, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleChoco98Frame, NativeBattleModulePoolTask, NativeBattleEffectOwnedModelState, NativeBattleChoco98ModelDrawStep> DrawModel, Func<NativeBattleChoco98Frame, NativeBattleModulePoolTask, NativeBattleChoco98ProgramStep> AdvanceProgram, Func<NativeBattleChoco98Frame, uint, NativeBattleChoco98NodesStep> AdvanceNodes, Func<NativeBattleChoco98Frame, NativeBattleModulePoolTask, NativeBattleChoco98SpriteDrawStep> AdvanceSprite, Func<NativeBattleChoco98Frame, NativeBattleChoco98VibrationStep> StartVibration, Func<NativeBattleChoco98Frame, NativeBattleChoco97Operation, NativeBattleChoco98Frame> Execute);

internal sealed record NativeBattleChocoControllerIo(ushort Effect, ImmutableArray<byte> Control, ImmutableArray<byte> Script, uint CursorAddress, NativeBattleChoco97IoBanks Banks)
{
    internal static NativeBattleChocoControllerIo From(NativeBattleChoco97IoState state) => new(97, state.Control, state.Script, state.CursorAddress, state.Banks);
    internal static NativeBattleChocoControllerIo From(NativeBattleChoco98IoState state) => new(98, state.Control, state.Script, state.CursorAddress, state.Banks);
    internal NativeBattleChoco97IoState To97()
    {
        if (Effect != 97)
        {
            throw new InvalidDataException("Native Choco IO view cannot reinterpret another effect's retained script.");
        }
        return new(Control, Script, CursorAddress, Banks);
    }
    internal NativeBattleChoco98IoState To98()
    {
        if (Effect != 98)
        {
            throw new InvalidDataException("Native Choco IO view cannot reinterpret another effect's retained script.");
        }
        return new(Control, Script, CursorAddress, Banks);
    }
    internal ushort WaitReleased => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
}
internal sealed record NativeBattleChocoControllerState(NativeBattleChoco97Context? Context, NativeBattleChoco97NodeState Nodes, NativeBattleChocoControllerIo? Io, bool IoScriptKnownCold, NativeBattleChoco97IoBanks? PendingIoBanks, NativeBattleGfAnimationSource? ProgramSource, bool ProgramSourceKnownCold, NativeBattleEffectOwnedModelState? Model, NativeBattleMoogleCameraState? Camera, ImmutableArray<byte> MorphScratch, ImmutableArray<byte>? ProjectedScratch, uint? FreezePrimary, uint? FreezeSecondary, uint? NextAllocation, ImmutableArray<byte>? ViewCopy, int? PrivateBufferAliasOffset)
{
    internal static NativeBattleChocoControllerState Read(NativeBattleEffectMemory memory, bool choco98)
    {
        if (choco98)
        {
            NativeBattleChoco98State state = memory.Choco98 ?? throw new NotSupportedException("Native Choco98 retained state is missing.");
            return new(state.Context, state.Nodes, state.Io is null ? null : NativeBattleChocoControllerIo.From(state.Io), state.IoScriptKnownCold, state.PendingIoBanks, state.ProgramSource, state.ProgramSourceKnownCold, state.Model, state.Camera, state.MorphScratch, state.ProjectedScratch, state.FreezePrimary, state.FreezeSecondary, state.NextAllocation, state.ViewCopy, state.PrivateBufferAliasOffset);
        }
        NativeBattleChoco97State prior = memory.Choco97 ?? throw new NotSupportedException("Native Choco97 retained state is missing.");
        return new(prior.Context, prior.Nodes, prior.Io is null ? null : NativeBattleChocoControllerIo.From(prior.Io), prior.IoScriptKnownCold, prior.PendingIoBanks, prior.ProgramSource, prior.ProgramSourceKnownCold, prior.Model, prior.Camera, prior.MorphScratch, prior.ProjectedScratch, prior.FreezePrimary, prior.FreezeSecondary, prior.NextAllocation, prior.ViewCopy, prior.PrivateBufferAliasOffset);
    }
    internal NativeBattleEffectMemory Write(NativeBattleEffectMemory memory, bool choco98) => choco98 ? memory with { Choco98 = new(Context, Nodes, Io?.To98(), IoScriptKnownCold, PendingIoBanks, ProgramSource, ProgramSourceKnownCold, Model, Camera, MorphScratch, ProjectedScratch, FreezePrimary, FreezeSecondary, NextAllocation, ViewCopy, PrivateBufferAliasOffset) } : memory with { Choco97 = new(Context, Nodes, Io?.To97(), IoScriptKnownCold, PendingIoBanks, ProgramSource, ProgramSourceKnownCold, Model, Camera, MorphScratch, ProjectedScratch, FreezePrimary, FreezeSecondary, NextAllocation, ViewCopy, PrivateBufferAliasOffset) };
}

internal sealed record NativeBattleChocoControllerResources(NativeBattleEffectHeapAllocation IoScript, ImmutableArray<NativeBattleChoco97NodeResource> Nodes, NativeBattleGfAnimationSource ProgramSource, NativeBattleChoco97ModelResources Model, NativeBattleChoco97CameraResources? Camera97, NativeBattleChoco98CameraResources? Camera98)
{
    internal static NativeBattleChocoControllerResources From(NativeBattleChoco97Resources resources) => new(resources.Io.Script, resources.Nodes, resources.ProgramSource, resources.Model, resources.Camera, null);
    internal static NativeBattleChocoControllerResources From(NativeBattleChoco98Resources resources) => new(resources.Io.Script, resources.Nodes, resources.ProgramSource, resources.Model, null, resources.Camera);
}
