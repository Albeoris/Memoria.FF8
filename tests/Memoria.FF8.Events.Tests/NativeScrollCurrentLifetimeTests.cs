using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeScrollCurrentLifetimeContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (EventEngine Engine, GameSession Session) Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                ResolveNativeNormalBackgroundScrollCenter = () => ((short)40, (short)-80),
                ResolveNativeActorScrollCenter = _ => ((short)200, (short)-4),
                CanSetNativeActorLinearBackgroundScroll = (_, _) => true,
                CanRestoreNativeActorScroll = (_, _) => true
            };
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] leave = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 601), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.MAPJUMP, 0), (Jsm.Opcode.RET, 8)];
            LoadInitializedFixtureField(engine, session, SyntheticObjects([1], [init, leave]));
            Require(engine.Scene.NativeLayerZeroScrollCurrent is { Version: 1, X: 0, Y: 0 } && engine.Scene.NormalBackgroundScrollCenter is null, "A fresh native process did not retain its genuine zero-filled current words through serial initialization and the first field transition.");
            engine.Step();
            Require(engine.Scene.NativeLayerZeroScrollCurrent is { X: 40, Y: -80 }, "The actual normal camera callback did not publish the process current words.");
            engine.Step();
            FieldTransition arrival = session.State.FieldArrival ?? throw new InvalidOperationException("Scroll lifetime fixture did not execute its ordinary departure.");
            Require(arrival.FieldId == 601, "Scroll lifetime fixture lost its current MAPJUMP packet.");
            (Jsm.Opcode, int)[] curve = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.LSCROLLA, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            engine.LoadField(601, SyntheticObjects([1], [init, curve]), [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            Require(engine.Scene.NormalBackgroundScrollCenter is null && engine.Scene.NativeLayerZeroScrollCurrent is { X: 40, Y: -80 }, "A normal field load invented a new projection or reset the retained process center.");
            return (engine, session);
        }
        var live = Create();
        live.Engine.Step();
        Require(live.Engine.Scene.ActorBackgroundScroll is { Mode: 1, Status: 1, Counter: 1, X: 40, Y: -80, PreviousX: 40, PreviousY: -80, ProjectedTargetX: 200, ProjectedTargetY: -4 } && live.Engine.Scene.NativeLayerZeroScrollCurrent is { X: 40, Y: -80 }, "First-callback LSCROLLA did not preserve the prior field's actual current words at counter0.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        for (int callback = 0; callback < 3; callback++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Process camera current words diverged after independent restoration.");
        }
        Require(live.Engine.Scene.NativeLayerZeroScrollCurrent is { X: 200, Y: -4 }, "The current interpolation endpoint was not published back to the process owner.");
        Console.WriteLine("Native scroll current: genuine cold zero, real normal callback publication, cross-field retention, first-callback LSCROLLA start and independent restoration passed.");
    }
}
