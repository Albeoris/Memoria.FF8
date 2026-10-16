using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorScrollCameraOneContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        (GameSession Session, EventEngine Engine) Create(bool cosine = false)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] prefix = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETCAMERA, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 2), (Jsm.Opcode.DSCROLLA, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000)];
            (Jsm.Opcode, int)[] follow = cosine
                ? [.. prefix, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65486), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.CSCROLL2, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SCROLLSYNC2, 0), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETCAMERA, 0), (Jsm.Opcode.PSHAC, 2), (Jsm.Opcode.DSCROLLA, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]
                : [.. prefix, (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 0)];
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.ResolveNativeActorScrollCenter = actor => actor < engine.Scene.NativeActorObjectIds.Count ? engine.Scene.CameraIndex == 1 ? ((short)0, (short)0) : ((short)(100 * engine.Scene.NativeActorObjectIds[actor] + engine.Frame), (short)-engine.Frame) : null;
            engine.ResolveNativeNormalBackgroundScrollCenter = () => ((short)(31 + engine.Frame), (short)-engine.Frame);
            engine.CanRestoreNativeActorScroll = (scene, _) => scene.ActorBackgroundScroll is { Mode: 0 } current && (scene.CameraIndex == 0 || current.X == 0 && current.Y == 0);
            engine.CanSetCosineBackgroundScroll = (layer, x, y, duration) => layer == 1 && x == 0 && y == -50 && duration == 3;
            engine.LoadField(600, SyntheticObjects([1, 16 << 7, 32 << 7], [init, follow, unused, target]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        for (int callback = 0; callback < 6 && live.Engine.Scene.ActorBackgroundScroll is null; callback++)
        {
            live.Engine.Step();
        }
        Require(live.Engine.Scene.ActorBackgroundScroll is { NativeObjectId: 2, ObjectId: 2, NativeActorId: 1, Mode: 0, Status: 1, X: 0, Y: 0, PreviousX: 0, PreviousY: 0 }, "Camera1 DSCROLLA lost raw/compact row0 selector or consumed selected row1 center as previous row0 history.");
        Require(live.Engine.Scene.NormalBackgroundScrollCenter is { CameraIndex: 1 } normal && normal.X == 31 + live.Engine.Frame && normal.Y == -live.Engine.Frame && live.Engine.Scene.NativeLayerZeroScrollCurrent is { X: 0, Y: 0 }, "Camera1 selected center and nonselected row0 current stopped having independent post-VM owners.");
        Require(!live.Session.State.Variables.ContainsKey(5000), "Camera1 DSCROLLA did not preserve its native return3 yield.");
        JsonElement saved = live.Engine.Capture();
        var restored = Create();
        restored.Engine.Restore(saved);
        for (int callback = 0; callback < 3; callback++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Camera1 independent row0/row1 scroll owners diverged after exact restore.");
            Require(live.Engine.Scene.NormalBackgroundScrollCenter!.X == 31 + live.Engine.Frame && live.Engine.Scene.ActorBackgroundScroll!.X == 0, "Continuous row0 follow stopped selected camera1 publication or retargeted its center.");
        }
        Require(live.Session.State.Variables[5000] == 9, "The callback after DSCROLLA did not resume the original VM continuation.");
        var selected = Create(cosine: true);
        bool issueObserved = false;
        selected.Engine.CommandExecuted += command =>
        {
            if (command.Name != nameof(Jsm.Opcode.CSCROLL2))
            {
                return;
            }
            issueObserved = true;
            NativeNormalBackgroundScrollCenter prior = selected.Engine.Scene.NormalBackgroundScrollCenter!;
            BackgroundScrollPosition row = selected.Engine.Scene.BackgroundScrollPositions[1];
            Require(row is { Mode: 5, Status: 0, Counter: null, PreviousX: null, PreviousY: null, TargetX: 0, TargetY: -50, Duration: 3 } && row.X == prior.X && row.Y == prior.Y, "CSCROLL2 lost low WORD operands, authentic selected-row current or preserved unknown prior/counter before475DF0.");
            Require(selected.Engine.Scene.ActorBackgroundScroll is { Mode: 0, Status: 1, X: 0, Y: 0 }, "Selected row1 CSCROLL2 replaced the independent row0 actor command.");
        };
        for (int callback = 0; callback < 8 && !issueObserved; callback++)
        {
            selected.Engine.Step();
        }
        Require(issueObserved && selected.Session.State.Variables[5001] == 10 && !selected.Session.State.Variables.ContainsKey(5002), "CSCROLL2 did not return2 in the same VM budget or SCROLLSYNC2 completed before its physical endpoint.");
        Require(selected.Engine.Scene.BackgroundScrollPositions[1] is { Counter: 1, Status: 1 } first && first.X == first.PreviousX && first.Y == first.PreviousY && selected.Engine.Scene.NormalBackgroundScrollCenter is null, "Selected cosine elapsed0 did not capture the actual row1 start or left a competing normal-center owner.");
        var selectedRestored = Create(cosine: true);
        selectedRestored.Engine.Restore(selected.Engine.Capture());
        bool completed = false;
        for (int callback = 0; callback < 7; callback++)
        {
            selected.Engine.Step();
            selectedRestored.Engine.Step();
            Require(JsonElement.DeepEquals(selected.Engine.Capture(), selectedRestored.Engine.Capture()), "Selected row1 cosine and concurrent row0 actor state diverged after exact restore.");
            completed |= selected.Engine.Scene.BackgroundScrollPositions[1] is { Status: 2, Counter: 3, X: 0, Y: -50 };
        }
        Require(completed && selected.Session.State.Variables[5002] == 11 && selected.Engine.Scene.CameraIndex == 0 && selected.Engine.Scene.ActorBackgroundScroll!.X > 200 && selected.Engine.Scene.BackgroundScrollPositions[1] is { Status: 2, X: 0, Y: -50 }, "Completed row1 cosine did not survive SCROLLSYNC2 and the later CA0/DSCROLLA selection.");
        Console.WriteLine("Actor scroll camera1: retained raw selector, independent zero row0/current selected row1, return3 timing and exact restore passed.");
        Console.WriteLine("Selected cosine row1: WORD operands, preserved issue history, return2, elapsed0/completion/sync, concurrent row0 and CA0 switch passed.");
    }
}
