using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticTextureTask(int Slot, ushort Flags, ushort State, int? Next, NativeBattleAuxiliaryKind Kind, uint UnknownByteMask, int PayloadLength, uint? ModelPointer, byte? CapturedSequence, int? ProgramOffset, int? ProgramLength, byte? Deadline, int? ModelIndex, ushort? ModelFlags0, ushort? ResourceFlags44, byte? ModelSequence);
public sealed record BattleDiagnosticTerminalTextureAttempt(BattleDiagnosticInvocationRelease Completion, BattleDiagnosticTerminalHoldState Hold, int QueuedActions, bool ActiveAction, BattleDiagnosticTextureQuiescence Textures, ImmutableArray<string> FailedGuards, string Result)
{
    public BattleDiagnosticNaturalVictory? NaturalVictory { get; init; }
}

public sealed record BattleDiagnosticTextureQuiescence(int Capacity, int PhysicalActiveCount, int? Head, int? Tail, bool Complete, ImmutableArray<BattleDiagnosticTextureTask> Tasks)
{
    public bool IsTerminalQuiescent
    {
        get
        {
            if (!Complete || Capacity != 16 || Tasks.IsDefaultOrEmpty || Tasks.Length > Capacity || PhysicalActiveCount != Tasks.Length || Head is not (>= 0 and < 16) || Tail is not (>= 0 and < 16))
            {
                return false;
            }
            HashSet<int> visited = [];
            int? next = Head;
            foreach (BattleDiagnosticTextureTask row in Tasks)
            {
                if (row is null || row.Slot != next || row.Slot is < 0 or >= 16 || !visited.Add(row.Slot) || (row.Flags & 1) == 0 || row.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || row.PayloadLength != 32 || (row.UnknownByteMask & 0x20F) != 0 || row.ModelIndex is not (>= 0 and < 7) || row.ModelPointer != NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode((byte)row.ModelIndex.Value)) || row.CapturedSequence is null || row.ProgramOffset is not >= 0 || row.ProgramLength is not > 0 || row.ProgramOffset >= row.ProgramLength || row.Deadline != 255 || row.ModelFlags0 is not ushort flags || (flags & 2) == 0 || row.ResourceFlags44 is not ushort resource || (resource & 0x40) != 0 || row.ModelSequence != row.CapturedSequence)
                {
                    return false;
                }
                next = row.Next;
            }
            return next is null && Tasks[^1].Slot == Tail;
        }
    }
}

public sealed partial class BattleSession
{

    public BattleDiagnosticTerminalTextureAttempt? LastDiagnosticTerminalTextureAttempt { get; private set; }

    private static bool DiagnosticTextureOtherOwnersReleased(BattleDiagnosticInvocationOwners owners)
    {
        return owners.DescriptorPackets == 0 && owners.NonPumpDescriptorTasks == 0 && owners.PendingContacts == 0 && owners.Effects == 0 && !owners.EffectHead && owners.Modules == 0 && !owners.ModuleHead && owners.Files == 0 && owners.ReturnedTaskPointer == 0 && !owners.DetachedReturnedTask && owners.CameraMask == 0 && !owners.SoundBusy && !owners.ReplacementResource && !owners.TimedOrReaction && owners.ActionHeaders == 0 && owners.ContactRows == 0 && owners.ObservedOriginalEffect && owners.SelectedReturnedDescriptor == 0 && !owners.HeaderGraphicsBusy && !owners.OriginalPools.IsDefault && owners.OriginalPools.All(pool => pool.ActiveRows == 0 && !pool.Head && !pool.Tail);
    }

