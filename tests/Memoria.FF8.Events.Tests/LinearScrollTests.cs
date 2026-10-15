using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLinearScrollAndSyncContract(string gameDirectory)
    {
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 65543), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 65543), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        EventEngine CreateSynthetic(bool certificate = true)
        {
            GameSession session = new(new GameState());
            EventEngine result = new(session) { CanSetLinearBackgroundScroll = query => certificate && query == new NativeLinearScrollQuery(1, 0, 0, 7, 0, 7) };
            result.LoadField(100, program, []);
            return result;
        }
        EventEngine phase = CreateSynthetic();
        phase.Scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(44, 8, 44, 8, -7, -8, 17, 3, 0);
        BackgroundScrollPosition? commandState = null;
        JsonElement? commandSnapshot = null;
        phase.CommandExecuted += command =>
        {
            if (command.Name == "LSCROLL3")
            {
                commandState = phase.Scene.BackgroundScrollPositions[1];
                commandSnapshot = phase.Capture();
            }
        };
        phase.Step();
        Require(commandState is { X: 0, Y: 0, PreviousX: -7, PreviousY: -8, Counter: 17, Mode: 4, Status: 0, Duration: 7, NativeLinearVersion: 1 }, "LSCROLL3 failed to write start/target lowWORDs or prematurely cleared prior counter/previous words.");
        Require(phase.Scene.BackgroundScrollPositions[1] is { X: 0, Y: 0, PreviousX: 0, PreviousY: 0, Counter: 1, Status: 1, NativeLinearVersion: 1 }, "Post-VM475DF0/475E60 did not snapshot start, reset counter and evaluate counter0.");
        JsonElement wait = phase.Capture().GetProperty("Fibers")[0];
        Require(wait.GetProperty("Position").GetInt32() == 9 && wait.GetProperty("Stack")[0].GetInt32() == 1 && wait.GetProperty("WaitReason").GetString() == "BackgroundScroll", "SCROLLSYNC2 consumed its PEEK layer or advanced its waiting PC.");
        EventEngine pending = CreateSynthetic();
        pending.Restore(commandSnapshot!.Value);
        pending.Step();
        Require(pending.Scene.BackgroundScrollPositions[1] is { Counter: 1, PreviousX: 0, PreviousY: 0, X: 0, Y: 0 }, "Restoring pending status0 invented cleared counter/previous words or missed their actual post-VM reset.");
        EventEngine replay = CreateSynthetic();
        replay.Restore(phase.Capture());
        for (int elapsed = 1; elapsed <= 7; elapsed++)
        {
            phase.Step();
            replay.Step();
            BackgroundScrollPosition state = phase.Scene.BackgroundScrollPositions[1];
            Require(state.X == elapsed && state.Y == 0 && state.Counter == Math.Min(elapsed + 1, 7) && state.Status == (elapsed == 7 ? 2 : 1) && !CapturedVariables(phase.Capture()).TryGetProperty("512", out _) && JsonElement.DeepEquals(phase.Capture(), replay.Capture()), "Native linear phase changed inclusive endpoints, held layer operand, earliest synchronization or restored VM.");
        }
        phase.Step();
        replay.Step();
        Require(CapturedVariables(phase.Capture()).GetProperty("512").GetInt32() == 9 && phase.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0 && JsonElement.DeepEquals(phase.Capture(), replay.Capture()), "SCROLLSYNC2 did not pop and continue on the first VM update after native completion.");
        EventEngine refused = CreateSynthetic(false);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Uncertified LSCROLL3 consumed its command.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "LSCROLL3")
        {
        }
        JsonElement held = refused.Capture().GetProperty("Fibers")[0];
        Require(held.GetProperty("Position").GetInt32() == 7 && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65543, -65536, 65543, -65536, 65536, 1]) && refused.Scene.BackgroundScrollPositions.Count == 0, "Refused LSCROLL3 changed original PC, raw operands or scroll records.");
        EventEngine liveRefused = CreateSynthetic();
        liveRefused.CanBeginLinearBackgroundScroll = (_, scene) => scene == liveRefused.Scene && false;
        try
        {
            liveRefused.Step();
            throw new InvalidOperationException("Live renderer refusal consumed LSCROLL3.");
        }
        catch (EventScriptException) when (liveRefused.LastFault?.Opcode == "LSCROLL3")
        {
        }
        Require(JsonElement.DeepEquals(held, liveRefused.Capture().GetProperty("Fibers")[0]) && liveRefused.Scene.BackgroundScrollPositions.Count == 0, "Live LSCROLL3 refusal mutated operands, PC or layer records.");
        EventEngine pureRestore = CreateSynthetic();
        pureRestore.CanBeginLinearBackgroundScroll = (_, _) => throw new InvalidOperationException("Restore consulted the host's live scene.");
        pureRestore.CanRestoreLinearBackgroundScroll = candidate => !ReferenceEquals(candidate, pureRestore.Scene) && candidate.BackgroundScrollPositions[1].NativeLinearVersion == 1;
        pureRestore.Restore(commandSnapshot.Value);
        Require(JsonElement.DeepEquals(commandSnapshot.Value, pureRestore.Capture()), "Pure scroll restore changed the captured command phase.");
        JsonElement beforeRestoreRefusal = pureRestore.Capture();
        pureRestore.CanRestoreLinearBackgroundScroll = _ => false;
        try
        {
            pureRestore.Restore(commandSnapshot.Value);
            throw new InvalidOperationException("Candidate-scene renderer refusal published the saved linear phase.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(beforeRestoreRefusal, pureRestore.Capture()), "Candidate-scene linear restore refusal changed the complete VM.");
        foreach ((short start, short target, short duration) in new (short, short, short)[] { (3, -8, 7), (12, 25, 0) })
        {
            GameSession sampleSession = new(new GameState());
            EventEngine sample = new(sampleSession) { CanSetLinearBackgroundScroll = query => query == new NativeLinearScrollQuery(1, start, 0, target, 0, duration) };
            sample.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, target), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.RET, 0)), []);
            for (int counter = 0; counter <= duration; counter++)
            {
                sample.Step();
                short expected = duration == 0 ? start : (short)(start + counter * (target - start) / duration);
                Require(sample.Scene.BackgroundScrollPositions[1].X == expected && sample.Scene.BackgroundScrollPositions[1].Status == (counter == duration ? 2 : 1), "Native signed linear IDIV truncation or zero-duration previous-position branch differs.");
            }
        }
        GameSession legacyLoopSession = new(new GameState());
        EventEngine legacyLoop = new(legacyLoopSession) { CanSetLinearBackgroundScroll = query => query == new NativeLinearScrollQuery(1, 0, 0, 7, 0, 7) };
        legacyLoop.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.JMP, -12), (Jsm.Opcode.RET, 0)), []);
        for (int tick = 0; tick < 9; tick++)
        {
            legacyLoop.Step();
        }
        JsonElement legacyFiber = legacyLoop.Capture().GetProperty("Fibers")[0];
        Require(legacyFiber.GetProperty("Position").GetInt32() == 9 && legacyFiber.GetProperty("WaitReason").GetString() == "BackgroundScroll" && legacyLoop.Scene.BackgroundScrollPositions[1] is { Counter: 1, Status: 1 }, "Backward JMP must restart the ordinary linear scroll within the same native budget and hold its actual synchronization command.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(135);
        string source = Path.Combine("../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        EventEngine CreateActual()
        {
            GameSession session = new(GameSession.Load(source));
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanSetLinearBackgroundScroll = query => field.Id == 135 && Convert.ToHexString(SHA256.HashData(field.Jsm)) == "4E838898400B0B44D1FFAD537A77FDD685111A7C25DF34366CD77CACE8A65344" && query == new NativeLinearScrollQuery(1, 0, 0, 351, 0, 351)
            };
            engine.LoadField(135, field.Jsm, field.Messages);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine actual = CreateActual();
        actual.Step();
        Require(actual.Frame == 3 && actual.Scene.BackgroundScrollPositions[1] is { X: 0, Y: 0, TargetX: 351, TargetY: 0, PreviousX: 0, PreviousY: 0, Duration: 351, Counter: 1, Status: 1, Mode: 4, NativeLinearVersion: 1 }, "Actual1353 LSCROLL3 changed native packet or initial linear phase.");
        JsonElement initial = actual.Capture();
        JsonElement actualWait = initial.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 13 && fiber.GetProperty("ScriptId").GetInt32() == 53);
        Require(actualWait.GetProperty("Position").GetInt32() == 314 && actualWait.GetProperty("Stack")[0].GetInt32() == 1 && actualWait.GetProperty("WaitReason").GetString() == "BackgroundScroll", "Actual135 SCROLLSYNC2 did not preserve layer1/operation314.");
        EventEngine independent = CreateActual();
        independent.Restore(initial);
        for (int frame = 4; frame <= 354; frame++)
        {
            actual.Step();
            independent.Step();
            BackgroundScrollPosition state = actual.Scene.BackgroundScrollPositions[1];
            Require(state.X == frame - 3 && state.Y == 0 && state.Counter == Math.Min(frame - 2, 351) && state.Status == (frame == 354 ? 2 : 1) && JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Actual135 native layer1 linear phase, held sync or complete independent VM differs.");
            if (frame == 200)
            {
                independent = CreateActual();
                independent.Restore(actual.Capture());
            }
        }
        actual.Step();
        independent.Step();
        Require(actual.Frame == 355 && actual.Scene.BackgroundScrollPositions[1] is { X: 351, Counter: 351, Status: 2 } && JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Actual sync completion did not obey its next-VM16-instruction budget.");
        JsonElement resumedWorker = actual.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 13 && fiber.GetProperty("ScriptId").GetInt32() == 53);
        Require(resumedWorker.GetProperty("Position").GetInt32() == 305 && resumedWorker.GetProperty("WaitReason").GetString() == "None" && resumedWorker.GetProperty("Stack")[0].GetInt32() == 0, "Native135 backward JMP yielded or exceeded the same16-operation budget instead of ending at305.");
        actual.Step();
        independent.Step();
        Require(actual.Frame == 356 && actual.Scene.BackgroundScrollPositions[1] is { X: 0, Counter: 1, Status: 1 } && JsonElement.DeepEquals(actual.Capture(), independent.Capture()), $"Original loop did not execute the next explicit-start scroll after its instruction budget resumed: {actual.Scene.BackgroundScrollPositions[1]}; worker={actual.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 13 && fiber.GetProperty("ScriptId").GetInt32() == 53)}.");
        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            node => node["Scene"]!["BackgroundScrollPositions"]!["1"]!["NativeLinearVersion"] = 0,
            node => node["Scene"]!["BackgroundScrollPositions"]!["1"]!["Counter"] = 0,
            node => node["Scene"]!["BackgroundScrollPositions"]!["1"]!["X"] = 1,
            node => node["Scene"]!["BackgroundScrollPositions"]!["1"]!["Duration"] = 350,
            node => node["Fibers"]!.AsArray().Single(fiber => fiber!["ObjectId"]!.GetValue<int>() == 13 && fiber["ScriptId"]!.GetValue<int>() == 53)!["Stack"] = new JsonArray(),
            node => node["Fibers"]!.AsArray().Single(fiber => fiber!["ObjectId"]!.GetValue<int>() == 13 && fiber["ScriptId"]!.GetValue<int>() == 53)!["Stack"]![0] = 0
        })
        {
            JsonNode altered = JsonNode.Parse(initial.GetRawText())!;
            corrupt(altered);
            JsonElement before = actual.Capture();
            try
            {
                actual.Restore(JsonSerializer.SerializeToElement(altered));
                throw new InvalidOperationException("Malformed linear phase or lost synchronization operand restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, actual.Capture()), "Malformed native linear-scroll restore changed the complete VM.");
        }
        Require(original.SequenceEqual(File.ReadAllBytes(source)), "Actual135 source checkpoint changed during component verification.");
        Console.WriteLine("LSCROLL3/SCROLLSYNC2: synthetic lowWORDs, retained pending counter/previous, inclusive linear updates, PEEK/PC wait, earliest pop, refusal and restores; actual1352->3->354 completion/355 budget/356 restart complete independent VM equality; six malformed checkpoint guards. No live actions or rendering proof.");
    }
}
