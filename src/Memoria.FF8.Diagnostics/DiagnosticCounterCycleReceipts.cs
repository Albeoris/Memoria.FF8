using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticCounterDispatchPackets([property: JsonRequired] long Serial, [property: JsonRequired] long Callback, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] long HeaderGeneration, [property: JsonRequired] NativeBattleActionPacketsState Packets)
{
    public ushort NormalizeTargetMask(BattleDiagnosticEffectDispatch dispatch, BattleDiagnosticCounterCommand queued)
    {
        if (Serial <= 0 || Serial != dispatch.Serial || Callback != dispatch.Callback || HeaderIndex != dispatch.NativeHeaderIndex || HeaderGeneration <= 0 || HeaderGeneration != dispatch.NativeHeaderGeneration || HeaderIndex != queued.HeaderIndex || HeaderGeneration != queued.HeaderGeneration || Packets is null || Packets.ActiveHeaderIndex != HeaderIndex)
        {
            throw new InvalidDataException("Counter dispatch packet proof lost its actual synchronous header/generation owner.");
        }
        NativeBattleActionHeaderSlot header = DiagnosticCounterCycleValidation.Header(Packets, HeaderIndex);
        if (header.ReadByte(0) != queued.ActorIndex || header.ReadByte(1) != queued.Command || DiagnosticCounterCycleValidation.Word(header, 4) != queued.ActionId || DiagnosticCounterCycleValidation.Word(header, 6) != queued.EffectId || header.ReadByte(2) != queued.Sequence)
        {
            throw new InvalidDataException("Counter dispatched header differs from its actually queued native command.");
        }
        ushort mask = NativeBattleActionGraphics.Normalize(Packets, HeaderIndex).TargetMask;
        if (dispatch.NativeTargetMask != mask)
        {
            throw new InvalidDataException("Counter dispatch recipient mask differs from its canonical primary-contact union.");
        }
        return mask;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticCounterAttemptReceipt([property: JsonRequired] int Number, [property: JsonRequired] long SubmittedCallback)
{
    [JsonRequired]
    public long? DrainedCallback { get; init; }
    [JsonRequired]
    public BattleDiagnosticRepairReceipt? PreparationRepair { get; init; }
    [JsonRequired]
    public ImmutableArray<BattleDiagnosticCounterContact> Contacts { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<BattleDiagnosticCounterCallbackObservation> Callbacks { get; init; } = [];
    [JsonRequired]
    public ImmutableArray<BattleDiagnosticCounterCommand> Commands { get; init; } = [];
    [JsonRequired]
    public BattleDiagnosticEffectDispatch? Dispatch { get; init; }
    [JsonRequired]
    public DiagnosticCounterDispatchPackets? DispatchPackets { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticCounterCycleReceipt([property: JsonRequired] int Execution, [property: JsonRequired] DiagnosticCounterCycleDeclaration Declaration, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] string TargetId, [property: JsonRequired] byte TargetIndex, [property: JsonRequired] long StartedCallback)
{
    [JsonRequired]
    public long? CompletedCallback { get; init; }
    [JsonRequired]
    public ImmutableArray<DiagnosticCounterAttemptReceipt> Attempts { get; init; } = [];
}

public static class DiagnosticCounterCycleValidation
{
    internal static ushort Word(NativeBattleActionHeaderSlot header, int offset) => (ushort)(header.ReadByte(offset) | header.ReadByte(offset + 1) << 8);

    internal static NativeBattleActionHeaderSlot Header(NativeBattleActionPacketsState packets, int index)
    {
        packets.Validate();
        if (index is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || index >= packets.ActionCount)
        {
            throw new InvalidDataException("Counter packet proof lacks its physically published action header.");
        }
        return packets.Headers[index];
    }

    public static void Validate(DiagnosticCounterCycleReceipt receipt, BattleInput input, bool requireCompleted = true)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        receipt.Declaration.Validate(input, 178, 230, 8);
        if (receipt.Execution is < 1 or > 2 || receipt.ActorId != input.ActorId || receipt.TargetId != input.TargetId || receipt.TargetId != receipt.Declaration.Focal.ActorId || receipt.ActorIndex >= 3 || receipt.TargetIndex is < 3 or >= 7 || receipt.StartedCallback < 0 || receipt.Attempts.IsDefault || receipt.Attempts.Length > receipt.Declaration.MaxTriggerAttempts || requireCompleted && (receipt.CompletedCallback is null || receipt.Attempts.IsEmpty))
        {
            throw new InvalidDataException("Counter cycle lost its distinct physical control/focal actors, bounded attempts or completion boundary.");
        }
        long previousCallback = receipt.StartedCallback;
        long previousPrimary = 0;
        long previousEpoch = 0;
        long previousAiScope = 0;
        int focalCount = 0;
        for (int index = 0; index < receipt.Attempts.Length; index++)
        {
            DiagnosticCounterAttemptReceipt attempt = receipt.Attempts[index];
            if (attempt.Number != index + 1 || attempt.SubmittedCallback < previousCallback || attempt.Contacts.IsDefault || attempt.Callbacks.IsDefault || attempt.Commands.IsDefault || attempt.DrainedCallback is long drained && drained < attempt.SubmittedCallback || (requireCompleted || index < receipt.Attempts.Length - 1) && attempt.DrainedCallback is null)
            {
                throw new InvalidDataException("Counter attempt did not follow an ordinary bounded submission/full-drain boundary.");
            }
            if (attempt.DrainedCallback is not null && attempt.Contacts.IsEmpty)
            {
                throw new InvalidDataException("A drained trigger attempt requires its actual committed ordinary primary contact.");
            }
            if (attempt.PreparationRepair is { } repair)
            {
                ValidatePreparationRepair(repair);
                if (receipt.Execution == 1 && index == 0 || repair.CallbackBefore > attempt.SubmittedCallback || index > 0 && repair.CallbackBefore < previousCallback)
                {
                    throw new InvalidDataException("Counter repair did not occur between a real full drain and the next ordinary input.");
                }
            }
            long limit = attempt.DrainedCallback ?? long.MaxValue;
            foreach (BattleDiagnosticCounterContact contact in attempt.Contacts)
            {
                BattleDiagnosticCounterPrimary primary = contact.Primary;
                if (primary.Serial <= previousPrimary || primary.Callback < attempt.SubmittedCallback || primary.Callback > limit)
                {
                    throw new InvalidDataException("Counter committed primary-contact lineage regressed or crossed an attempt boundary.");
                }
                previousPrimary = primary.Serial;
                ValidateContact(receipt, contact);
            }
            foreach (var observation in attempt.Callbacks)
            {
                if (observation.Callback < attempt.SubmittedCallback || observation.Callback > limit)
                {
                    throw new InvalidDataException("Counter callback observation crossed its actual attempt boundary.");
                }
                ValidateCallbackPacket(observation.Queue);
                if (observation.Queue.Callback > observation.Callback || observation.Actor.Index != observation.Queue.ActorIndex || observation.Actor.Variables.IsDefault || observation.Actor.Variables.Length != 8 || attempt.Callbacks.Any(other => other.Queue.Epoch == observation.Queue.Epoch && !SameQueue(other.Queue, observation.Queue)))
                {
                    throw new InvalidDataException("Counter callback phase changed its physical source epoch or actor state owner.");
                }
                if (observation.Phase == BattleDiagnosticCounterCallbackPhase.Queued)
                {
                    if (observation.Queue.Epoch <= previousEpoch)
                    {
                        throw new InvalidDataException("Counter physical callback queue epoch regressed.");
                    }
                    previousEpoch = observation.Queue.Epoch;
                    if (observation.Queue.ActorIndex == receipt.TargetIndex && observation.Queue.Stage == 2)
                    {
                        var sources = attempt.Contacts.Where(contact => contact.Primary.Serial == observation.Queue.PrimarySerial).ToArray();
                        if (sources.Length != 1 || observation.Queue.PendingOrder != sources[0].Primary.Recipient.PendingReaction.Order || observation.Queue.Callback < sources[0].Primary.Callback)
                        {
                            throw new InvalidDataException("Counter queued callback lacks its exact actual primary/pending-order source.");
                        }
                    }
                }
                if (observation.Phase == BattleDiagnosticCounterCallbackPhase.AiEntered)
                {
                    if (observation.AiScope is not long scope || scope <= previousAiScope)
                    {
                        throw new InvalidDataException("Counter accepted AI scope did not retain a fresh native activation identity.");
                    }
                    previousAiScope = scope;
                }
            }
            foreach (var queued in attempt.Commands)
            {
                if (queued.Callback < attempt.SubmittedCallback || queued.Callback > limit)
                {
                    throw new InvalidDataException("Counter queued command crossed its actual attempt boundary.");
                }
                ValidateCommandSource(receipt, attempt, queued);
            }
            if (attempt.Dispatch is { } dispatch)
            {
                focalCount++;
                if (index != receipt.Attempts.Length - 1 || dispatch.Callback < attempt.SubmittedCallback || dispatch.Callback > limit || dispatch.ActorId != receipt.TargetId || dispatch.Ability != BattleAbility.EnemyAbility || dispatch.ActionId != 230 || dispatch.NativeCommand != 8 || dispatch.RequestedEffect != 178 || dispatch.ExecutedEffect != 178 || dispatch.NativeActorIndex != receipt.TargetIndex || dispatch.NativeAiCallbackStage != 2 || !SameSha(dispatch.NativeAiScriptSha256, receipt.Declaration.Focal.ScriptSha256) || dispatch.Reflection is not null)
                {
                    throw new InvalidDataException("Counter focal dispatch lost its exact actual enemy callback2 invocation identity.");
                }
                BattleDiagnosticCounterCommand[] matches = attempt.Commands.Where(command => command.Command == 8 && command.ActionId == 230 && command.EffectId == 178 && command.HeaderIndex == dispatch.NativeHeaderIndex && command.HeaderGeneration == dispatch.NativeHeaderGeneration).ToArray();
                if (matches.Length != 1 || attempt.DispatchPackets is null)
                {
                    throw new InvalidDataException("Counter focal dispatch requires exactly one accepted source command and canonical packet proof.");
                }
                attempt.DispatchPackets.NormalizeTargetMask(dispatch, matches[0]);
            }
            else if (attempt.DispatchPackets is not null || attempt.Commands.Any(command => command.Command == 8 && command.ActionId == 230 && command.EffectId == 178) && attempt.DrainedCallback is not null)
            {
                throw new InvalidDataException("Counter accepted focal command drained without its real effect dispatch.");
            }
            previousCallback = attempt.DrainedCallback ?? attempt.SubmittedCallback;
        }
        if (requireCompleted && (focalCount != 1 || receipt.CompletedCallback < previousCallback) || focalCount > 1)
        {
            throw new InvalidDataException("A completed counter cycle requires exactly one actual focal response followed by full native drain.");
        }
    }

    private static void ValidateContact(DiagnosticCounterCycleReceipt cycle, BattleDiagnosticCounterContact contact)
    {
        BattleDiagnosticCounterPrimary primary = contact.Primary;
        BattleDiagnosticCounterActor recipient = primary.Recipient;
        NativeBattleActionHeaderSlot header = Header(contact.Packets, primary.HeaderIndex);
        int count = header.ReadByte(16) * (header.ReadByte(17) + 1);
        if (primary.HeaderGeneration <= 0 || primary.ActorId != cycle.ActorId || primary.ActorIndex != cycle.ActorIndex || primary.RecipientId != cycle.TargetId || primary.RecipientIndex != cycle.TargetIndex || primary.QueuedCommand != 1 || primary.ActionId != 0 || primary.InternalCommand is not (1 or 253) || contact.TriggerRecomputed != (primary.InternalCommand == 253) || recipient.Id != cycle.TargetId || recipient.Index != cycle.TargetIndex || recipient.ModelId != 84 || !recipient.Active || recipient.Hp != primary.AfterHp || recipient.Hp <= 0 || recipient.Variables.IsDefault || recipient.Variables.Length != 8 || recipient.LastAction is not { Command: 1, ActionId: 0 } memory || memory.AttackerIndex != cycle.ActorIndex || recipient.PendingReaction is not { Stage: 2, Order: not null } || header.ReadByte(0) != cycle.ActorIndex || header.ReadByte(1) != 1 || Word(header, 4) != 0 || Word(header, 6) != 0 || header.FirstContactIndex is not int first || contact.ContactIndex < first || contact.ContactIndex >= first + count || contact.Packets.ReadContact(contact.ContactIndex).Primary.ActorIndex != cycle.TargetIndex)
        {
            throw new InvalidDataException("Counter trigger is not the actually committed ordinary primary/header/B98/pending2 source chain.");
        }
    }

    private static void ValidateCallbackPacket(BattleDiagnosticCounterCallback queue)
    {
        if (queue.Epoch <= 0 || queue.Callback < 0 || queue.Location is null || queue.Location.Bank >= 3 || queue.Location.Slot >= 11 || queue.ActorIndex >= 7 || queue.Packet.IsDefault || queue.Packet.Length != 12 || (queue.UnknownByteMask & ~0xFFF) != 0 || (queue.UnknownByteMask & 0x33) != 0 || queue.Packet[0] != queue.ActorIndex || queue.Packet[1] != 255 || (queue.Packet[4] | queue.Packet[5] << 8) != queue.Stage)
        {
            throw new InvalidDataException("Counter callback lacks its actual physical bank/slot/epoch callback packet.");
        }
    }

    private static void ValidateCommandSource(DiagnosticCounterCycleReceipt cycle, DiagnosticCounterAttemptReceipt attempt, BattleDiagnosticCounterCommand command)
    {
        if (command.AiStage != 2 || command.ActorId != cycle.TargetId || command.ActorIndex != cycle.TargetIndex || !SameSha(command.ScriptSha256, cycle.Declaration.Focal.ScriptSha256) || command.Command != 8 || command.ActionId is not (230 or 232) || command.EffectId != (command.ActionId == 230 ? 178 : 0) || command.Sequence != (command.ActionId == 230 ? 13 : 15) || command.HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || command.HeaderGeneration <= 0)
        {
            throw new InvalidDataException("Counter callback produced an undeclared native reaction command.");
        }
        BattleDiagnosticCounterCallbackObservation[] entries = attempt.Callbacks.Where(value => value.Queue.Epoch == command.CallbackEpoch && value.Phase == BattleDiagnosticCounterCallbackPhase.AiEntered && value.AiScope == command.AiScope && value.AiStage == 2).ToArray();
        if (entries.Length != 1)
        {
            throw new InvalidDataException("Counter queued command lacks exactly one actual admitted callback2 AI scope.");
        }
        var entry = entries[0];
        var queue = entry.Queue;
        if (queue.ActorIndex != cycle.TargetIndex || queue.Stage != 2 || queue.PrimarySerial is not long primarySerial || entry.Actor.Id != cycle.TargetId || entry.Actor.Index != cycle.TargetIndex || entry.Actor.ModelId != 84 || !entry.Actor.Active || (entry.Actor.SavedStatuses & 0x25) != 0 || (entry.Actor.BattleStatuses & 9) != 0 || (entry.Actor.CombatantFlags & 0x4000) != 0 || entry.Actor.LastAction is not { Command: 1, ActionId: 0 } lastAction || lastAction.AttackerIndex != cycle.ActorIndex || !SameSha(entry.ScriptSha256, command.ScriptSha256) || entry.Callback > command.Callback)
        {
            throw new InvalidDataException("Counter actual admitted AI gates/B98 owner differ from the declared primary reaction.");
        }
        var contacts = attempt.Contacts.Where(value => value.Primary.Serial == primarySerial).ToArray();
        if (contacts.Length != 1 || queue.PendingOrder != contacts[0].Primary.Recipient.PendingReaction.Order || queue.Callback < contacts[0].Primary.Callback || command.HeaderGeneration <= contacts[0].Primary.HeaderGeneration)
        {
            throw new InvalidDataException("Counter physical callback epoch does not own its actual committed reaction order.");
        }
        BattleDiagnosticCounterCallbackPhase[] phases = [BattleDiagnosticCounterCallbackPhase.Queued, BattleDiagnosticCounterCallbackPhase.Taken, BattleDiagnosticCounterCallbackPhase.Started, BattleDiagnosticCounterCallbackPhase.AiEntered];
        int cursor = -1;
        foreach (var phase in phases)
        {
            int next = -1;
            for (int index = cursor + 1; index < attempt.Callbacks.Length; index++)
            {
                var observed = attempt.Callbacks[index];
                if (observed.Queue.Epoch == queue.Epoch && observed.Phase == phase && (phase != BattleDiagnosticCounterCallbackPhase.AiEntered || observed.AiScope == command.AiScope))
                {
                    next = index;
                    break;
                }
            }
            if (next < 0 || attempt.Callbacks.Count(observed => observed.Queue.Epoch == queue.Epoch && observed.Phase == phase && (phase != BattleDiagnosticCounterCallbackPhase.AiEntered || observed.AiScope == command.AiScope)) != 1)
            {
                throw new InvalidDataException("Counter queue/take/callback/admission source phases are incomplete or out of order.");
            }
            cursor = next;
        }
        if (attempt.DrainedCallback is not null)
        {
            int exited = -1;
            int completed = -1;
            for (int index = cursor + 1; index < attempt.Callbacks.Length; index++)
            {
                var observation = attempt.Callbacks[index];
                if (observation.Queue.Epoch == queue.Epoch && observation.Phase == BattleDiagnosticCounterCallbackPhase.AiExited && observation.AiScope == command.AiScope)
                {
                    exited = index;
                }
                if (observation.Queue.Epoch == queue.Epoch && observation.Phase == BattleDiagnosticCounterCallbackPhase.Completed)
                {
                    completed = index;
                }
            }
            if (exited < 0 || completed <= exited || attempt.Callbacks.Count(observed => observed.Queue.Epoch == queue.Epoch && observed.Phase == BattleDiagnosticCounterCallbackPhase.AiExited && observed.AiScope == command.AiScope) != 1 || attempt.Callbacks.Count(observed => observed.Queue.Epoch == queue.Epoch && observed.Phase == BattleDiagnosticCounterCallbackPhase.Completed) != 1)
            {
                throw new InvalidDataException("Drained counter command lacks the actual completed AI/callback source phases.");
            }
            var exit = attempt.Callbacks[exited];
            if (exit.AiStage != 2 || !SameSha(exit.ScriptSha256, command.ScriptSha256) || exit.Actor.Id != cycle.TargetId || exit.Actor.Index != cycle.TargetIndex || exit.Actor.ModelId != 84 || exit.Actor.Variables.IsDefault || exit.Actor.Variables.Length != 8)
            {
                throw new InvalidDataException("Counter AI exit phase changed its actual stage/script/physical variable owner.");
            }
            uint incremented = unchecked(entry.Actor.Variables[0] + 1);
            if (command.ActionId == 230 ? incremented < 5 || exit.Actor.Variables[0] != 0 : entry.Actor.Variables[1] != 1 || exit.Actor.Variables[0] != entry.Actor.Variables[0])
            {
                throw new InvalidDataException("Counter source variables differ from the exact admitted increment/reset or alternate-self branch.");
            }
        }
        ushort expectedSelected = checked((ushort)(1 << (command.ActionId == 230 ? cycle.ActorIndex : cycle.TargetIndex)));
        ushort expectedQueued = (ushort)(expectedSelected | ((command.AttackFlags & 0x80) != 0 ? 0x4000 : 0));
        if (command.SelectedTargetMask != expectedSelected || command.QueuedTargetMask != expectedQueued)
        {
            throw new InvalidDataException("Counter native selected and queued recipient masks differ from the actual last-attacker/self branch.");
        }
    }

    public static void ValidateChain(DiagnosticCounterCycleReceipt first, DiagnosticCounterCycleReceipt second, BattleInput input)
    {
        Validate(first, input);
        Validate(second, input);
        if (second.Attempts[0].PreparationRepair is { } preparation && preparation.CallbackBefore < first.CompletedCallback)
        {
            throw new InvalidDataException("Second counter cycle was repaired before the first actual focal cleanup.");
        }
        var one = first.Attempts[^1].Dispatch!;
        var two = second.Attempts[^1].Dispatch!;
        if (first.Execution != 1 || second.Execution != 2 || first.Declaration != second.Declaration || first.ActorId != second.ActorId || first.ActorIndex != second.ActorIndex || first.TargetId != second.TargetId || first.TargetIndex != second.TargetIndex || second.StartedCallback < first.CompletedCallback || two.Serial <= one.Serial || two.Callback < one.Callback || first.Attempts.SelectMany(value => value.Contacts).Max(value => value.Primary.Serial) >= second.Attempts.SelectMany(value => value.Contacts).Min(value => value.Primary.Serial) || first.Attempts.SelectMany(value => value.Callbacks).Max(value => value.Queue.Epoch) >= second.Attempts.SelectMany(value => value.Callbacks).Min(value => value.Queue.Epoch))
        {
            throw new InvalidDataException("Counter cycles did not preserve one uninterrupted distinct native source lineage.");
        }
    }

    public static void ValidatePreparationRepair(BattleDiagnosticRepairReceipt repair)
    {
        if (repair.LifecycleId == Guid.Empty || repair.Frame < 0 || repair.CallbackBefore <= 0 || repair.CallbackBefore != repair.CallbackAfter || repair.RestoreCount != 0 || repair.ResetCount != 0 || repair.RandomBefore is null || repair.RandomAfter is null || repair.ItemsBefore is null || repair.ItemsAfter is null || repair.Changes is null || repair.Changes.Length is < 1 or > 3 || string.IsNullOrWhiteSpace(repair.Scope) || !SameJson(repair.RandomBefore, repair.RandomAfter) || !SameJson(repair.ItemsBefore, repair.ItemsAfter))
        {
            throw new InvalidDataException("Counter preparation changed its callback/RNG/item/lifecycle owners or omitted the actual repair source.");
        }
        HashSet<string> changed = new(StringComparer.Ordinal);
        foreach (BattleDiagnosticRepairChange change in repair.Changes)
        {
            change.Request.Validate();
            var before = change.Before;
            var after = change.After;
            var request = change.Request;
            if (!changed.Add(request.ActorId) || before.Id != request.ActorId || after.Id != request.ActorId || before.NativeIndex is not (>= 0 and < 3) || after.NativeIndex != before.NativeIndex || after.ModelId != before.ModelId || !SameJson(before.NativeAiScriptSha256, after.NativeAiScriptSha256) || before.MaximumHp != after.MaximumHp || request.HitPoints > before.MaximumHp || after.CurrentHp != request.HitPoints || !SameJson(before.MagicStocks, after.MagicStocks) || (before.Statuses1 & 0x80010000) != 0)
            {
                throw new InvalidDataException("Counter preparation can only repair its actual declared player HP/status owner without changing resources or identity.");
            }
            uint saved = (uint)(before.Statuses0 & ~(request.RemoveStatuses0 | (request.Revive ? 1 : 0))) & 0xFCFF;
            if (request.HitPoints < (before.MaximumHp >> 1))
            {
                saved |= request.HitPoints < (before.MaximumHp >> 2) ? 0x300u : 0x200u;
            }
            if (!request.Revive && (before.CurrentHp == 0 || (before.Statuses0 & 1) != 0) || after.Statuses0 != saved || after.Statuses1 != (before.Statuses1 & ~request.RemoveStatuses1))
            {
                throw new InvalidDataException("Counter preparation differs from the existing declared player revival/status/HP writer.");
            }
        }
    }

    private static bool SameJson<T>(T left, T right) => System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(left), System.Text.Json.JsonSerializer.SerializeToElement(right));

    private static bool SameQueue(BattleDiagnosticCounterCallback left, BattleDiagnosticCounterCallback right) => left.Epoch == right.Epoch && left.Callback == right.Callback && left.Location == right.Location && left.ActorIndex == right.ActorIndex && left.Stage == right.Stage && left.PendingOrder == right.PendingOrder && left.PrimarySerial == right.PrimarySerial && left.UnknownByteMask == right.UnknownByteMask && left.Packet.AsSpan().SequenceEqual(right.Packet.AsSpan());

    private static bool SameSha(string? left, string? right) => left is not null && right is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
