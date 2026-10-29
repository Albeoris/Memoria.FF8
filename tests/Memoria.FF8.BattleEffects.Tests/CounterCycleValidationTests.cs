using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class CounterCycleValidationTests
{
    internal static BattleInput Input => new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.Attack, ActionId: 0);
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };

    internal static DiagnosticCounterCycleReceipt Cycle(int execution = 1)
    {
        // Component-only receipt/codec fixture; this does not establish original-resource action admission.
        long start = execution * 10;
        long epoch = execution;
        long primarySerial = execution;
        long generation = 2 * execution - 1;
        DiagnosticCounterCycleDeclaration declaration = new(new("enemy0", 8, 230, 178, 2, DiagnosticCounterCycleDeclaration.SupportedScriptSha256), 32);
        NativeBattleActionPacketsState Packets(byte actor, byte command, ushort action, ushort effect, byte sequence, byte target)
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(actor, command, sequence, -1, action, effect, null));
            (packets, _) = packets.AppendContact(new(new(target, sequence, 0, 0, 0, 10, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
        }
        BattleDiagnosticCounterActor recipient = new("enemy0", 3, 84, 100 - execution * 10, true, 0, new(0, 0, 1, 0, 0, 0), new(2, 0), [4, 0, 0, 0, 0, 0, 0, 0]);
        BattleDiagnosticCounterPrimary primary = new(primarySerial, start + 1, "party0", 0, "enemy0", 3, 1, 0, 1, 110 - execution * 10, 100 - execution * 10, 0, generation, recipient);
        BattleDiagnosticCounterContact contact = new(primary, 0, false, Packets(0, 1, 0, 0, 4, 3));
        byte[] bytes = new byte[12];
        bytes[0] = 3;
        bytes[1] = 255;
        bytes[4] = 2;
        BattleDiagnosticCounterCallback queue = new(epoch, start + 1, new(0, 0), 3, 2, 0, primarySerial, bytes.ToImmutableArray(), 0);
        BattleDiagnosticCounterCallbackObservation Phase(BattleDiagnosticCounterCallbackPhase phase) => new(phase, phase == BattleDiagnosticCounterCallbackPhase.Queued ? start + 1 : start + 2, queue, recipient with { PendingReaction = new(0, 0), Variables = phase == BattleDiagnosticCounterCallbackPhase.AiExited ? recipient.Variables.SetItem(0, 0) : recipient.Variables }, phase is BattleDiagnosticCounterCallbackPhase.AiEntered or BattleDiagnosticCounterCallbackPhase.AiExited ? 2 : null, phase is BattleDiagnosticCounterCallbackPhase.AiEntered or BattleDiagnosticCounterCallbackPhase.AiExited ? epoch : null, phase is BattleDiagnosticCounterCallbackPhase.AiEntered or BattleDiagnosticCounterCallbackPhase.AiExited ? declaration.Focal.ScriptSha256 : null);
        BattleDiagnosticCounterCommand command = new(start + 2, epoch, epoch, 2, declaration.Focal.ScriptSha256, "enemy0", 3, 8, 230, 178, 13, 1, 1, 0, 0, generation + 1);
        BattleDiagnosticEffectDispatch dispatch = new(execution, start + 3, "enemy0", BattleAbility.EnemyAbility, 230, 178, 178, 8) { NativeAiCallbackStage = 2, NativeAiScriptSha256 = declaration.Focal.ScriptSha256, NativeTargetMask = 1, NativeActorIndex = 3, NativeHeaderIndex = 0, NativeHeaderGeneration = generation + 1 };
        DiagnosticCounterAttemptReceipt attempt = new(1, start)
        {
            DrainedCallback = start + 5,
            Contacts = [contact],
            Callbacks = [Phase(BattleDiagnosticCounterCallbackPhase.Queued), Phase(BattleDiagnosticCounterCallbackPhase.Taken), Phase(BattleDiagnosticCounterCallbackPhase.Started), Phase(BattleDiagnosticCounterCallbackPhase.AiEntered), Phase(BattleDiagnosticCounterCallbackPhase.AiExited), Phase(BattleDiagnosticCounterCallbackPhase.Completed)],
            Commands = [command],
            Dispatch = dispatch,
            DispatchPackets = new(dispatch.Serial, dispatch.Callback, 0, generation + 1, Packets(3, 8, 230, 178, 13, 0))
        };
        return new(execution, declaration, "party0", 0, "enemy0", 3, start) { CompletedCallback = start + 5, Attempts = [attempt] };
    }

    [Fact]
    public void Exact_source_chain_accepts_primary_index_zero_and_round_trips_strictly()
    {
        DiagnosticCounterCycleReceipt restored = JsonSerializer.Deserialize<DiagnosticCounterCycleReceipt>(JsonSerializer.Serialize(Cycle(), Json), Json)!;
        DiagnosticCounterCycleValidation.Validate(restored, Input);
        DiagnosticCounterCycleValidation.ValidateChain(restored, Cycle(2), Input);
        Assert.Equal((ushort)1, restored.Attempts[0].DispatchPackets!.NormalizeTargetMask(restored.Attempts[0].Dispatch!, restored.Attempts[0].Commands[0]));
    }

    [Theory]
    [InlineData("b98")]
    [InlineData("pending_order")]
    [InlineData("primary_actor")]
    [InlineData("header_command")]
    [InlineData("header_unknown")]
    [InlineData("contact_index")]
    [InlineData("queue_epoch")]
    [InlineData("queue_payload")]
    [InlineData("queue_unknown")]
    [InlineData("queue_high_unknown")]
    [InlineData("counter_threshold")]
    [InlineData("counter_reset")]
    [InlineData("exit_stage")]
    [InlineData("exit_actor")]
    [InlineData("queue_phase")]
    [InlineData("gate_saved")]
    [InlineData("gate_battle")]
    [InlineData("gate_flags")]
    [InlineData("script")]
    [InlineData("selected_mask")]
    [InlineData("queued_mask")]
    [InlineData("source_generation")]
    [InlineData("dispatch_mask")]
    [InlineData("dispatch_ability")]
    [InlineData("missing_packet")]
    [InlineData("missing_completion")]
    public void Forged_or_missing_native_source_links_are_rejected(string change)
    {
        DiagnosticCounterCycleReceipt cycle = Cycle();
        DiagnosticCounterAttemptReceipt attempt = cycle.Attempts[0];
        BattleDiagnosticCounterContact contact = attempt.Contacts[0];
        var entry = attempt.Callbacks[3];
        var source = attempt.Commands[0];
        switch (change)
        {
            case "b98":
                contact = contact with { Primary = contact.Primary with { Recipient = contact.Primary.Recipient with { LastAction = contact.Primary.Recipient.LastAction! with { AttackerIndex = 1 } } } };
                break;
            case "pending_order":
                contact = contact with { Primary = contact.Primary with { Recipient = contact.Primary.Recipient with { PendingReaction = new(2, 1) } } };
                break;
            case "primary_actor":
                contact = contact with { Primary = contact.Primary with { ActorIndex = 1 } };
                break;
            case "header_command":
            case "header_unknown":
                var header = contact.Packets.Headers[0];
                header = change == "header_command" ? header with { Payload = header.Payload.SetItem(1, 8) } : header with { UnknownByteMask = 2 };
                contact = contact with { Packets = contact.Packets with { Headers = contact.Packets.Headers.SetItem(0, header) } };
                break;
            case "contact_index":
                contact = contact with { ContactIndex = 1 };
                break;
            case "queue_epoch":
                entry = entry with { Queue = entry.Queue with { Epoch = 99 } };
                break;
            case "queue_payload":
                entry = entry with { Queue = entry.Queue with { Packet = entry.Queue.Packet.SetItem(4, 3) } };
                break;
            case "queue_unknown":
                entry = entry with { Queue = entry.Queue with { UnknownByteMask = 1 } };
                break;
            case "queue_high_unknown":
                entry = entry with { Queue = entry.Queue with { UnknownByteMask = 0x1000 } };
                break;
            case "counter_threshold":
                entry = entry with { Actor = entry.Actor with { Variables = entry.Actor.Variables.SetItem(0, 3) } };
                break;
            case "counter_reset":
                var reset = attempt.Callbacks[4];
                attempt = attempt with { Callbacks = attempt.Callbacks.SetItem(4, reset with { Actor = reset.Actor with { Variables = reset.Actor.Variables.SetItem(0, 5) } }) };
                break;
            case "exit_stage":
                attempt = attempt with { Callbacks = attempt.Callbacks.SetItem(4, attempt.Callbacks[4] with { AiStage = 4 }) };
                break;
            case "exit_actor":
                attempt = attempt with { Callbacks = attempt.Callbacks.SetItem(4, attempt.Callbacks[4] with { Actor = attempt.Callbacks[4].Actor with { Index = 0 } }) };
                break;
            case "queue_phase":
                attempt = attempt with { Callbacks = attempt.Callbacks.RemoveAt(1) };
                break;
            case "gate_saved":
                entry = entry with { Actor = entry.Actor with { SavedStatuses = 1 } };
                break;
            case "gate_battle":
                entry = entry with { Actor = entry.Actor with { BattleStatuses = 1 } };
                break;
            case "gate_flags":
                entry = entry with { Actor = entry.Actor with { CombatantFlags = 0x4000 } };
                break;
            case "script":
                source = source with { ScriptSha256 = new string('0', 64) };
                break;
            case "selected_mask":
                source = source with { SelectedTargetMask = 8 };
                break;
            case "queued_mask":
                source = source with { QueuedTargetMask = 0x4001 };
                break;
            case "source_generation":
                source = source with { HeaderGeneration = 1 };
                break;
            case "dispatch_mask":
                attempt = attempt with { Dispatch = attempt.Dispatch! with { NativeTargetMask = 0 } };
                break;
            case "dispatch_ability":
                attempt = attempt with { Dispatch = attempt.Dispatch! with { Ability = BattleAbility.Attack } };
                break;
            case "missing_packet":
                attempt = attempt with { DispatchPackets = null };
                break;
            case "missing_completion":
                attempt = attempt with { Callbacks = attempt.Callbacks.RemoveAt(5) };
                break;
        }
        attempt = attempt with { Contacts = [contact], Commands = [source] };
        if (change != "queue_phase" && change != "missing_completion")
        {
            attempt = attempt with { Callbacks = attempt.Callbacks.SetItem(3, entry) };
        }
        cycle = cycle with { Attempts = [attempt] };
        Exception? failure = Record.Exception(() => DiagnosticCounterCycleValidation.Validate(cycle, Input));
        Assert.True(failure is InvalidDataException or NotSupportedException, failure?.ToString() ?? "Forged source chain was accepted.");
    }

    [Fact]
    public void Genuine_nonfocal_control_can_drain_before_the_next_attempt_without_a_fabricated_effect_zero_dispatch()
    {
        var cycle = Cycle();
        var one = cycle.Attempts[0];
        var noFocal = one with { Commands = [], Dispatch = null, DispatchPackets = null, Callbacks = one.Callbacks.SetItem(4, one.Callbacks[4] with { Actor = one.Callbacks[4].Actor with { Variables = one.Callbacks[4].Actor.Variables.SetItem(0, 5) } }) };
        var following = Cycle(2).Attempts[0] with { Number = 2 };
        var combined = cycle with { Attempts = [noFocal, following], CompletedCallback = 25 };
        DiagnosticCounterCycleValidation.Validate(combined, Input);
        Assert.Null(combined.Attempts[0].Dispatch);
        Assert.Equal(2, combined.Attempts.Length);
    }

    [Fact]
    public void Drained_retry_without_a_real_committed_control_contact_is_rejected()
    {
        var cycle = Cycle();
        var attempt = cycle.Attempts[0] with { Contacts = [], Callbacks = [], Commands = [], Dispatch = null, DispatchPackets = null };
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.Validate(cycle with { Attempts = [attempt] }, Input, false));
    }

    [Theory]
    [InlineData(uint.MaxValue, false)]
    [InlineData(3u, false)]
    [InlineData(4u, true)]
    [InlineData((uint)int.MaxValue, true)]
    public void Counter_threshold_uses_actual_unchecked_DWORD_increment_and_long_comparison(uint before, bool accepted)
    {
        var cycle = Cycle();
        var attempt = cycle.Attempts[0];
        var entry = attempt.Callbacks[3];
        attempt = attempt with { Callbacks = attempt.Callbacks.SetItem(3, entry with { Actor = entry.Actor with { Variables = entry.Actor.Variables.SetItem(0, before) } }) };
        cycle = cycle with { Attempts = [attempt] };
        if (accepted)
        {
            DiagnosticCounterCycleValidation.Validate(cycle, Input);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.Validate(cycle, Input));
        }
    }

    [Fact]
    public void Alternate_self_command_is_preserved_as_a_nonfocal_control_without_an_effect_zero_dispatch()
    {
        var cycle = Cycle();
        var attempt = cycle.Attempts[0];
        var contact = attempt.Contacts[0];
        contact = contact with { Primary = contact.Primary with { Recipient = contact.Primary.Recipient with { Variables = contact.Primary.Recipient.Variables.SetItem(1, 1) } } };
        var callbacks = attempt.Callbacks.Select(observation => observation with { Actor = observation.Actor with { Variables = observation.Actor.Variables.SetItem(0, 4).SetItem(1, 1) } }).ToImmutableArray();
        attempt = attempt with { Contacts = [contact], Callbacks = callbacks, Commands = [attempt.Commands[0] with { ActionId = 232, EffectId = 0, Sequence = 15, SelectedTargetMask = 8, QueuedTargetMask = 8 }], Dispatch = null, DispatchPackets = null };
        DiagnosticCounterCycleValidation.Validate(cycle with { Attempts = [attempt], CompletedCallback = null }, Input, false);
        var rejected = attempt with { Callbacks = callbacks.SetItem(3, callbacks[3] with { Actor = callbacks[3].Actor with { Variables = callbacks[3].Actor.Variables.SetItem(1, 0) } }) };
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.Validate(cycle with { Attempts = [rejected], CompletedCallback = null }, Input, false));
    }

    private static BattleDiagnosticRepairReceipt Repair()
    {
        BattleDiagnosticRepairRandomState random = new(NativeBattleRandomState.Initialize(0), null, null, null);
        BattleDiagnosticCombatant before = new("party0", 0, 1000, 1, 0, []) { NativeIndex = 0, ModelId = 0 };
        BattleDiagnosticCombatant after = before with { CurrentHp = 486, Statuses0 = 0x200 };
        return new(new Guid("32120000-0000-0000-0000-000000000001"), 15, 15, 15, random, random, 0, 0, [], [], [new(new("party0", 486, true), before, after)], "Component-only actual repair receipt contract.");
    }

    [Fact]
    public void Explicit_party_revival_preserves_rng_and_binds_to_the_next_submitted_attempt()
    {
        var repair = Repair();
        DiagnosticCounterCycleValidation.ValidatePreparationRepair(repair);
        var one = Cycle();
        var two = Cycle(2);
        two = two with { Attempts = [two.Attempts[0] with { PreparationRepair = repair }] };
        DiagnosticCounterCycleValidation.ValidateChain(one, two, Input);
    }

    [Theory]
    [InlineData("rng")]
    [InlineData("callback")]
    [InlineData("restore")]
    [InlineData("stock")]
    [InlineData("enemy")]
    [InlineData("hp")]
    [InlineData("status")]
    [InlineData("identity")]
    [InlineData("no_revival")]
    public void Repair_cannot_mutate_other_native_owners_or_waive_source_policy(string change)
    {
        var repair = Repair();
        var row = repair.Changes[0];
        repair = change switch
        {
            "rng" => repair with { RandomAfter = repair.RandomAfter with { Battle = repair.RandomAfter.Battle with { Indexes = repair.RandomAfter.Battle.Indexes.SetItem(0, unchecked((byte)(repair.RandomAfter.Battle.Indexes[0] + 1))) } } },
            "callback" => repair with { CallbackAfter = 16 },
            "restore" => repair with { RestoreCount = 1 },
            "stock" => repair with { ItemsAfter = [new(1, 1)] },
            "enemy" => repair with { Changes = [row with { Before = row.Before with { NativeIndex = 3 }, After = row.After with { NativeIndex = 3 } }] },
            "hp" => repair with { Changes = [row with { After = row.After with { CurrentHp = 487 } }] },
            "status" => repair with { Changes = [row with { After = row.After with { Statuses0 = 0 } }] },
            "identity" => repair with { Changes = [row with { After = row.After with { ModelId = 1 } }] },
            "no_revival" => repair with { Changes = [row with { Request = row.Request with { Revive = false } }] },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.ValidatePreparationRepair(repair));
    }

    [Fact]
    public void Counter_repair_cannot_precede_real_cleanup_or_be_injected_before_initial_admission()
    {
        var one = Cycle();
        var two = Cycle(2);
        var repair = Repair() with { CallbackBefore = 14, CallbackAfter = 14 };
        two = two with { Attempts = [two.Attempts[0] with { PreparationRepair = repair }] };
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.ValidateChain(one, two, Input));
        Assert.Throws<InvalidDataException>(() => DiagnosticCounterCycleValidation.Validate(one with { Attempts = [one.Attempts[0] with { PreparationRepair = Repair() with { CallbackBefore = 10, CallbackAfter = 10 } }] }, Input));
    }

    [Fact]
    public void Omitted_required_source_field_cannot_default_to_a_plausible_zero()
    {
        JsonObject source = JsonSerializer.SerializeToNode(Cycle(), Json)!.AsObject();
        source["attempts"]![0]!["contacts"]![0]!["primary"]!.AsObject().Remove("actor_index");
        Assert.Throws<JsonException>(() => source.Deserialize<DiagnosticCounterCycleReceipt>(Json));
    }
}
