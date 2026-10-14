using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void CausalReplayContract()
    {
        byte[] ProgramWithSpeed(int speed, int initializedValue)
        {
            return SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, speed), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, initializedValue), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        }
        (GameSession Session, EventEngine Engine) Create(int speed, int value)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            EventEngine engine = new(session, session.State.Variables)
            {
                ResolveWalkmesh = (x, y) => new WalkmeshPosition(new Vector3(x, y, 0), 0)
            };
            engine.LoadField(74, ProgramWithSpeed(speed, value), []);
            engine.Step();
            return (session, engine);
        }

        var first = Create(256, 41);
        Require(first.Session.State.ReplayJournal is { IsComplete: true }, "Actual NewGame failed to root engine history.");
        Require(first.Session.State.ReplayJournal!.Entries[1].CommandCount == 8, "Engine journal omitted executed stack/initialization/write commands.");
        Require(first.Session.State.ReplayJournal.Entries[1].Receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.Command && receipt.Value == 512), "Interpreter writes escaped their semantic operation provenance.");
        Require(first.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).FramesAdvanced == 3, "Synthetic route did not reach its requested three updates.");
        ReplayJournalEntry movement = first.Session.State.ReplayJournal!.Entries[^1];
        Require(movement.Navigation.Length == 3 && movement.Navigation[0].Start.X == 0 && movement.Navigation[^1].End.X == 3, "The engine failed to retain ordered causal navigation inputs.");
        var current = Create(1024, 99);
        int groundQueries = 0;
        current.Engine.ResolveWalkmesh = (x, y) =>
        {
            groundQueries++;
            return new WalkmeshPosition(new Vector3(x, y, 0), 0);
        };
        PlayerAction action = new((PlayerActionKind)movement.Code, movement.Value, movement.Text, movement.Horizontal, movement.Vertical, movement.Frames);
        PlayerActionResult rebuilt = current.Engine.Apply(action);
        Require(rebuilt.Accepted && rebuilt.FramesAdvanced == 3 && groundQueries > 0 && current.Engine.Actors[0].X == 12 && current.Engine.Actors[0].MovementSpeed == 1024, "Ordinary input reproduction did not recompute geometry and current MSPEED.");
        Require(current.Session.State.Variables[512] == 99, "Current initialization lost its freshly executed variable write.");
        Require(current.Session.State.ReplayJournal is { IsComplete: true } && current.Session.State.ReplayJournal.Entries[^1].Navigation.Length == 3, "Current movement did not record its own diagnostics.");
        var exact = Create(256, 41);
        exact.Engine.Apply(action);
        Require(JsonElement.DeepEquals(first.Engine.Capture(), exact.Engine.Capture()), "Identical ordinary inputs changed complete VM state relative to uninterrupted play.");
        GameState saved = first.Session.State with { EventState = first.Engine.Capture() };
        GameSession restoredSession = new(saved);
        EventEngine restored = new(restoredSession, restoredSession.State.Variables);
        using (restoredSession.BeginReplayCapture())
        {
            restored.LoadField(74, ProgramWithSpeed(256, 41), []);
            restored.Restore(saved.EventState!.Value);
        }
        int prefix = saved.ReplayJournal!.EntryCount;
        restored.Step();
        Require(restoredSession.State.ReplayJournal is { IsComplete: true } && restoredSession.State.ReplayJournal.EntryCount == prefix + 1, "A genuine independent save load invented an extra field initialization or invalidated history.");
        first.Session.State.Variables[900] = 123;
        first.Engine.Capture();
        Require(first.Session.State.ReplayJournal is { IsComplete: false }, "Unjournaled script memory mutation left a false complete history.");

        byte[] dialogueProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 700), (Jsm.Opcode.RET, 0));
        GameSession dialogueSession = GameSession.CreateColdProcess();
        dialogueSession.StartNewGame();
        EventEngine dialogueEngine = new(dialogueSession, dialogueSession.State.Variables);
        dialogueEngine.LoadField(74, dialogueProgram, ["Continue"]);
        dialogueEngine.Step();
        dialogueEngine.Step();
        ReplayJournalEntry pendingDialogue = dialogueSession.ReplayJournal!.Endpoint!;
        DialogueState pending = dialogueEngine.CurrentDialogue!;
        Require(pendingDialogue.ResultTrigger == (int)PlayerTriggerKind.Dialogue && pendingDialogue.ResultMessageId == pending.MessageId && pendingDialogue.ResultObjectId == pending.ObjectId && pendingDialogue.ResultScriptId == pending.ScriptId && pendingDialogue.ResultOperation == pending.Operation, "Engine endpoint omitted the actual resulting dialogue causal site.");
        dialogueEngine.ConfirmDialogue();
        ReplayJournalEntry decision = dialogueSession.State.ReplayJournal!.Entries[^1];
        Require(decision.Kind == ReplayEntryKind.Dialogue && decision.MessageId == 0 && decision.Receipts.Single(receipt => receipt.Kind == ReplayReceiptKind.Dialogue).Code == 0, "Direct host dialogue completion escaped its causal site and outcome receipt.");
        Require(decision.ResultMessageId is null && decision.ResultObjectId is null && decision.ResultScriptId is null && decision.ResultOperation is null, "Closed dialogue endpoint retained a stale post-input causal site.");
        dialogueEngine.Step();
        Require(dialogueSession.State.Variables[700] == 7, "Recorded dialogue completion did not release the current VM.");
        string capsule = JsonSerializer.Serialize(current.Session.State.ReplayJournal);
        ReplayJournal roundtrip = JsonSerializer.Deserialize<ReplayJournal>(capsule)!;
        Require(roundtrip.SessionId == current.Session.State.ReplayJournal!.SessionId && roundtrip.Entries[^1].Navigation.Length == 3, "Portable journal lost its session or semantic route after compression.");

        var gatewaySource = Create(256, 41);
        gatewaySource.Engine.InfGateways = [new InfGateway(new Vector3(1, -100, 0), new Vector3(1, 100, 0), new FieldTransition(179, 5, 6, 0, 128))];
        gatewaySource.Engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Require(gatewaySource.Session.State.FieldId == 179, "Synthetic ordinary movement did not execute its gateway contact.");
        ReplayJournalEntry gatewayInput = gatewaySource.Session.State.ReplayJournal!.Entries[^1];
        var gatewayCurrent = Create(1024, 99);
        gatewayCurrent.Engine.InfGateways = gatewaySource.Engine.InfGateways;
        gatewayCurrent.Engine.Apply(new PlayerAction((PlayerActionKind)gatewayInput.Code, gatewayInput.Value, gatewayInput.Text, gatewayInput.Horizontal, gatewayInput.Vertical, gatewayInput.Frames));
        Require(gatewayCurrent.Session.State.FieldId == 179 && gatewayCurrent.Session.State.FieldArrival == gatewaySource.Session.State.FieldArrival && gatewayCurrent.Session.State.Variables[512] == 99, "Causal navigation forced an old transition or lost the freshly executed gateway/init state.");
    }
}
