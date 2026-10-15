using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRequestPhysicalAliasContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            foreach ((int priority, ushort word) in new (int, ushort)[] { (21, 0), (277, 0), (0, 0) })
            {
                NativeRequestPhysicalAliasBusy(vitals, economy, opcode, priority, word);
            }
            foreach ((int priority, ushort word, bool knownOwner) in new (int, ushort, bool)[] { (21, ushort.MaxValue, true), (277, ushort.MaxValue, true), (22, 0, true), (21, 0, false) })
            {
                NativeRequestPhysicalAliasRefusal(vitals, economy, opcode, priority, word, knownOwner);
            }
            NativeRequestPhysicalAliasOrdinaryRequest(vitals, economy, opcode);
        }

        var valid = CreateRequestPhysicalAliasFixture(vitals, economy, Jsm.Opcode.REQ, 21, 99999, 0, true);
        JsonElement before = valid.Engine.Capture();
        foreach (ushort? word in new ushort?[] { null, ushort.MaxValue, 0x1234 })
        {
            JsonNode unknown = JsonNode.Parse(before.GetRawText())!;
            unknown["Actors"]![1]!["NativeActorWord398"] = word;
            try
            {
                valid.Engine.Restore(JsonSerializer.SerializeToElement(unknown));
                throw new InvalidOperationException("Unproved physical WORD398 owner restored without a supported producer.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, valid.Engine.Capture()), "Rejected unknown or nonzero WORD398 owner partially changed the live VM.");
        }
        foreach ((Jsm.Opcode opcode, int parameter, int operands) in new[] { (Jsm.Opcode.POPI_L, 19, 1), (Jsm.Opcode.PSHI_L, 19, 0), (Jsm.Opcode.SETLINE, 0, 6), (Jsm.Opcode.BGDRAW, 0, 1), (Jsm.Opcode.PSHN_L, 999, 72) })
        {
            NativeRequestPhysicalHeaderRefusal(vitals, economy, opcode, parameter, operands);
        }
        Console.WriteLine("Native REQ/REQSW/REQEW physical WORD398: occupied21 and lowBYTE277 drop invalid scripts, consume both operands and yield without MOVE backup; free/unknown/unproved owners refuse at original PC and stack; in-bounds busy/free requests and independent restores pass.");
    }

    private static void NativeRequestPhysicalAliasBusy(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int priority, ushort word)
    {
        var live = CreateRequestPhysicalAliasFixture(vitals, economy, opcode, priority, 99999, word, true);
        Require(live.Engine.Actors[1].NativeActorWord398 == word, "Fresh or explicit component physical WORD398 owner was lost.");
        JsonElement? beforeTarget = null;
        JsonElement? beforeFibers = null;
        int beforeFiberCount = 0;
        int issued = 0;
        long issueFrame = -1;
        live.Engine.CommandExecuted += command =>
        {
            if (command.ObjectId != 0)
            {
                return;
            }
            if (command.Name == "NOP")
            {
                Require(live.Engine.Actors[1].NativeScriptMovement is { Active: true, Suspended: false } && live.Engine.Actors[1].NativeMovementRequest is null, "Busy request fixture did not retain an active target MOVE before issue.");
                beforeTarget = JsonSerializer.SerializeToElement(live.Engine.Actors[1]);
                JsonElement saved = live.Engine.Capture();
                beforeFibers = TargetRequestPhysicalAliasFibers(saved);
                beforeFiberCount = saved.GetProperty("Fibers").GetArrayLength();
            }
            if (command.Name == opcode.ToString())
            {
                issued++;
                issueFrame = live.Engine.Frame;
                JsonElement saved = live.Engine.Capture();
                JsonElement caller = RequestPhysicalAliasCaller(saved);
                Require(beforeTarget is not null && beforeFibers is not null && JsonElement.DeepEquals(beforeTarget.Value, JsonSerializer.SerializeToElement(live.Engine.Actors[1])) && JsonElement.DeepEquals(beforeFibers.Value, TargetRequestPhysicalAliasFibers(saved)) && saved.GetProperty("Fibers").GetArrayLength() == beforeFiberCount, "Dropped busy request changed target body, slots, PC, operands, MOVE backup or fiber allocation.");
                Require(command.Arguments.SequenceEqual(new[] { priority, 99999 }) && caller.GetProperty("Position").GetInt32() == command.Operation + 1 && caller.GetProperty("Stack").GetArrayLength() == 0 && caller.GetProperty("WaitReason").GetString() == "None" && caller.GetProperty("RequestSequence").ValueKind == JsonValueKind.Null, "Dropped request did not consume its two original DWORD operands and advance without a wait owner.");
                Require(!live.Session.State.Variables.ContainsKey(5000), "Dropped cross-entity request executed its successor before yielding.");
            }
        };
        var independent = CreateRequestPhysicalAliasFixture(vitals, economy, opcode, priority, 99999, word, true);
        for (int tick = 0; tick < 16 && !live.Session.State.Variables.ContainsKey(5000); tick++)
        {
            independent.Engine.Restore(live.Engine.Capture());
            live.Engine.Step();
            independent.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Busy physical alias request diverged from independently restored pending or completed state.");
            if (live.Engine.Frame == issueFrame)
            {
                Require(!live.Session.State.Variables.ContainsKey(5000), "Native request return3 failed to yield the remaining instruction budget.");
            }
        }
        Require(issued == 1 && live.Session.State.Variables.GetValueOrDefault(5000) == 77 && live.Engine.Frame == issueFrame + 1 && live.Engine.Actors[1].NativeMovementRequest is null && !live.Session.State.Variables.ContainsKey(5001), "Busy request failed its single-issue next-callback successor or accidentally ran the requested script.");
    }

    private static void NativeRequestPhysicalAliasRefusal(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int priority, ushort word, bool knownOwner)
    {
        var live = CreateRequestPhysicalAliasFixture(vitals, economy, opcode, priority, 99999, word, knownOwner);
        JsonElement? beforeTarget = null;
        JsonElement? beforeFibers = null;
        int requestOperation = -1;
        live.Engine.CommandExecuted += command =>
        {
            if (command.ObjectId == 0 && command.Name == "NOP")
            {
                beforeTarget = JsonSerializer.SerializeToElement(live.Engine.Actors[1]);
                beforeFibers = TargetRequestPhysicalAliasFibers(live.Engine.Capture());
                requestOperation = command.Operation + 1;
            }
        };
        bool refused = false;
        for (int tick = 0; tick < 16 && !refused; tick++)
        {
            try
            {
                live.Engine.Step();
            }
            catch (EventScriptException)
            {
                refused = true;
            }
        }
        JsonElement saved = live.Engine.Capture();
        JsonElement caller = RequestPhysicalAliasCaller(saved);
        Require(refused && requestOperation >= 0 && live.Engine.LastFault?.Operation == requestOperation && caller.GetProperty("Position").GetInt32() == requestOperation && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 99999, priority }), "Unsupported request alias changed the original PC or popped its script/priority operands.");
        Require(beforeTarget is not null && beforeFibers is not null && JsonElement.DeepEquals(beforeTarget.Value, JsonSerializer.SerializeToElement(live.Engine.Actors[1])) && JsonElement.DeepEquals(beforeFibers.Value, TargetRequestPhysicalAliasFibers(saved)) && !live.Session.State.Variables.ContainsKey(5000), "Unsupported request alias changed target state, slots or successor before refusal.");
    }

    private static void NativeRequestPhysicalAliasOrdinaryRequest(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode)
    {
        var live = CreateRequestPhysicalAliasFixture(vitals, economy, opcode, 2, 18, 0, true);
        var independent = CreateRequestPhysicalAliasFixture(vitals, economy, opcode, 2, 18, 0, true);
        for (int tick = 0; tick < 24 && (!live.Session.State.Variables.ContainsKey(5000) || !live.Session.State.Variables.ContainsKey(5001)); tick++)
        {
            independent.Engine.Restore(live.Engine.Capture());
            live.Engine.Step();
            independent.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "Ordinary free native request diverged after independent restore.");
        }
        Require(live.Session.State.Variables.GetValueOrDefault(5000) == 77 && live.Session.State.Variables.GetValueOrDefault(5001) == 55 && live.Engine.LastFault is null && live.Engine.Actors[1].NativeActorWord398 == 0, "Physical alias support changed an ordinary in-bounds request or its physical header.");
    }

    private static void NativeRequestPhysicalHeaderRefusal(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int parameter, int operandCount)
    {
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
        for (int value = 1; value <= operandCount; value++)
        {
            main.Add((Jsm.Opcode.PSHN_L, value));
        }
        main.AddRange([(Jsm.Opcode.NOP, 0), (opcode, parameter), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.LoadField(600, SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement? beforeActor = null;
        JsonElement? beforeFiber = null;
        int operation = -1;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "NOP")
            {
                beforeActor = JsonSerializer.SerializeToElement(engine.Actors[0]);
                beforeFiber = RequestPhysicalAliasCaller(engine.Capture());
                operation = command.Operation + 1;
            }
        };
        bool refused = false;
        for (int tick = 0; tick < 16 && !refused; tick++)
        {
            try
            {
                engine.Step();
            }
            catch (EventScriptException)
            {
                refused = true;
            }
        }
        JsonElement saved = engine.Capture();
        JsonElement fiber = RequestPhysicalAliasCaller(saved);
        Require(refused && operation >= 0 && engine.LastFault?.Operation == operation && fiber.GetProperty("Position").GetInt32() == operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(Enumerable.Range(1, operandCount).Reverse()), $"Unrepresented native {opcode} header write changed the original PC or operand bank.");
        Require(beforeActor is not null && beforeFiber is not null && JsonElement.DeepEquals(beforeActor.Value, JsonSerializer.SerializeToElement(engine.Actors[0])) && JsonElement.DeepEquals(beforeFiber.Value, fiber) && engine.Actors[0].NativeActorWord398 == 0, $"Unrepresented native {opcode} header write mutated the actor, results or WORD398 owner before refusal.");
    }

    private static (GameSession Session, EventEngine Engine) CreateRequestPhysicalAliasFixture(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int priority, int scriptId, ushort word, bool knownOwner)
    {
        (Jsm.Opcode, int)[] issuerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] issuer = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, scriptId), (Jsm.Opcode.NOP, 0), (opcode, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] targetInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] target = knownOwner ? [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1, (16 << 7) | 2], [issuerInit, issuer, targetInit, target, requested]);
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            CanUseNativeScriptMove = body => body.ObjectId == 1,
            ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
            ResolveNativeScriptMovement = query =>
            {
                NativeScriptGroundProbe[] fan = Enumerable.Range(0, 3).Select(_ =>
                {
                    NativeScriptGroundProbe probe = new(query.FixedX, query.FixedY, query.FixedZ >> 12);
                    return probe with { ActorCollision = query.HasActorCollision!(probe) };
                }).ToArray();
                return new NativeScriptMovementResult(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, fan)]);
            }
        };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(engine.Actors.All(actor => actor.NativeActorWord398 == 0), "Fresh physical WORD398 did not follow authenticated native allocation ownership.");
        if (!knownOwner || word != 0)
        {
            // Fault injection exercises the execution guard for unsupported physical memory.
            // This deliberately invalid live state is never restored, saved or given a player journal.
            typeof(ActorState).GetProperty(nameof(ActorState.NativeActorWord398))!.SetValue(engine.Actors[1], knownOwner ? (ushort?)word : null);
        }
        return (session, engine);
    }

    private static JsonElement RequestPhysicalAliasCaller(JsonElement saved)
    {
        return saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
    }

    private static JsonElement TargetRequestPhysicalAliasFibers(JsonElement saved)
    {
        return JsonSerializer.SerializeToElement(saved.GetProperty("Fibers").EnumerateArray().Where(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1).ToArray());
    }
}
