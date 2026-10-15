using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFootstepOffAllContract()
    {
        byte[] Content(bool initializationClear)
        {
            List<(Jsm.Opcode Opcode, int Parameter)> removedInit = [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.FOOTSTEPON, 0)];
            if (initializationClear)
            {
                removedInit.Add((Jsm.Opcode.FOOTSTEPOFFALL, 0));
            }
            removedInit.AddRange([(Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)]);
            return SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 1, 48 << 7],
            [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.FOOTSTEP, 34), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOOTSTEPOFFALL, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.PSHN_L, 56), (Jsm.Opcode.FOOTSTEP, 78), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)],
                removedInit.ToArray()
            ]);
        }
        (GameSession Session, EventEngine Engine) Create(byte[] content, bool authenticated = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(600, content, [], nativeRuntimeProfile: authenticated ? NativeFieldRuntimeProfile.FF8En : null);
            return (session, engine);
        }
        byte[] content = Content(false);
        var live = Create(content);
        live.Engine.Step();
        Require(live.Engine.Actors.All(actor => actor.FootstepsEnabled) && live.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 1, 2]), "All-off fixture did not establish native enabled flags and remove the fourth actor during compaction.");
        live.Engine.Step();
        Require(!live.Engine.Actors[1].Visible && !live.Engine.Actors[2].Active, "All-off fixture did not retain its hidden/inactive dense actors.");
        NativeFootstepPairState?[] pairs = live.Engine.Actors.Select(actor => actor.NativeFootstepPair).ToArray();
        var restored = Create(content);
        restored.Engine.Restore(live.Engine.Capture());
        bool observed = false;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.FOOTSTEPOFFALL))
            {
                observed = true;
                Require(command.Arguments.Count == 0 && live.Engine.Actors.Take(3).All(actor => !actor.FootstepsEnabled) && live.Engine.Actors[3].FootstepsEnabled, "FOOTSTEPOFFALL consumed operands, omitted retained dense actors or cleared an actor removed by compaction.");
                Require(live.Engine.Actors.Select(actor => actor.NativeFootstepPair).SequenceEqual(pairs), "FOOTSTEPOFFALL changed selection bytes or their native provenance.");
            }
        };
        live.Engine.Step();
        restored.Engine.Step();
        Require(observed && live.Engine.Scene.FootstepOffAll is { ActorLayoutVersion: 1, ClearedMask: 128 } request && request.Targets.SequenceEqual([0, 1, 2]) && live.Session.State.Variables[500] == 9 && live.Engine.Actors[0].FootstepsEnabled && !live.Engine.Actors[1].FootstepsEnabled && !live.Engine.Actors[2].FootstepsEnabled, "All-off lost dense target provenance, introduced a wait or invented a global gate preventing later per-actor ON.");
        Require(live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Results").GetProperty("3").GetInt32() == 77 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "All-off consumed the sentinel or diverged after independent restore.");
        JsonElement completed = live.Engine.Capture();
        foreach (string part in new[] { "targets", "mask", "source", "frame", "runtime" })
        {
            JsonNode malformed = JsonNode.Parse(completed.GetRawText())!;
            JsonNode receipt = malformed["Scene"]!["FootstepOffAll"]!;
            if (part == "targets")
            {
                receipt["Targets"]![0] = 2;
            }
            else if (part == "mask")
            {
                receipt["ClearedMask"] = 0;
            }
            else if (part == "source")
            {
                receipt["Operation"] = 0;
            }
            else if (part == "frame")
            {
                receipt["Frame"] = live.Engine.Frame + 1;
            }
            else
            {
                receipt["ExecutableHash"] = new string('0', 64);
            }
            try
            {
                live.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed all-off provenance passed restore.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(completed, live.Engine.Capture()), "Rejected all-off receipt changed the receiver.");
        }
        var allocation = Create(Content(true));
        allocation.Engine.Step();
        Require(allocation.Engine.Scene.FootstepOffAll is { ActorLayoutVersion: 0 } allocationClear && allocationClear.Targets.SequenceEqual([0, 1, 2, 3]) && allocation.Engine.Actors.All(actor => !actor.FootstepsEnabled), "Initialization all-off failed the original allocated dense target range before compaction.");
        var allocationRestored = Create(Content(true));
        allocationRestored.Engine.Restore(allocation.Engine.Capture());
        Require(JsonElement.DeepEquals(allocation.Engine.Capture(), allocationRestored.Engine.Capture()), "Precompaction all-off provenance failed independent restore.");
        live.Engine.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.Engine.Scene.FootstepOffAll is null, "Fresh allocation retained a different field's all-off mutation receipt.");
        byte[] noActors = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOOTSTEPOFFALL, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.RET, 0));
        noActors[2] = 1;
        noActors[3] = 0;
        var empty = Create(noActors);
        empty.Engine.Step();
        Require(empty.Engine.Scene.FootstepOffAll?.Targets.Length == 0 && empty.Engine.Scene.NativeActorObjectIds.Count == 0 && empty.Session.State.Variables[500] == 9 && empty.Engine.Actors[0].NativeFootstepPair is null, "Zero actor count tried to read caller actor memory or introduced a wait.");
        var emptyRestored = Create(noActors);
        emptyRestored.Engine.Restore(empty.Engine.Capture());
        Require(JsonElement.DeepEquals(empty.Engine.Capture(), emptyRestored.Engine.Capture()), "Zero-target all-off failed independent restore.");
        var refused = Create(noActors, false);
        try
        {
            refused.Engine.Step();
            throw new InvalidOperationException("Unknown native actor layout was admitted by all-off.");
        }
        catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.FOOTSTEPOFFALL))
        {
        }
        Require(refused.Engine.Scene.FootstepOffAll is null && refused.Engine.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && refused.Engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77, "All-off refusal changed its PC, sentinel or receipt.");
        Console.WriteLine("Native FOOTSTEPOFFALL: zero operands, actual dense actor mask80 clear, hidden/inactive retained targets, pre/postcompaction, later per-actor ON, unchanged pairs, zero count, atomic restore and native-layout refusal verified.");
    }
}
