using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void SemanticSpatialReplayContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (EventEngine Engine, GameSession Session) Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0
            };
            (Jsm.Opcode, int)[] setup = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] current = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, -456), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.FACEDIRA, 0), (Jsm.Opcode.PSHN_L, 37), (Jsm.Opcode.POPM_B, 5005), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            LoadSemanticFixtureField(engine, session, SyntheticObjects([1], [setup, current]));
            return (engine, session);
        }

        var semantic = Create();
        float direction = semantic.Engine.Actors[0].Direction;
        Require(!semantic.Engine.IsSemanticSpatialReplay, "Spatial replay was enabled for ordinary field loading.");
        using (semantic.Engine.BeginSemanticSpatialReplay())
        {
            Require(semantic.Engine.IsSemanticSpatialReplay, "Completed-visit scope was not enabled.");
            for (int callback = 0; callback < 4 && !semantic.Session.State.Variables.ContainsKey(5005); callback++)
            {
                semantic.Engine.Step();
            }
            ActorState leader = semantic.Engine.Actors[0];
            Require(semantic.Session.State.Variables.GetValueOrDefault(5005) == 37 && leader.X == 123 && leader.Y == -456 && leader.Z == 7 && semantic.Engine.Scene.NativePlayerPosition == new FieldFixedPosition(123 << 12, -456 << 12, 7 << 12), "Current MOVE/JOIN/facing did not release the current script with a coherent current computed leader placement.");
            Require(leader.Direction == direction && leader.NativeScriptMovement is null && leader.NativeJump is null && leader.RotationTarget is null, "Spatial replay installed a physical movement owner or changed ignored facing.");
        }
        Require(!semantic.Engine.IsSemanticSpatialReplay, "Completed-visit scope leaked into live play.");
        var ordinary = Create();
        bool refused = false;
        try
        {
            ordinary.Engine.Step();
        }
        catch (EventScriptException exception) when (exception.Message.Contains("MOVE", StringComparison.Ordinal))
        {
            refused = true;
        }
        Require(refused && !ordinary.Session.State.Variables.ContainsKey(5005) && ordinary.Engine.Actors[0].X == 0, "Ordinary live MOVE bypassed its original physical contract after a separate replay scope.");
        Console.WriteLine("Semantic spatial policy: current computed MOVE, JOIN and absent-target facing do not block current script; raw leader owner coherent; ordinary live contract isolated PASS.");
    }
}
