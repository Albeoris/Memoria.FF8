using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleComponentAdmissionIdentity(ushort Effect, byte Command, ushort Action, byte? Actor, int HeaderIndex, long Callback)
{
    public string? ActorId { get; init; }
    public BattleActionHeaderReference? HeaderReference { get; init; }
}

public sealed record BattleComponentTextureQuiescenceObservation(ushort Effect, long GraphicsCallback, int NativeActive, uint CanonicalReturnedTask, BattleDiagnosticInvocationOwners Owners, BattleDiagnosticTextureQuiescence Textures, string ResidentFingerprint, ImmutableArray<string> RawBlockers)
{
    [JsonIgnore]
    public bool IsEligible => Owners is not null && Textures is not null && !RawBlockers.IsDefault && NativeActive == 0 && CanonicalReturnedTask == 0 && RawBlockers.Length == 1 && RawBlockers[0] == "effect-or-auxiliary-task" && Textures.IsTerminalQuiescent && Owners.AuxiliaryHead && BattleComponentOwnerClosure.AreNonAuxiliaryOwnersReleased(Owners);
}

public static class BattleComponentOwnerClosure
{
    public static bool AreNonAuxiliaryOwnersReleased(BattleDiagnosticInvocationOwners owners)
    {
        return owners is not null && owners.DescriptorPackets == 0 && owners.NonPumpDescriptorTasks == 0 && owners.PendingContacts == 0 && owners.Effects == 0 && !owners.EffectHead && owners.Modules == 0 && !owners.ModuleHead && owners.Files == 0 && owners.ReturnedTaskPointer == 0 && !owners.DetachedReturnedTask && owners.CameraMask == 0 && !owners.SoundBusy && !owners.ReplacementResource && !owners.ActionOrCommand && !owners.TimedOrReaction && owners.ActionHeaders >= 0 && owners.ContactRows >= 0 && owners.RetainedActiveHeader is not (< 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity) && owners.RetainedActiveContact is not (< 0 or > NativeBattleRetainedActionPackets.ContactCapacity) && owners.ObservedOriginalEffect && owners.SelectedReturnedDescriptor == 0 && !owners.HeaderGraphicsBusy && !owners.OriginalPools.IsDefault && owners.OriginalPools.All(pool => pool is not null && pool.ActiveRows == 0 && !pool.Head && !pool.Tail);
    }
}

public sealed record BattleComponentTerminalTextureCertificate(string CompletionKind, bool Completed, bool Drained, bool ResidentTasksRemain, ushort Effect, string SourceSha256, BattleDiagnosticEffectDispatch? NativeDispatch, BattleComponentAdmissionIdentity? NativeAdmission, BattleComponentTextureQuiescenceObservation First, BattleComponentTextureQuiescenceObservation Last, long FirstPresentedFrame, long LastPresentedFrame, long TailPresentedFrames, long NativeCallbacksAdvanced);

public sealed partial class BattleSession
{
    /// <summary>Observe a conditional resident-texture fixed point without changing any native owner or the raw drain contract.</summary>
    public BattleComponentTextureQuiescenceObservation? ObserveComponentTerminalTextures(ushort expectedEffect)
    {
            if (Presentation is not { } process || expandedHostedEvent is not null || ObserveDiagnosticDrainBlockers() is not ["effect-or-auxiliary-task"])
        {
            return null;
        }
        return process.ObserveComponentTerminalTextures(expectedEffect, CaptureDiagnosticInvocationOwnerState(process.Graphics), ["effect-or-auxiliary-task"]);
    }
}
