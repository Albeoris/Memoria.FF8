using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDrawPointNoEligibleContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeDrawPointCatalog catalog = assets.ReadNativeDrawPointCatalog();
        Require(catalog.PointBytes[13] == 6 && catalog.NoEligibleMessage.Contains("No one can draw", StringComparison.Ordinal), "Authenticated907 point14/commonmessage7 differs from its original resource golden.");
        foreach ((int raw, int depletion) in new[] { (14, 0), (270, 0), (-242, 0), (14, 1), (14, 2), (14, 3) })
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, depletion << 2), (Jsm.Opcode.POPM_B, 119), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.DRAWPOINT, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            (GameSession Session, EventEngine Engine) Create(bool provider = true)
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session)
                {
                    ReadNativeDrawPointCatalog = provider ? assets.ReadNativeDrawPointCatalog : null
                };
                engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return (session, engine);
            }

            (GameSession session, EventEngine engine) = Create();
            NativeCharacterVitals originalVitals = session.CharacterVitals!;
            engine.Step();
            NativeDrawPointMemory bank = session.EventRandom!.DrawPoints!;
            engine.Step();
            Require(engine.CurrentDialogue is { Channel: 6, MessageId: -1, CanConfirm: true, FirstChoice: null } message && message.Text == catalog.NoEligibleMessage && !engine.IsUserControlEnabled, "No-eligible DRAWPOINT bypassed its actual common confirmation or restored control early.");
            Require(engine.Scene.DrawPointInteraction is { PointIndex: 13, PointByte: 6, RandomStateBefore: 1, RandomStateAfter: 1103527590 } issued && issued.Quantity == (depletion == 1 ? 4 : 7) && issued.DepletionAtIssue == depletion && engine.Actors[0].NativeMovementAccelerationTarget == 1 && session.EventRandom!.State == 1103527590, "DRAWPOINT lost its lowBYTE index, unsigned RNG sample, depletion-sensitive quantity or sole shared516 phase.");
            Require(session.EventRandom!.DrawPoints == bank && session.CharacterVitals!.Records.Select(row => row.MagicInventory).Zip(originalVitals.Records.Select(row => row.MagicInventory)).All(pair => pair.First.SequenceEqual(pair.Second)) && !session.State.Variables.ContainsKey(500), "No-eligible DRAWPOINT awarded magic, depleted the point or executed its successor before confirmation.");
            JsonElement pending = engine.Capture();
            JsonElement held = pending.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(held.GetProperty("Position").GetInt32() == 8 && held.GetProperty("Stack")[0].GetInt32() == raw, "Held DRAWPOINT popped its original fullDWORD operand or advanced PC.");
            (GameSession restoredSession, EventEngine restored) = Create();
            restored.Restore(pending);
            Require(JsonElement.DeepEquals(pending, restored.Capture()), "Pending DRAWPOINT did not restore independently.");
            foreach (string field in new[] { "Quantity", "RandomStateAfter", "PointByte", "OwnerSequence" })
            {
                JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
                malformed["Scene"]!["DrawPointInteraction"]![field] = 999;
                try
                {
                    restored.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed draw-point request was accepted.");
                }
                catch (Exception exception) when (exception is InvalidDataException or JsonException)
                {
                }
                Require(JsonElement.DeepEquals(pending, restored.Capture()), "Malformed draw-point restore mutated the VM before refusal.");
            }
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()) && session.EventRandom!.State == 1103527590 && session.EventRandom.DrawPoints == bank, "Held no-eligible message redrew RNG or changed depletion.");
            Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted, "Ordinary draw-point message confirmation was refused.");
            Require(engine.Scene.DrawPointInteraction is not null && !session.State.Variables.ContainsKey(500), "Zero-callback confirmation advanced the field VM outside its real callback.");
            engine.Step();
            restored.Step();
            Require(engine.Scene.DrawPointInteraction is null && engine.CurrentDialogue is null && engine.IsUserControlEnabled && session.State.Variables[500] == 77 && engine.Actors[0].NativeMovementAccelerationTarget == 1, "Closing DRAWPOINT did not return2/pop once and run UCON/successor in the same callback.");
            JsonElement completed = engine.Capture();
            JsonElement results = completed.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
            Require(results.GetProperty("0").GetInt32() == 0 && results.GetProperty("3").GetInt32() == 0 && session.EventRandom!.DrawPoints == bank && restoredSession.EventRandom!.DrawPoints == bank && JsonElement.DeepEquals(completed, restored.Capture()), "No-eligible native result, saved owners or restored confirmation differed.");

            (_, EventEngine unknown) = Create(provider: false);
            unknown.Step();
            try
            {
                unknown.Step();
                throw new InvalidOperationException("DRAWPOINT silently replaced its missing original provider.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement refused = unknown.Capture();
            JsonElement refusedOwner = refused.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(unknown.LastFault?.Opcode == "DRAWPOINT" && unknown.Scene.DrawPointInteraction is null && unknown.CurrentDialogue is null && refusedOwner.GetProperty("Position").GetInt32() == 8 && refusedOwner.GetProperty("Stack")[0].GetInt32() == raw && refused.GetProperty("EventRandom").GetProperty("State").GetUInt32() == 1, "Missing draw-point provider changed PC/operand/RNG before refusal.");
        }
        Console.WriteLine("Native no-eligible DRAWPOINT: original point/common7, one RNG sample, BYTE wrap, held full operand/shared516, ordinary confirmation/R0=0, no award/depletion, independent restore and missing-provider refusal passed.");
    }
}
