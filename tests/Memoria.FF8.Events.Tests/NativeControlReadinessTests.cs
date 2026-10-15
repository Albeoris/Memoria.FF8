using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeControlReadinessContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        byte[] content = SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 3], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 34), (Jsm.Opcode.REQSW, 2), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 35), (Jsm.Opcode.REQSW, 2), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 34), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 35), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        EventEngine Create(byte[]? selectedProgram = null)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.MarkReplayUnavailable("Declared native request-slot readiness component; no actual field visit or movement claimed.");
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(74, selectedProgram ?? content, ["Declared readiness dialogue"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        Require(live.NextTrigger.Kind == PlayerTriggerKind.Waiting && !live.IsUserControlEnabled, "Native startup became playable before default allocation activation.");
        live.Step();
        JsonElement allocated = live.Capture();
        Require(allocated.GetProperty("DefaultsStarted").GetBoolean() && live.IsUserControlEnabled && allocated.GetProperty("Fibers").EnumerateArray().Any(fiber => !fiber.GetProperty("Initialization").GetBoolean() && !fiber.GetProperty("Started").GetBoolean()) && live.NextTrigger.Kind == PlayerTriggerKind.Waiting, "Allocated native startup became playable before its selected default owner ran.");
        live.Step();
        JsonElement afterRequest = live.Capture();
        Require(afterRequest.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 33 && !fiber.GetProperty("Started").GetBoolean()), "Native higher request failed to retain its unstarted lower default.");
        Require(afterRequest.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 34 && fiber.GetProperty("Started").GetBoolean() && fiber.GetProperty("WaitReason").GetString() == "Frames"), "Native requested slot did not own its real WAIT.");
        Require(live.PlayerActor is { ObjectId: 0, Active: true, Visible: true } && live.IsUserControlEnabled && live.NextTrigger.Kind == PlayerTriggerKind.Playable && JsonElement.DeepEquals(afterRequest, live.Capture()), "UCON input readiness depended on a pending lower default or mutated the scheduler.");
        live.Step();
        live.Step();
        JsonElement nested = live.Capture();
        Require(nested.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 35 && fiber.GetProperty("NativeActorSlot").GetInt32() == 2 && fiber.GetProperty("WaitReason").GetString() == "Frames"), "Higher nested request did not preserve native slot2 WAIT ownership.");
        Require(nested.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 33 && !fiber.GetProperty("Started").GetBoolean()) && live.NextTrigger.Kind == PlayerTriggerKind.Playable, "Nested background preemption falsely blocked ordinary native control.");
        EventEngine restored = Create();
        restored.Restore(nested);
        Require(restored.NextTrigger.Kind == PlayerTriggerKind.Playable && JsonElement.DeepEquals(nested, restored.Capture()), "Readiness or native pending slots changed during independent current restore.");
        live.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Readiness change altered the next native callback schedule.");
        EventEngine disabled = Create(SyntheticObjects([0], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.RET, 8)]]));
        disabled.Step();
        Require(!disabled.IsUserControlEnabled && disabled.NextTrigger.Kind == PlayerTriggerKind.Waiting, "Executed UCOFF control block no longer takes precedence.");
        EventEngine pendingSelected = Create(SyntheticObjects([2, (16 << 7) | 1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]));
        pendingSelected.Step();
        pendingSelected.Step();
        Require(pendingSelected.PlayerActor is { Active: true, Visible: true } && pendingSelected.IsUserControlEnabled && pendingSelected.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2 && !fiber.GetProperty("Started").GetBoolean()) && pendingSelected.NextTrigger.Kind == PlayerTriggerKind.Waiting, "An unstarted selected native owner was ignored despite an already known visible leader and UCON.");
        pendingSelected.Step();
        Require(pendingSelected.NextTrigger.Kind == PlayerTriggerKind.Playable, "Executed selected native WAIT remained a startup barrier after control was available.");
        EventEngine hidden = Create(SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]));
        hidden.Step();
        hidden.Step();
        Require(hidden.PlayerActor is { Active: true, Visible: false } && hidden.IsUserControlEnabled && hidden.NextTrigger.Kind == PlayerTriggerKind.Waiting && !hidden.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted, "Hidden current native leader became playable or accepted ordinary movement.");
        EventEngine inactive = Create(SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)]]));
        inactive.Step();
        inactive.Step();
        Require(inactive.PlayerActor is { Active: false } && inactive.NextTrigger.Kind == PlayerTriggerKind.Waiting && !inactive.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted, "Inactive current native leader became playable or accepted ordinary movement.");
        EventEngine dialogue = Create(SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.RET, 8)]]));
        dialogue.Step();
        dialogue.Step();
        Require(dialogue.IsUserControlEnabled && dialogue.CurrentDialogue?.CanConfirm == true && dialogue.NextTrigger.Kind == PlayerTriggerKind.Dialogue, "Native dialogue lost its precedence over available user control.");
        Console.WriteLine("Native control readiness: startup gate, unstarted lower default, higher/nested native WAIT, selected-owner startup, known visible/hidden/inactive leader, UCOFF/dialogue and restore/next-callback schedule PASS.");
    }
}
