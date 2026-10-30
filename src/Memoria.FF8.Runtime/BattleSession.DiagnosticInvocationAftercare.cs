using System.Collections.Immutable;
using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticCommandBankClosure(byte Bank, byte Tail, ImmutableArray<NativeBattleCommandLink> Links);
public sealed record BattleDiagnosticNoOpPlayerCallback(byte Bank, byte Slot, byte Subcommand, string ActorId, byte ActorIndex, int ModelId, uint PassiveFlags, ushort Stage, NativeBattleCommandPacket Packet, NativeBattleCommandLink Link, int SourceHeaderIndex, long SourceHeaderGeneration);
public sealed record BattleDiagnosticInvocationAftercarePlan(Guid LifecycleId, long FocalSerial, long MainCallback, long MainFrame, long MainTimestamp, BattleDiagnosticRepairRandomState MainRandom, BattleDiagnosticNoOpPlayerCallback Callback, BattleDiagnosticTextureQuiescence? TerminalTextures)
{
    public ImmutableArray<BattleDiagnosticCommandBankClosure> CommandBanks { get; init; }
}
public sealed record BattleDiagnosticInvocationAftercareReceipt(Guid LifecycleId, long FocalSerial, long MainCallback, long DispatchCallback, long HandlerCompletedCallback, long BankReleasedCallback, long CompletedCallback, long CompletedFrame, long CompletedTimestamp, BattleDiagnosticNoOpPlayerCallback Callback, NativeBattleCommandPacket ReleasedBankPacket, NativeBattleCommandLink ReleasedBankLink, BattleDiagnosticRepairRandomState HandlerRandomBefore, BattleDiagnosticRepairRandomState HandlerRandomAfter, int ProducedActions, int ProducedHeaders, int ProducedContacts, long DispatchSerialBefore, long DispatchSerialAfter, int CompletionPacketSlot, NativeBattleDescriptorPacket CompletionPacket, NativeBattleDescriptorPacket RemovedCompletionPacket, bool NativeEventCompleted, bool BankReleased, bool PacketRemoved, BattleDiagnosticInvocationOwners CleanupOwners, BattleDiagnosticTextureQuiescence? CleanupTextures)
{
    public ImmutableArray<BattleDiagnosticCommandBankClosure> CommandBanks { get; init; }
}

public sealed partial class BattleSession
{
    public BattleDiagnosticInvocationAftercareReceipt? LastDiagnosticInvocationAftercare { get; private set; }
    public event Action<BattleDiagnosticInvocationAftercareReceipt>? DiagnosticInvocationAftercareCompleted;
    public object? DiagnosticInvocationAftercareProgress => diagnosticAftercare is not { } state ? null : new
    {
        state.Plan.FocalSerial,
        state.Failed,
        state.HandlerActive,
        state.HandlerDone,
        state.BankReleased,
        state.EventCompleted,
        state.DispatchCallback,
        state.HandlerCompletedCallback,
        state.BankReleasedCallback,
        state.EventCallback,
        state.ProducedActions,
        state.ProducedHeaders,
        state.ProducedContacts,
        state.SerialBefore,
        state.SerialAfter,
        state.CompletionSlot,
        HasCompletionPacket = state.CompletionPacket is not null,
        HasRetiredPacket = state.RetiredPacket is not null,
        state.RandomBefore,
        state.RandomAfter
    };
    private sealed class DiagnosticAftercareState
    {
        internal required BattleDiagnosticInvocationAftercarePlan Plan;
        internal ImmutableDictionary<int, NativeBattleAuxiliaryTask> MainTextures = ImmutableDictionary<int, NativeBattleAuxiliaryTask>.Empty;
        internal bool Failed;
        internal bool HandlerActive;
        internal bool HandlerDone;
        internal bool BankReleased;
        internal bool EventCompleted;
        internal long DispatchCallback;
        internal long HandlerCompletedCallback;
        internal long BankReleasedCallback;
        internal long EventCallback;
        internal long EventFrame;
        internal long EventGraphics;
        internal BattleDiagnosticRepairRandomState? RandomBefore;
        internal BattleDiagnosticRepairRandomState? RandomAfter;
        internal int HeadersBefore;
        internal int ContactsBefore;
        internal int ProducedActions;
        internal int ProducedHeaders;
        internal int ProducedContacts;
        internal long SerialBefore;
        internal long SerialAfter;
        internal NativeBattleCommandPacket? ReleasedBankPacket;
        internal NativeBattleCommandLink? ReleasedBankLink;
        internal int CompletionSlot = -1;
        internal NativeBattleDescriptorPacket? CompletionPacket;
        internal NativeBattleDescriptorPacket? RetiredPacket;
    }

