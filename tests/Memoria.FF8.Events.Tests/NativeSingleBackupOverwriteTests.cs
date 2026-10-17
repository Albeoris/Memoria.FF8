using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSingleBackupOverwriteContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] content = SyntheticObjects([0, (16 << 7) | 4, (32 << 7), (48 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 111), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.PSHN_L, 48), (Jsm.Opcode.MOVEA, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHAC, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVEA, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            var result = new EventEngine(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                ResolveNativeScriptDirection = query => query.TargetX == 500 * 4096 && query.FixedX >= 5 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, 64, 0) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] probes = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, probes)]);
                }
            };
            result.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return result;
        }
        EventEngine live = Create();
        while (live.Actors[1].NativeScriptMovement is not { ScriptId: 20, Active: true, Suspended: false } && live.Frame < 12)
        {
            live.Step();
        }
        Require(live.Actors[1].NativeMovementRequest?.SavedMovement.ScriptId == 17, "Original request did not preserve the first physical backup.");
        JsonElement originalLower = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        typeof(ActorState).GetProperty(nameof(ActorState.NativePushContactPending))!.SetValue(live.Actors[1], true);
        EventEngine copy = Create();
        copy.Restore(live.Capture());
        live.Step();
        copy.Step();
        JsonElement overwritten = live.Capture();
        Require(JsonElement.DeepEquals(overwritten, copy.Capture()) && live.Actors[1].NativeMovementRequest is null && live.Actors[1].NativeScriptMovement is { ScriptId: 20, Active: true } && JsonElement.DeepEquals(originalLower, overwritten.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17)), "Empty push did not overwrite/consume the single physical backup while preserving the lower issued MOVEA receipt.");
        EventEngine latentCopy = Create();
        latentCopy.Restore(overwritten);
        foreach (string property in new[] { "NativeMoveOperation", "NativeMoveTarget" })
        {
            JsonNode invalid = JsonNode.Parse(overwritten.GetRawText())!;
            JsonNode lower = invalid["Fibers"]!.AsArray().Single(node => node!["ScriptId"]!.GetValue<int>() == 17)!;
            lower[property] = lower[property]!.GetValue<int>() + 1;
            try
            {
                latentCopy.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("Corrupt latent native movement receipt was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(overwritten, latentCopy.Capture()), "Invalid latent receipt restore mutated the physical body or lower fiber.");
        }
        for (int tick = 0; tick < 10; tick++)
        {
            live.Step();
            copy.Step();
            latentCopy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()) && JsonElement.DeepEquals(live.Capture(), latentCopy.Capture()), "Consumed single backup or latent issued MOVEA completion diverged after independent restore.");
        }
        JsonElement final = live.Capture();
        Require(live.Actors[1].X == 500 && live.Actors[1].NativeMovementRequest is null && !final.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 20) && final.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17 && !fiber.GetProperty("Complete").GetBoolean() && fiber.GetProperty("WaitReason").GetString() == nameof(WaitReason.NativeHalt)), "Parent RET restored an already-consumed old backup or lower MOVEA failed to poll current completion542 before entering its native idle HALT.");
    }
}
