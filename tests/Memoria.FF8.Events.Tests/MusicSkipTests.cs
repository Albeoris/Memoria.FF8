using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicSkipContract()
    {
        foreach (int position in new[] { 0, 65537, -1, -8388608 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, position), (Jsm.Opcode.MUSICSKIP, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.RET, 0));
            GameSession session = new(new GameState());
            session.StartNewGame();
            EventEngine first = new(session, session.State.Variables);
            first.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            first.Step();
            Require(first.Scene.MusicPlayRequest is { Opcode: "MUSICSKIP", ConsumedPending: true, Music: 26, Bank: 1, Channel: 0, Volume: 127 } request && request.SkipPosition == position && first.Scene.MusicChannel112?.ChannelId == -1 && !session.State.Variables.ContainsKey(500), "MUSICSKIP lost full position, primary routing, bank toggle or return3 yield.");
            JsonElement checkpoint = first.Capture();
            GameSession copySession = new(new GameState());
            copySession.StartNewGame();
            EventEngine copy = new(copySession, copySession.State.Variables);
            copy.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            copy.Restore(checkpoint);
            first.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(first.Capture(), copy.Capture()) && session.State.Variables[500] == 9, "MUSICSKIP yield or independent restore diverged.");
            JsonNode malformed = JsonNode.Parse(checkpoint.GetRawText())!;
            malformed["Scene"]!["MusicPlayRequest"]!.AsObject().Remove("SkipPosition");
            try
            {
                copy.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("MUSICSKIP accepted a request without its original operand.");
            }
            catch (InvalidDataException)
            {
            }
            byte[] noPending = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, position), (Jsm.Opcode.MUSICSKIP, 0), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_B, 501), (Jsm.Opcode.RET, 0));
            first.LoadField(100, noPending, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            first.Step();
            Require(first.Scene.MusicPlayRequest is { ConsumedPending: false, Channel: null, Bank: 1 } empty && empty.SkipPosition == position && session.State.Variables[501] == 11 && first.Scene.MusicChannel108?.MusicId == 26, "No-pending MUSICSKIP changed music, retained an operand or yielded.");
        }
    }
}
