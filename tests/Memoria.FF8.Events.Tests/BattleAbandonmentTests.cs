using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleAbandonmentContract()
    {
        GameSession session = CreateNativeMusicSession();
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine engine = new(session) { UseInteractiveBattle = (_, _) => true, ValidateInteractiveBattle = (_, _) => null };
        engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        while (engine.PendingInteractiveBattle is null && engine.Frame < 4)
        {
            engine.Step();
        }
        NativeBattleCompletion request = engine.PendingInteractiveBattle ?? throw new InvalidOperationException("Abandonment component did not reach its actual BATTLE wait.");
        engine.AdvanceInteractiveBattleCallbacks(1);
        BattleEncounterOccurrence occurrence = new("abandoned-scripted-component", request.Encounter, new string('A', 64), SceneKind.Field, request.FieldId, request.Frame, request.ProgramHash, new(request.FieldId, request.ObjectId, request.ScriptId, request.Operation, request.Flags, request.ProgramHash));
        NativeBattleAbandonment abandoned = engine.CaptureNativeBattleAbandonment(occurrence, 1);
        JsonElement fibers = engine.Capture().GetProperty("Fibers").Clone();
        engine.CommitNativeBattleAbandonment(abandoned);
        Require(engine.Scene.NativeBattleResult == 1 && engine.PendingInteractiveBattle is null && engine.Scene.NativeBattleCompletions![0] is { ReturnFrame: null, Result: null, Abandonment: not null }, "Fresh defeat fabricated a BATTLE return or lost the physical result1 byte.");
        Require(JsonElement.DeepEquals(fibers, engine.Capture().GetProperty("Fibers")), "Abandonment advanced or completed the old VM fiber.");
        // Declared fresh-allocation component, with original runtime profile; no claim to execute field75 JSM.
        byte[] destination = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        engine.LoadField(75, destination, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, fieldLoadReason: 0);
        JsonElement saved = engine.Capture();
        engine.Restore(saved);
        Require(engine.Scene.NativeBattleResult == 1 && engine.Scene.NativeAbandonedBattleOutcome == abandoned && engine.Scene.NativeBattleCompletions![0].Abandonment == abandoned && engine.Scene.NativeBattleWaits!.Count == 0, "Fresh allocation or restore lost the abandoned wait history or physical result1.");
    }
}