    private BattleDiagnosticInvocationRelease? TrySealDiagnosticTerminalTextures(BattleDiagnosticInvocationRelease release, NativeBattleGraphicsState graphics)
    {
        BattleDiagnosticNaturalVictory? naturalVictory = DiagnosticTerminalHold is { Declaration.Candidate: BattleOutcome.Victory, Phase: BattleDiagnosticTerminalHoldPhase.Armed, Sequence: 0, Rearms: 0, HeldCallback: null, HeldFrame: null, Receipts.IsEmpty: true } ? new(DiagnosticLifecycleId, NativeHostCallbacks ?? Frame, Frame, DetermineOutcome(), NativeEntryReady, HasPendingExpandedTerminal, DebugOutcome is not null || nativeEscape.Completed || nativeScriptState.ScriptedExitRequested || encounterPolicies?.TerminalReached == true, combatants.Select(actor => new BattleDiagnosticVictoryCombatant(actor.Id, actor.NativeActorIndex ?? NativeBattleSlots.Unassigned, actor.IsPlayer, actor.NativeCombatantFlags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.CurrentHp)).ToImmutableArray()) : null;
        bool admittedOrProspectiveVictory = DiagnosticTerminalHold is { Phase: BattleDiagnosticTerminalHoldPhase.Held, Sequence: 1, Rearms: 0, Declaration.Candidate: BattleOutcome.Victory } || naturalVictory?.IsNaturalVictory == true;
        if (LastDiagnosticTerminalTextureAttempt is null && DiagnosticTerminalHold is { Declaration.Candidate: BattleOutcome.Victory } declared && diagnosticInvocationPolicy is { Command: 16, ActionId: 53 or 56 } && release.Boundary == BattleDiagnosticInvocationRelease.GraphicsFrameBoundary)
        {
            BattleDiagnosticTextureQuiescence observedTextures = CaptureDiagnosticTextureQuiescence(graphics);
            ImmutableArray<string>.Builder failures = ImmutableArray.CreateBuilder<string>();
            void Require(bool condition, string reason)
            {
                if (!condition)
                {
                    failures.Add(reason);
                }
            }
            Require(admittedOrProspectiveVictory, "single-held-or-proved-prospective-victory");
            Require(release.PacketRemoved && release.ExecutingCompletion is not null && release.NativeEventCompleted && release.PresentationAcknowledged && release.HeaderCapturedBeforeReset && release.CurrentCompletionPacketGuaranteedRemoval, "same-frame-completion-witness");
            Require(release.Dispatch.NativeCommand == 16 && release.Dispatch.ActionId is 53 or 56, "slots-native-action");
            Require(declared.Declaration.PartyActorIds.Length == 1 && declared.Declaration.PartyActorIds[0] == release.Dispatch.ActorId, "declared-focal-party");
            Require(ActiveAction is null && actions.Count == 0, "no-active-or-queued-actions");
            Require(release.Blockers.Length == 1 && release.Blockers[0] == "effect-or-auxiliary-task" && release.Owners.AuxiliaryHead, "sole-auxiliary-owner");
            Require(DiagnosticTextureOtherOwnersReleased(release.Owners) && !release.Owners.ActionOrCommand, "other-native-owners-released");
            Require(observedTextures.IsTerminalQuiescent, "terminal-texture-quiescence");
            Require(observedTextures.Tasks.All(task => task.ModelIndex == release.Dispatch.NativeActorIndex), "focal-texture-model");
            LastDiagnosticTerminalTextureAttempt = NativeBattleStorageSnapshots.Capture(new BattleDiagnosticTerminalTextureAttempt(release, declared, actions.Count, ActiveAction is not null, observedTextures, failures.ToImmutable(), "pending") { NaturalVictory = naturalVictory });
        }
        if (!admittedOrProspectiveVictory || DiagnosticTerminalHold is not { Declaration.Candidate: BattleOutcome.Victory } hold || release.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || !release.PacketRemoved || release.ExecutingCompletion is null || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !release.CurrentCompletionPacketGuaranteedRemoval || release.Dispatch.NativeCommand != 16 || release.Dispatch.ActionId is not (53 or 56) || hold.Declaration.PartyActorIds.Length != 1 || hold.Declaration.PartyActorIds[0] != release.Dispatch.ActorId || ActiveAction is not null || actions.Count != 0 || release.Blockers.Length != 1 || release.Blockers[0] != "effect-or-auxiliary-task" || !release.Owners.AuxiliaryHead || !DiagnosticTextureOtherOwnersReleased(release.Owners) || release.Owners.ActionOrCommand)
        {
            return null;
        }
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        if (!textures.IsTerminalQuiescent || textures.Tasks.Any(task => task.ModelIndex != release.Dispatch.NativeActorIndex))
        {
            return null;
        }
        return NativeBattleStorageSnapshots.Capture(release with { Boundary = BattleDiagnosticInvocationRelease.TerminalTextureBoundary, TerminalTextures = new(textures, hold) { NaturalVictory = naturalVictory } });
    }

    private static BattleDiagnosticTextureQuiescence CaptureDiagnosticTextureQuiescence(NativeBattleGraphicsState graphics)
    {
        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary;
        ImmutableArray<NativeBattleAuxiliaryTask> slots = auxiliary.Slots;
        if (slots.IsDefault)
        {
            return new(0, 0, auxiliary.Head, auxiliary.Tail, false, []);
        }
        bool complete = slots.Length == 16;
        int active = 0;
        foreach (NativeBattleAuxiliaryTask task in slots)
        {
            if (task is null)
            {
                complete = false;
            }
            else if ((task.Flags & 1) != 0)
            {
                active++;
            }
        }
        ImmutableArray<BattleDiagnosticTextureTask>.Builder rows = ImmutableArray.CreateBuilder<BattleDiagnosticTextureTask>();
        HashSet<int> visited = [];
        int? next = auxiliary.Head;
        while (next is int slot)
        {
            if ((uint)slot >= (uint)slots.Length || rows.Count >= 16 || !visited.Add(slot) || slots[slot] is not { } task)
            {
                complete = false;
                break;
            }
            bool known = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && !task.Payload.IsDefault && task.Payload.Length == 32 && (task.UnknownByteMask & 0x20F) == 0;
            uint? pointer = known ? BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()) : null;
            byte? sequence = known ? task.Payload[9] : null;
            (int? index, NativeBattleGraphicsModel? model) = ResolveDiagnosticTextureModel(task, graphics, pointer);
            NativeBattleAuxiliaryProgram? program = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 ? task.Program : null;
            int? length = program is not null && !program.Source.IsDefault ? program.Source.Length : null;
            byte? deadline = program is not null && !program.Source.IsDefault && (uint)program.Offset < program.Source.Length ? program.Source[program.Offset] : null;
            rows.Add(new(slot, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Payload.IsDefault ? -1 : task.Payload.Length, pointer, sequence, program?.Offset, length, deadline, index, model?.Flags0, model?.ResourceFlags44, model?.Sequence));
            complete &= (task.Flags & 1) != 0;
            next = task.Next;
        }
        complete &= next is null && rows.Count == active && (rows.Count == 0 ? auxiliary.Head is null && auxiliary.Tail is null : rows[rows.Count - 1].Slot == auxiliary.Tail);
        return new(slots.Length, active, auxiliary.Head, auxiliary.Tail, complete, rows.ToImmutable());
    }
}
