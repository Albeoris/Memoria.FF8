using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void RemainingPresentationContracts()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession CreateTimerSession(NativeExamTimerState state)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            session.SetExamTimer(state);
            return session;
        }
        EventEngine LoadTimerField(GameSession session, byte[] program)
        {
            EventEngine engine = new(session);
            engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        byte[] TimerProgram(params (Jsm.Opcode, int)[] operations) => SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), .. operations, (Jsm.Opcode.RET, 8)]]);
        byte[] timerProgram = TimerProgram((Jsm.Opcode.PSHN_L, 65536 + 4096), (Jsm.Opcode.SHADETIMER, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0));
        EventEngine timer = LoadTimerField(CreateTimerSession(NativeExamTimerState.FromColdProcess()), timerProgram);
        timer.Step();
        Require(timer.ExamTimer is { DisplayIntensity814: 4096 } && timer.ExamTimer.ReadDisplayColor4A6EB0() == 128 && CapturedVariables(timer.Capture()).GetProperty("5000").GetInt32() == 73, "SHADETIMER did not store lowWORD intensity, publish the actual UI color or continue in the same VM budget.");
        EventEngine timerCopy = LoadTimerField(CreateTimerSession(NativeExamTimerState.FromColdProcess()), timerProgram);
        timerCopy.Restore(timer.Capture());
        timer.Step();
        timerCopy.Step();
        Require(JsonElement.DeepEquals(timer.Capture(), timerCopy.Capture()), "SHADETIMER changed its process UI owner across independent restore.");

        (Jsm.Opcode, int)[] module = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65535), (Jsm.Opcode.BGANIMEFLAG, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.RBGANIME, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] backgroundProgram = SyntheticObjects([0], [module]);
        backgroundProgram[2] = 1;
        backgroundProgram[3] = 0;
        EventEngine Background()
        {
            EventEngine engine = new(new GameSession(new GameState()));
            engine.LoadField(100, backgroundProgram, []);
            return engine;
        }
        EventEngine background = Background();
        background.Step();
        JsonElement animation = background.Capture().GetProperty("BackgroundAnimations")[0];
        Require(animation.GetProperty("NativeFlag404").GetInt32() == -1 && CapturedVariables(background.Capture()).GetProperty("5000").GetInt32() == 7, "BGANIMEFLAG did not retain signed lowWORD404 or return2 continuation.");
        EventEngine backgroundCopy = Background();
        backgroundCopy.Restore(background.Capture());
        for (int callback = 0; callback < 6; callback++)
        {
            background.Step();
            backgroundCopy.Step();
            Require(JsonElement.DeepEquals(background.Capture(), backgroundCopy.Capture()), "BGANIMEFLAG changed its retained module word or independent animation clock after restore.");
        }
        Console.WriteLine("SHADETIMER/BGANIMEFLAG: lowWORD stores, existing process/UI and background-module ownership, return2 and independent restoration passed.");
    }
}
