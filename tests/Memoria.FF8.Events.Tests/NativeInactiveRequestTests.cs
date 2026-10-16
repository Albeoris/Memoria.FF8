using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeInactiveRequestContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            foreach (string mode in new[] { "Accepted", "Queued", "Occupied", "Removed" })
            {
                int script = mode is "Occupied" or "Removed" ? 131071 : 65555;
                int priority = opcode == Jsm.Opcode.REQ ? 257 : 1;
                List<(Jsm.Opcode, int)> caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 77)];
                if (mode is "Queued" or "Occupied")
                {
                    caller.AddRange([(Jsm.Opcode.PSHN_L, mode == "Queued" ? 3 : 1), (Jsm.Opcode.PSHN_L, mode == "Queued" ? 20 : 19), (Jsm.Opcode.REQ, 1)]);
                }
                caller.AddRange([(Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, script), (opcode, 1), (Jsm.Opcode.POPM_B, 720), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
                List<(Jsm.Opcode, int)> target = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1)];
                if (mode == "Removed")
                {
                    target.Add((Jsm.Opcode.UNUSE, 0));
                }
                target.Add((Jsm.Opcode.RET, 8));
                byte[] content = SyntheticObjects([1, (16 << 7) | 4], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], caller.ToArray(), target.ToArray(), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 721), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]);
                EventEngine Create()
                {
                    EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy())) { AnimationFrameCount = (_, _) => 4 };
                    engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                    return engine;
                }
                EventEngine live = Create();
                long issued = -1;
                long started = -1;
                long returned = -1;
                long continued = -1;
                live.CommandExecuted += command =>
                {
                    if (command.ObjectId == 0 && command.Name == opcode.ToString() && command.Arguments.Count == 2 && command.Arguments[1] == script)
                    {
                        issued = live.Frame;
                    }
                    if (command is { ObjectId: 1, ScriptId: 19, Name: "LBL" })
                    {
                        started = live.Frame;
                    }
                    if (command is { ObjectId: 1, ScriptId: 19, Name: "RET" })
                    {
                        returned = live.Frame;
                    }
                    if (command is { ObjectId: 0, Name: "POPM_B" })
                    {
                        continued = live.Frame;
                    }
                };
                while (issued < 0 && live.Frame < 15)
                {
                    live.Step();
                }
                JsonElement pending = live.Capture();
                JsonElement held = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
                Require(issued > 0 && !live.Actors[1].Active && live.Scene.NativeActorObjectIds.Contains(1) == (mode != "Removed") && !CapturedVariables(pending).TryGetProperty("720", out _) && held.GetProperty("Stack").GetArrayLength() == 1 && held.GetProperty("Stack")[0].GetInt32() == 77, "Native inactive/null/occupied request changed retention/activation, consumed unrelated operands or lost issue return3 yield.");
                if (mode is "Accepted" or "Queued")
                {
                    JsonElement requested = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 19);
                    Require(requested.GetProperty("Priority").GetInt32() == 1 && requested.GetProperty("NativeActorSlot").GetInt32() == 1 && (opcode != Jsm.Opcode.REQEW || held.GetProperty("RequestSequence").GetInt64() == requested.GetProperty("Sequence").GetInt64()), "Native direct request did not narrow the global script label to WORD or retain its exact target/priority owner.");
                    if (opcode == Jsm.Opcode.REQSW && mode == "Queued")
                    {
                        Require(held.GetProperty("WaitReason").GetString() == "RequestStart" && !requested.GetProperty("Started").GetBoolean(), "Queued REQSW continued before selecting its lower native slot.");
                    }
                }
                EventEngine restored = Create();
                restored.Restore(pending);
                for (int tick = 0; tick < 20; tick++)
                {
                    live.Step();
                    restored.Step();
                    Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Inactive request queued/end/null/occupied continuation diverged after independent pending restore.");
                }
                Require(continued > issued && CapturedVariables(live.Capture()).GetProperty("720").GetInt32() == 77 && !live.Actors[1].Active && (mode != "Removed" || started == -1 && returned == -1), "Native request failed its successor, activated the body or executed a removed raw pointer slot.");
                if (mode is "Accepted" or "Queued")
                {
                    Require(started > 0 && returned > started && (opcode != Jsm.Opcode.REQEW || continued > returned) && (opcode != Jsm.Opcode.REQSW || mode != "Queued" || continued > started), "Native request start/end wait released before its selected-slot completion/yield contract.");
                }
                if (opcode == Jsm.Opcode.REQ || mode is "Removed" or "Occupied")
                {
                    Require(continued == issued + 1, "No-wait/null/occupied request introduced an extra wait instead of the native return3 callback yield.");
                }
            }
        }
        Console.WriteLine("Inactive REQ/REQSW/REQEW: retained versus init-removed pointers, WORD global labels, inactive queued start/end timing, occupied invalid-label drops, return3 yields, untouched stack and independent pending restore passed.");
    }
}
