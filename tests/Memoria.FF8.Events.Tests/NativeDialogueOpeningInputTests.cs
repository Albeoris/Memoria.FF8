using System.Text.Json;
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
        EventEngine Create(bool persistent = false, bool mixed = false)
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
            engine.LoadField(600, SyntheticObjects(groups, scripts), ["First", "Second"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
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
            while (live.Dialogues.Single().NativeWindow!.Progress < 4096)
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
        while (mixed.Dialogues.Count < 2 || mixed.Dialogues.Single(dialogue => dialogue.Channel == 0).NativeWindow!.Progress < 4096)
        {
            mixed.Step();
        }
        DialogueState newer = mixed.Dialogues.Single(dialogue => dialogue.Channel == 1);
        Require(newer.NativeWindow is { Progress: < 4096 }, "Mixed opening fixture lost its independently opening second channel.");
        EventEngine restored = Create(mixed: true);
        restored.Restore(mixed.Capture());
        mixed.Apply(new PlayerAction(PlayerActionKind.Confirm));
        restored.Apply(new PlayerAction(PlayerActionKind.Confirm));
        mixed.Step();
        restored.Step();
        Require(mixed.DialogueOutcomes.Count == 1 && !mixed.Dialogues.Single(dialogue => dialogue.Channel == 0).CanConfirm && mixed.Dialogues.Single(dialogue => dialogue.Channel == 1).CanConfirm && JsonElement.DeepEquals(mixed.Capture(), restored.Capture()), "Broadcast confirmation closed an opening sibling or failed to confirm its fully opened peer.");
        Console.WriteLine("Native dialogue opening input: early Confirm/Cancel/raw rejection, MESSYNC, independently opening channels and restored ownership PASS.");
    }
}
