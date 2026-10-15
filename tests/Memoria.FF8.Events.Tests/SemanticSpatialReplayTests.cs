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
        (EventEngine Engine, GameSession Session) Create(int? observerStore = null)
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
            if (observerStore is int local)
            {
                current = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 112), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, local), (Jsm.Opcode.JMP, -8), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.POPM_B, 84), (Jsm.Opcode.RET, 8)];
            }
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
        var observer = Create(observerStore: 1024);
        System.Text.Json.JsonElement observedBefore = observer.Engine.Capture();
        var pending = observedBefore.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean() && !fiber.GetProperty("Complete").GetBoolean());
        ReplayScriptGoal observerGoal = observer.Engine.GetSemanticProducerGoal(0, 1, pending.GetProperty("Position").GetInt32() + 7);
        Require(observer.Engine.HasSemanticProducerWork(observerGoal), "Ordinary live spatial polling stopped being current work.");
        using (observer.Engine.BeginSemanticSpatialReplay())
        {
            Require(!observer.Engine.HasSemanticProducerWork(observerGoal) && System.Text.Json.JsonElement.DeepEquals(observedBefore, observer.Engine.Capture()), "Completed-visit observer did not yield purely, or changed its current VM/position/local latch.");
            ReplayScriptGoal futureStory = observer.Engine.GetSemanticProducerGoal(0, 1, pending.GetProperty("Position").GetInt32() + 10);
            Require(observer.Engine.HasSemanticProducerWork(futureStory), "A persistent story goal after a local spatial cycle was incorrectly yielded.");
        }
        Require(observer.Engine.HasSemanticProducerWork(observerGoal), "Spatial observer yield leaked outside the completed-visit scope.");
        var persistentLoop = Create(observerStore: 84);
        var persistentBefore = persistentLoop.Engine.Capture();
        var persistentPending = persistentBefore.GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean() && !fiber.GetProperty("Complete").GetBoolean());
        ReplayScriptGoal persistentGoal = persistentLoop.Engine.GetSemanticProducerGoal(0, 1, persistentPending.GetProperty("Position").GetInt32() + 7);
        using (persistentLoop.Engine.BeginSemanticSpatialReplay())
        {
            Require(persistentLoop.Engine.HasSemanticProducerWork(persistentGoal), "A spatial loop containing a persistent story write was incorrectly yielded.");
        }
        Console.WriteLine("Semantic spatial policy: current computed MOVE, JOIN and absent-target facing do not block current script; raw leader owner coherent; ordinary live contract isolated PASS.");
    }
}
