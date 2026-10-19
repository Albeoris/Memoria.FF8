using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSharedChoiceFocusContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.ASK, Jsm.Opcode.AASK })
        {
            (Jsm.Opcode, int)[] Choice(int label, int channel, bool delay)
            {
                List<(Jsm.Opcode, int)> code = [(Jsm.Opcode.LBL, label)];
                if (delay)
                {
                    code.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0)]);
                }
                else
                {
                    code.AddRange([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.MESFORCUS, 0)]);
                }
                code.AddRange([(Jsm.Opcode.PSHN_L, channel), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1)]);
                if (opcode == Jsm.Opcode.AASK)
                {
                    code.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3)]);
                }
                code.AddRange([(opcode, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
                return code.ToArray();
            }
            byte[] program = SyntheticObjects([1, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], Choice(1, 0, false), [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)], Choice(17, 1, true)]);
            EventEngine Create()
            {
                EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy()));
                engine.LoadField(100, program, ["Choose"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                engine.Step();
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            Require(live.Scene.NativeDialogueFocus == 7 && live.CurrentDialogue is null && live.Dialogues.Count == 1, "Choice issue changed native focus before its first incomplete poll.");
            live.Step();
            Require(live.Scene.NativeDialogueFocus == 0 && live.Dialogues.Count == 2 && (opcode == Jsm.Opcode.ASK ? live.Scene.NativeAskPreviousFocus : live.Scene.NativeAaskPreviousFocus) == 0, "Second same-opcode choice failed to overwrite the native shared prior-focus global after the first poll.");
            EventEngine copy = Create();
            copy.Restore(live.Capture());
            foreach (string property in new[] { "NativeDialogueFocus", opcode == Jsm.Opcode.ASK ? "NativeAskPreviousFocus" : "NativeAaskPreviousFocus" })
            {
                JsonElement before = copy.Capture();
                JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
                invalid["Scene"]!.AsObject().Remove(property);
                try
                {
                    copy.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException("Active native choice accepted missing authoritative focus history.");
                }
                catch (InvalidDataException)
                {
                    Require(JsonElement.DeepEquals(before, copy.Capture()), "Refused choice-focus restore changed live state.");
                }
            }
            live.Step();
            copy.Step();
            Require(live.Scene.NativeDialogueFocus == 1 && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Actor-ordered choice polls did not reassert their own channel after independent restore.");
            live.ConfirmDialogue(1);
            copy.ConfirmDialogue(1);
            Require(live.Scene.NativeDialogueFocus == 1, "Choice selection restored focus before its completing VM poll.");
            live.Step();
            copy.Step();
            Require(live.Scene.NativeDialogueFocus == 0 && live.CurrentDialogue?.Channel == 0 && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Completing second choice failed to restore the actual overwritten global marker.");
            live.ConfirmDialogue(0);
            copy.ConfirmDialogue(0);
            Require(copy.Dialogues.Count == 0, "Closing-choice fixture unexpectedly retained a visible window.");
            JsonElement closing = copy.Capture();
            JsonNode missingPrior = JsonNode.Parse(closing.GetRawText())!;
            missingPrior["Scene"]!.AsObject().Remove(opcode == Jsm.Opcode.ASK ? "NativeAskPreviousFocus" : "NativeAaskPreviousFocus");
            try
            {
                copy.Restore(JsonSerializer.SerializeToElement(missingPrior));
                throw new InvalidOperationException("Closing native choice consumer accepted missing authoritative global focus history.");
            }
            catch (InvalidDataException)
            {
                Require(JsonElement.DeepEquals(closing, copy.Capture()), "Refused closed-choice focus restore changed live state.");
            }
            live.Step();
            copy.Step();
            Require(live.Scene.NativeDialogueFocus == 0 && live.CurrentDialogue is null && JsonElement.DeepEquals(live.Capture(), copy.Capture()), "First choice restored a per-window historical marker instead of the shared native global.");
        }
        Console.WriteLine("ASK/AASK focus: unchanged issue marker, actor-ordered poll focus, distinct opcode globals, overlap overwrite, VM completion restoration and independent restore passed.");
    }
}
