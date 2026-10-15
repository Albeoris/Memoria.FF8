using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Synthetic component programs exercise the current interactive battle/standalone-state host contract.
// Installed initial vitals are authentic inputs; these tests do not claim an original field journey or native combat parity.
public sealed class BattleIntegrationTests
{
    private const int FixtureField = 999;
    private static readonly Lazy<NativeCharacterVitals> InitialVitals = new(() =>
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameAssets assets = new(directory);
        return assets.ReadNewGameCharacterVitals();
    });

    [Fact]
    public void CompletedBattleHistorySurvivesFreshFieldClockAllocation()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_W, 800));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        engine.AdvanceInteractiveBattleCallbacks(3);
        engine.ReturnFromInteractiveBattle(3);
        NativeBattleCompletion completed = Assert.Single(engine.Scene.NativeBattleCompletions!);
        engine.LoadField(FixtureField + 1, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Null(engine.Scene.FieldCallbackClock!.BattleCallbacks);
        Assert.Equal(completed, Assert.Single(engine.Scene.NativeBattleCompletions!));
        JsonElement nextVisit = engine.Capture();
        session.SetEventState(nextVisit);
        engine.Restore(nextVisit);
        Assert.True(JsonElement.DeepEquals(nextVisit, engine.Capture()));
    }

    [Fact]
    public void PendingBattleFreezesFieldDispatchAndInputAndRestoresCurrentCheckpoint()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_W, 800));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        Assert.Equal(PlayerTriggerKind.Battle, engine.NextTrigger.Kind);
        Assert.NotNull(engine.PendingInteractiveBattle);
        JsonElement before = engine.Capture();
        engine.Step();
        PlayerActionResult input = engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 20));
        Assert.False(input.Accepted);
        Assert.Equal(0, input.FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.False(session.State.Variables.ContainsKey(800));

        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State with { EventState = before }))!;
        GameSession restoredSession = new(saved);
        EventEngine restored = CreateEngine(restoredSession);
        restored.LoadField(FixtureField, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        restored.Restore(before);
        restored.Step();
        Assert.True(JsonElement.DeepEquals(before, restored.Capture()));
        engine.AdvanceInteractiveBattleCallbacks(40);
        restored.AdvanceInteractiveBattleCallbacks(40);
        engine.ReturnFromInteractiveBattle(40);
        restored.ReturnFromInteractiveBattle(40);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), restored.Capture()));
        Assert.Equal(42, restoredSession.State.Variables[800]);
    }

    [Fact]
    public void ReturnResumesOpcodeAndRefreshesMemoryBeforeOrdinaryStep()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 321), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 654), (Jsm.Opcode.POPM_W, 802));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        bool outcomePublished = false;
        engine.AdvanceInteractiveBattleCallbacks(57);
        engine.ReturnFromInteractiveBattle(57, () => outcomePublished = true);
        Assert.Equal(57, engine.Scene.NativeBattleCompletions![0].EmulatedCombatCallbacks);
        Assert.True(outcomePublished);
        Assert.Equal(321, session.State.Variables[800]);
        engine.Step();
        Assert.Equal(654, session.State.Variables[802]);
    }

    [Fact]
    public void ReturnTransitionReachesRequestedDestination()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.MAPJUMP3, 50));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        engine.AdvanceInteractiveBattleCallbacks(20);
        engine.ReturnFromInteractiveBattle(20);
        Assert.Equal(PlayerTriggerKind.FieldTransition, engine.NextTrigger.Kind);
        Assert.Equal(100, session.State.FieldId);
    }

    [Fact]
    public void ReturnMovieResumesRequestedMovie()
    {
        byte[] source = EncodeProgram([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.RET, 0)]);
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        engine.Step();
        engine.AdvanceInteractiveBattleCallbacks(20);
        engine.ReturnFromInteractiveBattle(20);
        Assert.Equal(PlayerTriggerKind.Movie, engine.NextTrigger.Kind);
        Assert.Equal(SceneKind.Movie, session.State.Scene);
    }

    [Fact]
    public void UnsupportedResumedOpcodeReportsItsActualFault()
    {
        // SHAKE is unsupported in this component without the explicitly required installed lookup table.
        byte[] source = Program((Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SHAKE, 0));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        engine.AdvanceInteractiveBattleCallbacks(20);
        Assert.Throws<EventScriptException>(() => engine.ReturnFromInteractiveBattle(20));
        Assert.Equal("SHAKE", engine.LastFault!.Opcode);
    }

    [Fact]
    public void BattleCallbacksAdvanceProvedNativeClocksWhileFieldFrameRemainsFrozen()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_W, 800));
        (GameSession session, EventEngine engine) = Fixture(source);
        engine.Step();
        long fieldFrame = engine.Frame;
        long timerCalls = session.ExamTimer!.ClockCalls;
        long mainLoopCalls = session.State.WorldProcess!.MainLoopCallbacks;
        engine.AdvanceInteractiveBattleCallbacks(7);
        Assert.Equal(fieldFrame, engine.Frame);
        Assert.Equal(timerCalls + 4 * 7, session.ExamTimer.ClockCalls);
        Assert.Equal(mainLoopCalls + 7, session.State.WorldProcess.MainLoopCallbacks);
        Assert.Equal(7, engine.Scene.FieldCallbackClock!.BattleCallbacks);
        JsonElement pending = engine.Capture();
        engine.AdvanceInteractiveBattleCallbacks(0);
        Assert.True(JsonElement.DeepEquals(pending, engine.Capture()));
        engine.Step();
        Assert.Equal(fieldFrame, engine.Frame);

        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State with { EventState = pending }))!;
        GameSession restoredSession = new(saved);
        EventEngine restored = CreateEngine(restoredSession);
        restored.LoadField(FixtureField, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        restored.Restore(pending);
        Assert.Equal(7, restored.Scene.FieldCallbackClock!.BattleCallbacks);
        Assert.Equal(session.ExamTimer.ClockCalls, restoredSession.ExamTimer!.ClockCalls);
        Assert.Equal(session.State.WorldProcess.MainLoopCallbacks, restoredSession.State.WorldProcess!.MainLoopCallbacks);
        engine.ReturnFromInteractiveBattle(7);
        restored.ReturnFromInteractiveBattle(7);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), restored.Capture()));

        JsonElement complete = restored.Capture();
        JsonNode corrupt = JsonNode.Parse(complete.GetRawText())!;
        corrupt["Scene"]!["FieldCallbackClock"]!["BattleCallbacks"] = 8;
        Assert.Throws<InvalidDataException>(() => restored.Restore(JsonSerializer.SerializeToElement(corrupt)));
        Assert.True(JsonElement.DeepEquals(complete, restored.Capture()));
    }

    private static (GameSession Session, EventEngine Engine) Fixture(byte[] source)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(InitialVitals.Value.DeepCopy());
        // Declare the synthetic component's field owner; no original visit is claimed.
        session.Restore(session.State with { FieldId = FixtureField, FieldName = "synthetic-interactive-battle-component" });
        EventEngine engine = CreateEngine(session);
        engine.LoadField(FixtureField, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        return (session, engine);
    }

    private static EventEngine CreateEngine(GameSession session)
    {
        return new EventEngine(session)
        {
            UseInteractiveBattle = (_, _) => true,
            ValidateInteractiveBattle = (_, _) => null,
            ReadMovieCameraFlags = (disc, movie) => NativeMovieCameraReader.Read(File.ReadAllBytes(MovieClock.GetCameraPath(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory, disc, movie)))
        };
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[] afterBattle)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0), .. afterBattle, (Jsm.Opcode.RET, 0)];
        return EncodeProgram(operations);
    }

    private static byte[] EncodeProgram((Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        // Script0 is the native synchronous initializer; yielding BATTLE/MOVIEREADY belongs to
        // script1 on an ordinary callback, exactly as in HostedAutomaticBattleTests.
        operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), .. operations];
        operations[2] = (Jsm.Opcode.LBL, 1);
        const int operationsOffset = 16;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
