using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void DialogueChannelContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.AMES, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 160), (Jsm.Opcode.PSHN_L, 120), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 180), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WINCLOSE, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, content, ["Persistent", "First automatic", "Second automatic"]);
        engine.Step();
        Require(engine.Dialogues.Count == 3 && engine.CurrentDialogue?.Channel == 2 && engine.Dialogues.Single(dialogue => dialogue.Channel == 0).CanConfirm == false, "Original automatic and persistent channels lost independent state.");
        var snapshot = engine.Capture();
        GameSession restoredSession = new(new GameState());
        EventEngine restored = new(restoredSession, restoredSession.State.Variables);
        restored.LoadField(100, content, ["Persistent", "First automatic", "Second automatic"]);
        restored.Restore(snapshot);
        Require(snapshot.GetRawText() == restored.Capture().GetRawText(), "Multiple dialogue channels changed after restore.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Automatic channels refused the confirm edge.");
        Require(engine.Dialogues.Count == 1 && engine.CurrentDialogue?.Channel == 0 && session.Dialog == "Persistent" && restoredSession.Dialog == "Persistent" && engine.DialogueOutcomes.Count == 2, "One native confirm did not dismiss both automatic windows and preserve the persistent panel.");
        Require(!engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && engine.Dialogues.Count == 1, "Persistent AMES was dismissed without MESSYNC or WINCLOSE.");
        Require(!engine.IsDialogueBlocking && engine.NextTrigger.Kind == PlayerTriggerKind.Waiting, "Persistent AMES prevented ordinary script continuation.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).FramesAdvanced == 3 && restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).FramesAdvanced == 3, "Ordinary waiting stopped at a persistent background window.");
        Require(!engine.IsDialogueOpen && session.Dialog is null && restoredSession.Dialog is null, $"Script WINCLOSE left a stale panel: open={engine.IsDialogueOpen}, original={session.Dialog}, restored={restoredSession.Dialog}.");
        Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Restored channel continuation diverged.");

        byte[] synchronized = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        engine.LoadField(101, synchronized, ["Synchronized"]);
        engine.Step();
        Require(engine.CurrentDialogue is { Channel: 3, CanConfirm: true }, "MESSYNC did not enable the persistent window's confirm wait.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && session.Dialog is null, "Synchronized window failed to close.");
        engine.Step();
        Require(session.State.Variables[512] == 77, "MESSYNC did not release its original fiber after confirm.");

        engine.LoadField(100, content, ["Persistent", "First automatic", "Second automatic"]);
        engine.Step();
        JsonObject legacy = JsonNode.Parse(engine.Capture().GetRawText())!.AsObject();
        foreach (JsonNode? dialogue in legacy["Dialogues"]!.AsArray())
        {
            dialogue!.AsObject().Remove("CanConfirm");
        }
        engine.Restore(System.Text.Json.JsonSerializer.SerializeToElement(legacy));
        Require(!engine.Dialogues.Single(dialogue => dialogue.Channel == 0).CanConfirm && engine.Dialogues.Single(dialogue => dialogue.Channel == 1).CanConfirm, "Legacy checkpoint inference changed original AMES/RAMESW confirmation modes.");
    }
}
