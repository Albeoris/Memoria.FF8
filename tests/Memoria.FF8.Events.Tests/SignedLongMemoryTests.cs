using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSignedLongMemoryContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (int source in new[] { int.MinValue, -0x800001, -0x800000, -1, 0, 0x7FFFFF, 0x800000, int.MaxValue })
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHSM_L, 4000), (Jsm.Opcode.POPM_L, 4004), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.POPM_B, 4008), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
                EventEngine engine = new(session, NativeEventMemory.Empty.Write(4000, 4, NativeScalarValue.Exact(source))) { AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            live.Step();
            Require(live.EventMemory.Read(4004, 4).RequireExact() == Math.Clamp(source, -0x800000, 0x7FFFFF) && live.EventMemory.Read(4000, 4).RequireExact() == source && live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack")[0].GetInt32() == 77, "PSHSM_L failed native signed24 saturation, changed its DWORD source or consumed the existing stack value.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            for (int callback = 0; callback < 2; callback++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Signed-long memory diverged on an independently restored Wait callback.");
            }
            Require(live.EventMemory.Read(4008, 1).RequireExact() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Signed-long read did not complete immediately or diverged on the next Wait callback after independent restore.");
        }

        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(0) with { Alternatives = new[] { -0x800001, -0x7FFFFF, 0x7FFFFE, 0x800001 }.Select(value => new NativeTimerAlternative(0, 0, value)).ToArray() };
        NativeScalarValue uncertain = NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 600, new string('A', 64), 0, 1, 5, 2, timer));
        NativeScalarValue saturated = uncertain.SaturateSignedLong();
        Require(saturated.Values.SequenceEqual(new[] { -0x800000, -0x7FFFFF, 0x7FFFFE, 0x7FFFFF }) && saturated.Origin?.Projections is [{ Width: 4, Signed: false, SaturateSignedLong: true }] && uncertain.Values.SequenceEqual(new[] { -0x800001, -0x7FFFFF, 0x7FFFFE, 0x800001 }), "Signed-long saturation selected one uncertain result, discarded its executed origin or changed source alternatives.");
        NativeScalarValue decoded = JsonSerializer.Deserialize<NativeScalarValue>(JsonSerializer.Serialize(saturated))!;
        decoded.Validate();
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saturated), JsonSerializer.SerializeToElement(decoded)) && decoded.ToWidth(1, true).Values.SequenceEqual(new[] { -2, -1, 0, 1 }) && decoded.SaturateSignedLong().Origin!.Projections!.Length == 1, "Signed24 provenance failed roundtrip, later BYTE projection or idempotent saturation.");
        NativeScalarValue collapsed = NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 600, new string('A', 64), 0, 1, 5, 2, timer with { Alternatives = [new NativeTimerAlternative(0, 0, 0x800000), new NativeTimerAlternative(0, 0, 0x800001)] })).SaturateSignedLong();
        Require(collapsed.IsExact && collapsed.RequireExact() == 0x7FFFFF, "Unanimous saturated alternatives failed to become their exact native result.");
        JsonNode malformed = JsonNode.Parse(JsonSerializer.Serialize(saturated))!;
        malformed["Origin"]!["Projections"]![0]!["Width"] = 2;
        try
        {
            _ = JsonSerializer.Deserialize<NativeScalarValue>(malformed.ToJsonString());
            throw new InvalidOperationException("Signed-long saturation accepted a malformed projection width.");
        }
        catch (InvalidDataException)
        {
        }

        foreach (int address in new[] { -1, 65534, 181 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHSM_L, address), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
            GameSession session = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(60) });
            EventEngine refused = new(session);
            refused.LoadField(600, content, []);
            try
            {
                refused.Step();
                refused.Step();
                throw new InvalidOperationException($"Signed-long read accepted an invalid address or crossed the canonical timer owner: {address}.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.PSHSM_L))
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack").GetArrayLength() == 1 && fiber.GetProperty("Stack")[0].GetInt32() == 77, "Rejected signed-long read changed the original PC or operand stack.");
        }
        Console.WriteLine("Native PSHSM_L: DWORD source, signed24 saturation, immediate stack push, independent next-callback restore, uncertain threshold projection/roundtrip, unanimous result and atomic invalid-address/owner refusal pass.");
    }
}