    private DiagnosticInvocationCompletion? diagnosticFocalCompletionScope;
    private BattleDiagnosticNoOpPlayerCallback? diagnosticBornNoOpCallback;
    private int diagnosticFocalCallbackCount;
    private DiagnosticAftercareState? diagnosticAftercare;

    private void BeginDiagnosticInvocationFocalCompletion(DiagnosticInvocationCompletion? attempt)
    {
        diagnosticFocalCompletionScope = attempt is { HeaderCaptured: true } ? attempt : null;
        if (diagnosticFocalCompletionScope is not null)
        {
            diagnosticBornNoOpCallback = null;
            diagnosticFocalCallbackCount = 0;
        }
    }

    private void EndDiagnosticInvocationFocalCompletion() => diagnosticFocalCompletionScope = null;

    private static bool IsKnownDiagnosticNoOpPlayer(BattleCombatant actor) => actor.IsActive && actor.IsPlayer && actor.NativeIndex is >= 0 and < 3 && actor.ModelId >= 0 && actor.ModelId != 4 && actor.NativePlayerStatRules?.NativeStatusPassiveFlags == 0;

    private void ObserveDiagnosticInvocationCallbackQueued(BattleCombatant actor, ushort stage, NativeBattleCommandLocation location)
    {
        if (diagnosticAftercare is { HandlerActive: true } running)
        {
            running.Failed = true;
        }
        if (diagnosticFocalCompletionScope is not { } scope)
        {
            return;
        }
        diagnosticFocalCallbackCount++;
        if (diagnosticFocalCallbackCount != 1 || stage != 2 || location.Bank != 0 || !IsKnownDiagnosticNoOpPlayer(actor) || scope.Dispatch.NativeTargetMask is not ushort targets || (targets & (1 << actor.NativeIndex)) == 0)
        {
            diagnosticBornNoOpCallback = null;
            return;
        }
        NativeBattleCommandBank bank = nativeCommandBanks.Banks[location.Bank];
        diagnosticBornNoOpCallback = new(location.Bank, location.Slot, 0, actor.Id, checked((byte)actor.NativeIndex), actor.ModelId, 0, stage, bank.Packets[2 * location.Slot], bank.Links[location.Slot], scope.Dispatch.NativeHeaderIndex!.Value, scope.Dispatch.NativeHeaderGeneration!.Value);
    }

    private static bool DiagnosticNonAftercareOwnersReleased(BattleDiagnosticInvocationOwners owners)
    {
        return owners.DescriptorPackets == 0 && owners.NonPumpDescriptorTasks == 0 && owners.PendingContacts == 0 && owners.Effects == 0 && !owners.EffectHead && owners.Modules == 0 && !owners.ModuleHead && owners.Files == 0 && owners.ReturnedTaskPointer == 0 && !owners.DetachedReturnedTask && owners.CameraMask == 0 && !owners.SoundBusy && !owners.ReplacementResource && !owners.TimedOrReaction && owners.ActionHeaders == 0 && owners.ContactRows == 0 && owners.ObservedOriginalEffect && owners.SelectedReturnedDescriptor == 0 && !owners.HeaderGraphicsBusy && !owners.OriginalPools.IsDefault && owners.OriginalPools.All(pool => pool.ActiveRows == 0 && !pool.Head && !pool.Tail);
    }

