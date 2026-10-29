using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void DialogueChannelContract()
    {
        DialogueChannelReuseContract();
        DialogueChannelStyleContract();
        byte[] content = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.AMES, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 160), (Jsm.Opcode.PSHN_L, 120), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 180), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.WINCLOSE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        string[] messages = ["Persistent", "First automatic", "Second automatic"];
        GameSession session = CreateChannelSession();
        EventEngine engine = LoadChannels(session, 100, content, messages);
        engine.Step();
        Require(engine.Dialogues.Count == 0, "Synchronous no-op INIT unexpectedly opened default-script windows.");
        for (int callback = 0; callback < 8 && engine.Dialogues.Count < 3; callback++)
        {
            engine.Step();
        }
        Require(engine.Dialogues.Count == 3 && engine.CurrentDialogue?.Channel == 2 && engine.Dialogues.Single(dialogue => dialogue.Channel == 0).CanConfirm == false, "Original automatic and persistent channels lost independent state.");
        for (int callback = 0; callback < 8 && engine.Dialogues.Any(dialogue => dialogue.NativeWindow!.Progress < 4096); callback++)
        {
            engine.Step();
        }
        Require(engine.Dialogues.Count == 3 && engine.Dialogues.All(dialogue => dialogue.NativeWindow is { Version: 1, Progress: 4096, Delta: 512 }), "Opened channels did not reach their full native window state before confirmation.");
        var snapshot = engine.Capture();
        GameSession restoredSession = CreateChannelSession();
        EventEngine restored = LoadChannels(restoredSession, 100, content, messages);
        restored.Restore(snapshot);
        Require(snapshot.GetRawText() == restored.Capture().GetRawText(), "Multiple dialogue channels changed after restore.");
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Automatic channels refused the confirm edge.");
        Require(engine.Dialogues.Count == 1 && engine.Dialogues.Single() is { Channel: 0, MessageId: 0, CanConfirm: false } && engine.Scene.NativeDialogueFocus == 2 && engine.CurrentDialogue is null && session.Dialog is null && restoredSession.Dialog is null && engine.DialogueOutcomes.Count == 2, "One native confirm changed the retained persistent channel or invented a focus fallback after closing both automatic windows.");
        Require(engine.DialogueOutcomes.Select(outcome => outcome.MessageId).SequenceEqual([1, 2]), "Shared automatic confirmation lost original ascending channel order.");
        var persistentChannels = engine.Capture().GetProperty("Dialogues").Clone();
        long frameBeforeInteraction = engine.Frame;
        int actionsBeforeInteraction = engine.ActionLog.Count;
        RejectUnregisteredInteraction(engine);
        RejectUnregisteredInteraction(restored);
        Require(persistentChannels.GetRawText() == engine.Capture().GetProperty("Dialogues").GetRawText() && engine.Frame == frameBeforeInteraction && engine.ActionLog.Count == actionsBeforeInteraction && engine.LastFault is null, "Refused interaction dismissed persistent AMES or advanced its callback/action log.");
        Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Independent interaction refusals changed their retained native input or complete VM differently.");
        Require(!engine.IsDialogueBlocking && engine.NextTrigger.Kind == PlayerTriggerKind.Waiting, "Persistent AMES prevented ordinary script continuation.");
        for (int callback = 0; callback < 12 && engine.IsDialogueOpen; callback++)
        {
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Wait)).FramesAdvanced == 1 && restored.Apply(new PlayerAction(PlayerActionKind.Wait)).FramesAdvanced == 1, "Ordinary waiting stopped at a persistent background window.");
            Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Restored window closing callback diverged.");
        }
        Require(!engine.IsDialogueOpen && session.Dialog is null && restoredSession.Dialog is null, $"Script WINCLOSE left a stale panel: open={engine.IsDialogueOpen}, original={session.Dialog}, restored={restoredSession.Dialog}.");
        Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Restored channel continuation diverged.");

        byte[] synchronized = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        engine.LoadField(101, synchronized, ["Synchronized"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int callback = 0; callback < 8 && engine.CurrentDialogue is not { Channel: 3, CanConfirm: true }; callback++)
        {
            engine.Step();
        }
        Require(engine.CurrentDialogue is { Channel: 3, CanConfirm: true }, "MESSYNC did not enable the persistent window's confirm wait.");
        for (int callback = 0; callback < 8 && engine.CurrentDialogue!.NativeWindow!.Progress < 4096; callback++)
        {
            engine.Step();
        }
        Require(engine.CurrentDialogue is { Channel: 3, CanConfirm: true, NativeWindow.Progress: 4096 }, "MESSYNC lost its occupied ready window while its opening samples completed.");
        var synchronizedSnapshot = engine.Capture();
        GameSession synchronizedSession = CreateChannelSession();
        EventEngine synchronizedRestore = LoadChannels(synchronizedSession, 101, synchronized, ["Synchronized"]);
        synchronizedRestore.Restore(synchronizedSnapshot);
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && synchronizedRestore.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Synchronized windows refused their confirm edge.");
        Require(!session.State.Variables.ContainsKey(512) && engine.CurrentDialogue is { Channel: 3, CanConfirm: false }, "MESSYNC consumed its operands before the native closing callbacks.");
        for (int callback = 0; callback < 12 && !session.State.Variables.ContainsKey(512); callback++)
        {
            engine.Step();
            synchronizedRestore.Step();
            Require(engine.Capture().GetRawText() == synchronizedRestore.Capture().GetRawText(), "Restored MESSYNC closing callback diverged.");
        }
        Require(session.State.Variables[512] == 77 && synchronizedSession.State.Variables[512] == 77 && !engine.IsDialogueOpen && session.Dialog is null, "MESSYNC did not release its original fiber after native window closure.");

        engine.LoadField(100, content, messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int callback = 0; callback < 8 && engine.Dialogues.Count < 3; callback++)
        {
            engine.Step();
        }
        var complete = engine.Capture();
        JsonObject legacy = JsonNode.Parse(complete.GetRawText())!.AsObject();
        foreach (JsonNode? dialogue in legacy["Dialogues"]!.AsArray())
        {
            dialogue!.AsObject().Remove("CanConfirm");
        }
        try
        {
            engine.Restore(System.Text.Json.JsonSerializer.SerializeToElement(legacy));
            throw new InvalidOperationException("A checkpoint without explicit native confirmability was accepted.");
        }
        catch (InvalidDataException exception) when (exception.Message.Contains("confirmability", StringComparison.Ordinal))
        {
        }
        Require(complete.GetRawText() == engine.Capture().GetRawText(), "Missing confirmability mutated the current windows or native VM.");

        static void RejectUnregisteredInteraction(EventEngine value)
        {
            try
            {
                value.Apply(new PlayerAction(PlayerActionKind.Confirm));
                throw new InvalidOperationException("A component without original field registration admitted native interaction.");
            }
            catch (NotSupportedException exception) when (exception.Message == "Native interaction requires the completed original field loader and retained actor layout.")
            {
            }
        }

        static GameSession CreateChannelSession()
        {
            using GameAssets assets = new(TestResources.GameDirectory);
            GameSession value = CreateNativeMusicSession();
            value.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            return value;
        }

        static EventEngine LoadChannels(GameSession value, int field, byte[] program, string[] text)
        {
            EventEngine valueEngine = new(value);
            valueEngine.LoadField(field, program, text, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return valueEngine;
        }
    }

    private static void DialogueChannelStyleContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 4096), (Jsm.Opcode.MESMODE, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0x18000), (Jsm.Opcode.MESMODE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WINCLOSE, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create()
        {
            EventEngine engine = new(new GameSession(new GameState()));
            engine.LoadField(100, content, []);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Require(live.Scene.MessageChannelStyles[1] == new DialogueChannelStyle(8, 4096) && !live.Scene.MessageChannelStyles[1].HasPanel, "MESMODE or WINCLOSE lost the retained borderless channel style.");
        Require(live.Scene.MessageChannelStyles[2] == new DialogueChannelStyle(11, -32768) && live.Scene.MessageChannelStyles[2].BackgroundAlpha == 127, "MESMODE lost independent transparency flags or signed mode width.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        Require(live.Capture().GetRawText() == restored.Capture().GetRawText(), "Channel style state changed after restore.");
        live.Step();
        restored.Step();
        Require(live.Capture().GetRawText() == restored.Capture().GetRawText(), "Restored MESMODE continuation diverged.");
    }

    private static void DialogueChannelReuseContract()
    {
        List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0)];
        void Message(int channel, int message, Jsm.Opcode opcode)
        {
            operations.Add((Jsm.Opcode.PSHN_L, channel));
            operations.Add((Jsm.Opcode.PSHN_L, message));
            operations.Add((Jsm.Opcode.PSHN_L, 32));
            operations.Add((Jsm.Opcode.PSHN_L, 64));
            operations.Add((opcode, 0));
        }
        Message(1, 0, Jsm.Opcode.AMES);
        Message(2, 1, Jsm.Opcode.AMES);
        operations.Add((Jsm.Opcode.PSHN_L, 1));
        operations.Add((Jsm.Opcode.WINCLOSE, 0));
        operations.Add((Jsm.Opcode.PSHN_L, 2));
        operations.Add((Jsm.Opcode.WINCLOSE, 0));
        operations.Add((Jsm.Opcode.PSHN_L, 1));
        operations.Add((Jsm.Opcode.WAIT, 0));
        Message(1, 2, Jsm.Opcode.AMES);
        Message(2, 3, Jsm.Opcode.AMES);
        operations.Add((Jsm.Opcode.PSHN_L, 1));
        operations.Add((Jsm.Opcode.WAIT, 0));
        Message(2, 4, Jsm.Opcode.RAMESW);
        Message(1, 5, Jsm.Opcode.RAMESW);
        operations.Add((Jsm.Opcode.PSHN_L, 1));
        operations.Add((Jsm.Opcode.WAIT, 0));
        operations.Add((Jsm.Opcode.RET, 0));
        byte[] content = SyntheticProgram(operations.ToArray());
        string[] messages = ["Old one", "Old two", "Persistent one", "Persistent two", "Automatic two", "Automatic one"];
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(102, content, messages);
        RejectOccupiedChannel(engine);
        Require(engine.Dialogues.Select(dialogue => dialogue.Channel).SequenceEqual([1, 2]) && engine.Dialogues.Select(dialogue => dialogue.MessageId).SequenceEqual([2, 3]), "Occupied RAMESW changed the accepted persistent-channel reuse prefix.");
        Require(engine.CurrentDialogue is { Channel: 2, MessageId: 3, CanConfirm: false } && engine.DialogueOutcomes.Count == 0, "Occupied RAMESW replaced or confirmed an existing persistent channel.");
        var held = engine.Capture();
        var heldMain = held.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 0);
        Require(heldMain.GetProperty("Position").GetInt32() == 33 && heldMain.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([64, 32, 4, 2]), "Unsupported occupied RAMESW did not retain its original instruction and all four operands.");
        GameSession restoredSession = new(new GameState());
        EventEngine restored = new(restoredSession);
        restored.LoadField(102, content, messages);
        restored.Restore(held);
        Require(held.GetRawText() == restored.Capture().GetRawText() && session.Dialog == restoredSession.Dialog, "Occupied-channel refusal lost its complete standalone VM or channel focus.");
        RejectOccupiedChannel(restored);
        var retriedMain = restored.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 0);
        Require(retriedMain.GetProperty("Position").GetInt32() == 33 && heldMain.GetProperty("Stack").GetRawText() == retriedMain.GetProperty("Stack").GetRawText() && engine.Dialogues.SequenceEqual(restored.Dialogues), "Restored occupied-channel refusal consumed operands or changed the accepted channels.");

        static void RejectOccupiedChannel(EventEngine candidate)
        {
            try
            {
                candidate.Step();
                throw new InvalidOperationException("Occupied RAMESW silently retried an unsupported physical stack-residue path.");
            }
            catch (EventScriptException exception) when (candidate.LastFault?.Opcode == "RAMESW" && exception.Message.Contains("return5", StringComparison.Ordinal))
            {
            }
        }
    }
}
