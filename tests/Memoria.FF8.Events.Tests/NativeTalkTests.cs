using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTalkSelectionContract()
    {
        EventEngine Create(int distance = 175, int height = 0, int facing = 128, int secondDistance = 1000, bool hidden = false, bool talkOff = false)
        {
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 48), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] Target(int label, int y, int z, bool hide, bool disabled)
            {
                List<(Jsm.Opcode, int)> instructions = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, z), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.TALKRADIUS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0)];
                if (hide)
                {
                    instructions.Add((Jsm.Opcode.HIDE, 0));
                }
                if (disabled)
                {
                    instructions.Add((Jsm.Opcode.TALKOFF, 0));
                }
                instructions.Add((Jsm.Opcode.RET, 0));
                return instructions.ToArray();
            }
            byte[] content = SyntheticObjects([0, 16 << 7, 32 << 7], [player, Target(16, distance, height, hidden, talkOff), Target(32, secondDistance, 0, false, false)]);
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                ResolveNativeTriangleHeight = (_, _, y) => y == distance ? height : 0
            };
            engine.LoadField(100, content, [], new FieldTransition(100, 0, 0, 0, facing));
            engine.Step();
            engine.Step();
            Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeFieldLoadVersion == 1, "Declared synthetic talk fixture did not execute canonical initialization.");
            return engine;
        }

        Require(Create().SelectInteractionTarget() == 1 && Create(distance: 176).SelectInteractionTarget() is null, "Native talk lost the strict playerPush+targetTalk distance boundary.");
        Require(Create(height: 255).SelectInteractionTarget() == 1 && Create(height: -255).SelectInteractionTarget() == 1 && Create(height: 256).SelectInteractionTarget() is null && Create(height: -256).SelectInteractionTarget() is null, "Native action query lost strict vertical ±256 bounds.");
        Require(Create(facing: 65).SelectInteractionTarget() == 1 && Create(facing: 64).SelectInteractionTarget() is null && Create(facing: 192).SelectInteractionTarget() is null, "Native action query lost its strict facing error below64.");
        Require(Create(secondDistance: 100).SelectInteractionTarget() == 1, "Equal-angle action candidates changed native array-order tie selection to nearest distance.");
        Require(Create(hidden: true).SelectInteractionTarget() == 1 && Create(talkOff: true).SelectInteractionTarget() is null, "Native action query changed retained hidden-model or TALKOFF semantics.");
        EventEngine pure = Create();
        JsonElement before = pure.Capture();
        Require(pure.SelectInteractionTarget() == 1 && JsonElement.DeepEquals(before, pure.Capture()), "Pure action selection changed actor, fiber or scene state.");
        JsonNode legacy = JsonNode.Parse(before.GetRawText())!;
        legacy["Scene"]!["NativeFieldLoadVersion"] = 0;
        legacy["Scene"]!.AsObject().Remove("NativePlayerEarlyRetryState");
        pure.Restore(JsonSerializer.SerializeToElement(legacy));
        Require(pure.SelectInteractionTarget() is null, "Unknown legacy field-load marker silently acquired native summed-radius action behavior.");
        Console.WriteLine("Native action4796E0: integer XYZ, strict summed radius/height/facing, angle-first native-order ties, hidden retained models, TALKOFF, pure selection and legacy fallback verified.");
    }
}
