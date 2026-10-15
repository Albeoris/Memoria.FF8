using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void SemanticSalaryContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (GameSession Session, EventEngine Engine) Create(int additionalRank)
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            Require(session.ReplayJournal is { IsComplete: true }, $"Semantic fixture initializer invalidated its journal: {session.ReplayJournal?.UnavailableReason}.");
            (Jsm.Opcode, int)[] initialization = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.SARALYON, 0), (Jsm.Opcode.SARALYDISPOFF, 0), (Jsm.Opcode.PSHN_L, additionalRank), (Jsm.Opcode.ADDSEEDLEVEL, 0), (Jsm.Opcode.PSHN_L, 0x5FFF), (Jsm.Opcode.POPM_L, 8), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            EventEngine engine = new(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0
            };
            LoadSemanticFixtureField(engine, session, SyntheticObjects([1], [initialization, main]));
            Require(session.SeeD!.SalaryMovementCounter == 0x5FFF, "Declared salary producer did not execute its canonical counter write.");
            return (session, engine);
        }

        uint[] payments = new uint[2];
        int index = 0;
        foreach (int rank in new[] { 150, 350 })
        {
            var current = Create(rank);
            uint beforeGil = current.Session.Economy!.Gil;
            ushort beforeRank = current.Session.SeeD!.RankPoints;
            long frame = current.Engine.Frame;
            float x = current.Engine.Actors[0].X;
            float y = current.Engine.Actors[0].Y;
            List<ReplayReceipt> receipts = [];
            current.Engine.CausalReceiptRecorded += receipts.Add;
            ReplayJournalEntry cause = new(ReplayEntryKind.PersistentEffect, SceneKind.Field, 600, 12345) { Code = (int)ReplayPersistentEffectKind.SalaryPayment, EffectArguments = [5, 1] };
            Require(current.Engine.ApplySemanticEffect(cause).Accepted && current.Engine.Frame == frame + 1 && current.Engine.Actors[0].X == x && current.Engine.Actors[0].Y == y, "Semantic salary phase skipped its actual callback or injected a physical approach.");
            payments[index++] = current.Session.Economy!.Gil - beforeGil;
            Require(current.Session.SeeD!.RankPoints == beforeRank - 10 && current.Session.SeeD!.SalaryMovementCounter == 0 && current.Engine.Scene.NativeSalaryMovementUnits == 0 && receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.PersistentEffect && receipt.Code == (int)ReplayPersistentEffectKind.SalaryPayment && receipt.Arguments!.SequenceEqual([5, 1])), "Semantic salary did not use the current native saved owners and retain its actual cause.");
            Require(current.Session.ReplayJournal!.Endpoint is { Kind: ReplayEntryKind.PersistentEffect } entry && entry.EffectArguments!.SequenceEqual([5, 1]), "Semantic salary input was not recorded independently from its recomputed output.");
            ReplayJournalEntry noPayment = cause with { EffectArguments = [0, 1] };
            uint paidGil = current.Session.Economy!.Gil;
            Require(current.Engine.ApplySemanticEffect(noPayment).Accepted && current.Session.Economy!.Gil == paidGil && current.Session.ReplayJournal!.Endpoint!.EffectArguments!.SequenceEqual([0, 1]), "A retained payment intent fabricated another payment or lost its cause after a current no-op.");
            long beforeBatch = current.Engine.Frame;
            Require(current.Engine.ApplySemanticEffect(noPayment with { Frames = 3 }).Accepted && current.Engine.Frame == beforeBatch + 3 && current.Session.ReplayJournal!.Endpoint is { Frames: 3 } batch && batch.EffectArguments!.SequenceEqual([0, 1]), "Compacted salary cause multiplicity did not execute and record exactly three current callbacks.");
            JsonElement beforeRefusal = current.Engine.Capture();
            try
            {
                current.Engine.ApplySemanticEffect(cause with { EffectArguments = [4, 1] });
                throw new InvalidOperationException("Semantic salary accepted an unsupported movement input.");
            }
            catch (NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(beforeRefusal, current.Engine.Capture()), "Rejected semantic salary input mutated the current VM.");
        }
        Require(payments.SequenceEqual(new uint[] { 4000, 6000 }), "Semantic salary copied an old amount instead of recomputing the current rank and native table.");
        Console.WriteLine("Semantic salary: executed current initialization, current native kernel yields different current payments from one retained cause, actual callbacks without pose writes, shared causal event, no-op input retention and atomic invalid-unit refusal PASS.");
    }
}
