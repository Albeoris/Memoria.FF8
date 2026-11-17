using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeWaitContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create((Jsm.Opcode, int)[] main, bool preempt = false, bool opaque = false)
        {
            List<(Jsm.Opcode, int)> initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0)];
            if (opaque)
            {
                initialize.AddRange([(Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.EFFECTLOAD, 0)]);
            }
            initialize.Add((Jsm.Opcode.RET, 8));
            List<(Jsm.Opcode, int)[]> scripts = [initialize.ToArray(), [(Jsm.Opcode.LBL, 1), .. main], [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]];
            ushort[] labels = [2];
            if (preempt)
            {
                labels = [2, (8 << 7) | 1];
                scripts.AddRange([[(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            }
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 2, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(600, SyntheticObjects(labels, scripts), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        static JsonElement Fiber(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
        static bool Successor(EventEngine engine) => CapturedVariables(engine.Capture()).TryGetProperty("5000", out _);
        (Jsm.Opcode, int)[] Waiting(int duration) => [(Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];

        EventEngine live = Create(Waiting(5), preempt: true);
        live.Step();
        JsonElement held = Fiber(live);
        int operation = held.GetProperty("Position").GetInt32();
        Require(held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([4, 123]) && held.GetProperty("WaitReason").GetString() == "None", "Native WAIT did not retain its decremented DWORD operand and issuing PC before preemption.");
        EventEngine restored = Create(Waiting(5), preempt: true);
        restored.Restore(live.Capture());
        for (int callback = 0; callback < 9; callback++)
        {
            live.Step();
            restored.Step();
            Require(Fiber(live).GetProperty("Position").GetInt32() == operation && Fiber(live).GetProperty("Stack")[0].GetInt32() == 4 && !Successor(live) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "A suspended native WAIT counted elapsed callbacks or diverged after independent restore.");
        }
        for (int remaining = 3; remaining >= 0; remaining--)
        {
            live.Step();
            restored.Step();
            JsonElement current = Fiber(live);
            Require(!Successor(live) && current.GetProperty("Position").GetInt32() == operation + (remaining == 0 ? 1 : 0) && current.GetProperty("Stack").GetArrayLength() == (remaining == 0 ? 1 : 2) && current.GetProperty("Stack")[0].GetInt32() == (remaining == 0 ? 123 : remaining) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), $"Resumed WAIT lost its selected-dispatch countdown, lower stack or completion yield: expected {remaining}, fiber {current}.");
        }
        live.Step();
        restored.Step();
        Require(Successor(live) && Fiber(live).GetProperty("Results").GetProperty("7").GetInt32() == 123 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native WAIT failed its next-callback successor or preserved lower operand.");

        EventEngine one = Create(Waiting(1));
        one.Step();
        Require(!Successor(one) && Fiber(one).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([123]), "WAIT1 did not consume exactly one operand and yield before its successor.");
        one.Step();
        Require(Successor(one), "WAIT1 did not resume on its next selected callback.");
        foreach (int initial in new[] { 0, -1, -65536 })
        {
            EventEngine wrapping = Create(Waiting(initial));
            wrapping.Step();
            JsonElement pending = Fiber(wrapping);
            int waitOperation = pending.GetProperty("Position").GetInt32();
            Require(pending.GetProperty("Stack")[0].GetInt32() == unchecked(initial - 1) && !Successor(wrapping), "Native WAIT narrowed or clamped its wrapping DWORD countdown.");
            EventEngine copy = Create(Waiting(initial));
            copy.Restore(wrapping.Capture());
            wrapping.Step();
            copy.Step();
            Require(Fiber(wrapping).GetProperty("Position").GetInt32() == waitOperation && Fiber(wrapping).GetProperty("Stack")[0].GetInt32() == unchecked(initial - 2) && JsonElement.DeepEquals(wrapping.Capture(), copy.Capture()), "Wrapped WAIT did not retain its current operand through restore.");
        }
        EventEngine uncertain = Create([(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], opaque: true);
        try
        {
            uncertain.Step();
            throw new InvalidOperationException("Native WAIT consumed an uncertain original DWORD.");
        }
        catch (EventScriptException) when (uncertain.LastFault?.Opcode == "WAIT")
        {
        }
        JsonElement refused = Fiber(uncertain);
        Require(refused.GetProperty("Position").GetInt32() == uncertain.LastFault!.Operation && refused.GetProperty("Stack").GetArrayLength() == 1 && refused.GetProperty("Stack")[0].ValueKind == JsonValueKind.Object, "Unsupported WAIT changed its issuing PC or consumed its uncertain scalar.");
        EventEngine Door()
        {
            List<(Jsm.Opcode, int)[]> scripts = [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]
            ];
            for (int label = 19; label <= 23; label++)
            {
                scripts.Add([(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)]);
            }
            byte[] content = SyntheticObjects([(16 << 7) | 7, 1], scripts);
            content[1] = 1;
            content[3] = 1;
            EventEngine engine = new(CreateDirectedLinearTurnSession(vitals, economy));
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        static JsonElement DoorLower(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        EventEngine door = Door();
        door.Step();
        Require(DoorLower(door).GetProperty("Stack")[0].GetInt32() == 4, "Door default did not issue its first WAIT countdown before the actor's higher request.");
        door.Step();
        EventEngine doorCopy = Door();
        doorCopy.Restore(door.Capture());
        for (int callback = 0; callback < 8; callback++)
        {
            door.Step();
            doorCopy.Step();
            Require(DoorLower(door).GetProperty("Stack")[0].GetInt32() == 4 && JsonElement.DeepEquals(door.Capture(), doorCopy.Capture()), "Suspended door WAIT decremented beneath a higher native request or changed after restore.");
        }
        door.Step();
        doorCopy.Step();
        Require(DoorLower(door).GetProperty("Stack")[0].GetInt32() == 3 && JsonElement.DeepEquals(door.Capture(), doorCopy.Capture()), "Door RET did not resume its lower WAIT exactly once within the same remaining entity budget.");
        Console.WriteLine("Native WAIT: selected-dispatch DWORD countdown, suspended request stack, completion yield, wrapping operands, uncertain refusal and independent restore PASS.");
    }
}
