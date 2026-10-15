using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCosineOffsetExplicitStartContract()
    {
        byte[] content = FieldMediaProgram((Jsm.Opcode.PSHN_L, 65541), (Jsm.Opcode.PSHN_L, 65542), (Jsm.Opcode.PSHN_L, 65543), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, -65736), (Jsm.Opcode.PSHN_L, 65836), (Jsm.Opcode.PSHN_L, -65566), (Jsm.Opcode.PSHN_L, 65566), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.COFFSETS, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 27), (Jsm.Opcode.POPM_W, 512));
        EventEngine engine = CreateFieldMedia(content);
        Vector3 physical = new(engine.Actors[0].X, engine.Actors[0].Y, engine.Actors[0].Z);
        int triangle = engine.Actors[0].WalkmeshTriangle;
        int speed = engine.Actors[0].MovementSpeed;
        float direction = engine.Actors[0].Direction;
        ushort? landing = null;
        ushort? completion = null;
        bool observed = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "DOFFSET")
            {
                landing = engine.Actors[0].NativeLandingTriangle508;
                completion = engine.Actors[0].NativeMovementCompletion542;
            }
            if (command.Name == "COFFSETS")
            {
                Require(command.Arguments.Count == 7 && engine.Actors[0].NativeDisplayOffset is { X: 5, Y: 6, Z: 7, StartX: 100, StartY: -200, StartZ: 300, TargetX: -30, TargetY: 30, TargetZ: 0, Duration: 4, Counter: 0, Mode: 2 }, "COFFSETS must write seven low-WORD starts/targets/duration while retaining current words until post-VM evaluation.");
                Require(landing is not null && completion is not null && engine.Actors[0].NativeLandingTriangle508 == landing && engine.Actors[0].NativeMovementCompletion542 == completion, "COFFSETS changed unrelated physical landing/completion words508/542.");
                observed = true;
            }
        };
        engine.Step();
        Require(observed && engine.Actors[0].DisplayOffset == new Vector3(100, -200, 300) && CapturedVariables(engine.Capture()).GetProperty("514").GetInt32() == 88, "COFFSETS did not continue its successor in the issuing callback and publish explicit start during post-VM.");
        JsonElement first = engine.Capture();
        EventEngine restored = CreateFieldMedia(content);
        restored.Restore(first);
        // Native signed cosine table values at phases160/192/224/0 are -2896/0/2896/4096.
        Vector3[] golden = [new(81, -167, 257), new(35, -85, 150), new(-10, -4, 44), new(-30, 30, 0)];
        foreach (Vector3 expected in golden)
        {
            engine.Step();
            restored.Step();
            Require(engine.Actors[0].DisplayOffset == expected && JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && !CapturedVariables(engine.Capture()).TryGetProperty("512", out _), "COFFSETS cosine table result, exact restored callback or OFFSETSYNC observation order differs.");
            Require(new Vector3(engine.Actors[0].X, engine.Actors[0].Y, engine.Actors[0].Z) == physical && engine.Actors[0].WalkmeshTriangle == triangle && engine.Actors[0].MovementSpeed == speed && engine.Actors[0].Direction == direction && engine.Actors[0].NativeLandingTriangle508 == landing && engine.Actors[0].NativeMovementCompletion542 == completion, "Display-only cosine offsets changed physical position, triangle, speed, facing or landing/completion words.");
        }
        Require(engine.Actors[0].NativeDisplayOffset is { Mode: 3, Counter: 4 }, "COFFSETS did not complete at its unsigned duration.");
        engine.Step();
        restored.Step();
        Require(CapturedVariables(engine.Capture()).GetProperty("512").GetInt32() == 27 && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "OFFSETSYNC did not continue its successor in the first callback observing completed mode3.");

        JsonObject malformed = JsonNode.Parse(first.GetRawText())!.AsObject();
        malformed["Actors"]![0]!["NativeDisplayOffset"]!["StartX"] = 99;
        JsonElement before = engine.Capture();
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Altered explicit cosine start was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, engine.Capture()), "Malformed COFFSETS restore changed the complete VM.");

        byte[] zeroContent = FieldMediaProgram((Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.PSHN_L, -15), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.COFFSETS, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.POPM_W, 512));
        EventEngine zero = CreateFieldMedia(zeroContent);
        zero.Step();
        Require(zero.Actors[0].NativeDisplayOffset is { X: 100, Y: 200, Z: 300, StartX: 11, StartY: -15, StartZ: 8, Duration: 0, Counter: 0, Mode: 3 }, "Zero-WORD-duration cosine offsets must select target at counter0, preserving independently written start.");
        zero.Step();
        Require(CapturedVariables(zero.Capture()).GetProperty("512").GetInt32() == 13, "Zero-duration cosine OFFSETSYNC did not continue on the next ordinary callback.");

        byte[] longContent = FieldMediaProgram((Jsm.Opcode.PSHN_L, -32768), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 32767), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.COFFSETS, 0), (Jsm.Opcode.OFFSETSYNC, 0));
        EventEngine longDuration = CreateFieldMedia(longContent);
        longDuration.Step();
        Require(longDuration.Actors[0].NativeDisplayOffset is { Duration: ushort.MaxValue, Counter: 1, Mode: 2, X: short.MinValue }, "COFFSETS interpreted durationFFFF as a signed delay or skipped counter0.");
        EventEngine longRestored = CreateFieldMedia(longContent);
        longRestored.Restore(longDuration.Capture());
        longDuration.Step();
        longRestored.Step();
        Require(longDuration.Actors[0].NativeDisplayOffset is { Counter: 2, Mode: 2 } && JsonElement.DeepEquals(longDuration.Capture(), longRestored.Capture()), "Unsigned COFFSETS duration failed current phase restoration.");
        Require(NativeCosineInterpolation.Interpolate(-32768, 32767, 65535, 65534) == 32751 && NativeCosineInterpolation.Interpolate(-32768, 32767, 65535, 65535) == 32767, "Native unsigned-duration last counter or cosine endpoint wraps incorrectly.");

        byte[] actualShape = FieldMediaProgram((Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 656), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.COFFSETS, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512));
        EventEngine twenty = CreateFieldMedia(actualShape);
        twenty.Step();
        for (int elapsed = 1; elapsed <= 20; elapsed++)
        {
            twenty.Step();
            if (elapsed == 10)
            {
                Require(twenty.Actors[0].DisplayOffset.Z == 328, "Twenty-frame cosine midpoint is not half the explicit656 start.");
            }
        }
        Require(twenty.Actors[0].NativeDisplayOffset is { X: 0, Y: 0, Z: 0, Mode: 3, Counter: 20 } && !CapturedVariables(twenty.Capture()).TryGetProperty("512", out _), "Actual219 operand shape did not complete after counter20 post-VM.");
        twenty.Step();
        Require(CapturedVariables(twenty.Capture()).GetProperty("512").GetInt32() == 7, "Twenty-frame cosine offset completion did not resume its original successor.");

        EventEngine unknown = CreateFieldMedia(actualShape, native: false);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("COFFSETS inferred an unknown actor's native display state.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "COFFSETS")
        {
        }
        JsonElement held = unknown.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(unknown.Actors[0].NativeDisplayOffset is null && unknown.Actors[0].DisplayOffset == Vector3.Zero && held.GetProperty("Stack").GetArrayLength() == 7, "Unknown-state COFFSETS refusal consumed original operands or supplied offset defaults.");

        EventEngine missing = CreateFieldMedia(FieldMediaProgram((Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.COFFSETS, 0)));
        missing.Step();
        JsonObject incomplete = JsonNode.Parse(missing.Capture().GetRawText())!.AsObject();
        JsonNode issuing = incomplete["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == 1)!;
        issuing["Stack"]!.AsArray().RemoveAt(0);
        issuing["StackExpressions"]!.AsArray().RemoveAt(0);
        int position = issuing["Position"]!.GetValue<int>();
        missing.Restore(JsonSerializer.SerializeToElement(incomplete));
        JsonElement allocated = missing.Capture().GetProperty("Actors");
        try
        {
            missing.Step();
            throw new InvalidOperationException("COFFSETS accepted six operands.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == "COFFSETS")
        {
        }
        JsonElement missingFiber = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(missingFiber.GetProperty("Position").GetInt32() == position && missingFiber.GetProperty("Stack").GetArrayLength() == 6 && JsonElement.DeepEquals(allocated, missing.Capture().GetProperty("Actors")), "Incomplete COFFSETS packet changed its original PC, operands or actor words.");
        Console.WriteLine("COFFSETS: seven WORD operands, cosine goldens, issuing/post-VM ordering, unsigned/zero duration, physical-body independence, exact restore, actual219 twenty-frame shape and atomic unknown/malformed refusal passed.");
    }
}
