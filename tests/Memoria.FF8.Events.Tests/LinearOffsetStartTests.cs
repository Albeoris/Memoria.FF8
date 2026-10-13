using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLinearOffsetExplicitStartContract()
    {
        (Jsm.Opcode, int)[] offsets = [(Jsm.Opcode.PSHN_L, 65541), (Jsm.Opcode.PSHN_L, 65542), (Jsm.Opcode.PSHN_L, 65543), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, -65736), (Jsm.Opcode.PSHN_L, 65836), (Jsm.Opcode.PSHN_L, -65566), (Jsm.Opcode.PSHN_L, 65566), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.LOFFSETS, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 27), (Jsm.Opcode.POPM_W, 512)];
        byte[] content = FieldMediaProgram(offsets);
        EventEngine engine = CreateFieldMedia(content);
        bool observed = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "LOFFSETS")
            {
                Require(command.Arguments.Count == 7 && engine.Actors[0].NativeDisplayOffset is { X: 5, Y: 6, Z: 7, StartX: 100, StartY: -200, StartZ: 300, TargetX: -30, TargetY: 30, TargetZ: 0, Duration: 4, Counter: 0, Mode: 1 }, "LOFFSETS did not preserve current words while writing explicit low-WORD start, target, duration and linear phase.");
                observed = true;
            }
        };
        engine.Step();
        Require(observed && engine.Actors[0].DisplayOffset == new Vector3(100, -200, 300), "Native LOFFSETS counter0 did not publish its explicit start after VM dispatch.");
        JsonElement first = engine.Capture();
        EventEngine restored = CreateFieldMedia(content);
        restored.Restore(first);
        Vector3[] expected = [new(68, -143, 225), new(35, -85, 150), new(3, -28, 75), new(-30, 30, 0)];
        foreach (Vector3 value in expected)
        {
            engine.Step();
            restored.Step();
            Require(engine.Actors[0].DisplayOffset == value && JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && !engine.Capture().GetProperty("Variables").TryGetProperty("512", out _), "Linear offset progression, restored VM or OFFSETSYNC completion ordering differs from native.");
        }
        engine.Step();
        restored.Step();
        Require(engine.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 27 && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Completed explicit-start offset did not resume on its next ordinary VM callback.");
        JsonObject malformed = JsonNode.Parse(first.GetRawText())!.AsObject();
        malformed["Actors"]![0]!["NativeDisplayOffset"]!["StartX"] = 99;
        JsonElement before = engine.Capture();
        try
        {
            engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Altered explicit start was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, engine.Capture()), "Malformed LOFFSETS restore mutated the complete VM.");

        byte[] zeroContent = FieldMediaProgram((Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.PSHN_L, -15), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.LOFFSETS, 0), (Jsm.Opcode.OFFSETSYNC, 0));
        EventEngine zero = CreateFieldMedia(zeroContent);
        zero.Step();
        Require(zero.Actors[0].NativeDisplayOffset is { X: 11, Y: -15, Z: 8, TargetX: 100, TargetY: 200, TargetZ: 300, Duration: 0, Counter: 0, Mode: 3 }, "Native zero-duration linear explicit offset must complete at its start while retaining its independently written target.");
        EventEngine legacy = CreateFieldMedia(content, native: false);
        try
        {
            legacy.Step();
            throw new InvalidOperationException("LOFFSETS inferred native allocation for a legacy actor.");
        }
        catch (EventScriptException) when (legacy.LastFault?.Opcode == "LOFFSETS")
        {
        }
        Require(legacy.Actors[0].NativeDisplayOffset is null && legacy.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 7, "Unproved LOFFSETS allocation consumed its original operands.");

        byte[] waitingContent = FieldMediaProgram([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.LOFFSETS, 0)]);
        EventEngine missing = CreateFieldMedia(waitingContent);
        missing.Step();
        JsonObject incomplete = JsonNode.Parse(missing.Capture().GetRawText())!.AsObject();
        JsonNode fiber = incomplete["Fibers"]!.AsArray().Single(item => item!["ScriptId"]!.GetValue<int>() == 1)!;
        fiber["Stack"]!.AsArray().RemoveAt(0);
        fiber["StackExpressions"]!.AsArray().RemoveAt(0);
        int position = fiber["Position"]!.GetValue<int>();
        missing.Restore(JsonSerializer.SerializeToElement(incomplete));
        NativeDisplayOffsetState? original = missing.Actors[0].NativeDisplayOffset;
        try
        {
            missing.Step();
            throw new InvalidOperationException("LOFFSETS consumed an incomplete operand packet.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == "LOFFSETS")
        {
        }
        JsonElement failedFiber = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(failedFiber.GetProperty("Position").GetInt32() == position && failedFiber.GetProperty("Stack").GetArrayLength() == 6 && missing.Actors[0].NativeDisplayOffset == original, "LOFFSETS missing-operand refusal changed PC, operands or display words.");
        Console.WriteLine("LOFFSETS: explicit signed starts/targets, WORD duration, linear counter0/completion, zero-duration start, independent VM restore and atomic missing/legacy/malformed refusal passed.");
    }
}
