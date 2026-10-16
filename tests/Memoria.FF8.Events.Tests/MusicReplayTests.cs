using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicReplayContract()
    {
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 34), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.MUSICREPLAY, 0), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        (GameSession Session, EventEngine Engine) Fresh(byte[]? source = null)
        {
            GameSession session = CreateNativeMusicSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(74, source ?? content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Fresh();
        int replayOutputs = 0;
        live.Engine.AudioCommandExecuted += command =>
        {
            if (command is NativeMusicReplayRequest)
            {
                replayOutputs++;
            }
        };
        live.Engine.Step();
        live.Engine.Step();
        live.Engine.Step();
        Require(replayOutputs == 1 && live.Engine.Scene.MusicReplayRequest is { Bank: 1, Music: 34, Volume: 127, PriorKeepBit400: true, ResourceOwner: "MusicPlayRequest" } && live.Engine.Scene.MusicProcess is { Bank201: 1, Pending: true, Requested213: 99 } && live.Engine.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 34 } && live.Session.WorldControls is { MusicKeep207: 0 } controls && (controls.EventFlags104 & 0x400) == 0 && !live.Session.State.Variables.ContainsKey(500), "MUSICREPLAY consumed pending music, selected the unplayed buffer, changed bank/music byte, lost keep-bit writes, touched an operand or failed return3.");
        JsonElement held = live.Engine.Capture();
        var restored = Fresh();
        restored.Engine.Restore(held);
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Session.State.Variables[500] == 1234 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "MUSICREPLAY did not retain the complete unrelated operand or restore its next callback identically.");
        byte[] stopped = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.MUSICREPLAY, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        live.Engine.LoadField(100, stopped, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        live.Engine.Step();
        Require(live.Engine.Scene.MusicReplayRequest is { Bank: 1, Music: 34, PriorKeepBit400: false } && live.Engine.Scene.MusicChannel108 is { ChannelId: 0, MusicId: 255 } && live.Session.WorldControls is { MusicKeep207: 1 }, "MUSICREPLAY changed native BYTE199 after stop or lost the clear-bit keep branch.");
        JsonNode malformed = JsonNode.Parse(held.GetRawText())!;
        malformed["Scene"]!["MusicReplayRequest"]!["Music"] = 99;
        JsonElement before = restored.Engine.Capture();
        try
        {
            restored.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("MUSICREPLAY accepted a pending song as the selected buffer's resource.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Engine.Capture()), "Rejected MUSICREPLAY restore mutated the VM.");
        byte[] absent = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5678), (Jsm.Opcode.MUSICREPLAY, 0), (Jsm.Opcode.RET, 0));
        var refused = Fresh(absent);
        try
        {
            refused.Engine.Step();
            throw new InvalidOperationException("MUSICREPLAY invented a selected-buffer resource from cold bootstrap state.");
        }
        catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.MUSICREPLAY))
        {
        }
        Require(refused.Engine.Scene.MusicReplayRequest is null && refused.Engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 5678, "MUSICREPLAY missing-owner refusal changed its operand or created a playback request.");
        Console.WriteLine("Native MUSICREPLAY321: current buffer resource independent of pending song, no operands/bank toggle/music-byte write, keep-bit ownership, unconditional return3, output receipt, independent restore/next callback and atomic missing/malformed resource refusal pass.");
    }
}
