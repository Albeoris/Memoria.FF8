using System.Text.Json;
using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void SemanticAdmissionContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (EventEngine Engine, GameSession Session) Create(int distance, bool disableGateway = false)
        {
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
            if (disableGateway)
            {
                player = [.. player.Take(player.Length - 1), (Jsm.Opcode.MAPJUMPOFF, 0), player[^1]];
            }
            (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, distance), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.TALKRADIUS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] targetIdle = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] talk = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] push = [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 8)];
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            Require(session.ReplayJournal is { IsComplete: true }, $"Semantic fixture initializer invalidated its journal: {session.ReplayJournal?.UnavailableReason}.");
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0
            };
            LoadSemanticFixtureField(engine, session, SyntheticObjects([1, (16 << 7) | 3], [player, idle, target, targetIdle, talk, push]));
            Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeFieldLoadVersion == 1, "Semantic fixture did not execute the original New Game party loader.");
            return (engine, session);
        }

        var near = Create(100);
        List<ReplaySemanticTrigger> physical = [];
        near.Engine.SemanticTriggerAdmitted += physical.Add;
        Require(near.Engine.Apply(new PlayerAction(PlayerActionKind.Interact, 1)).Accepted, "Nearby ordinary actor interaction was not admitted.");
        Require(physical is [{ Kind: ReplaySemanticTriggerKind.ActorInteraction, NativeObjectId: 1, ObjectId: 1, ScriptId: 18, NativeSlot: 7, LocalScript: 2, Inferred: false }], "Ordinary talk did not capture its actual native producer without inference.");
        Require(near.Session.ReplayJournal!.Endpoint!.SemanticAdmissions.SequenceEqual(physical), "Ordinary admission did not enter its game-owned input transaction.");
        near.Engine.Step();
        Require(near.Session.State.Variables.GetValueOrDefault(5000) == 7 && near.Session.State.Variables.GetValueOrDefault(5002) == 1, "Ordinary request did not execute the actual target script and current KEYON edge.");
        Require(near.Session.ReplayJournal!.Endpoint!.Receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.InputSample && receipt.ObjectId == 1 && receipt.ScriptId == 18 && receipt.Code == (int)Jsm.Opcode.KEYON), "Current native KEYON consumption did not retain its authenticated input sample site.");

        var far = Create(10000);
        JsonElement beforeCandidates = far.Engine.Capture();
        ReplaySemanticTrigger candidate = far.Engine.GetSemanticTriggerCandidates().Single(trigger => trigger.Kind == ReplaySemanticTriggerKind.ActorInteraction && trigger.ObjectId == 1);
        Require(candidate.Inferred && JsonElement.DeepEquals(beforeCandidates, far.Engine.Capture()) && far.Engine.SelectInteractionTarget() is null, "Current producer discovery mutated the VM or fabricated physical eligibility.");
        Require(!far.Engine.Apply(new PlayerAction(PlayerActionKind.Interact, 1)).Accepted, "Distant ordinary interaction bypassed the native geometry selector.");
        float x = far.Engine.Actors[0].X;
        float y = far.Engine.Actors[0].Y;
        long frame = far.Engine.Frame;
        List<ReplaySemanticTrigger> semantic = [];
        far.Engine.SemanticTriggerAdmitted += semantic.Add;
        Require(far.Engine.ApplySemanticTrigger(candidate).Accepted && far.Engine.Frame == frame && far.Engine.Actors[0].X == x && far.Engine.Actors[0].Y == y && !far.Session.State.Variables.ContainsKey(5000), "Historical event input moved the player, advanced time or executed a script outside its scheduler.");
        Require(semantic is [{ Inferred: true, ScriptId: 18, NativeSlot: 7 }] && far.Session.ReplayJournal!.Endpoint is { Kind: ReplayEntryKind.SemanticTrigger, SemanticTrigger.Inferred: true } semanticEntry && semanticEntry.SemanticAdmissions.SequenceEqual(semantic), "Inferred historical input lost its provenance or fresh admission journal.");
        far.Engine.Step();
        Require(far.Session.State.Variables.GetValueOrDefault(5000) == 7 && far.Session.State.Variables.GetValueOrDefault(5002) == 1 && far.Engine.Actors[0].X == x && far.Engine.Actors[0].Y == y, "Historical event did not execute the same current VM/request/input path without a position patch.");
        ReplaySemanticTrigger pushCandidate = far.Engine.GetSemanticTriggerCandidates().Single(trigger => trigger.Kind == ReplaySemanticTriggerKind.PushContact && trigger.ObjectId == 1);
        Require(far.Engine.ApplySemanticTrigger(pushCandidate).Accepted, "Historical push did not use its native slot6 admission.");
        far.Engine.Step();
        Require(far.Session.State.Variables.GetValueOrDefault(5001) == 9, "Historical push did not execute current local3 through its native scheduler.");

        JsonElement beforeRefusal = far.Engine.Capture();
        try
        {
            far.Engine.ApplySemanticTrigger(candidate with { NativeObjectId = 0 });
            throw new InvalidOperationException("Semantic input admitted a different original native object.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(beforeRefusal, far.Engine.Capture()) && far.Session.ReplayJournal!.IsComplete, "Rejected semantic producer changed VM state or invalidated its authentic journal.");

        var gateway = Create(10000);
        FieldTransition destination = new(175, 123, 456, 9, 64);
        gateway.Engine.InfGateways = [new InfGateway(Vector3.Zero, new Vector3(0, 10, 0), destination, NativeIndex: 11)];
        ReplaySemanticTrigger exit = gateway.Engine.GetSemanticExitCandidates(SceneKind.Field, 175).Single();
        Require(exit is { Inferred: true, InfIndex: 11 } && gateway.Engine.ApplySemanticTrigger(exit).Accepted && gateway.Session.State.FieldArrival == destination && gateway.Engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "Historical gateway confused filtered list ordinal with original INF index or failed to consume the current packet.");
        Require(gateway.Session.ReplayJournal!.Endpoint!.SemanticAdmissions is [{ InfIndex: 11 }], "Current gateway admission lost its native INF identity.");
        var disabledGateway = Create(10000, disableGateway: true);
        disabledGateway.Engine.InfGateways = [new InfGateway(Vector3.Zero, new Vector3(0, 10, 0), destination, NativeIndex: 11)];
        JsonElement beforeDisabledQuery = disabledGateway.Engine.Capture();
        Require(disabledGateway.Engine.Scene.MapJumpDisabled && disabledGateway.Engine.GetSemanticExitCandidates(SceneKind.Field, 175).Count == 0 && JsonElement.DeepEquals(beforeDisabledQuery, disabledGateway.Engine.Capture()), "A native MAPJUMPOFF gateway remained eligible as a semantic exit, or pure filtering mutated the VM.");
        Console.WriteLine("Semantic admissions: ordinary authenticated talk, pure inferred producer discovery, geometry-preserving ordinary refusal, historical talk/push through current native scheduler, current KEYON sample, fresh provenance journal and atomic identity refusal PASS.");
    }

    private static void LoadSemanticFixtureField(EventEngine engine, GameSession session, byte[] content)
    {
        (Jsm.Opcode, int)[] boot = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] depart = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.MAPJUMP, 0), (Jsm.Opcode.RET, 8)];
        engine.LoadField(74, SyntheticObjects([1], [boot, depart]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        FieldTransition arrival = session.State.FieldArrival ?? throw new InvalidOperationException("Semantic fixture bootstrap did not execute its actual MAPJUMP.");
        Require(arrival.FieldId == 600 && engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "Semantic fixture lost its native bootstrap departure.");
        engine.LoadField(600, content, [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
    }
}
