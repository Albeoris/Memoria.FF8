using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBlockingDialogueContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.MESW, Jsm.Opcode.AMESW, Jsm.Opcode.ASK, Jsm.Opcode.AASK, Jsm.Opcode.MESSYNC })
        {
            bool choice = opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
            int[] operands = opcode switch
            {
                Jsm.Opcode.MESW => [0, 0],
                Jsm.Opcode.AMESW => [0, 0, 20, 30],
                Jsm.Opcode.ASK => [0, 0, 3, 4, 3, 4],
                Jsm.Opcode.AASK => [0, 0, 3, 4, 3, 4, 20, 30],
                _ => [0]
            };
            List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 123456)];
            if (opcode == Jsm.Opcode.MESSYNC)
            {
                operations.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0)]);
            }
            operations.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
            operations.AddRange([(opcode, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.POPM_L, 5004), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            byte[] program = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], operations.ToArray()]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session);
                engine.LoadField(100, program, ["Select"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                engine.Step();
                return engine;
            }
            JsonElement Consumer(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            void Held(EventEngine engine, int operation)
            {
                JsonElement fiber = Consumer(engine);
                Require(fiber.GetProperty("Position").GetInt32() == operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(operands.Reverse().Append(123456)) && fiber.GetProperty("WaitReason").GetString() == "Dialogue", "Native dialogue changed its held issuing PC or physical operands before completion.");
            }
            EventEngine live = Create();
            for (int callback = 0; callback < 6 && !Consumer(live).TryGetProperty("NativeDialogueConsumer", out _); callback++)
            {
                live.Step();
            }
            int operation = Consumer(live).GetProperty("NativeDialogueConsumer").GetProperty("Operation").GetInt32();
            Held(live, operation);
            if (choice)
            {
                Require(live.CurrentDialogue is null && live.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted && live.CurrentDialogue?.Channel == 0, "Ordinary Wait could not reach a choice's first native focus poll.");
            }
            while (live.Dialogues.Single().NativeWindow!.Progress < 4096)
            {
                live.Step();
                Held(live, operation);
            }
            Require(live.Apply(new PlayerAction(choice ? PlayerActionKind.Choose : PlayerActionKind.Confirm, choice ? 4 : 0)).Accepted, "Native dialogue confirmation was refused.");
            Held(live, operation);
            Require(Consumer(live).GetProperty("Results").GetProperty("0").GetInt32() == 901 && live.Dialogues.Single() is { CanConfirm: false, NativeWindow.Delta: 512 }, "Host selection changed native R0 or began choice closing before its VM poll.");
            EventEngine copy = Create();
            copy.Restore(live.Capture());
            foreach (string property in new[] { "NativeDialogueConsumer" })
            {
                JsonElement before = copy.Capture();
                JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
                invalid["Fibers"]!.AsArray().Single(node => node!["ScriptId"]!.GetValue<int>() == 1)!.AsObject().Remove(property);
                try
                {
                    copy.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException("Native held dialogue accepted a missing typed consumer.");
                }
                catch (InvalidDataException)
                {
                    Require(JsonElement.DeepEquals(before, copy.Capture()), "Malformed native consumer restore mutated live state.");
                }
            }
            int closingCallbacks = 0;
            do
            {
                Require(live.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted && copy.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted, "Closing native dialogue blocked ordinary Wait.");
                closingCallbacks++;
                Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Native selected/closing dialogue changed after independent restore.");
                if (Consumer(live).TryGetProperty("NativeDialogueConsumer", out _))
                {
                    Held(live, operation);
                    Require(!CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "Dialogue successor executed while its physical closing window remained active.");
                }
            }
            while (Consumer(live).TryGetProperty("NativeDialogueConsumer", out _) && closingCallbacks < 10);
            Require(closingCallbacks == 5 && live.Dialogues.Count == 0 && Consumer(live).GetProperty("Results").GetProperty("0").GetInt32() == (choice ? 1 : 901), "Native close duration, deferred R0 or zero-progress completion changed.");
            bool successor = CapturedVariables(live.Capture()).TryGetProperty("5000", out _);
            Require(successor == (opcode == Jsm.Opcode.MESSYNC), "Native return2/return3 dialogue completion budget changed.");
            if (!successor)
            {
                Require(Consumer(live).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([123456]), "Native completion did not pop exactly its original operands.");
                live.Step();
                copy.Step();
            }
            Require(CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && CapturedVariables(live.Capture()).GetProperty("5004").GetInt32() == 123456 && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Native dialogue completion lost its next-budget successor or lower stack value.");
        }
        Console.WriteLine("Native blocking dialogue: held physical operands/PC, deferred R0, real selected/closing window, zero-progress completion, return3/2 budgets, independent restore and atomic malformed-consumer refusal passed.");
    }
}
