using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void MoveCancelTargetAndWait()
    {
        EventEngine Create()
        {
            (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 1), (Jsm.Opcode.MOVECANCEL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] targetInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            EventEngine engine = new(CreateInitializedSession())
            {
                AnimationFrameCount = (_, _) => 9,
                CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe fan = new(x, query.FixedY, 0);
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, [fan, fan, fan])]);
                }
            };
            engine.LoadField(600, SyntheticObjects([1, (16 << 7) | 1], [callerInit, caller, targetInit, target]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        JsonElement moving = live.Capture();
        Require(live.Actors[1].MovementTarget is not null && !CapturedVariables(moving).TryGetProperty("512", out _), "MOVESYNC did not retain its genuinely executing target.");
        bool cancellationResetIdle = false;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.MOVECANCEL))
            {
                cancellationResetIdle = live.Actors[1].AnimationFrame == 0 && live.Actors[1].AnimationFirst == 0 && live.Actors[1].AnimationLast == 8 && live.Actors[1].AnimationLoop;
            }
        };
        EventEngine copy = Create();
        copy.Restore(moving);
        for (int callback = 0; callback < 2; callback++)
        {
            live.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "MOVECANCEL target or MOVESYNC completion diverged after independent restore.");
        }
        // Native524620 clears mode572 without writing completion542; the original MOVE consumer remains held.
        Require(live.Actors[1].X > 0 && live.Actors[1].X < 1000 && live.Actors[1].MovementTarget is null && !CapturedVariables(live.Capture()).TryGetProperty("512", out _) && live.Actors[1].NativeMovementCompletion542 == 1 && live.Actors[1].NativeScriptMovement is { Active: false, Cancelled: true }, "MOVECANCEL failed to stop the target in place or invented a write to physical completion542/the held consumer.");
        Require(cancellationResetIdle && live.Actors[1].AnimationFrame == 1 && live.Actors[1].AnimationFirst == 0 && live.Actors[1].AnimationLast == 8 && live.Actors[1].AnimationLoop, "MOVECANCEL did not restart the full native idle animation before its ordinary post-VM sample.");
    }

    private static void SetPcPartyActivation()
    {
        EventEngine Run(params (Jsm.Opcode Opcode, int Parameter)[] commands)
        {
            GameSession session = CreateInitializedSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            List<(Jsm.Opcode, int)> init = [commands[0], (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0)];
            init.AddRange(commands.Skip(1));
            engine.LoadField(100, SyntheticProgram(init.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        EventEngine absent = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(absent.Actors[0].CharacterId == 5 && !absent.Actors[0].Active && absent.Actors[0].Visible, "SETPC absent-party registration changed visibility instead of clearing active state.");
        EventEngine present = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(present.Actors[0].CharacterId == 1 && present.Actors[0].Active, "SETPC did not preserve an active party member or truncate character ID to a byte.");
        EventEngine inactive = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(!inactive.Actors[0].Active, "SETPC implicitly activated a party member instead of preserving UNUSE.");
        // This independent synthetic world models the late NPC USE branch; it is not an action trace through that story branch.
        EventEngine npc = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.USE, 0), (Jsm.Opcode.RET, 0));
        Require(npc.Scene.Party.SequenceEqual([255, 255, 255]) && npc.Actors[0].CharacterId == 5 && npc.Actors[0].Active, "The late conditional USE branch failed to activate a registered character as an NPC.");
    }


    private static EventEngine CreatePartyRequestFixture(int slot, int priority, bool targetBusy, Jsm.Opcode opcode)
    {
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, 2), (opcode, slot), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_L, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] targetInit = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] targetDefault = targetBusy ? [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 4), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 516), (Jsm.Opcode.RET, 8)];
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.LoadField(600, SyntheticObjects([1, (2 << 7) | 2], [callerInit, caller, targetInit, targetDefault, requested]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void PartyRequestWaitAndGuard() => LegacyPartyRequestContract(Jsm.Opcode.PREQEW);

    private static void PartyRequestNoWaitAndGuard() => LegacyPartyRequestContract(Jsm.Opcode.PREQ);

    private static void LegacyPartyRequestContract(Jsm.Opcode opcode)
    {
        foreach (bool busy in new[] { false, true })
        {
            EventEngine live = CreatePartyRequestFixture(0, 1, busy, opcode);
            for (int callback = 0; callback < 3; callback++)
            {
                live.Step();
            }
            JsonElement pending = live.Capture();
            JsonElement caller = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(!CapturedVariables(pending).TryGetProperty("516", out _) && caller.GetProperty("WaitReason").GetString() == (opcode == Jsm.Opcode.PREQEW ? nameof(WaitReason.RequestEnd) : nameof(WaitReason.None)), "Party request lost its completion-wait distinction or skipped the target's selected WAIT.");
            JsonElement suspended = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 3);
            EventEngine copy = CreatePartyRequestFixture(0, 1, busy, opcode);
            copy.Restore(pending);
            bool observedIndependentCaller = false;
            for (int callback = 0; callback < 6; callback++)
            {
                live.Step();
                copy.Step();
                if (busy && callback == 0)
                {
                    Require(JsonElement.DeepEquals(suspended, live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 3)), "Higher party request advanced the suspended default countdown or changed its execution owner.");
                }
                JsonElement current = CapturedVariables(live.Capture());
                observedIndependentCaller |= current.TryGetProperty("512", out _) && !current.TryGetProperty("516", out _);
                Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Party request target/caller or selected WAIT diverged after independent restore.");
            }
            JsonElement variables = CapturedVariables(live.Capture());
            Require(variables.GetProperty("512").GetInt32() == 11 && variables.GetProperty("516").GetInt32() == 7 && observedIndependentCaller == (opcode == Jsm.Opcode.PREQ), "Party request did not preserve source/target write order and independent execution.");
        }
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = CreatePartyRequestFixture(slot, priority, true, opcode);
            for (int callback = 0; callback < 5; callback++)
            {
                ignored.Step();
            }
            Require(CapturedVariables(ignored.Capture()).GetProperty("512").GetInt32() == 11 && !ignored.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 4), "Absent party slot or occupied native slot did not consume and drop the request.");
        }
        EventEngine refused = CreatePartyRequestFixture(0, 8, false, opcode);
        refused.Step();
        refused.Step();
        try
        {
            refused.Step();
            throw new InvalidOperationException("Out-of-range native request priority was accepted.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == opcode.ToString())
        {
        }
        JsonElement interrupted = refused.Capture();
        JsonElement faultFiber = interrupted.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(faultFiber.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && faultFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([2, 8]) && !CapturedVariables(interrupted).TryGetProperty("512", out _), "Invalid native request changed its original operands or source successor.");
        ExpectInterruptedRestoreRefusal(refused, interrupted);
        if (opcode == Jsm.Opcode.PREQ)
        {
            LegacyPartyRequestSignalContract();
        }
    }

    private static void LegacyPartyRequestSignalContract()
    {
        (Jsm.Opcode, int)[] sourceInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] source = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PREQ, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 13), (Jsm.Opcode.POPM_B, 1029), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] targetInit = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] targetDefault = [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 4), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 12), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.DIRA, 0), (Jsm.Opcode.JMP, -8), (Jsm.Opcode.RET, 8)];
        EventEngine engine = new(CreateInitializedSession()) { AnimationFrameCount = (_, _) => 4, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
        engine.LoadField(600, SyntheticObjects([1, (2 << 7) | 2], [sourceInit, source, targetInit, targetDefault, requested]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        int faceSamples = 0;
        engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.DIRA))
            {
                faceSamples++;
            }
        };
        for (int callback = 0; callback < 8; callback++)
        {
            engine.Step();
        }
        // The source WAIT holds three callbacks while the independent loop uses its shared sixteen-op budgets.
        Require(faceSamples == 7 && engine.EventMemory.Read(1029, 1).RequireExact() == 128 && !engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 4 && !fiber.GetProperty("Complete").GetBoolean()), "Command-driven bit128 signal did not release the independent native PREQ facing loop within its sixteen-instruction budgets.");
    }

    private static void PartyTurnAnglesAndResume()
    {
        EventEngine Create(int start, int x, int y, int duration)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = CreateInitializedSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            List<(Jsm.Opcode, int)> init = caller.Take(5).ToList();
            init.Insert(1, (Jsm.Opcode.SETMODEL, 0));
            init.Add((Jsm.Opcode.RET, 8));
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0)];
            main.AddRange(caller.Skip(5).SkipLast(1));
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            List<(Jsm.Opcode, int)> playerInit = player.ToList();
            playerInit.Insert(1, (Jsm.Opcode.SETMODEL, 1));
            engine.LoadField(600, SyntheticObjects([1, 16 << 7], [init.ToArray(), main.ToArray(), playerInit.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            engine.Step();
            engine.Step();
            return engine;
        }
        EventEngine wrapped = Create(250, 10, -100, 260);
        Require(wrapped.Actors[0].RotationTarget == 260 && wrapped.Actors[0].RotationTotalFrames == 4 && wrapped.Actors[0].RotationFrames == 4, "PCTURN did not capture the nearest wrapped angle or duration's low byte.");
        Require(wrapped.Actors[0].Direction == 250 && wrapped.Actors[0].RotationFrames == 4, "PCTURN skipped the native elapsed0 sample.");
        wrapped.Step();
        Require(wrapped.Actors[0].Direction == 251, "PCTURN first eased step did not use native integer cosine interpolation.");
        var midturn = wrapped.Capture();
        EventEngine resumed = Create(250, 10, -100, 260);
        resumed.Restore(midturn);
        Require(midturn.GetRawText() == resumed.Capture().GetRawText(), "PCTURN midturn state failed to round-trip.");
        for (int index = 0; index < 3; index++)
        {
            wrapped.Step();
            resumed.Step();
            Require(wrapped.Capture().GetRawText() == resumed.Capture().GetRawText(), "Restoring PCTURN changed an angular sample or completion tick.");
        }
        wrapped.Step();
        resumed.Step();
        Require(JsonElement.DeepEquals(wrapped.Capture(), resumed.Capture()), "PCTURN completion poll diverged after independent restore.");
        Require(wrapped.Actors[0].Direction == 4 && wrapped.Actors[0].RotationTarget is null && !wrapped.Actors[0].RotationUsesNativeCosine && wrapped.Actors[0].RotationFrames == 0 && CapturedVariables(wrapped.Capture()).GetProperty("512").GetInt32() == 7, "PCTURN did not include its endpoint and release its source wait after duration+1 ticks.");
        EventEngine tie = Create(0, 0, 100, 4);
        tie.Step();
        tie.Step();
        Require(tie.Actors[0].Direction == 64 && tie.Actors[0].RotationTarget == 128, "PCTURN changed the native positive half-turn tie into a negative turn.");
        EventEngine immediate = Create(0, 100, 0, 256);
        Require(immediate.Actors[0].Direction == 64 && immediate.Actors[0].RotationTarget is null, "Zero-duration PCTURN did not complete its actual issuing callback body sample.");
        immediate.Step();
        Require(immediate.Actors[0].Direction == 64 && immediate.Actors[0].RotationTarget is null, "Zero-duration PCTURN did not complete on its first driver sample.");
        EventEngine aligned = Create(64, 100, 0, 15);
        Require(aligned.Actors[0].RotationTarget is null && !aligned.Actors[0].RotationUsesNativeCosine && aligned.Actors[0].RotationFrames == 0 && CapturedVariables(aligned.Capture()).GetProperty("512").GetInt32() == 7, "Already aligned PCTURN introduced a rotation wait.");
    }


    private static void RefreshedPartyTurnTarget()
    {
        (Jsm.Opcode, int)[] active = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] inactive = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] disable = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] program = SyntheticObjects([(16 << 7) | 1, 0, (32 << 7) | 1], [active, inactive, disable, callerInit, caller]);
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
        engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Require(engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 1, 0, 2 }) && engine.Actors[1].Active, "Post-init compaction failed to retain the original first native actor before later UNUSE.");
        engine.Step();
        Require(!engine.Actors[1].Active && engine.Actors[0].Active && engine.Scene.FieldPartyNativeActorIds[0] == 0 && session.State.Variables[512] == 10 && engine.Actors[2].Direction == 64 && engine.Actors[2].RotationTarget is null, "Refreshed PGETINFO/PCTURN re-compacted a later UNUSED actor or selected the active sorted duplicate.");
        var pending = engine.Capture();
        engine.Step();
        string completed = engine.Capture().GetRawText();
        Require(engine.Actors[2].Direction == 64 && !engine.Actors[1].Active && engine.Actors[1].X == 10 && engine.Actors[0].X == -10, "Refreshed PCTURN changed its target or failed its native fixed angle.");
        EventEngine copy = new(CreateInitializedSession()) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
        copy.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        copy.Restore(pending);
        copy.Step();
        Require(completed == copy.Capture().GetRawText(), "Refreshed duplicate-target PCTURN changed its fixed target after exact restore.");
    }

}
