using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMovieActorLifecycleTests
{
    [Fact]
    public void MovieStartAndCleanupScaleCurrentWordsAndActiveMovementWithoutInventingCallbacks()
    {
        var first = Create();
        first.Engine.Step();
        first.Engine.Step();
        Assert.False(first.Session.IsMoviePlaying);
        Assert.Equal(1, first.Engine.Scene.MovieActors!.Mode);
        Assert.Null(first.Engine.Scene.MovieActors.StartFrame);
        first.Engine.Step();
        Assert.True(first.Session.IsMoviePlaying);
        Assert.Equal(3, first.Engine.Scene.MovieActors.StartFrame);
        Assert.Equal(16014, first.Engine.Actors[1].MovementSpeed);
        Assert.Equal((short)16014, first.Engine.Actors[1].NativeMovementBaseSpeed);
        Assert.Equal(32, first.Engine.Actors[1].AnimationSpeed);
        first.Session.SetMoviePlayback(new(700, 1099, true, false, false));
        first.Engine.Step();
        Assert.Equal((short)1001, first.Engine.Actors[1].NativeScriptMovement!.Speed);
        first.Engine.Step();
        ActorState moving = first.Engine.Actors[1];
        Assert.Equal(13345, moving.MovementSpeed);
        Assert.Equal((short)13345, moving.NativeMovementBaseSpeed);
        Assert.Equal((short)13345, moving.NativeScriptMovement!.Speed);
        Assert.True(moving.NativeScriptMovement.Active);
        JsonElement saved = first.Engine.Capture();
        var restored = Create(first.Session.State);
        restored.Engine.Restore(saved);
        Assert.True(JsonElement.DeepEquals(saved, restored.Engine.Capture()));
        long frame = first.Engine.Frame;
        long clock = first.Session.ExamTimer!.ClockCalls;
        first.Session.CompleteMovie();
        restored.Session.CompleteMovie();
        Assert.Equal(frame, first.Engine.Frame);
        Assert.Equal(clock, first.Session.ExamTimer.ClockCalls);
        Assert.Equal(6672, moving.MovementSpeed);
        Assert.Equal((short)6672, moving.NativeMovementBaseSpeed);
        Assert.Equal((short)6672, moving.NativeScriptMovement.Speed);
        Assert.Equal(16, moving.AnimationSpeed);
        Assert.Equal(frame, first.Engine.Scene.MovieActors.EndFrame);
        Assert.True(JsonElement.DeepEquals(first.Engine.Capture(), restored.Engine.Capture()));
        first.Session.CompleteMovie();
        Assert.Equal(6672, moving.MovementSpeed);
        JsonElement completed = first.Engine.Capture();
        JsonNode missing = JsonNode.Parse(completed.GetRawText())!;
        missing["Actors"]![1]!.AsObject().Remove("NativeMovementBaseSpeed");
        Assert.Throws<InvalidDataException>(() => first.Engine.Restore(JsonSerializer.SerializeToElement(missing)));
        Assert.True(JsonElement.DeepEquals(completed, first.Engine.Capture()));
        first.Engine.Step();
        restored.Engine.Step();
        Assert.True(JsonElement.DeepEquals(first.Engine.Capture(), restored.Engine.Capture()));
        Assert.Equal(clock + 2, first.Session.ExamTimer.ClockCalls);
    }

    [Fact]
    public void ZeroCallbackSkipBalancesStartWordsAndDoesNotAcquireMissingHistoricalMovieAuthority()
    {
        var fixture = Create();
        fixture.Engine.Step();
        fixture.Engine.Step();
        fixture.Engine.Step();
        long frame = fixture.Engine.Frame;
        fixture.Session.SetMoviePlayback(new(0, 1099, true, false, false));
        fixture.Session.CompleteMovie(true);
        Assert.Equal(frame, fixture.Engine.Frame);
        Assert.Equal(8007, fixture.Engine.Actors[1].MovementSpeed);
        Assert.Equal((short)8007, fixture.Engine.Actors[1].NativeMovementBaseSpeed);
        Assert.Equal(16, fixture.Engine.Actors[1].AnimationSpeed);
        JsonElement saved = fixture.Engine.Capture();
        JsonNode absent = JsonNode.Parse(saved.GetRawText())!;
        absent["Scene"]!.AsObject().Remove("MovieActors");
        Assert.Throws<InvalidDataException>(() => fixture.Engine.Restore(JsonSerializer.SerializeToElement(absent)));
        Assert.True(JsonElement.DeepEquals(saved, fixture.Engine.Capture()));
    }

    private static (GameSession Session, EventEngine Engine) Create(GameState? state = null)
    {
        var session = new GameSession(state ?? new GameState { Scene = SceneKind.Field, FieldId = 600, ExamTimer = NativeExamTimerState.FromReachedSet(600) });
        var engine = new EventEngine(session)
        {
            MoviePlayback = () => session.State.MoviePlayback,
            AnimationFrameCount = (_, _) => 4,
            CanUseNativeScriptMove = _ => true,
            ResolveNativeScriptDirection = request => new(NativeScriptDirectionStatus.Moving, request.FixedX, request.FixedY, request.MovementDirection, request.TurnAccumulator),
            ResolveNativeScriptMovement = BlockedGround
        };
        engine.LoadField(600, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return (session, engine);
    }

    private static NativeScriptMovementResult BlockedGround(NativeScriptMovementQuery request)
    {
        NativeScriptGroundProbe probe = new(request.FixedX, request.FixedY, request.FixedZ >> 12);
        probe = probe with { ActorCollision = request.HasActorCollision?.Invoke(probe) == true };
        return new(NativeScriptMovementStatus.Blocked, request.FixedX, request.FixedY, request.FixedZ, request.Triangle, request.Direction, [new(request.FixedX, request.FixedY, request.Direction, [probe, probe, probe])]);
    }

    private static byte[] Program()
    {
        (Jsm.Opcode Opcode, int Parameter)[][] scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 8007), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1001), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.PSHN_L, 78881), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]
        ];
        int count = scripts.Sum(script => script.Length);
        byte[] result = new byte[24 + count * 4];
        result[3] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(6), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 257);
        int operation = 0;
        for (int index = 0; index < scripts.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12 + index * 2), checked((ushort)operation));
            foreach (var item in scripts[index])
            {
                int encoded = item.Parameter != 0 || item.Opcode == Jsm.Opcode.PSHN_L ? (int)item.Opcode << 24 | item.Parameter & 0xFFFFFF : (int)item.Opcode;
                BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(24 + operation++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(20), checked((ushort)count));
        return result;
    }
}
