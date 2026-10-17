using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLadderRequestContract()
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            EventEngine live = CreateNativeLadder(up: true, request: opcode);
            EventEngine independent = CreateNativeLadder(up: true, request: opcode);
            NativeLadderButtons(live, 0x3000);
            NativeLadderButtons(independent, 0x3000);
            bool requested = false;
            bool requestedLabel = false;
            bool requestedReturn = false;
            live.CommandExecuted += command =>
            {
                if (command.ObjectId == 0 && command.ScriptId == 2)
                {
                    requestedLabel |= command.Name == "LBL";
                    requestedReturn |= command.Name == "RET";
                }
                if (command.Name != opcode.ToString())
                {
                    return;
                }
                ActorState actor = live.Actors[0];
                Require(actor.NativeLadder is { Active: true, ScriptId: 1 } && actor.NativeMovementCompletion542 == 4 && actor.NativeTurnAccumulator474 == 0 && actor.NativeMovementRequest is null && actor.NativeScriptMovement is null, "Higher native request changed the active mode4 body, installed a mode1 backup or missed the reached exit phase.");
                requested = true;
            };
            for (int callback = 0; callback < 12 && !requested; callback++)
            {
                live.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Mode4 request issue diverged before independent restoration.");
            }
            Require(requested, "Higher native request did not preempt the held mode4 VM consumer.");
            independent.Restore(live.Capture());
            bool completedUnderUpperWait = false;
            for (int callback = 0; callback < 16; callback++)
            {
                live.Step();
                independent.Step();
                JsonElement saved = live.Capture();
                ActorState actor = live.Actors[0];
                Require(actor.NativeMovementRequest is null && actor.NativeScriptMovement is null && JsonElement.DeepEquals(saved, independent.Capture()), "Mode4 beneath a higher request gained an invented scalar backup or diverged after restore.");
                if (CapturedVariables(saved).TryGetProperty("6000", out JsonElement lowerResult))
                {
                    Require(completedUnderUpperWait && lowerResult.GetInt32() == 73 && CapturedVariables(saved).GetProperty("6001").GetInt32() == 99 && actor.NativeLadder is { Active: false } && actor.NativeMovementCompletion542 == 2 && actor.NativeTurnAccumulator474 == 1 && actor.WalkmeshTriangle == 9, "RET8 did not resume the original lower LADDER consumer without physical restore/reset.");
                    independent.Restore(saved);
                    break;
                }
                //Completed noninitialization fibers are reaped after the callback.
                //The upper fiber is required only before RET has resumed the lower.
                JsonElement upper = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 2);
                if (actor.NativeMovementCompletion542 == 2 && !upper.GetProperty("Complete").GetBoolean())
                {
                    completedUnderUpperWait = true;
                    Require(actor.NativeLadder is { Active: true, AutomaticCompletion: false } && actor.WalkmeshTriangle == 9 && actor.NativeLadder.Duration == 1 && actor.NativeTurnAccumulator474 == 1 && !CapturedVariables(saved).TryGetProperty("6000", out _), "Physical mode4 did not finish its unchanged controlled exit/drain while the lower VM stayed held.");
                }
                independent.Restore(saved);
            }
            Require(requestedLabel && requestedReturn && completedUnderUpperWait && CapturedVariables(live.Capture()).GetProperty("6000").GetInt32() == 73, "Mode4 request failed its genuine requested LBL/RET frame or did not return to its original lower consumer.");
        }
        Console.WriteLine("Native mode4 requests: REQ/REQSW/REQEW preserve the physical exit/drain beneath higher HIDE/UCOFF/WAIT, create no mode1 backup, and RET8 resumes the lower consumer; independent restores PASS.");
    }
}
