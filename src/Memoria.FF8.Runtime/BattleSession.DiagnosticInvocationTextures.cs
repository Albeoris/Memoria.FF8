using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticInvocationPendingTexture(int Slot, BattlePartyActorReference Actor, byte NativeActorIndex, byte CapturedSequence, int ProgramOffset, int ProgramLength, string ProgramSha256, byte Deadline, ushort ModelFlags0, ushort ResourceFlags44, byte Sequence);
public sealed record BattleDiagnosticInvocationTextureRelease(BattleDiagnosticInvocationPendingTexture Texture, long Callback, long SimulationFrame, long GraphicsCallback, long Timestamp, ushort ModelFlags0, ushort ResourceFlags44, byte Sequence, bool TextureReset, bool BlinkRestored);

public sealed partial class BattleSession
{
    private sealed record DiagnosticFollowupTexture(BattleDiagnosticInvocationPendingTexture Pending, BattleModelOwner Owner, NativeBattleAuxiliaryTask Task, BattleDiagnosticInvocationTextureRelease? Release = null);
    private Dictionary<int, DiagnosticFollowupTexture>? diagnosticFollowupTextures;

    public BattleComponentTextureQuiescenceObservation? ObserveDiagnosticFollowupQuiescence(BattleDiagnosticInvocationFollowupRelease release)
    {
        if (!diagnosticFollowupResidentTail || !ReferenceEquals(release, LastDiagnosticInvocationFollowupRelease) || release.QuiescentTextures is not { } proof || Presentation is not { } process)
        {
            return null;
        }
        BattleDiagnosticInvocationFollowupQuiescence? current = TryCaptureDiagnosticFollowupQuiescence(process, release.FocalRetirement, CaptureDiagnosticInvocationOwnerState(process.Graphics));
        return current is not null && current.Observation.ResidentFingerprint == proof.Observation.ResidentFingerprint ? current.Observation : null;
    }

    public void CloseDiagnosticFollowupQuiescenceObservation(BattleDiagnosticInvocationFollowupRelease release)
    {
        if (!ReferenceEquals(release, LastDiagnosticInvocationFollowupRelease))
        {
            throw new InvalidDataException("Resident observation close changed its actual diagnostic receipt.");
        }
        diagnosticFollowupResidentTail = false;
    }

    private BattleDiagnosticInvocationFollowupQuiescence? TryCaptureDiagnosticFollowupQuiescence(BattlePresentationProcess process, BattleDiagnosticInvocationRelease retirement, BattleDiagnosticInvocationOwners owners)
    {
        if (diagnosticFollowupAmbiguous || !ReferenceEquals(retirement, LastDiagnosticInvocationCompletion) || retirement.Dispatch.Serial != diagnosticDispatchSerial || retirement.Dispatch.HeaderReference is not { } reference || process.ReadDiagnosticCounterHeaderGeneration(reference.Ordinal) is not long generation || generation <= 0 || generation != retirement.Dispatch.NativeHeaderGeneration ||
            diagnosticFollowupCallbacks is not { Count: 1 } callbacks || callbacks.Single() is not { Key.Stage: 2, Key.Actor: BattlePartyActorReference actor, Value.Started: not null, Value.Completed: not null, Value.NoOp: not null } || actor.ActorId != retirement.Dispatch.ActorId ||
            expandedHostedEvent is not null || ActiveAction is not null || actions.Count != 0 || !owners.AuxiliaryHead || !DiagnosticFollowupOwnersEmpty(owners with { AuxiliaryHead = false }) || diagnosticFollowupTextures is not { Count: > 0 } textures || textures.Count != retirement.PendingTextures.Length)
        {
            return null;
        }
        NativeBattleGraphicsState graphics = process.Graphics;
        foreach ((int slot, DiagnosticFollowupTexture texture) in textures)
        {
            if (texture.Release is not null || !retirement.PendingTextures.Contains(texture.Pending) || !ReferenceEquals(graphics.Auxiliary.Slots[slot], texture.Task) || texture.Task.ModelOwner != texture.Owner || graphics.NativeBindings.DecodeLiteral(texture.Pending.NativeActorIndex, graphics.ActorModels) != texture.Owner)
            {
                return null;
            }
        }
        BattleComponentTextureQuiescenceObservation? observation = process.ObserveComponentTerminalTextures(retirement.Dispatch.RequestedEffect, owners, ["effect-or-auxiliary-task"]);
        return observation is null ? null : new(observation, textures.Values.Select(texture => texture.Pending).ToImmutableArray(), diagnosticDispatchSerial, generation) { NoOp = callbacks.Single().Value.NoOp };
    }

