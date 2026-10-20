using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDialogueBusyContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession Session()
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            return session;
        }
        foreach (Jsm.Opcode secondOpcode in new[] { Jsm.Opcode.AMESW, Jsm.Opcode.AMES, Jsm.Opcode.MES })
        {
            (Jsm.Opcode, int)[] Message(int label, int message, Jsm.Opcode opcode)
            {
                List<(Jsm.Opcode, int)> ops = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, message)];
                if (opcode != Jsm.Opcode.MES)
                {
                    ops.AddRange([(Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 64)]);
                }
                ops.AddRange([(opcode, 0), (Jsm.Opcode.PSHN_L, 700 + message), (Jsm.Opcode.POPM_W, 512 + 2 * message), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
                return ops.ToArray();
            }
            byte[] content = SyntheticObjects([1, (8 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], Message(1, 0, Jsm.Opcode.AMESW), [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], Message(9, 1, secondOpcode)]);
            EventEngine Create()
            {
                GameSession session = Session();
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(600, content, ["First", "Second"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            live.Step();
            JsonElement busy = live.Capture();
            JsonElement waiting = busy.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && !fiber.GetProperty("Initialization").GetBoolean());
            int preservedOperation = waiting.GetProperty("Position").GetInt32();
            int operands = secondOpcode == Jsm.Opcode.MES ? 2 : 4;
            Require(live.CurrentDialogue is { MessageId: 0, ObjectId: 0 } && waiting.GetProperty("Stack").GetArrayLength() == operands && waiting.GetProperty("WaitReason").GetString() == "Frames", "Busy dialogue replaced the first actor's window or consumed the second actor's original operands.");
            EventEngine restored = Create();
            restored.Restore(busy);
            for (int tick = 0; tick < 3; tick++)
            {
                live.Step();
                restored.Step();
                JsonElement blocked = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && !fiber.GetProperty("Initialization").GetBoolean());
                Require(blocked.GetProperty("Position").GetInt32() == preservedOperation && blocked.GetProperty("Stack").GetArrayLength() == operands && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Busy-channel retries lost the held operation/operands or independent VM equality.");
            }
            Require(live.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "First original dialogue decision was refused.");
            for (int callback = 0; callback < 8 && live.CurrentDialogue?.MessageId != 1; callback++)
            {
                live.Step();
                restored.Step();
            }
            Require(live.CurrentDialogue is { MessageId: 1, ObjectId: 1 } && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Second actor did not open its own window after the original occupied channel closed.");
            JsonElement opened = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && !fiber.GetProperty("Initialization").GetBoolean());
            Require(opened.GetProperty("Stack").GetArrayLength() == (secondOpcode == Jsm.Opcode.AMESW ? 4 : 0) && !CapturedVariables(live.Capture()).TryGetProperty("514", out _), "Successful native dialogue opening failed its return1/return3 yield or consumed operands more than once.");
            if (secondOpcode == Jsm.Opcode.AMESW)
            {
                live.Apply(new PlayerAction(PlayerActionKind.Confirm));
                restored.Apply(new PlayerAction(PlayerActionKind.Confirm));
                live.Step();
                restored.Step();
                Require(!CapturedVariables(live.Capture()).TryGetProperty("514", out _), "Closing native return3 executed the next command during the same actor turn.");
                for (int callback = 0; callback < 8 && !CapturedVariables(live.Capture()).TryGetProperty("514", out _); callback++)
                {
                    live.Step();
                    restored.Step();
                }
                Require(live.DialogueOutcomes.Select(outcome => outcome.MessageId).SequenceEqual([0, 1]), "Same-channel independent actors changed the authentic dialogue decision order.");
            }
            else
            {
                live.Step();
                restored.Step();
                Require(live.CurrentDialogue is { MessageId: 1, CanConfirm: false }, "Persistent native AMES/MES lost its separate confirmation policy.");
            }
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Dialogue channel opening/closing yielded different restored VM or variable state.");
        }
        byte[] syncContent = DefaultRoutineProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        GameSession syncSession = Session();
        EventEngine sync = new(syncSession);
        sync.LoadField(600, syncContent, ["Synchronized"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        sync.Step();
        sync.Step();
        sync.Step();
        Require(sync.CurrentDialogue is { CanConfirm: true }, "Native MESSYNC did not admit its existing persistent channel.");
        sync.Apply(new PlayerAction(PlayerActionKind.Confirm));
        for (int callback = 0; callback < 8 && !syncSession.State.Variables.ContainsKey(512); callback++)
        {
            sync.Step();
        }
        Require(syncSession.State.Variables.TryGetValue(512, out int successor) && successor == 77, "Native529900 closing return2 failed to continue its successor during the same actor callback.");
        byte[] rameswCollision = SyntheticObjects([0, 8 << 7], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession rejectedSession = Session();
        EventEngine rejected = new(rejectedSession);
        rejected.LoadField(600, rameswCollision, ["First", "Unsupported occupied RAMESW"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        try
        {
            rejected.Step();
            throw new InvalidOperationException("Occupied native RAMESW invented logical preserved-stack retry semantics.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == "RAMESW")
        {
        }
        JsonElement failed = rejected.Capture();
        Require(rejected.CurrentDialogue is { MessageId: 0 } && failed.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 4, "Unsupported occupied RAMESW overwrote the original window or consumed operands before refusal.");
        Console.WriteLine("Native occupied211: AMESW/AMES/MES retain original operands/PC while yielding; two actors open and confirm in original order; return3 opening/closing cadence and independent retry restore passed.");
    }
}