    private BattleDiagnosticInvocationRelease? TrySealDiagnosticInvocationAftercare(DiagnosticInvocationCompletion attempt, BattleDiagnosticInvocationRelease release, NativeBattleGraphicsState graphics)
    {
        if (diagnosticAftercare is not null || release.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || !release.PacketRemoved || release.ExecutingCompletion is null || !release.NativeEventCompleted || !release.PresentationAcknowledged || !release.HeaderCapturedBeforeReset || !DiagnosticNonAftercareOwnersReleased(release.Owners) || ActiveAction is not null || actions.Count != 0 || diagnosticFocalCallbackCount != 1 || diagnosticBornNoOpCallback is not { } callback || callback.SourceHeaderIndex != attempt.Dispatch.NativeHeaderIndex || callback.SourceHeaderGeneration != attempt.Dispatch.NativeHeaderGeneration || nativeCommandBanks.BoundCommands.Count() != 1)
        {
            return null;
        }
        NativeBattleCommandBank bank = nativeCommandBanks.Banks[callback.Bank];
        if (!DiagnosticPhysicalCommandBanksMatch(callback) || !ReferenceEquals(bank.Packets[2 * callback.Slot], callback.Packet) || bank.LogicalCommands[2 * callback.Slot]?.NativeCallback is not { Stage: 2 } bound || bound.ActorIndex != callback.ActorIndex || bank.LogicalCommands[2 * callback.Slot + 1] is not null || !IsKnownDiagnosticNoOpPlayer(GetActor(callback.ActorId)))
        {
            return null;
        }
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        if (release.Owners.AuxiliaryHead ? !textures.IsTerminalQuiescent : !textures.Complete || textures.PhysicalActiveCount != 0)
        {
            return null;
        }
        BattleDiagnosticInvocationAftercarePlan plan = new(DiagnosticLifecycleId, release.Dispatch.Serial, release.Callback, release.SimulationFrame, release.Timestamp, ObserveDiagnosticRepairRandom(), callback with { Link = bank.Links[callback.Slot] }, release.Owners.AuxiliaryHead ? textures : null) { CommandBanks = CaptureDiagnosticCommandBanks() };
        diagnosticAftercare = new() { Plan = plan, MainTextures = textures.Tasks.ToImmutableDictionary(row => row.Slot, row => graphics.Auxiliary.Slots[row.Slot]) };
        return release with { Boundary = BattleDiagnosticInvocationRelease.AftercareBoundary, Aftercare = plan };
    }

