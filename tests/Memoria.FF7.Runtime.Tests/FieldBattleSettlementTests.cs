using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class FieldBattleSettlementTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_SETTLEMENT_SAVE");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fixture))
        {
            Console.WriteLine("SKIP installed field settlements: selected original resources or continuation fixture are unset.");
            return;
        }
        byte[] originalBytes = File.ReadAllBytes(fixture);
        using MemoryStream input = new(originalBytes);
        CombinedSessionSave normalized = CombinedSessionCodec.Read(input);
        Ff7SessionSave host = normalized.Ff7!;
        Ff7GuestBattleSettlementReceipt escaped = normalized.GuestBattleSettlements!.Last();
        Check(host.Battle is null && escaped.Outcome == Ff7BattleOutcome.Escaped, "The selected fixture must precede the next encounter after a committed field escape.");
        Check(host.Progress.BattleEscapes == 19 && host.Progress.CompletedBattles.Length == 69 && escaped.SettledHp == 1947, "The reached continuation must retain its recorded escape/win/guest HP facts.");
        Ff7FieldBattleResultReceipt owner = new(escaped.FieldGeneration, escaped.RequestId, escaped.FormationId, escaped.Outcome);
        Check(host.Progress.FieldBattleResults!.Contains(owner), "Codec load did not project the proved legacy field result owner.");
        CombinedSessionSave legacy = normalized with { Ff7 = host with { Progress = host.Progress with { FieldBattleResults = null } } };
        Ff7SessionSave legacyHost = legacy.Ff7!;
        legacy.Validate();
        Check(!JsonSerializer.Serialize(legacy.Ff7!.Progress).Contains("FieldBattleResults", StringComparison.Ordinal), "Absent optional field results must preserve the old serialized progress shape.");
        CombinedSessionSave restored = legacy.RestoreLegacyFieldBattleResults();
        Check(JsonSerializer.Serialize(restored) == JsonSerializer.Serialize(normalized) && JsonSerializer.Serialize(restored.RestoreLegacyFieldBattleResults()) == JsonSerializer.Serialize(restored), "Legacy owner projection must be deterministic and idempotent.");
        Check(JsonSerializer.Serialize(restored.Ff7!.Progress with { FieldBattleResults = null }) == JsonSerializer.Serialize(legacyHost.Progress) && restored.Ff8Payload!.SequenceEqual(legacy.Ff8Payload!), "Compatibility projection changed recorded progress, RNG, inventory or guest HP/status bytes.");
        foreach (int reply in new[] { 0, 1, 42 })
        {
            CombinedSessionSave overridden = legacy with { Ff7 = legacyHost with { Process = legacyHost.Process with { Receipts = legacyHost.Process.Receipts.Select(receipt => receipt.Request.Id == escaped.RequestId ? receipt with { Reply = new(true, reply) } : receipt).ToArray() } } };
            Check(overridden.RestoreLegacyFieldBattleResults().Ff7!.Progress.FieldBattleResults!.Single(result => result.RequestId == escaped.RequestId).Outcome == Ff7BattleOutcome.Escaped, "A native result-byte override must not reclassify the explicit saved guest outcome.");
        }
        CombinedSessionSave legacyDefeat = legacy with { GuestBattleSettlements = legacy.GuestBattleSettlements!.Select(guest => guest.EncounterId == escaped.EncounterId ? guest with { Outcome = Ff7BattleOutcome.Defeat, SettledHp = 0 } : guest).ToArray() };
        Check(legacyDefeat.RestoreLegacyFieldBattleResults().Ff7!.Progress.FieldBattleResults!.Single(result => result.RequestId == escaped.RequestId).Outcome == Ff7BattleOutcome.Defeat, "Legacy projection must preserve the explicit defeat outcome without guessing from the completed host result byte.");
        Check(host.Progress.FieldBattleResults!.All(field => !host.Progress.CompletedBattles.Any(victory => victory.FieldGeneration == field.FieldGeneration && victory.RequestId == field.RequestId) && !host.Progress.WorldBattleResults.Any(world => world.FieldGeneration == field.FieldGeneration && world.RequestId == field.RequestId)), "Victory and world results must retain their existing owners.");
        Reject(() => (normalized with { Ff7 = host with { Progress = host.Progress with { FieldBattleResults = [owner with { Outcome = Ff7BattleOutcome.Defeat }] } } }).Validate());
        Reject(() => (host.Progress with { FieldBattleResults = [owner, owner] }).Validate());
        Reject(() => (host.Progress with { FieldBattleResults = [owner with { Outcome = Ff7BattleOutcome.Victory }] }).Validate());
        Ff7BattleReceipt victoryOwner = host.Progress.CompletedBattles.Last();
        Reject(() => (host.Progress with { FieldBattleResults = [owner, new(victoryOwner.FieldGeneration, victoryOwner.RequestId, victoryOwner.Formation, Ff7BattleOutcome.Escaped)] }).Validate());
        Ff7WorldBattleResultReceipt worldOwner = host.Progress.WorldBattleResults.Last();
        Reject(() => (host.Progress with { FieldBattleResults = [owner, new(worldOwner.FieldGeneration, worldOwner.RequestId, worldOwner.Formation, worldOwner.Outcome)] }).Validate());
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session session = Ff7Session.Restore(installation, host);
        session.AdvanceTick(false);
        Check(session.Host.Capture().Receipts.All(receipt => receipt.Request.Id != escaped.RequestId), "The regression must actually retire the volatile completed host receipt.");
        CombinedSessionSave retired = normalized with { Ff7 = session.Capture() };
        retired.Validate();
        CombinedSessionSave coldEnvelope = RoundTrip(retired);
        Check(coldEnvelope.Ff8Payload!.SequenceEqual(normalized.Ff8Payload!) && coldEnvelope.Ff7!.Progress.FieldBattleResults!.Contains(owner), "Retirement/codec lost the durable owner or changed the guest HP/status payload.");
        using Ff7Session cold = Ff7Session.Restore(installation, coldEnvelope.Ff7!);
        session.AdvanceTick(false);
        cold.AdvanceTick(false);
        Equal(session.Capture(), cold.Capture(), "Retired escape cold continuation");
        Check(session.Progress.BattleEscapes == 19 && session.Progress.CompletedBattles.Length == 69, "Receipt retirement invented a result or repeated escape accounting.");
        long requestId = session.Progress.NextEncounterRequestId;
        session.Host.StartEncounter(new(requestId, 2, 7, 0, new FieldBattleCommand((ushort)escaped.FormationId)), 0);
        Ff7SessionSave active = session.Capture();
        active = active with { Progress = active.Progress with { NextEncounterRequestId = requestId + 1 } };
        Check(active.Battle is not null, "The second same-field encounter did not start.");
        Reject(() => (active with { Progress = active.Progress with { FieldBattleResults = [.. active.Progress.FieldBattleResults!, new(active.FieldGeneration, requestId, escaped.FormationId, Ff7BattleOutcome.Escaped)] } }).Validate());
        CombinedSessionSave second = RoundTrip(normalized with { Ff7 = active });
        second.Validate();
        using Ff7Session nextCold = Ff7Session.Restore(installation, second.Ff7!);
        Check(nextCold.Capture().Progress.FieldBattleResults!.Contains(owner), "Starting another same-generation battle invalidated the previous guest settlement owner.");
        Ff7BattleSnapshot terminal = active.Battle! with { Outcome = Ff7BattleOutcome.Escaped, TerminalFrames = 45 };
        using Ff7Session defeat = Ff7Session.Restore(installation, active with { Battle = terminal with { Outcome = Ff7BattleOutcome.Defeat, RequestMode = 1, Actors = terminal.Actors.Select(actor => actor.Actor.Slot < 3 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray() } });
        Check(defeat.ConfirmBattleDefeat() && !defeat.ConfirmBattleDefeat() && defeat.Progress.FieldBattleResults!.Single(result => result.RequestId == requestId).Outcome == Ff7BattleOutcome.Defeat && defeat.Progress.BattleEscapes == 19, "Returning field defeat must commit its native outcome once without incrementing escapes.");
        using Ff7Session completion = Ff7Session.Restore(installation, active with { Battle = terminal });
        for (int tick = 0; tick < 300 && !completion.Battle!.ResultReady; tick++)
        {
            completion.AdvanceTick(false);
        }
        Check(completion.ConfirmBattleEscape() && !completion.ConfirmBattleEscape(), "A new field escape must commit once.");
        Ff7FieldBattleResultReceipt nextOwner = new(host.FieldGeneration, requestId, escaped.FormationId, Ff7BattleOutcome.Escaped);
        Check(completion.Progress.FieldBattleResults!.Count(result => result == nextOwner) == 1 && completion.Progress.BattleEscapes == 20 && completion.Progress.CompletedBattles.Length == 69, "Future field completion did not commit exactly one durable nonvictory owner without rewards.");
        completion.AdvanceTick(false);
        Check(completion.Host.Capture().Receipts.All(receipt => receipt.Request.Id != requestId), "The new completed field request did not retire.");
        Ff7GuestBattleSettlementReceipt nextGuest = escaped with { EncounterId = Guid.NewGuid(), RequestId = requestId };
        CombinedSessionSave nextEnvelope = RoundTrip(normalized with { Ff7 = completion.Capture(), GuestBattleSettlements = [.. normalized.GuestBattleSettlements!, nextGuest] });
        nextEnvelope.Validate();
        using Ff7Session after = Ff7Session.Restore(installation, nextEnvelope.Ff7!);
        after.AdvanceTick(false);
        Check(after.Progress.FieldBattleResults!.Contains(nextOwner) && after.Progress.CompletedBattles.Length == 69, "Cold continuation replayed or lost the new field escape.");
        Check(File.ReadAllBytes(fixture).SequenceEqual(originalBytes), "The standalone fixture was rewritten.");
        Console.WriteLine($"Field nonvictory durable owners, native reply overrides, retirement/next encounter, combined codec/JSON/cold and once-only accounting passed; source {Convert.ToHexString(SHA256.HashData(originalBytes))} unchanged.");
    }

    private static CombinedSessionSave RoundTrip(CombinedSessionSave source)
    {
        using MemoryStream encoded = new();
        CombinedSessionCodec.Write(encoded, source);
        encoded.Position = 0;
        return CombinedSessionCodec.Read(encoded);
    }

    private static void Equal(Ff7SessionSave first, Ff7SessionSave second, string boundary)
    {
        Check(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), $"{boundary} changed after cold restore.");
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("An invalid durable field owner was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
