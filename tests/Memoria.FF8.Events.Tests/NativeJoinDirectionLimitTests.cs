using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJoinDirectionLimitContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        (Jsm.Opcode, int)[] Actor(int label, int character, int y) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> leader = Actor(8, 0, 0).ToList();
        leader.InsertRange(2, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0)]);
        List<(Jsm.Opcode, int)> follower = Actor(16, 1, 300).ToList();
        follower.InsertRange(follower.Count - 1, [(Jsm.Opcode.PSHN_L, 39), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.MLIMIT, 0)]);
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], leader.ToArray(), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 15), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], follower.ToArray(), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = new(new GameState { CharacterVitals = initial.DeepCopy(), WorldControls = NativeWorldControlState.CreateNewGame() });
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativeJoinMovement = actor => actor.ObjectId == 2,
                CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                // Component ground certificates isolate the shared native direction kernel.
                ResolveNativeScriptMovement = query => new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, 1000 * 4096, 300 * 4096, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(1000 * 4096, 300 * 4096, query.Direction, [new NativeScriptGroundProbe(1000 * 4096, 300 * 4096, query.FixedZ >> 12), new NativeScriptGroundProbe(1000 * 4096, 300 * 4096, query.FixedZ >> 12), new NativeScriptGroundProbe(1000 * 4096, 300 * 4096, query.FixedZ >> 12)])]),
                ResolveNativeJoinMovement = query => new NativeJoinMovementResult(false, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(query.FixedX, query.FixedY, query.Direction)], [[new NativePlayerGroundProbe(query.FixedX, query.FixedY, query.FixedZ >> 12), new NativePlayerGroundProbe(query.FixedX, query.FixedY, query.FixedZ >> 12), new NativePlayerGroundProbe(query.FixedX, query.FixedY, query.FixedZ >> 12)]])
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        while (live.Actors[2].NativeScriptMovement is not { Active: false } && live.Frame < 12)
        {
            live.Step();
        }
        ActorState actor = live.Actors[2];
        Require(actor.NativeScriptMovement is { Active: false, Asynchronous: true } && actor.NativeMovementCompletion542 == 2 && actor.NativeTurnAccumulator474 == 5 && actor.MovementDirectionLimit == 5 && actor.Direction == 44, "Executed RFMOVE did not leave its completed physical474/610/facing for JOIN.");
        short retained = actor.NativeTurnAccumulator474!.Value;
        NativeScriptDirectionQuery? issued = null;
        Func<NativeScriptDirectionQuery, NativeScriptDirectionResult?> direction = live.ResolveNativeScriptDirection!;
        live.ResolveNativeScriptDirection = query =>
        {
            if (live.Capture().GetProperty("PartyJoin").ValueKind != JsonValueKind.Null && issued is null)
            {
                issued = query;
            }
            return direction(query);
        };
        while (live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && live.Frame < 25)
        {
            live.Step();
        }
        Require(issued is { DirectionLimit: 5 } && issued.TurnAccumulator == retained && actor.NativeTurnAccumulator474 == 0 && actor.Direction == 39 && actor.X == 1000 && actor.Y == 300, $"JOIN reset or ignored retained474/MLIMIT, or committed blocked coordinates incorrectly: query={issued}, turn={actor.NativeTurnAccumulator474}, facing={actor.Direction}, XY={actor.X}/{actor.Y}.");
        JsonElement pending = live.Capture();
        EventEngine copy = Create();
        copy.Restore(pending);
        for (int tick = 0; tick < 4; tick++)
        {
            live.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Limited JOIN direction or physical474 diverged across independent restore.");
        }
        JsonElement valid = copy.Capture();
        JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
        invalid["Actors"]![2]!.AsObject().Remove("NativeTurnAccumulator474");
        try
        {
            copy.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Missing current physical474 was accepted as zero.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(valid, copy.Capture()), "Missing474 restore partially mutated current JOIN.");
        Func<NativeJoinMovementQuery, NativeJoinMovementResult?> finish = query => new NativeJoinMovementResult(true, 0, 0, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(0, 0, query.Direction)], [[new NativePlayerGroundProbe(0, 0, query.FixedZ >> 12), new NativePlayerGroundProbe(0, 0, query.FixedZ >> 12), new NativePlayerGroundProbe(0, 0, query.FixedZ >> 12)]]);
        live.ResolveNativeJoinMovement = finish;
        copy.ResolveNativeJoinMovement = finish;
        for (int tick = 0; tick < 4; tick++)
        {
            live.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Limited JOIN completion changed after restore.");
        }
        Require(live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && live.Actors[1].NativeTurnAccumulator474 == 0 && live.Actors[2].NativeTurnAccumulator474 != 0 && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77, "JOIN completion lost its retained turn or same-callback successor.");
        Console.WriteLine("Native component RFMOVE produces474=5/610=5/facing44; JOIN retains474 then turns toward its new target to474=0/facing39 despite blocked geometry; pending restore, missing-word atomic refusal and completion successor pass.");
    }
}