    private bool TryCaptureDiagnosticFollowupTextures(BattleDiagnosticInvocationRelease retirement, out ImmutableArray<BattleDiagnosticInvocationPendingTexture> pending)
    {
        pending = [];
        NativeBattleGraphicsState graphics = Presentation!.Graphics;
        if (!retirement.Owners.AuxiliaryHead)
        {
            return graphics.Auxiliary.Head is null;
        }
        if (retirement.Dispatch.NativeActorIndex is not byte native || retirement.Dispatch.ActorSlot is not { Side: BattleSide.Allies } || retirement.Dispatch.ActorId is not { } actorId)
        {
            return false;
        }
        Dictionary<int, DiagnosticFollowupTexture> captured = [];
        for (int? next = graphics.Auxiliary.Head; next is int slot; next = graphics.Auxiliary.Slots[slot].Next)
        {
            if (slot is < 0 or >= 16 || captured.ContainsKey(slot))
            {
                return false;
            }
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[slot];
            if ((task.Flags & 1) == 0 || task.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || task.ModelOwner is not BattlePartyActorModel owner || owner.Actor.ActorId != actorId || graphics.NativeBindings.DecodeLiteral(native, graphics.ActorModels) != owner ||
                task.Payload.IsDefault || task.Payload.Length != 32 || (task.UnknownByteMask & 0x20F) != 0 || task.Program is not { } program || program.Offset < 0 || program.Offset >= program.Source.Length || program.Source[program.Offset] != 255)
            {
                return false;
            }
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
            if ((model.Flags0 & 2) == 0 || model.ResourceFlags44 is not ushort flags || (flags & 0x40) != 0 || model.Sequence is not byte sequence || sequence != task.Payload[9])
            {
                return false;
            }
            BattleDiagnosticInvocationPendingTexture texture = new(slot, owner.Actor, native, task.Payload[9], program.Offset, program.Source.Length, Convert.ToHexStringLower(SHA256.HashData(program.Source.AsSpan())), 255, model.Flags0, flags, sequence);
            captured.Add(slot, new(texture, owner, task));
        }
        if (captured.Count == 0 || graphics.Auxiliary.Slots.Count(task => (task.Flags & 1) != 0) != captured.Count)
        {
            return false;
        }
        diagnosticFollowupTextures = captured;
        pending = captured.Values.Select(texture => texture.Pending).ToImmutableArray();
        return true;
    }

    private void ObserveDiagnosticFollowupTextureReleased(NativeBattleGraphicsState graphics, int slot, NativeBattleAuxiliaryTask task, NativeBattleGraphicsModel model)
    {
        if (diagnosticFollowupRetirement is not { } retirement || diagnosticFollowupTextures is not { } textures || diagnosticFollowupAmbiguous)
        {
            return;
        }
        if (!textures.TryGetValue(slot, out DiagnosticFollowupTexture? texture) || texture.Release is not null || !ReferenceEquals(texture.Task, task) || task.ModelOwner != texture.Owner || graphics.NativeBindings.DecodeLiteral(texture.Pending.NativeActorIndex, graphics.ActorModels) != texture.Owner ||
            retirement.LifecycleId != DiagnosticLifecycleId || !ReferenceEquals(retirement.Dispatch, diagnosticInvocationDispatch) || (NativeHostCallbacks ?? Frame) <= retirement.Callback || graphics.Callback < retirement.GraphicsCallback || Frame < retirement.SimulationFrame || model.ResourceFlags44 is not ushort flags || model.Sequence is not byte sequence ||
            (model.Flags0 & 2) != 0 && (flags & 0x40) == 0 && sequence == texture.Pending.CapturedSequence)
        {
            diagnosticFollowupAmbiguous = true;
            return;
        }
        textures[slot] = texture with { Release = new(texture.Pending, NativeHostCallbacks ?? Frame, Frame, graphics.Callback, Stopwatch.GetTimestamp(), model.Flags0, flags, sequence, true, true) };
    }
}
