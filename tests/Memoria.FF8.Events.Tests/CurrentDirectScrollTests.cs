using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCurrentDirectScrollContract()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] first = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65544), (Jsm.Opcode.DSCROLL, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] secondInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] second = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -65544), (Jsm.Opcode.DSCROLL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1, (16 << 7) | 1], [init, first, secondInit, second]);
        EventEngine Create(bool known, bool admitted = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { CanSetBackgroundScrollPosition = (layer, _, _) => admitted && layer == 0 };
            engine.LoadField(600, content, []);
            engine.Step();
            if (known)
            {
                engine.Scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(13, -7, 13, -7, 5, 6, 0, 3, 2, 17, 1);
            }
            return engine;
        }
        EventEngine live = Create(true);
        List<int> order = [];
        live.CommandExecuted += command =>
        {
            if (command.Name == "DSCROLL")
            {
                order.Add(command.ObjectId);
                Require(live.Scene.BackgroundScrollPositions[0] is { X: 13, Y: -7, PreviousX: 5, PreviousY: 6, Mode: 3, Status: 0, Duration: 17, NativeActorId: 1 }, "Direct command changed current/previous words or unused metadata before post-VM475DF0.");
            }
        };
        live.Step();
        Require(order.SequenceEqual([0, 1]) && live.Scene.BackgroundScrollPositions[0] is { X: 0, Y: -8, TargetX: 0, TargetY: -8, PreviousX: 13, PreviousY: -7, Counter: 0, Mode: 3, Status: 2, Duration: 17, NativeActorId: 1 }, "Native actor order, lowWORD targets or same-callback direct replacement failed.");
        Require(!CapturedVariables(live.Capture()).TryGetProperty("5000", out _) && !CapturedVariables(live.Capture()).TryGetProperty("5001", out _), "Direct return3 did not yield each caller's instruction budget.");
        EventEngine resumed = Create(true);
        resumed.Restore(live.Capture());
        live.Step();
        resumed.Step();
        Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()) && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 9 && CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 7, "Independent direct-scroll restore or next-callback successor order diverged.");
        EventEngine initiallyUnknown = Create(false);
        initiallyUnknown.Step();
        Require(initiallyUnknown.Scene.BackgroundScrollPositions[0] is { X: 0, Y: -8, PreviousX: null, PreviousY: null, Counter: 0, Duration: null, Status: 2 }, "Direct placement required or invented unused prior current/previous/duration words.");
        byte[] unknownContent = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.DSCROLL, 0), (Jsm.Opcode.RET, 8));
        EventEngine Unknown()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { CanSetBackgroundScrollPosition = (_, _, _) => true };
            engine.LoadField(600, unknownContent, []);
            return engine;
        }
        EventEngine unknown = Unknown();
        JsonElement pending = default;
        unknown.CommandExecuted += command =>
        {
            if (command.Name == "DSCROLL")
            {
                Require(unknown.Scene.BackgroundScrollPositions[0] is { X: null, Y: null, PreviousX: null, PreviousY: null, Counter: null, Duration: null, Status: 0 }, "Pending direct placement supplied target-as-current or zero for unobserved native words.");
                pending = unknown.Capture();
            }
        };
        unknown.Step();
        EventEngine pendingResume = Unknown();
        pendingResume.Restore(pending);
        unknown.Step();
        pendingResume.Step();
        Require(JsonElement.DeepEquals(unknown.Capture(), pendingResume.Capture()), "Unknown direct pending words did not survive independent restore and target commit.");
        foreach (string word in new[] { "X", "Duration" })
        {
            var malformed = System.Text.Json.Nodes.JsonNode.Parse(pending.GetRawText())!;
            malformed["Scene"]!["BackgroundScrollPositions"]!["0"]!.AsObject().Remove(word);
            JsonElement before = pendingResume.Capture();
            try
            {
                pendingResume.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Missing physical scroll metadata was inferred as unknown.");
            }
            catch (JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, pendingResume.Capture()), "Malformed nullable scroll restore changed current VM or services.");
        }
        GameSession linearSession = new(new GameState());
        EventEngine explicitStart = new(linearSession) { CanSetLinearBackgroundScroll = _ => true, CanRestoreLinearBackgroundScroll = _ => true };
        explicitStart.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.PSHN_L, -9), (Jsm.Opcode.PSHN_L, 29), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)), []);
        explicitStart.CommandExecuted += command =>
        {
            if (command.Name == "LSCROLL3")
            {
                Require(explicitStart.Scene.BackgroundScrollPositions[0] is { X: 17, Y: -9, PreviousX: null, PreviousY: null, Counter: null, Duration: 4, Mode: 4, Status: 0 }, "Explicit LSCROLL3 start required or invented unconsumed prior words.");
            }
        };
        explicitStart.Step();
        Require(explicitStart.Scene.BackgroundScrollPositions[0] is { X: 17, Y: -9, PreviousX: 17, PreviousY: -9, Counter: 1, Duration: 4, Mode: 4, Status: 1 }, "Explicit-start post-VM phase did not establish its consumed previous/counter words.");
        foreach ((bool known, bool admitted) in new[] { (false, false), (true, false) })
        {
            EventEngine refused = Create(known, admitted);
            BackgroundScrollPosition? prior = refused.Scene.BackgroundScrollPositions.GetValueOrDefault(0);
            try
            {
                refused.Step();
                throw new InvalidOperationException("DSCROLL ignored renderer refusal.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "DSCROLL")
            {
            }
            JsonElement caller = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean());
            Require(caller.GetProperty("Position").GetInt32() == JsmProgram.Read(content).Operations.First(operation => operation.Opcode == Jsm.Opcode.DSCROLL).Index && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65544, 65536]) && refused.Scene.BackgroundScrollPositions.GetValueOrDefault(0) == prior, "Direct preflight refusal consumed original operands, changed its PC or changed current scroll state.");
        }
        Console.WriteLine("Native direct scroll: nonzero prior center/two-actor replacement, nullable unknown transient/unused words, signedWORD/yield3/same-callback target commit, independent pending/completed restore and renderer refusal passed.");
    }
}
