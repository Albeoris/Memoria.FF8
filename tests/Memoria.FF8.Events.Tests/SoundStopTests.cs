using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSoundStopContract()
    {
        foreach (uint rawMask in new[] { 1u, 0xFF800005u })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.SESTOP, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            GameSession session = new(new GameState());
            session.SetGlobalSoundProcess(NativeGlobalSoundProcess.UnknownBufferless().RecordEffect(123, 1, out _));
            string processBefore = JsonSerializer.Serialize(session.GlobalSoundProcess);
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(unchecked((int)rawMask))));
            EventEngine first = new(session);
            first.LoadField(100, content, []);
            first.Step();
            NativeSoundStop stopped = first.Scene.SoundStop ?? throw new InvalidOperationException("SESTOP did not record its original stop request.");
            Require(processBefore == JsonSerializer.Serialize(session.GlobalSoundProcess), "SESTOP read or changed unknown allocation/history instead of stopping its explicit voices.");
            Require(stopped.RawChannelMask == rawMask && stopped.ChannelMask == (rawMask & 0xFFFFFF) && stopped.StopMode == 0 && stopped.PollResult == 0 && stopped.BackendStatus == GlobalSoundBackendStatus.Muted && stopped.Voices.SequenceEqual(rawMask == 1 ? new[] { 1 } : new[] { 1, 3, 24 }), "SESTOP lost its DWORD operand, low24-bit selection or native immediate return.");
            JsonElement state = first.Capture();
            JsonElement stoppedFiber = state.GetProperty("Fibers")[0];
            //This synthetic script runs in synchronous initialization: Step retires WAIT and yields at RET,
            //with no ordinary frame wait. The sound command must preserve the sentinel and introduce no wait.
            Require(stoppedFiber.GetProperty("Results").GetProperty("3").GetInt32() == 1234 && stoppedFiber.GetProperty("Stack").GetArrayLength() == 0 && stoppedFiber.GetProperty("Position").GetInt32() == 7 && stoppedFiber.GetProperty("WaitReason").GetString() == "None", "SESTOP changed its sentinel, argument count or exact synchronous initialization boundary.");
            GameSession otherSession = new(new GameState());
            EventEngine restored = new(otherSession);
            restored.LoadField(100, content, []);
            restored.Restore(state);
            Require(processBefore == JsonSerializer.Serialize(otherSession.GlobalSoundProcess), "SESTOP restore changed the partial global sound process.");
            for (int tick = 0; tick < 4; tick++)
            {
                first.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "SESTOP provenance or VM diverged after independent restore.");
            }
            byte[] next = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0));
            first.LoadField(101, next, []);
            Require(first.Scene.SoundStop == stopped, "Normal field load erased the original stop request provenance.");
        }
        foreach (uint rawMask in new[] { 0u, 0xFF000000u, 0xFFFFFFu, uint.MaxValue })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.SESTOP, 0), (Jsm.Opcode.RET, 0));
            GameSession session = new(new GameState());
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(unchecked((int)rawMask))));
            EventEngine refused = new(session);
            refused.LoadField(100, content, []);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Unknown special-mask sound stop bypassed original voice authority.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "SESTOP")
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(refused.Scene.SoundStop is null && fiber.GetProperty("Position").GetInt32() == 2 && unchecked((uint)fiber.GetProperty("Stack")[0].GetInt32()) == rawMask, "Refused SESTOP changed its PC, raw operand or stop provenance.");
        }
        Console.WriteLine("Native SESTOP: DWORD mask/low24-bit voices, immediate zero poll and muted stop request; independent restore/history retained, special masks refused before consume.");
    }
}
