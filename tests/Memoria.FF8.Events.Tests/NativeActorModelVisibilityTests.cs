using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorModelVisibilityContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode visibility in new[] { Jsm.Opcode.SHOW, Jsm.Opcode.HIDE })
        {
            byte[] program = SyntheticObjects([1],
            [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (visibility, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
            ]);
            EventEngine Create()
            {
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(vitals);
                session.InitializeNewGameEconomy(economy);
                EventEngine engine = new(session) { CanDisableAdditionalControl = _ => true, AnimationFrameCount = (_, _) => 4 };
                engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }

            EventEngine live = Create();
            live.Step();
            live.Step();
            Require(live.Actors[0].AdditionalControlDisabled && live.Actors[0].ResolvedModelAdditionalControlDisabled, "ACTORMODE3 did not set both retained actor intent and effective model suppression.");
            live.Step();
            ActorState actor = live.Actors[0];
            Require(actor.AdditionalControlDisabled && !actor.ResolvedModelAdditionalControlDisabled, $"{visibility} failed to clear the effective model flag independently of retained ACTORMODE3 intent.");
            Require(actor.Visible == (visibility == Jsm.Opcode.SHOW), $"{visibility} failed to retain its ordinary visibility behavior.");

            JsonElement saved = live.Capture();
            EventEngine restored = Create();
            restored.Restore(saved);
            Require(restored.Actors[0].AdditionalControlDisabled && !restored.Actors[0].ResolvedModelAdditionalControlDisabled, "Snapshot restore replaced the effective SHOW/HIDE model flag with retained actor intent.");
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Independent snapshot restoration changed the split actor/model visibility state.");

            JsonNode oldSnapshot = JsonNode.Parse(saved.GetRawText())!;
            oldSnapshot["Actors"]![0]!.AsObject().Remove("NativeModelAdditionalControlDisabled");
            EventEngine oldRestore = Create();
            oldRestore.Restore(JsonSerializer.SerializeToElement(oldSnapshot));
            Require(oldRestore.Actors[0].AdditionalControlDisabled && oldRestore.Actors[0].Visible == actor.Visible && oldRestore.Frame == live.Frame, "An older snapshot missing the optional effective model flag lost retained actor facts or callback progress.");
            Require(JsonElement.DeepEquals(saved.GetProperty("Fibers"), oldRestore.Capture().GetProperty("Fibers")), "Restoring an older optional presentation field replayed or changed script PCs.");

            live.Step();
            restored.Step();
            Require(live.Actors[0].AdditionalControlDisabled && live.Actors[0].ResolvedModelAdditionalControlDisabled, "A later ACTORMODE3 failed to reapply effective suppression after SHOW/HIDE.");
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "SHOW/HIDE model state changed subsequent ACTORMODE3 behavior after independent restoration.");
            live.Step();
            restored.Step();
            Require(!live.Actors[0].AdditionalControlDisabled && !live.Actors[0].ResolvedModelAdditionalControlDisabled, "ACTORMODE2 failed to clear both retained and effective suppression.");
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "SHOW/HIDE model state changed subsequent ACTORMODE2 behavior after independent restoration.");
        }
    }
}
