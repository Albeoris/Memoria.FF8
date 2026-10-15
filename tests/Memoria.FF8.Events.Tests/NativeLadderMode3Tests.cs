using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    // Component geometry:525900/525A30 issue, then4789A0 mode3 setup/equality and475C60 interpolation.
    // The native automatic branch advances renderer WORD82 separately from pre-VM body WORD518.
    private static void NativeLadderMode3Contract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeInputConfiguration input = assets.ReadNewGameInputConfiguration();
        foreach (bool up in new[] { true, false })
        {
            EventEngine live = CreateLadderMode3Fixture(vitals, economy, input, up);
            ReachLadderMode3Issue(live);
            ActorState actor = live.Actors[0];
            Require(!live.IsUserControlEnabled && actor.NativePartyFlags is { AutoAnimation4: false } && actor.NativeLadderMode3 is { Active: true, Duration: 4 } ladder && ladder.Up == up && ladder.Entry == new FieldFixedPosition(4096, 8192, 12288) && ladder.Target == new FieldFixedPosition(29 << 12, 8192, 12288) && ladder.FixedX == 4096 && actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == 0 && actor.NativeLandingTriangle508 == 9 && actor.WalkmeshTriangle == 7 && actor.NativeAnimationHeader is { AnimationId: 6, Frame: 0, First: 0, Terminal: 64, Flags: 0x2000 } && actor.AnimationFrame == 0, "Mode3 issue did not consume XYZ DWORDs/low animation WORD, select its loop header, retain original entry and defer interpolation to the following callback.");
            JsonElement issue = live.Capture();
            JsonElement owner = LadderMode3Caller(live);
            Require(owner.GetProperty("Position").GetInt32() == actor.NativeLadderMode3!.Operation && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeLadderMode3" && !LadderMode3HasSuccessor(live), "Mode3 did not consume exactly four operands and hold its original instruction.");
            EventEngine restored = CreateLadderMode3Fixture(vitals, economy, input, up);
            restored.Restore(issue);
            JsonElement trace = issue.GetProperty("Scene").GetProperty("PartyTrace");
            NativeLadderButtons(live, 0xF000);
            NativeLadderButtons(restored, 0xF000);
            for (int tick = 1; tick <= 4; tick++)
            {
                live.Step();
                restored.Step();
                Require(actor.NativeTurnAccumulator474 == tick && actor.NativeMovementCompletion542 == 1 && actor.NativeLadderMode3!.FixedX == NativeJumpMath.Interpolate(4096, 29 << 12, 4, (short)tick) && actor.WalkmeshTriangle == 7 && actor.NativeAnimationHeader!.Frame == (tick % 4) * 16 && actor.AnimationFrame == (tick + 1) % 4 && !LadderMode3HasSuccessor(live) && JsonElement.DeepEquals(trace, live.Capture().GetProperty("Scene").GetProperty("PartyTrace")) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Automatic mode3 changed increment-before-interpolation timing, merged physical/header animation clocks, advanced trace/followers or diverged after independent restore.");
            }
            EventEngine endpoint = CreateLadderMode3Fixture(vitals, economy, input, up);
            endpoint.Restore(live.Capture());
            live.Step();
            restored.Step();
            endpoint.Step();
            Require(actor.NativeMovementCompletion542 == 2 && actor.NativeTurnAccumulator474 == 4 && actor.WalkmeshTriangle == 9 && actor.NativeLadderMode3 is { Active: true } && !LadderMode3HasSuccessor(live) && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), endpoint.Capture()), "Mode3 failed its separate old-equality completion callback or copied the landing triangle early.");
            live.Step();
            restored.Step();
            endpoint.Step();
            Require(actor.NativeLadderMode3 is { Active: false } && LadderMode3HasSuccessor(live) && CapturedVariables(live.Capture()).GetProperty("6000").GetInt32() == 73 && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), endpoint.Capture()), "Mode3 completion failed return2 same-callback successor or independent equality-phase continuation.");
            foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
            {
                state => state["Actors"]![0]!["NativeLadderMode3"]!["Operation"] = actor.NativeLadderMode3!.Operation + 1,
                state => state["Actors"]![0]!["NativeLadderMode3"]!["FixedX"] = 4097,
                state => state["Actors"]![0]!["NativeTurnAccumulator474"] = 5,
                state => state["Actors"]![0]!["NativeMovementCompletion542"] = 3,
                state => state["Actors"]![0]!["NativeLadderMode3"]!["Duration"] = 5,
                state => state["Actors"]![0]!["NativeLadderMode3"]!["InitializationSpeed"] = 14,
                state => state["Actors"]![0]!["NativeLandingTriangle508"] = 10,
                state => state["Scene"]!["NativePlayerPosition"]!["X"] = 4097,
                state => state["Scene"]!.AsObject().Remove("NativeLadderSpeed490E")
            })
            {
                JsonNode malformed = JsonNode.Parse(issue.GetRawText())!;
                corrupt(malformed);
                JsonElement before = live.Capture();
                try
                {
                    live.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed mode3 owner, body, counter, phase or speed owner restored.");
                }
                catch (Exception exception) when (exception is InvalidDataException or JsonException)
                {
                }
                Require(JsonElement.DeepEquals(before, live.Capture()), "Malformed mode3 restore partially changed its destination.");
            }
        }

        EventEngine latched = CreateLadderMode3Fixture(vitals, economy, input, true, changeGlobalSpeed: true);
        ReachLadderMode3Issue(latched);
        Require(latched.Actors[0].NativeLadderMode3 is { InitializationSpeed: 28, Duration: 4 } && latched.Scene.NativeLadderSpeed490E == 28, "Mode3 did not capture the actual initialization speed producer.");
        for (int callback = 0; callback < 4 && latched.Scene.NativeLadderSpeed490E != 0; callback++)
        {
            latched.Step();
        }
        Require(latched.Scene.NativeLadderSpeed490E == 0 && latched.Actors[0].NativeLadderMode3 is { InitializationSpeed: 28, Duration: 4, Active: true }, "Concurrent executed LADDERSPEED0 did not preserve the previously initialized physical duration.");
        EventEngine latchedCopy = CreateLadderMode3Fixture(vitals, economy, input, true, changeGlobalSpeed: true);
        latchedCopy.Restore(latched.Capture());
        for (int callback = 0; callback < 8 && !LadderMode3HasSuccessor(latched); callback++)
        {
            latched.Step();
            latchedCopy.Step();
            Require(JsonElement.DeepEquals(latched.Capture(), latchedCopy.Capture()), "Latched mode3 changed duration or refused its genuine current zero global speed after independent restore.");
        }
        Require(LadderMode3HasSuccessor(latched) && latched.Actors[0].NativeLadderMode3!.InitializationSpeed == 28, "Latched mode3 failed completion after an independently executed zero global speed writer.");

        EventEngine wide = CreateLadderMode3Fixture(vitals, economy, input, true, targetX: 65565);
        ReachLadderMode3Issue(wide);
        // Native DWORD square wrapping:262256² mod2^32=58732800; isqrt=7663; unsigned speedBYTE28 gives273.
        Require(wide.Actors[0].NativeLadderMode3 is { Target.X: 268554240, Duration: 273, FixedX: 4096 } && wide.Scene.NativeLadderSpeed490E == 28, "Mode3 narrowed its fullDWORD coordinate to a WORD or did not use the executed speed BYTE and original DWORD distance arithmetic.");
        EventEngine wideCopy = CreateLadderMode3Fixture(vitals, economy, input, true, targetX: 65565);
        wideCopy.Restore(wide.Capture());
        wide.Step();
        wideCopy.Step();
        Require(wide.Actors[0].NativeLadderMode3!.FixedX == NativeJumpMath.Interpolate(4096, 65565 << 12, 273, 1) && JsonElement.DeepEquals(wide.Capture(), wideCopy.Capture()), "Mode3 fullDWORD coordinate did not retain original large-delta division order or independent restore continuation.");

        foreach (bool up in new[] { true, false })
        {
            EventEngine zero = CreateLadderMode3Fixture(vitals, economy, input, up, targetX: 1);
            ReachLadderMode3Issue(zero);
            Require(zero.Actors[0].NativeLadderMode3 is { Duration: 0, Active: true } && zero.Actors[0].NativeMovementCompletion542 == 1 && zero.Actors[0].NativeTurnAccumulator474 == 0 && zero.Actors[0].WalkmeshTriangle == 7, "Zero-distance mode3 skipped its genuine setup callback or inferred a positive traversal duration.");
            EventEngine copy = CreateLadderMode3Fixture(vitals, economy, input, up, targetX: 1);
            copy.Restore(zero.Capture());
            zero.Step();
            copy.Step();
            Require(zero.Actors[0].NativeMovementCompletion542 == 2 && zero.Actors[0].WalkmeshTriangle == 9 && zero.Actors[0].X == 1 && zero.Actors[0].NativeTurnAccumulator474 == 0 && !LadderMode3HasSuccessor(zero) && JsonElement.DeepEquals(zero.Capture(), copy.Capture()), "Zero-distance mode3 failed its next-callback equality/triangle transfer or changed its entry during zero-duration interpolation.");
            zero.Step();
            copy.Step();
            Require(LadderMode3HasSuccessor(zero) && JsonElement.DeepEquals(zero.Capture(), copy.Capture()), "Zero-distance mode3 did not preserve return2 and independent restore continuation.");
        }

        foreach (bool up in new[] { true, false })
        {
            EventEngine controlled = CreateLadderMode3Fixture(vitals, economy, input, up, automatic: false);
            ReachLadderMode3Issue(controlled);
            ActorState actor = controlled.Actors[0];
            EventEngine restored = CreateLadderMode3Fixture(vitals, economy, input, up, automatic: false);
            restored.Restore(controlled.Capture());
            void StepHeld(int buttons)
            {
                NativeLadderButtons(controlled, buttons);
                NativeLadderButtons(restored, buttons);
                controlled.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(controlled.Capture(), restored.Capture()), "Controlled mode3 mapped held input diverged after independent restore.");
            }
            StepHeld(up ? 0x2000 : 0x8000);
            Require(actor.NativeTurnAccumulator474 == 0 && actor.NativeMovementCompletion542 == 1 && actor.NativeLadderMode3!.FixedX == 4096, "Mode3 used mode4's wider forward mask instead of its one original forward bit.");
            int forward = up ? 0x1000 : 0x4000;
            StepHeld(forward);
            Require(actor.NativeTurnAccumulator474 == 1 && actor.NativeLadderMode3!.FixedX == NativeJumpMath.Interpolate(4096, 29 << 12, 4, 1), "Controlled mode3 did not increment before its first interpolation.");
            StepHeld(forward);
            Require(controlled.ButtonInput!.RawPressed == 0 && actor.NativeTurnAccumulator474 == 2, "Controlled mode3 consumed button edges instead of mapped held input.");
            StepHeld(0xF000);
            Require(actor.NativeTurnAccumulator474 == 2 && actor.NativeMovementCompletion542 == 1, "Mode3 reverse/forward were treated as prioritized branches instead of independently decrementing then incrementing.");
            StepHeld(forward);
            StepHeld(forward);
            Require(actor.NativeTurnAccumulator474 == 4 && actor.NativeMovementCompletion542 == 1 && actor.WalkmeshTriangle == 7, "Controlled mode3 completed on increment instead of the next forward equality observation.");
            StepHeld(0);
            Require(actor.NativeMovementCompletion542 == 1, "Controlled mode3 completed at its endpoint without the required forward input.");
            StepHeld(forward);
            Require(actor.NativeMovementCompletion542 == 2 && actor.WalkmeshTriangle == 9 && !LadderMode3HasSuccessor(controlled), "Controlled mode3 did not commit its triangle and completion on held forward equality.");
            StepHeld(0);
            Require(LadderMode3HasSuccessor(controlled), "Controlled mode3 did not release its held instruction on the following VM callback.");

            EventEngine reverseZero = CreateLadderMode3Fixture(vitals, economy, input, up, automatic: false);
            ReachLadderMode3Issue(reverseZero);
            NativeLadderButtons(reverseZero, 0xF000);
            reverseZero.Step();
            Require(reverseZero.Actors[0].NativeMovementCompletion542 == 2 && reverseZero.Actors[0].NativeTurnAccumulator474 == 1 && reverseZero.Actors[0].WalkmeshTriangle == 7 && reverseZero.Actors[0].NativeLadderMode3!.FixedX == NativeJumpMath.Interpolate(4096, 29 << 12, 4, 1), "Mode3 reverse-at-zero completion prevented the independent forward branch or incorrectly transferred its landing triangle.");
            EventEngine reverseCopy = CreateLadderMode3Fixture(vitals, economy, input, up, automatic: false);
            reverseCopy.Restore(reverseZero.Capture());
            reverseZero.Step();
            reverseCopy.Step();
            Require(LadderMode3HasSuccessor(reverseZero) && JsonElement.DeepEquals(reverseZero.Capture(), reverseCopy.Capture()), "Mode3 independent simultaneous-input completion could not restore and release its instruction.");
        }

        NativeLadderMode3Refusals(vitals, economy, input);
        NativeLadderMode3PartyOverlap(vitals, economy, input);
        EventEngine mode4 = CreateNativeLadder(up: true);
        ReachNativeLadderClimb(mode4);
        NativeLadderButtons(mode4, 0x2000);
        mode4.Step();
        Require(mode4.Actors[0].NativeLadder is { Active: true } && mode4.Actors[0].NativeTurnAccumulator474 == 1, "Shared ladder change removed mode4 UP2's distinct wider forward input group.");
        Console.WriteLine("Native mode3 ladders: four-operand native widths, explicit UCOFF/header and renderer clocks, first setup callback, automatic/held independent branches, completion return2, atomic refusal/restore and bounded mode4 UP2 input regression pass.");
    }

    private static void NativeLadderMode3Refusals(NativeCharacterVitals vitals, NativeEconomy economy, NativeInputConfiguration input)
    {
        foreach (string variation in new[] { "selector-word", "selector-negative", "unknown-selector", "unknown-coordinate", "missing-metadata", "zero-speed" })
        {
            int selector = variation switch { "selector-word" => 262, "selector-negative" => -1, _ => 65542 };
            EventEngine engine = CreateLadderMode3Fixture(vitals, economy, input, true, selector: selector, speed: variation == "zero-speed" ? 256 : 284, unknownSelector: variation == "unknown-selector", unknownCoordinate: variation == "unknown-coordinate");
            if (variation == "missing-metadata")
            {
                engine.AnimationFrameCount = (_, animation) => animation == 6 ? 0 : 4;
            }
            JsonElement? actorBefore = null;
            JsonElement? fiberBefore = null;
            engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.NOP))
                {
                    actorBefore = JsonSerializer.SerializeToElement(engine.Actors[0]);
                    fiberBefore = LadderMode3Caller(engine);
                }
            };
            try
            {
                ReachLadderMode3Issue(engine);
                throw new InvalidOperationException($"Mode3 admitted unsupported {variation}.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.LADDERUP))
            {
            }
            Require(actorBefore is not null && fiberBefore is not null && engine.Actors[0].NativeLadderMode3 is null && JsonElement.DeepEquals(actorBefore.Value, JsonSerializer.SerializeToElement(engine.Actors[0])) && JsonElement.DeepEquals(fiberBefore.Value, LadderMode3Caller(engine)) && LadderMode3Caller(engine).GetProperty("Stack").GetArrayLength() == 4 && !LadderMode3HasSuccessor(engine), $"Mode3 {variation} refusal consumed an operand or partially changed its PC/body/header before validation.");
        }
    }

    private static void NativeLadderMode3PartyOverlap(NativeCharacterVitals vitals, NativeEconomy economy, NativeInputConfiguration input)
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.JOIN, Jsm.Opcode.SPLIT })
        {
            (Jsm.Opcode, int)[] leaderInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.RET, 0)];
            List<(Jsm.Opcode, int)> leader = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)];
            if (opcode == Jsm.Opcode.SPLIT)
            {
                foreach (int value in new[] { 1, 2, 3, 29, 2, 3, 1, 2, 3 })
                {
                    leader.Add((Jsm.Opcode.PSHN_L, value));
                }
            }
            leader.AddRange([(Jsm.Opcode.NOP, 0), (opcode, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
            (Jsm.Opcode, int)[] companionInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] companion = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 29), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.LADDERUP, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            session.SetInputConfiguration(input);
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = (_, _, _) => 3,
                ResolvePartyTraceTriangle = (_, _, _) => 7,
                CanUseNativeJoinMovement = _ => true,
                ResolveNativeScriptDirection = _ => throw new InvalidOperationException("Party overlap called its direction provider before rejecting the active mode3 owner."),
                ResolveNativeJoinMovement = _ => throw new InvalidOperationException("Party overlap called its movement provider before rejecting the active mode3 owner.")
            };
            engine.LoadField(600, SyntheticObjects([1, (16 << 7) | 1], [leaderInit, leader.ToArray(), companionInit, companion]), [], new FieldTransition(600, 1, 2, 7, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            JsonElement? before = null;
            engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.NOP) && command.ObjectId == 0)
                {
                    before = engine.Capture();
                    Require(engine.Actors[1].NativeLadderMode3 is { Active: true } && engine.Scene.PartyBindingMemory is { First: 0, Second: 1 }, "Party overlap fixture did not retain its genuinely executing companion ladder and original saved bindings.");
                }
            };
            try
            {
                for (int callback = 0; callback < 12; callback++)
                {
                    engine.Step();
                }
                throw new InvalidOperationException($"Native {opcode} replaced an active companion ladder.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            JsonElement after = engine.Capture();
            Require(before is not null && engine.LastFault!.Message.Contains("mode3", StringComparison.Ordinal) && JsonElement.DeepEquals(before.Value.GetProperty("Actors"), after.GetProperty("Actors")) && JsonElement.DeepEquals(before.Value.GetProperty("Scene"), after.GetProperty("Scene")) && JsonElement.DeepEquals(before.Value.GetProperty("Fibers"), after.GetProperty("Fibers")) && JsonElement.DeepEquals(before.Value.GetProperty("PartyJoin"), after.GetProperty("PartyJoin")) && JsonElement.DeepEquals(before.Value.GetProperty("PartySplit"), after.GetProperty("PartySplit")) && LadderMode3Caller(engine).GetProperty("Stack").GetArrayLength() == (opcode == Jsm.Opcode.SPLIT ? 9 : 0) && !LadderMode3HasSuccessor(engine), $"Native {opcode} overlap refusal changed its PC/operands, party process, physical placement or animation header before validating the active companion ladder.");
        }
    }

    private static void NativeLadderMode3RequestContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeInputConfiguration input = assets.ReadNewGameInputConfiguration();
        EventEngine Create() => CreateLadderMode3Fixture(vitals, economy, input, true, automatic: false, preempt: true);
        EventEngine live = Create();
        ReachLadderMode3Issue(live);
        for (int callback = 0; callback < 8 && live.IsUserControlEnabled; callback++)
        {
            live.Step();
        }
        Require(!live.IsUserControlEnabled && live.Actors[0].NativeLadderMode3 is { Active: true } && live.Actors[0].NativeMovementRequest is null && live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2 && fiber.GetProperty("Started").GetBoolean() && fiber.GetProperty("WaitReason").GetString() == "Frames"), "Higher request failed to enter UCOFF/WAIT over the retained mode3 ladder or invented a mode1 movement backup.");
        int before = live.Actors[0].NativeTurnAccumulator474!.Value;
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (int callback = 0; callback < 8 && !LadderMode3HasSuccessor(live); callback++)
        {
            live.Step();
            restored.Step();
            Require(live.Actors[0].NativeMovementRequest is null && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Higher mode3 request changed the physical owner, invented a mode1 backup or diverged after active restore.");
        }
        Require(before < live.Actors[0].NativeTurnAccumulator474 && live.Actors[0].NativeLadderMode3 is { Active: false } && LadderMode3HasSuccessor(live) && live.LastFault is null, "UCOFF in the higher request failed to advance and complete the retained automatic mode3 body.");
        Console.WriteLine("Mode3 request: higher UCOFF/WAIT/RET8 retains physical ladder, advances automatically without mode1 backup and restores active state exactly PASS.");
    }

    private static EventEngine CreateLadderMode3Fixture(NativeCharacterVitals vitals, NativeEconomy economy, NativeInputConfiguration input, bool up, bool automatic = true, int selector = 65542, int speed = 284, bool unknownSelector = false, bool unknownCoordinate = false, int targetX = 29, bool changeGlobalSpeed = false, bool preempt = false)
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.LADDERANIME, 3), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.INITTRACE, 0)];
        if (automatic)
        {
            main.Add((Jsm.Opcode.UCOFF, 0));
        }
        main.AddRange([(Jsm.Opcode.PSHN_L, speed), ((Jsm.Opcode)372, 0)]);
        if (unknownSelector || unknownCoordinate)
        {
            main.AddRange([(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0)]);
        }
        main.Add(unknownCoordinate ? (Jsm.Opcode.PSHI_L, 0) : (Jsm.Opcode.PSHN_L, targetX));
        main.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3)]);
        main.Add(unknownSelector ? (Jsm.Opcode.PSHI_L, 0) : (Jsm.Opcode.PSHN_L, selector));
        main.AddRange([(Jsm.Opcode.NOP, 0), (up ? Jsm.Opcode.LADDERUP : Jsm.Opcode.LADDERDOWN, 65545), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
        List<(Jsm.Opcode, int)[]> scripts = [init, main.ToArray()];
        if (preempt)
        {
            scripts.Add([(Jsm.Opcode.LBL, 2), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
        }
        if (changeGlobalSpeed || preempt)
        {
            scripts.Add([(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)]);
            scripts.Add(preempt ? [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), ((Jsm.Opcode)372, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
        }
        byte[] content = SyntheticObjects(changeGlobalSpeed || preempt ? [(16 << 7) | 1, (ushort)(preempt ? 2 : 1)] : [1], scripts);
        if (changeGlobalSpeed || preempt)
        {
            // A genuine logical module independently writes the shared global speed; it has no allocated body.
            content[2] = 1;
            content[3] = 1;
        }
        int operations = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        for (int offset = operations; offset < content.Length; offset += 4)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset));
            if (encoded == (int)Jsm.Opcode.SETMODEL)
            {
                BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(offset), encoded << 24);
            }
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        session.SetInputConfiguration(input);
        EventEngine engine = new(session)
        {
            LoadSoundResource = _ => SoundResourceStatus.Muted,
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 3,
            ResolvePartyTraceTriangle = (_, _, _) => 7
        };
        engine.LoadField(600, content, [], new FieldTransition(600, 1, 2, 7, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void ReachLadderMode3Issue(EventEngine engine)
    {
        for (int callback = 0; callback < 12 && engine.Actors[0].NativeLadderMode3 is null; callback++)
        {
            engine.Step();
        }
        Require(engine.Actors[0].NativeLadderMode3 is { Active: true }, "Native mode3 fixture never reached its instruction.");
    }

    private static JsonElement LadderMode3Caller(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);

    private static bool LadderMode3HasSuccessor(EventEngine engine) => CapturedVariables(engine.Capture()).TryGetProperty("6000", out _);
}