    private void BeginDiagnosticInvocationAftercareHandler(BattleNativeCallback callback, BattleCombatant actor)
    {
        if (diagnosticAftercare is not { } state || state.Failed || state.HandlerDone)
        {
            return;
        }
        BattleDiagnosticNoOpPlayerCallback expected = state.Plan.Callback;
        if (callback.Stage != 2 || callback.ActorIndex != expected.ActorIndex || actor.Id != expected.ActorId || !IsKnownDiagnosticNoOpPlayer(actor) || nativeDispatchCommandLocation is not { } location || location.Bank != expected.Bank || location.Slot != expected.Slot || !ReferenceEquals(nativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot], expected.Packet))
        {
            state.Failed = true;
            return;
        }
        state.HandlerActive = true;
        state.DispatchCallback = NativeHostCallbacks!.Value;
        state.RandomBefore = ObserveDiagnosticRepairRandom();
        state.HeadersBefore = nativeActionPackets.ActionCount;
        state.ContactsBefore = nativeActionPackets.ContactCount;
        state.SerialBefore = diagnosticDispatchSerial;
    }

    private void EndDiagnosticInvocationAftercareHandler()
    {
        if (diagnosticAftercare is not { HandlerActive: true } state)
        {
            return;
        }
        state.HandlerActive = false;
        state.HandlerDone = true;
        state.HandlerCompletedCallback = NativeHostCallbacks!.Value;
        state.RandomAfter = ObserveDiagnosticRepairRandom();
        state.ProducedActions = actions.Count;
        state.ProducedHeaders = nativeActionPackets.ActionCount - state.HeadersBefore;
        state.ProducedContacts = nativeActionPackets.ContactCount - state.ContactsBefore;
        state.SerialAfter = diagnosticDispatchSerial;
        if (state.ProducedActions != 0 || state.ProducedHeaders != 0 || state.ProducedContacts != 0 || state.SerialBefore != state.SerialAfter || !SameDiagnosticAftercareRandom(state.RandomBefore!, state.RandomAfter) || state.CompletionPacket is null)
        {
            state.Failed = true;
        }
    }

    private static bool SameDiagnosticAftercareRandom(BattleDiagnosticRepairRandomState first, BattleDiagnosticRepairRandomState second) => first.Battle.Lane == second.Battle.Lane && first.Battle.Indexes.AsSpan().SequenceEqual(second.Battle.Indexes.AsSpan()) && first.Crt == second.Crt && first.Ui == second.Ui && first.Global == second.Global;

    private void ObserveDiagnosticInvocationAftercareDescriptorQueued(int slot, ushort command, uint argument, NativeBattleDescriptorPacket packet)
    {
        if (diagnosticAftercare is not { HandlerActive: true } state)
        {
            return;
        }
        if (command != 10 || argument != 0x47DDA0 || state.CompletionPacket is not null)
        {
            state.Failed = true;
            return;
        }
        state.CompletionSlot = slot;
        state.CompletionPacket = packet;
    }

    private void ObserveDiagnosticInvocationAftercareBankReleased(NativeBattleCommandLocation location, NativeBattleCommandPacket? original)
    {
        if (diagnosticAftercare is not { HandlerDone: true, BankReleased: false } state || location.Bank != state.Plan.Callback.Bank || location.Slot != state.Plan.Callback.Slot)
        {
            return;
        }
        NativeBattleCommandBank bank = nativeCommandBanks.Banks[location.Bank];
        if (!ReferenceEquals(original, state.Plan.Callback.Packet) || bank.LogicalCommands[2 * location.Slot] is not null || bank.LogicalCommands[2 * location.Slot + 1] is not null || bank.Links[location.Slot] is not { Previous: 0, Next: 0, State: 0 })
        {
            state.Failed = true;
            return;
        }
        state.BankReleased = true;
        state.BankReleasedCallback = NativeHostCallbacks!.Value;
        state.ReleasedBankPacket = bank.Packets[2 * location.Slot];
        state.ReleasedBankLink = bank.Links[location.Slot];
    }

    private bool IsDiagnosticInvocationAftercareCompletion(NativeBattleGraphicsState graphics, uint callback)
    {
        if (diagnosticAftercare is not { HandlerDone: true, BankReleased: true, Failed: false, EventCompleted: false } state || callback != 0x47DDA0 || state.CompletionSlot < 0 || graphics.Descriptors.Queue.Length != 1 || graphics.Descriptors.Queue[0] != state.CompletionSlot || !ReferenceEquals(graphics.Descriptors.Packets[state.CompletionSlot], state.CompletionPacket) || nativeActionScratch.EventActorB0F != state.Plan.Callback.ActorIndex)
        {
            return false;
        }
        state.EventCallback = NativeHostCallbacks!.Value;
        state.EventFrame = Frame;
        state.EventGraphics = graphics.Callback;
        return true;
    }

    private void ObserveDiagnosticInvocationAftercareNativeEvent(bool matched, bool completed)
    {
        if (!matched || diagnosticAftercare is not { } state)
        {
            return;
        }
        state.EventCompleted = completed;
        state.Failed |= !completed;
    }

    private void ObserveDiagnosticInvocationAftercarePacketRemoved(NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket original)
    {
        if (diagnosticAftercare is not { EventCompleted: true, Failed: false } aftercare || slot != aftercare.CompletionSlot || !ReferenceEquals(original, aftercare.CompletionPacket))
        {
            return;
        }
        if (aftercare.RetiredPacket is not null)
        {
            aftercare.Failed = true;
            return;
        }
        aftercare.RetiredPacket = state.Packets[slot];
    }

    private void ObserveDiagnosticInvocationAftercareEffectDispatch()
    {
        if (diagnosticAftercare is { } state && LastDiagnosticInvocationAftercare is null)
        {
            state.Failed = true;
        }
    }

    private void ObserveDiagnosticInvocationAftercareFrameEnd()
    {
        if (diagnosticAftercare is not { EventCompleted: true, Failed: false } state || LastDiagnosticInvocationAftercare is not null || NativeGraphics is not { } graphics)
        {
            return;
        }
        if (state.Plan.LifecycleId != DiagnosticLifecycleId || state.Plan.FocalSerial != diagnosticInvocationDispatch?.Serial || state.SerialBefore != state.Plan.FocalSerial || state.SerialAfter != state.Plan.FocalSerial || diagnosticDispatchSerial != state.SerialAfter || state.EventCallback != NativeHostCallbacks || state.EventFrame != Frame || state.EventGraphics + 1 != graphics.Callback || state.CompletionPacket is null || state.RetiredPacket is null || !ReferenceEquals(graphics.Descriptors.Packets[state.CompletionSlot], state.RetiredPacket) || !state.RetiredPacket.Bytes.AsSpan().SequenceEqual(state.CompletionPacket.Bytes.SetItem(1, 0).AsSpan()))
        {
            state.Failed = true;
            return;
        }
        BattleDiagnosticInvocationOwners owners = CaptureDiagnosticInvocationOwnerState(graphics);
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        if (!DiagnosticNonAftercareOwnersReleased(owners) || owners.ActionOrCommand || !DiagnosticPhysicalCommandBanksMatch(null) || !DiagnosticAftercareTextureContinuity(state, graphics, textures))
        {
            CaptureDiagnosticInvocationPendingOwners(graphics);
            state.Failed = true;
            return;
        }
        LastDiagnosticInvocationAftercare = new(DiagnosticLifecycleId, state.Plan.FocalSerial, state.Plan.MainCallback, state.DispatchCallback, state.HandlerCompletedCallback, state.BankReleasedCallback, NativeHostCallbacks!.Value, Frame, Stopwatch.GetTimestamp(), state.Plan.Callback, state.ReleasedBankPacket!, state.ReleasedBankLink!, state.RandomBefore!, state.RandomAfter!, state.ProducedActions, state.ProducedHeaders, state.ProducedContacts, state.SerialBefore, state.SerialAfter, state.CompletionSlot, state.CompletionPacket, state.RetiredPacket, state.EventCompleted, state.BankReleased, true, owners, owners.AuxiliaryHead ? textures : null) { CommandBanks = CaptureDiagnosticCommandBanks() };
        DiagnosticInvocationAftercareCompleted?.Invoke(LastDiagnosticInvocationAftercare);
    }

    private ImmutableArray<BattleDiagnosticCommandBankClosure> CaptureDiagnosticCommandBanks() => nativeCommandBanks.Banks.Select((bank, index) => new BattleDiagnosticCommandBankClosure(checked((byte)index), bank.Tail, bank.Links)).ToImmutableArray();

    private bool DiagnosticPhysicalCommandBanksMatch(BattleDiagnosticNoOpPlayerCallback? expected)
    {
        if (nativeCommandBanks.Banks.IsDefault || nativeCommandBanks.Banks.Length != 3)
        {
            return false;
        }
        for (int bankIndex = 0; bankIndex < 3; bankIndex++)
        {
            NativeBattleCommandBank bank = nativeCommandBanks.Banks[bankIndex];
            if (bank.Links.IsDefault || bank.Links.Length != 11)
            {
                return false;
            }
            for (int slot = 0; slot < 11; slot++)
            {
                NativeBattleCommandLink link = bank.Links[slot];
                if (expected is not null && bankIndex == expected.Bank && slot == expected.Slot)
                {
                    if (link.Previous != 255 || link.Next != 255 || link.State != 0 || bank.Tail != slot)
                    {
                        return false;
                    }
                }
                else if (link.Previous != 0 || link.Next != 0 || link.State != 0)
                {
                    return false;
                }
            }
            if ((expected is null || expected.Bank != bankIndex) && bank.Tail != 255)
            {
                return false;
            }
        }
        return true;
    }

    private static bool DiagnosticAftercareTextureContinuity(DiagnosticAftercareState state, NativeBattleGraphicsState graphics, BattleDiagnosticTextureQuiescence current)
    {
        if (graphics.Auxiliary.Head is null)
        {
            return current.Complete && current.PhysicalActiveCount == 0;
        }
        if (state.Plan.TerminalTextures is not { } original || !current.IsTerminalQuiescent || current.Head != original.Head || current.Tail != original.Tail || current.Tasks.Length != state.MainTextures.Count)
        {
            return false;
        }
        return current.Tasks.All(row => state.MainTextures.TryGetValue(row.Slot, out NativeBattleAuxiliaryTask? task) && ReferenceEquals(task, graphics.Auxiliary.Slots[row.Slot]));
    }

}
