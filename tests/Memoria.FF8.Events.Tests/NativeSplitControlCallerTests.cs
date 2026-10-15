using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSplitControlCallerContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        (Jsm.Opcode, int)[] Actor(int label, int character, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1, (24 << 7) | 1, (32 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(8, 0, -3000), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 81), (Jsm.Opcode.POPM_B, 6101), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(16, 1, 0), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(24, 4, 3000), [(Jsm.Opcode.LBL, 25), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, -2800), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SPLIT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(initial.DeepCopy());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeJoinMovement = _ => true,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                // Declared component geometry isolates the native caller/binding contract.
                ResolveNativeJoinMovement = query =>
                {
                    int x = query.FixedX + 20 * 4096;
                    NativePlayerGroundProbe[] fan = new[] { 32, -32, 0 }.Select(offset =>
                    {
                        int probeX = x + query.Radius * FieldNativeWalkmeshProbe.Sine(query.Direction + offset);
                        int probeY = query.FixedY - query.Radius * FieldNativeWalkmeshProbe.Sine(query.Direction + offset + 64);
                        NativePlayerGroundProbe probe = new(probeX, probeY, query.FixedZ >> 12);
                        return probe with { ActorCollision = query.HasActorCollision!(probe) };
                    }).ToArray();
                    bool collision = fan.Any(probe => probe.ActorCollision);
                    return new NativeJoinMovementResult(!collision, collision ? query.FixedX : x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction)], [fan]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Func<NativeJoinMovementQuery, NativeJoinMovementResult?> geometry = live.ResolveNativeJoinMovement!;
        bool committedPeerObserved = false;
        live.ResolveNativeJoinMovement = query =>
        {
            if (query.ObjectId == 2)
            {
                ActorState peer = live.Actors[1];
                Require(peer.X == -2980 && query.HasActorCollision!(new NativePlayerGroundProbe(-2980 * 4096, 0, 0)), "Later moving peer did not see the earlier committed collision body.");
                committedPeerObserved = true;
            }
            return geometry(query);
        };
        live.Step();
        live.ResolveNativeJoinMovement = geometry;
        Require(committedPeerObserved, "SPLIT omitted ordered enabled-peer contact evaluation.");
        JsonElement pending = live.Capture();
        JsonElement heldCompanion = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && !fiber.GetProperty("Initialization").GetBoolean());
        Require(heldCompanion.GetProperty("WaitReason").GetString() == "Frames" && !CapturedVariables(pending).TryGetProperty("6101", out _), "SPLIT did not encounter its companion's live WAIT slot.");
        Require(pending.GetProperty("PartySplit").GetProperty("CallerObjectId").GetInt32() == 4 && pending.GetProperty("PartySplit").GetProperty("Targets").EnumerateArray().Select(target => target.GetProperty("ObjectId").GetInt32()).SequenceEqual([1, 2, 3]) && live.IsUserControlEnabled, "SPLIT still required caller to be a party leader or disabled control.");
        Require(live.Actors.Skip(1).Take(3).All(actor => actor.CollisionEnabled) && live.PlayerActor is { CharacterId: 0, X: -2980 } && live.Scene.NativePlayerPosition == new FieldFixedPosition(-2980 * 4096, 0, 0), "Enabled party peers or noncaller runtime leader pose were not committed.");
        EventEngine resumed = Create();
        resumed.Restore(pending);
        for (int tick = 0; tick < 15; tick++)
        {
            bool held = live.Capture().GetProperty("PartySplit").ValueKind != JsonValueKind.Null;
            live.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Arbitrary-caller party SPLIT diverged after independent restore.");
            if (held)
            {
                JsonElement companion = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && !fiber.GetProperty("Initialization").GetBoolean());
                Require(JsonElement.DeepEquals(heldCompanion, companion) && !CapturedVariables(live.Capture()).TryGetProperty("6101", out _), "SPLIT advanced or cancelled its held companion VM instead of preserving its slot, operands and WAIT receipt.");
            }
        }
        Require(live.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && CapturedVariables(live.Capture()).GetProperty("6000").GetInt32() == 77 && live.Actors.Skip(1).Take(3).All(actor => actor.NativeMovementCompletion542 == 2), "SPLIT did not complete all physical bodies and its same-budget successor.");
        Require(CapturedVariables(live.Capture()).GetProperty("6101").GetInt32() == 81 && live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("Sequence").GetInt64() == heldCompanion.GetProperty("Sequence").GetInt64() && fiber.GetProperty("WaitReason").GetString() == "NativeHalt"), "Completed SPLIT did not release the same companion slot to resume its expired WAIT and ordinary successor.");
        JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
        malformed["PartySplit"]!["Targets"]!.AsArray().RemoveAt(1);
        JsonElement before = resumed.Capture();
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("SPLIT accepted a missing saved body.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, resumed.Capture()), "Missing-body refusal mutated current VM or services.");
    }
}
