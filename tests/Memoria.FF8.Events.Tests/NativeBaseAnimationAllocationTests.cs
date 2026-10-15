using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBaseAnimationAllocationContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] content = SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 1],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.BASEANIME, 5), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        // These native immediate opcodes require the high-byte encoding even for argument0.
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        for (int offset = operationsOffset; offset < content.Length; offset += 4)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset));
            if (encoded is (int)Jsm.Opcode.SETMODEL or (int)Jsm.Opcode.BASEANIME)
            {
                BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(offset), encoded << 24);
            }
        }
        List<(int Model, int Animation)> selected = [];
        EventEngine Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session)
            {
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                AnimationFrameCount = (model, animation) =>
                {
                    selected.Add((model, animation));
                    if (model is not (0 or 1) || animation is < 0 or > 7)
                    {
                        throw new InvalidDataException($"Declared model{model} metadata has no animation{animation}; no substitute clip or frame count is permitted.");
                    }
                    return animation + 4;
                }
            };
            engine.LoadField(600, content, [], new FieldTransition(600, 0, 0, 0, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        Require(live.Actors.All(actor => actor.BaseAnimationId == 0 && actor.WalkAnimationId == 0 && actor.RunAnimationId == 0 && actor.NativeBaseAnimationVersion == 1), "Fresh native physical allocation did not establish actual zero bytes591..593.");
        List<(int Object, int Base, int Walk, int Run)> modelWrites = [];
        live.CommandExecuted += command =>
        {
            if (command.Name == "SETMODEL")
            {
                ActorState actor = live.Actors[command.ObjectId];
                modelWrites.Add((actor.ObjectId, actor.BaseAnimationId, actor.WalkAnimationId, actor.RunAnimationId));
            }
        };
        for (int callback = 0; callback < 4 && live.Scene.NativeFieldLoadVersion == 0; callback++)
        {
            live.Step();
        }
        Require(live.Scene.NativeFieldLoadVersion == 1 && live.LastFault is null, "Native party field load did not complete with its actual default animation metadata.");
        ActorState hidden = live.Actors[1];
        ActorState explicitBase = live.Actors[2];
        Require(!hidden.Visible && hidden.ModelId == 0 && hidden.BaseAnimationId == 0 && hidden.WalkAnimationId == 0 && hidden.RunAnimationId == 0 && hidden.AnimationId == 0 && hidden.AnimationFirst == 0 && hidden.AnimationLast == 3 && hidden.AnimationLoop, "Hidden SETMODEL-only body did not select actual fresh idle0 and its full decoded range.");
        Require(explicitBase.ModelId == 1 && explicitBase.BaseAnimationId == 5 && explicitBase.WalkAnimationId == 6 && explicitBase.RunAnimationId == 7 && explicitBase.AnimationId == 5 && explicitBase.AnimationLast == 8, "SETMODEL changed executed BASEANIME ownership or field load substituted an idle clip.");
        Require(modelWrites.Count == 4 && modelWrites.Take(3).All(write => write.Base == 0 && write.Walk == 0 && write.Run == 0) && modelWrites[^1] == (2, 5, 6, 7), "SETMODEL initialized or reset base-animation bytes instead of preserving their current native owner.");
        Require(selected.Contains((0, 0)) && selected.Contains((1, 5)) && selected.All(selection => selection.Animation != 42), "Native metadata lookup used an unrelated42 default or omitted an actual configured idle.");
        EventEngine independent = Create();
        independent.Restore(live.Capture());
        for (int callback = 0; callback < 4; callback++)
        {
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Native base-allocation or explicit-base animation diverged after independent restoration.");
        }
        Console.WriteLine("Native base animation allocation: actual zero591/592/593, hidden SETMODEL-only idle0, preserved BASEANIME across model writes, decoded ranges and independent continuation pass.");
    }
}
