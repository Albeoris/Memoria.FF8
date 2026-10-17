using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeGameOverContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] content = SyntheticObjects([1, (16 << 7) | 1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.GAMEOVER, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        NativeFieldRequestPacket packet = new() { Kind = 0, FieldId = 623, X = -12, Y = 34, ExtraWord = -56, Triangle = 0x7FFF, DirectionWord = 0xABCD };
        (EventEngine Engine, GameSession Session) Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            session.RequestField(600, 0);
            session.SetFieldRequestPacket(packet);
            EventEngine engine = new(session);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return (engine, session);
        }
        JsonElement Caller(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && item.GetProperty("ScriptId").GetInt32() == 1);
        int operation = JsmProgram.Read(content).Operations.Single(item => item.Opcode == Jsm.Opcode.GAMEOVER).Index;
        var live = Create();
        bool laterActorObserved = false;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.GAMEOVER))
            {
                Require(command.Arguments.Count == 0 && live.Engine.NativeGameOver is null && live.Session.FieldRequestPacket == packet.WithGameOverRequest(), "GAMEOVER consumed operands, accepted loss before arbitration or changed retained packet words.");
            }
            if (command.Name == nameof(Jsm.Opcode.POPM_B))
            {
                laterActorObserved = true;
                Require(live.Engine.NativeGameOver is null, "GAMEOVER stopped or accepted loss before the later actor's turn.");
            }
        };
        PlayerActionResult result = live.Engine.Apply(new(PlayerActionKind.Wait, Frames: 300));
        NativeGameOverReceipt receipt = live.Engine.NativeGameOver ?? throw new InvalidOperationException("Final request-kind4 did not accept native loss.");
        JsonElement held = Caller(live.Engine);
        Require(laterActorObserved && result.Trigger.Kind == PlayerTriggerKind.NativeGameOver && result.FramesAdvanced == 1 && receipt.FieldId == 600 && receipt.Operation == operation && receipt.Frame == live.Engine.Frame && receipt.RequestKind == 4 && receipt.ExecutableHash == NativeFieldRuntimeProfile.FF8EnExecutableHash && live.Session.State.Scene == SceneKind.Field && live.Engine.LastFault is null, "GAMEOVER lost its exact source, failed complete-callback arbitration, fabricated title or continued the input frame budget.");
        Require(held.GetProperty("Position").GetInt32() == operation && !held.GetProperty("Complete").GetBoolean() && held.GetProperty("WaitReason").GetString() == "None" && held.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([77]) && CapturedVariables(live.Engine.Capture()).GetProperty("5000").GetInt32() == 88 && !CapturedVariables(live.Engine.Capture()).TryGetProperty("5001", out _), "GAMEOVER advanced its issuing PC, consumed the stack, completed its script or stopped another actor.");

        JsonElement frozen = live.Engine.Capture();
        live.Engine.Step();
        live.Engine.Update(TimeSpan.FromSeconds(10));
        Require(!live.Engine.Apply(new(PlayerActionKind.FieldButtons, Value: 64)).Accepted && JsonElement.DeepEquals(frozen, live.Engine.Capture()), "Accepted native loss allowed later callbacks or physical field input to mutate the VM.");
        var restored = Create();
        restored.Engine.Restore(frozen);
        restored.Engine.Step();
        Require(restored.Session.NativeGameOver == receipt && JsonElement.DeepEquals(frozen, restored.Engine.Capture()), "Full VM restore lost its native terminal-loss owner or resumed the held GAMEOVER.");

        var fieldOnly = Create();
        fieldOnly.Engine.RestoreFieldState(frozen);
        Require(fieldOnly.Engine.NativeGameOver is null && fieldOnly.Session.FieldRequestPacket == packet, "Dormant field-only restore overwrote current process request or terminal-loss owners.");
        JsonObject corrupt = JsonNode.Parse(frozen.GetRawText())!.AsObject();
        corrupt["NativeGameOver"]!["Operation"] = operation + 1;
        bool rejected = false;
        try
        {
            restored.Engine.Restore(JsonSerializer.SerializeToElement(corrupt));
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, "A forged native loss source survived VM checkpoint validation.");

        var overwritten = Create();
        overwritten.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.POPM_B))
            {
                overwritten.Session.SetFieldRequestPacket(overwritten.Session.FieldRequestPacket!.WithScriptMenuRequest(8));
            }
        };
        overwritten.Engine.Step();
        Require(overwritten.Engine.NativeGameOver is null && overwritten.Session.FieldRequestPacket == packet.WithGameOverRequest().WithScriptMenuRequest(8) && Caller(overwritten.Engine).GetProperty("Position").GetInt32() == operation, "A later real packet writer was overridden by premature loss acceptance.");
        Console.WriteLine("GAMEOVER: zero operands, retained packet words/PC, actor-only yield, complete-callback arbitration, typed terminal loss, input/callback freeze and full versus field-only checkpoint ownership passed.");
    }
}
