using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDialogueOpeningInputContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeInputConfiguration configuration = assets.ReadNewGameInputConfiguration();
        EventEngine Create(bool persistent = false, bool mixed = false, string first = "First")
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0)];
            if (persistent)
            {
                main.AddRange([(Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESSYNC, 0)]);
            }
            else
            {
                main.Add((Jsm.Opcode.MESW, 0));
            }
            main.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            List<(Jsm.Opcode, int)[]> scripts = [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], main.ToArray()];
            ushort[] groups = [1];
            if (mixed)
            {
                groups = [1, (8 << 7) | 1];
                scripts.AddRange([[(Jsm.Opcode.LBL, 8), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MESW, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            }
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(configuration);
            EventEngine engine = new(session);
            engine.LoadField(600, SyntheticObjects(groups, scripts), [first, "Second"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        foreach (bool persistent in new[] { false, true })
        {
            EventEngine live = Create(persistent);
            for (int callback = 0; callback < 4 && live.CurrentDialogue is not { CanConfirm: true }; callback++)
            {
                live.Step();
            }
            Require(live.CurrentDialogue?.NativeWindow is { Progress: < 4096, Delta: 512 }, "Opening-input fixture did not reach a confirmable native message before full opening.");
            EventEngine copy = Create(persistent);
            copy.Restore(live.Capture());
            foreach (PlayerActionKind kind in new[] { PlayerActionKind.Confirm, PlayerActionKind.Cancel })
            {
                live.Apply(new PlayerAction(kind));
                copy.Apply(new PlayerAction(kind));
                Require(live.DialogueOutcomes.Count == 0 && live.CurrentDialogue is { CanConfirm: true, NativeWindow.Delta: 512 } && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Early semantic input confirmed or closed an opening native message.");
            }
            live.Apply(new PlayerAction(PlayerActionKind.FieldButtons, configuration.RawForMappedPulse(0x40)));
            copy.Apply(new PlayerAction(PlayerActionKind.FieldButtons, configuration.RawForMappedPulse(0x40)));
            live.Step();
            copy.Step();
            Require(live.DialogueOutcomes.Count == 0 && live.CurrentDialogue is { CanConfirm: true, NativeWindow.Delta: 512 } && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Raw confirmation bypassed native window opening or changed restored ownership.");
            live.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0));
            copy.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0));
            while (live.Dialogues.Single().NativeWindow is { Progress: < 4096 } or { Text.Ready: false })
            {
                live.Step();
                copy.Step();
            }
            live.Apply(new PlayerAction(PlayerActionKind.Cancel));
            copy.Apply(new PlayerAction(PlayerActionKind.Cancel));
            Require(live.DialogueOutcomes.Count == 1 && live.CurrentDialogue is not { CanConfirm: true }, "Fully opened native message did not accept its ordinary cancellation.");
            for (int callback = 0; callback < 8 && live.Dialogues.Count != 0; callback++)
            {
                live.Step();
                copy.Step();
                Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Confirmed message opening/closing ownership diverged after restore.");
            }
            Require(live.Dialogues.Count == 0, "Confirmed native message retained a closed channel.");
        }
        EventEngine mixed = Create(mixed: true);
        while (mixed.Dialogues.Count < 2 || mixed.Dialogues.Single(dialogue => dialogue.Channel == 0).NativeWindow is { Progress: < 4096 } or { Text.Ready: false })
        {
            mixed.Step();
        }
        DialogueState newer = mixed.Dialogues.Single(dialogue => dialogue.Channel == 1);
        Require(newer.NativeWindow is { Progress: < 4096 } or { Text.Ready: false }, "Mixed opening fixture lost its independently opening second channel.");
        EventEngine restored = Create(mixed: true);
        restored.Restore(mixed.Capture());
        mixed.Apply(new PlayerAction(PlayerActionKind.Confirm));
        restored.Apply(new PlayerAction(PlayerActionKind.Confirm));
        mixed.Step();
        restored.Step();
        Require(mixed.DialogueOutcomes.Count == 1 && !mixed.Dialogues.Single(dialogue => dialogue.Channel == 0).CanConfirm && mixed.Dialogues.Single(dialogue => dialogue.Channel == 1).CanConfirm && JsonElement.DeepEquals(mixed.Capture(), restored.Capture()), "Broadcast confirmation closed an opening sibling or failed to confirm its fully opened peer.");

        EventEngine cadence = Create(first: "AB{Line}C{End}");
        while (cadence.Dialogues.Count == 0 || cadence.Dialogues.Single().NativeWindow!.Progress < 4096)
        {
            cadence.Step();
        }
        NativeDialogueTextProgress reveal = cadence.Dialogues.Single().NativeWindow!.Text!;
        Require(reveal is { Glyphs: "ABC", Position: 1, Ready: false }, "The UI sample reaching full opening did not reveal exactly its first native glyph.");
        GameplaySaveFieldScripts semantic = cadence.CaptureGameplaySaveScripts();
        Require(semantic.Dialogues?.Windows.Single().Window?.Text == reveal, "Semantic dialogue capture discarded the current glyph cursor or END ownership.");
        EventEngine semanticCopy = Create(first: "AB{Line}C{End}");
        semanticCopy.RestoreGameplaySaveScripts(JsonSerializer.Deserialize<GameplaySaveFieldScripts>(JsonSerializer.Serialize(semantic))!);
        Require(semanticCopy.Dialogues.Single().NativeWindow!.Text == reveal, "Semantic dialogue restore changed the pending reveal cursor.");
        JsonNode legacy = JsonNode.Parse(cadence.Capture().GetRawText())!;
        legacy["Dialogues"]![0]!["NativeWindow"]!.AsObject().Remove("Text");
        EventEngine legacyCopy = Create(first: "AB{Line}C{End}");
        legacyCopy.Restore(JsonSerializer.SerializeToElement(legacy));
        legacyCopy.Apply(new PlayerAction(PlayerActionKind.Confirm));
        Require(legacyCopy.DialogueOutcomes.Count == 1, "An older save missing optional reveal state did not retain its already-revealed compatibility default.");
        EventEngine cadenceCopy = Create(first: "AB{Line}C{End}");
        cadenceCopy.Restore(cadence.Capture());
        cadence.Step();
        cadenceCopy.Step();
        Require(reveal is { Position: 3, Ready: false } && JsonElement.DeepEquals(cadence.Capture(), cadenceCopy.Capture()), "Two native UI samples did not reveal two glyphs while preserving the pending END.");
        cadence.Apply(new PlayerAction(PlayerActionKind.Confirm));
        cadenceCopy.Apply(new PlayerAction(PlayerActionKind.Confirm));
        Require(cadence.DialogueOutcomes.Count == 0, "The final visible glyph enabled confirmation before the budgeted native END.");
        cadence.Step();
        cadenceCopy.Step();
        Require(reveal.Ready && cadence.DialogueOutcomes.Count == 0 && JsonElement.DeepEquals(cadence.Capture(), cadenceCopy.Capture()), "END readiness or an early queued confirm changed after restore.");
        cadence.Apply(new PlayerAction(PlayerActionKind.Confirm));
        Require(cadence.DialogueOutcomes.Count == 1, "A native-ready message refused confirmation.");

        EventEngine named = Create(first: "{Char Squall}{Line}AB{End}");
        named.Scene.Names[0] = "Cecilia";
        named.Step();
        Require(named.Dialogues.Single().NativeWindow!.Text?.Glyphs == "CeciliaAB", "Native reveal counted a name directive instead of its current expanded character name.");
        named.Scene.MessageChannelTiming[0] = new(0, 0);
        while (named.Dialogues.Single().NativeWindow!.Progress < 4096)
        {
            named.Step();
        }
        Require(named.Dialogues.Single().NativeWindow!.Text is { Position: 9, Ready: true }, "Native speed zero did not reveal the complete text and END without an extra callback delay.");

        EventEngine fractional = Create(first: "ABCDE{End}");
        while (fractional.Dialogues.Count == 0 || fractional.Dialogues.Single().NativeWindow!.Progress < 4096)
        {
            fractional.Step();
        }
        fractional.Scene.MessageChannelTiming[0] = new(2048, 0);
        fractional.Step();
        Require(fractional.Dialogues.Single().NativeWindow!.Text is { Position: 2, Ready: false } && fractional.Scene.MessageChannelTiming[0].Counter20 == 0, "Native half-speed phase did not spend exactly one glyph across two UI samples.");
        fractional.Scene.MessageChannelTiming[0] = new(ushort.MaxValue, 0);
        fractional.Step();
        Require(fractional.Dialogues.Single().NativeWindow!.Text is { Position: 2, Ready: false } && fractional.Scene.MessageChannelTiming[0].Counter20 == 65534, "Native signed WORD speed or wrapped phase was replaced with an unsigned elapsed timer.");
        Console.WriteLine("Native dialogue opening input: early Confirm/Cancel/raw rejection, MESSYNC, independently opening channels and restored ownership PASS.");
    }
}
