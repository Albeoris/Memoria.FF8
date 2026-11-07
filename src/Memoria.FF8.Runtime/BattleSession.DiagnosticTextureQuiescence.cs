using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticTextureTask(int Slot, ushort Flags, ushort State, int? Next, NativeBattleAuxiliaryKind Kind, uint UnknownByteMask, int PayloadLength, uint? ModelPointer, byte? CapturedSequence, int? ProgramOffset, int? ProgramLength, byte? Deadline, int? ModelIndex, ushort? ModelFlags0, ushort? ResourceFlags44, byte? ModelSequence);
public sealed record BattleDiagnosticTerminalTextureAttempt(BattleDiagnosticInvocationRelease Completion, BattleDiagnosticTerminalHoldState Hold, int BornCallbackCount, BattleDiagnosticNoOpPlayerCallback? BornNoOpCallback, int BoundCommands, int QueuedActions, bool ActiveAction, bool PhysicalBanksMatch, ImmutableArray<BattleDiagnosticCommandBankClosure> CommandBanks, BattleDiagnosticTextureQuiescence Textures, ImmutableArray<string> FailedGuards, string Result);

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
                if (row is null || row.Slot != next || row.Slot is < 0 or >= 16 || !visited.Add(row.Slot) || (row.Flags & 1) == 0 || row.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || row.PayloadLength != 32 || (row.UnknownByteMask & 0x20F) != 0 || row.ModelIndex is not (>= 0 and < 7) || row.ModelPointer != 0x1D972C0u + 156u * (uint)row.ModelIndex.Value || row.CapturedSequence is null || row.ProgramOffset is not >= 0 || row.ProgramLength is not > 0 || row.ProgramOffset >= row.ProgramLength || row.Deadline != 255 || row.ModelFlags0 is not ushort flags || (flags & 2) == 0 || row.ResourceFlags44 is not ushort resource || (resource & 0x40) != 0 || row.ModelSequence != row.CapturedSequence)
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

    private BattleDiagnosticInvocationRelease? TrySealDiagnosticTerminalTextures(BattleDiagnosticInvocationRelease release, NativeBattleGraphicsState graphics)
    {
        if (LastDiagnosticTerminalTextureAttempt is null && DiagnosticTerminalHold is { Declaration.Candidate: BattleOutcome.Victory } declared && diagnosticInvocationPolicy is { Command: 16, ActionId: 53 or 56 } && release.Boundary == BattleDiagnosticInvocationRelease.GraphicsFrameBoundary)
        {
            BattleDiagnosticTextureQuiescence observedTextures = CaptureDiagnosticTextureQuiescence(graphics);
            int boundCommands = nativeCommandBanks.BoundCommands.Count();
            bool physicalBanksMatch = DiagnosticPhysicalCommandBanksMatch(null);
            ImmutableArray<string>.Builder failures = ImmutableArray.CreateBuilder<string>();
            void Require(bool condition, string reason)
            {
                if (!condition)
                {
                    failures.Add(reason);
                }
            }
            Require(declared is { Phase: BattleDiagnosticTerminalHoldPhase.Held, Sequence: 1, Rearms: 0 }, "single-held-victory");
            Require(release.PacketRemoved && release.ExecutingCompletion is not null && release.NativeEventCompleted && release.PresentationAcknowledged && release.HeaderCapturedBeforeReset && release.CurrentCompletionPacketGuaranteedRemoval, "same-frame-completion-witness");
            Require(release.Dispatch.NativeCommand == 16 && release.Dispatch.ActionId is 53 or 56, "slots-native-action");
            Require(declared.Declaration.PartyActorIds.Length == 1 && declared.Declaration.PartyActorIds[0] == release.Dispatch.ActorId, "declared-focal-party");
            Require(diagnosticFocalCallbackCount == 0, "zero-born-callbacks");
            Require(ActiveAction is null && actions.Count == 0, "no-active-or-queued-actions");
            Require(boundCommands == 0, "zero-bound-commands");
            Require(physicalBanksMatch, "empty-physical-command-banks");
            Require(release.Blockers.Length == 1 && release.Blockers[0] == "effect-or-auxiliary-task" && release.Owners.AuxiliaryHead, "sole-auxiliary-owner");
            Require(DiagnosticNonAftercareOwnersReleased(release.Owners) && !release.Owners.ActionOrCommand, "other-native-owners-released");
            Require(observedTextures.IsTerminalQuiescent, "terminal-texture-quiescence");
            Require(observedTextures.Tasks.All(task => task.ModelIndex == release.Dispatch.NativeActorIndex), "focal-texture-model");
            LastDiagnosticTerminalTextureAttempt = NativeBattleStorageSnapshots.Capture(new BattleDiagnosticTerminalTextureAttempt(release, declared, diagnosticFocalCallbackCount, diagnosticBornNoOpCallback, boundCommands, actions.Count, ActiveAction is not null, physicalBanksMatch, CaptureDiagnosticCommandBanks(), observedTextures, failures.ToImmutable(), "pending"));
        }
        if (DiagnosticTerminalHold is not { Phase: BattleDiagnosticTerminalHoldPhase.Held, Sequence: 1, Rearms: 0, Declaration.Candidate: BattleOutcome.Victory } hold || release.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || !release.PacketRemoved || release.ExecutingCompletion is null || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !release.CurrentCompletionPacketGuaranteedRemoval || release.Dispatch.NativeCommand != 16 || release.Dispatch.ActionId is not (53 or 56) || hold.Declaration.PartyActorIds.Length != 1 || hold.Declaration.PartyActorIds[0] != release.Dispatch.ActorId || diagnosticFocalCallbackCount != 0 || ActiveAction is not null || actions.Count != 0 || nativeCommandBanks.BoundCommands.Any() || !DiagnosticPhysicalCommandBanksMatch(null) || release.Blockers.Length != 1 || release.Blockers[0] != "effect-or-auxiliary-task" || !release.Owners.AuxiliaryHead || !DiagnosticNonAftercareOwnersReleased(release.Owners) || release.Owners.ActionOrCommand)
        {
            return null;
        }
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        if (!textures.IsTerminalQuiescent || textures.Tasks.Any(task => task.ModelIndex != release.Dispatch.NativeActorIndex))
        {
            return null;
        }
        return NativeBattleStorageSnapshots.Capture(release with { Boundary = BattleDiagnosticInvocationRelease.TerminalTextureBoundary, TerminalTextures = new(textures, hold, CaptureDiagnosticCommandBanks()) });
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
            uint relative = pointer is uint actual ? unchecked(actual - 0x1D972C0u) : uint.MaxValue;
            int? index = relative % 156 == 0 && relative / 156 < 7 ? (int)(relative / 156) : null;
            NativeBattleGraphicsModel? model = index is int selected && !graphics.Models.IsDefault && selected < graphics.Models.Length ? graphics.Models[selected] : null;
            NativeBattleAuxiliaryProgram? program = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 ? task.Program : null;
            int? length = program is not null && !program.Source.IsDefault ? program.Source.Length : null;
            byte? deadline = program is not null && !program.Source.IsDefault && (uint)program.Offset < program.Source.Length ? program.Source[program.Offset] : null;
            rows.Add(new(slot, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Payload.IsDefault ? -1 : task.Payload.Length, pointer, sequence, program?.Offset, length, deadline, model?.Index, model?.Flags0, model?.ResourceFlags44, model?.Sequence));
            complete &= (task.Flags & 1) != 0;
            next = task.Next;
        }
        complete &= next is null && rows.Count == active && (rows.Count == 0 ? auxiliary.Head is null && auxiliary.Tail is null : rows[rows.Count - 1].Slot == auxiliary.Tail);
        return new(slots.Length, active, auxiliary.Head, auxiliary.Tail, complete, rows.ToImmutable());
    }
}
