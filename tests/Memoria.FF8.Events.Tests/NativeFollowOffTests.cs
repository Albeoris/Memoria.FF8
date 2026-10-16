using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFollowOffContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach (int character in new[] { 1, 7, 255 })
        {
            bool member = character != 7;
            int slot = character == 255 ? 2 : 1;
            (Jsm.Opcode, int)[] leader = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([1, (16 << 7) | 1], [leader, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, ushort.MaxValue), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
                EventEngine engine = new(session) { AnimationFrameCount = (model, _) => model >= 0 ? 4 : throw new InvalidOperationException($"Empty FOLLOWOFF must not read clip metadata (model{model}).") };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            live.Step();
            ActorState actor = live.Actors[1];
            JsonElement detached = live.Capture();
            Require(CapturedVariables(detached).GetProperty("5000").GetInt32() == 77 && !CapturedVariables(detached).TryGetProperty("5001", out _) && live.Scene.Party.SequenceEqual([0, 1, 255]) && live.Scene.PartyBindingMemory?.At(slot) == (member ? 1 : 255) && live.Scene.FieldPartyNativeActorIds[slot] == 255, "FOLLOWOFF consumed an operand, yielded, changed canonical/saved party or inferred a runtime binding.");
            Require(member ? actor.NativeFollowOff is { AnimationFrames: null } && actor.NativeFollowOff.PartySlot == slot && actor.NativePartyFlags is { AutoAnimation4: false, DetachedFollow64: true } && actor.NativeAnimationHeader is { BoundModelId: -1, Frame: 0, First: 0, Terminal: null, Flags: 0x2000 } : actor.NativeFollowOff is null, "Empty FOLLOWOFF invented idle metadata or applied its member-only stores to a nonparty actor.");
            EventEngine copy = Create();
            copy.Restore(detached);
            for (int tick = 0; tick < 4; tick++)
            {
                live.Step();
                copy.Step();
                Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Empty detached/repeated FOLLOWOFF changed its opaque animation or aliases after independent restore.");
            }
            Require(CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 78 && actor.NativeAnimationBackup == new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0), "Repeated FOLLOWOFF failed after runtime detachment or altered the independent animation backup.");
            if (member)
            {
                JsonElement valid = live.Capture();
                foreach (bool missing in new[] { true, false })
                {
                    JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
                    JsonObject receipt = malformed["Actors"]![1]!["NativeFollowOff"]!.AsObject();
                    if (missing)
                    {
                        receipt.Remove("AnimationFrames");
                    }
                    else
                    {
                        receipt["AnimationFrames"] = 0;
                    }
                    try
                    {
                        live.Restore(JsonSerializer.SerializeToElement(malformed));
                        throw new InvalidOperationException("Malformed nullable FOLLOWOFF provenance was accepted.");
                    }
                    catch (Exception exception) when (exception is InvalidDataException or JsonException)
                    {
                    }
                    Require(JsonElement.DeepEquals(valid, live.Capture()), "Malformed FOLLOWOFF receipt restore changed current VM or native owners.");
                }
            }
        }
        NativeFollowOffMotionContract(assets);
        Console.WriteLine("FOLLOWOFF: empty member/nonmember, zero operands/return2, saved/runtime mapping separation, repeated idle write, nullable required provenance, active motion/rotation preservation and independent restore passed.");
    }

    private static void NativeFollowOffMotionContract(GameAssets assets)
    {
        foreach (bool rotation in new[] { false, true })
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0)];
            main.AddRange(rotation ? [(Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.LTURNR, 0)] : [(Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RCMOVE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.POPM_B, 5000)]);
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([3, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], main.ToArray(), [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)], rotation ? [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, CanUseNativeScriptMove = _ => true, ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator), ResolveNativeScriptMovement = query => new(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(query.FixedX, query.FixedY, query.Direction, [new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0)])]) };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            while (live.Actors[0].NativeFollowOff is null && live.Frame < 12)
            {
                live.Step();
            }
            ActorState actor = live.Actors[0];
            Require(actor.NativeFollowOff is { AnimationFrames: 4 } && actor.NativePartyFlags is { AutoAnimation4: false, DetachedFollow64: true } && live.Scene.FieldPartyNativeActorIds[0] == 255 && live.Scene.PartyBindingMemory?.First == 0 && actor.NativeMovementRequest is null && (rotation ? actor.RotationTarget is not null && actor.RotationUsesNativeLinear : actor.NativeScriptMovement is { Active: true, Asynchronous: true, HoldsFacing: true, TargetX: 40960000 } && actor.MovementTarget is not null && actor.NativeMotionAuthority is { FacingHold: 1, MovementDirection: 64 }), "FOLLOWOFF cancelled or reset an independent native movement/rotation/backup/facing owner.");
            EventEngine copy = Create();
            copy.Restore(live.Capture());
            for (int callback = 0; callback < 3; callback++)
            {
                live.Step();
                copy.Step();
                Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "FOLLOWOFF detached body motion/rotation diverged after independent restore.");
            }
        }
    }
}
