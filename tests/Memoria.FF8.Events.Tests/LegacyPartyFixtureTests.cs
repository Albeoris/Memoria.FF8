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
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.MOVECANCEL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([0, 16 << 7], [caller, target]);
        content[2] = 1;
        content[3] = 1;
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9 };
        engine.LoadField(100, content, []);
        engine.Step();
        var moving = engine.Capture();
        Require(engine.Actors[1].MovementTarget is not null && !session.State.Variables.ContainsKey(512), "Target MOVESYNC did not wait for its real scripted movement.");
        engine.Step();
        Require(engine.Actors[1].X > 0 && engine.Actors[1].X < 1000 && engine.Actors[1].MovementTarget is null && session.State.Variables[512] == 7, "MOVECANCEL failed to stop the native target in place and release its movement wait.");
        Require(engine.Actors[1].AnimationFrame == 0 && engine.Actors[1].AnimationFirst == 0 && engine.Actors[1].AnimationLast == 8 && engine.Actors[1].AnimationLoop, "MOVECANCEL did not restart the target base animation's complete native frame range.");
        string cancelled = engine.Capture().GetRawText();
        engine.Restore(moving);
        engine.Step();
        Require(cancelled == engine.Capture().GetRawText(), "Restoring the moving target changed cancellation or movement-wait completion.");
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
        Require(npc.Scene.Party.Count == 0 && npc.Actors[0].CharacterId == 5 && npc.Actors[0].Active, "The late conditional USE branch failed to activate a registered character as an NPC.");
    }


    private static EventEngine CreatePartyRequestFixture(int slot, int priority, bool targetBusy, Jsm.Opcode opcode)
    {
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, 1), (opcode, slot), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)> targetInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0)];
        if (targetBusy)
        {
            targetInit.Add((Jsm.Opcode.PSHN_L, 5));
            targetInit.Add((Jsm.Opcode.WAIT, 0));
        }
        targetInit.Add((Jsm.Opcode.RET, 0));
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 513), (Jsm.Opcode.RET, 0)];
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
        engine.LoadField(100, SyntheticObjects([0, (16 << 7) | 1], [caller, targetInit.ToArray(), requested]), []);
        return engine;
    }


    private static void PartyRequestWaitAndGuard()
    {
        EventEngine Create(int slot, int priority, bool targetBusy) => CreatePartyRequestFixture(slot, priority, targetBusy, Jsm.Opcode.PREQEW);
        EventEngine accepted = Create(0, 1, false);
        accepted.Step();
        accepted.Step();
        var waiting = accepted.Capture();
        Require(CapturedVariables(waiting).TryGetProperty("512", out _) == false && waiting.GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == nameof(WaitReason.RequestEnd), "PREQEW caller resumed before its registered party actor completed the requested script.");
        accepted.Step();
        Require(!CapturedVariables(accepted.Capture()).TryGetProperty("512", out _), "PREQEW end wait released before the target WAIT duration.");
        accepted.Step();
        Require(CapturedVariables(accepted.Capture()).GetProperty("513").GetInt32() == 7 && !CapturedVariables(accepted.Capture()).TryGetProperty("512", out _), "The target completion did not precede its waiting caller's next scheduler turn.");
        accepted.Step();
        string completed = accepted.Capture().GetRawText();
        Require(CapturedVariables(accepted.Capture()).GetProperty("512").GetInt32() == 11, "PREQEW end wait failed to release after target completion.");
        accepted.Restore(waiting);
        accepted.Step();
        accepted.Step();
        accepted.Step();
        Require(completed == accepted.Capture().GetRawText(), "Restoring PREQEW's pending relationship changed target/caller completion order.");
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = Create(slot, priority, true);
            ignored.Step();
            ignored.Step();
            Require(CapturedVariables(ignored.Capture()).GetProperty("512").GetInt32() == 11 && ignored.Capture().GetProperty("Fibers").GetArrayLength() == 2, "PREQEW absent slot or occupied same-priority request was not consumed as a no-op.");
        }
        EventEngine conflicting = Create(0, 1, true);
        conflicting.Step();
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Unsupported PREQEW preemption executed.");
        }
        catch (EventScriptException)
        {
        }
        var fault = conflicting.Capture();
        var callerFault = fault.GetProperty("Fibers")[0];
        Require(conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQEW) && callerFault.GetProperty("Position").GetInt32() == 7 && callerFault.GetProperty("Stack")[0].GetInt32() == 1 && callerFault.GetProperty("Stack")[1].GetInt32() == 1, "Unsupported PREQEW preemption changed its PC or consumed original operands.");
        conflicting.Restore(fault);
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("A restored PREQEW preemption fault resumed.");
        }
        catch (EventScriptException)
        {
        }
        Require(fault.GetRawText() == conflicting.Capture().GetRawText(), "Unsupported PREQEW preemption did not remain frozen after restore.");
    }


    private static void PartyRequestNoWaitAndGuard()
    {
        EventEngine accepted = CreatePartyRequestFixture(0, 1, false, Jsm.Opcode.PREQ);
        accepted.Step();
        accepted.Step();
        var running = accepted.Capture();
        Require(CapturedVariables(running).GetProperty("512").GetInt32() == 11 && !CapturedVariables(running).TryGetProperty("513", out _), "PREQ caller waited for its target instead of continuing immediately.");
        var caller = running.GetProperty("Fibers")[0];
        Require(caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.None) && caller.GetProperty("RequestSequence").ValueKind == System.Text.Json.JsonValueKind.Null, "PREQ installed a caller wait relationship.");
        accepted.Step();
        accepted.Step();
        string completed = accepted.Capture().GetRawText();
        Require(CapturedVariables(accepted.Capture()).GetProperty("513").GetInt32() == 7, "PREQ target failed to finish independently of its caller.");
        accepted.Restore(running);
        accepted.Step();
        accepted.Step();
        Require(completed == accepted.Capture().GetRawText(), "Restoring the independent PREQ target changed its completion.");
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = CreatePartyRequestFixture(slot, priority, true, Jsm.Opcode.PREQ);
            ignored.Step();
            ignored.Step();
            Require(CapturedVariables(ignored.Capture()).GetProperty("512").GetInt32() == 11 && ignored.Capture().GetProperty("Fibers").GetArrayLength() == 2, "PREQ absent slot or occupied same-priority request was not consumed as a no-op.");
        }
        EventEngine conflicting = CreatePartyRequestFixture(0, 1, true, Jsm.Opcode.PREQ);
        conflicting.Step();
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Unsupported PREQ preemption executed.");
        }
        catch (EventScriptException)
        {
        }
        var fault = conflicting.Capture();
        var preserved = fault.GetProperty("Fibers")[0];
        Require(conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQ) && preserved.GetProperty("Position").GetInt32() == 7 && preserved.GetProperty("Stack")[0].GetInt32() == 1 && preserved.GetProperty("Stack")[1].GetInt32() == 1, "Unsupported PREQ preemption consumed operands or changed its PC.");
        conflicting.Restore(fault);
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Restored unsupported PREQ preemption resumed.");
        }
        catch (EventScriptException)
        {
        }
        Require(fault.GetRawText() == conflicting.Capture().GetRawText(), "Unsupported PREQ preemption did not stay frozen across restore.");

        (Jsm.Opcode, int)[] signalCaller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PREQ, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 13), (Jsm.Opcode.POPM_B, 1029), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] registered = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] dormant = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] facingLoop = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 12), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.DIRA, 0), (Jsm.Opcode.JMP, -8), (Jsm.Opcode.RET, 0)];
        GameSession signalSession = CreateInitializedSession();
        EventEngine signaled = new(signalSession);
        signaled.LoadField(100, SyntheticObjects([0, (16 << 7) | 2], [signalCaller, registered, dormant, facingLoop]), []);
        int faceSamples = 0;
        signaled.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.DIRA))
            {
                faceSamples++;
            }
        };
        for (int frame = 0; frame < 5; frame++)
        {
            signaled.Step();
        }
        Require(faceSamples == 3 && signalSession.State.Variables[1029] == 128 && !signaled.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 18 && !fiber.GetProperty("Complete").GetBoolean()), "Command-driven bit128 signal failed to release the independent PREQ facing loop.");
    }


    private static void PartyTurnAnglesAndResume()
    {
        EventEngine Create(int start, int x, int y, int duration)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = CreateInitializedSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            engine.LoadField(100, SyntheticObjects([0, 16 << 7], [caller, player]), []);
            engine.Step();
            engine.Step();
            return engine;
        }
        EventEngine wrapped = Create(250, 10, -100, 260);
        Require(wrapped.Actors[0].RotationTarget == 260 && wrapped.Actors[0].RotationTotalFrames == 4 && wrapped.Actors[0].RotationFrames == 5, "PCTURN did not capture the nearest wrapped angle or duration's low byte.");
        wrapped.Step();
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
        Require(wrapped.Actors[0].Direction == 4 && wrapped.Actors[0].RotationTarget is null && !wrapped.Actors[0].RotationUsesNativeCosine && wrapped.Actors[0].RotationFrames == 0 && CapturedVariables(wrapped.Capture()).GetProperty("512").GetInt32() == 7, "PCTURN did not include its endpoint and release its source wait after duration+1 ticks.");
        EventEngine tie = Create(0, 0, 100, 4);
        tie.Step();
        tie.Step();
        tie.Step();
        Require(tie.Actors[0].Direction == 64 && tie.Actors[0].RotationTarget == 128, "PCTURN changed the native positive half-turn tie into a negative turn.");
        EventEngine immediate = Create(0, 100, 0, 256);
        Require(immediate.Actors[0].Direction == 0 && immediate.Actors[0].RotationTarget == 64, "Zero-duration PCTURN incorrectly skipped its next native driver tick.");
        immediate.Step();
        Require(immediate.Actors[0].Direction == 64 && immediate.Actors[0].RotationTarget is null, "Zero-duration PCTURN did not complete on its first driver sample.");
        EventEngine aligned = Create(64, 100, 0, 15);
        Require(aligned.Actors[0].RotationTarget is null && !aligned.Actors[0].RotationUsesNativeCosine && aligned.Actors[0].RotationFrames == 0 && CapturedVariables(aligned.Capture()).GetProperty("512").GetInt32() == 7, "Already aligned PCTURN introduced a rotation wait.");
    }


    private static void RefreshedPartyTurnTarget()
    {
        (Jsm.Opcode, int)[] active = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] inactive = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] disable = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([(16 << 7) | 1, 0, (32 << 7) | 1], [active, inactive, disable, callerInit, caller]);
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 9, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 1, 0, 2 }) && engine.Actors[1].Active, "Post-init compaction failed to retain the original first native actor before later UNUSE.");
        engine.Step();
        Require(!engine.Actors[1].Active && engine.Actors[0].Active && engine.Scene.FieldPartyNativeActorIds[0] == 0 && session.State.Variables[512] == 10 && engine.Actors[2].RotationTarget == 64, "Refreshed PGETINFO/PCTURN re-compacted a later UNUSED actor or selected the active sorted duplicate.");
        var pending = engine.Capture();
        engine.Step();
        string completed = engine.Capture().GetRawText();
        Require(engine.Actors[2].Direction == 64 && !engine.Actors[1].Active && engine.Actors[1].X == 10 && engine.Actors[0].X == -10, "Refreshed PCTURN changed its target or failed its native fixed angle.");
        engine.Restore(pending);
        engine.Step();
        Require(completed == engine.Capture().GetRawText(), "Refreshed duplicate-target PCTURN changed its fixed target after exact restore.");
    }

}
