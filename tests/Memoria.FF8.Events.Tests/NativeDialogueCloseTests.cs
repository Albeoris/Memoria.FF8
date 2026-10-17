using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDialogueCloseContract()
    {
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.AMES, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WINCLOSE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.SetWorldControls(NativeWorldControlState.CreateNewGame());
            session.SetWorldEntryMemory(NativeWorldEntryMemory.CreateNewGame());
            session.SetEventRandom(session.EventRandom!.ClearSavedStateForNewGame());
            EventEngine engine = new(session);
            engine.LoadField(600, content, ["Window"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        JsonElement Fiber(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        int closeOperation = JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.WINCLOSE).Index;
        EventEngine live = Create();
        live.Step();
        DialogueState? opening = null;
        for (int callback = 0; callback < 4; callback++)
        {
            live.Step();
            opening ??= live.DialogueTemplates.Single();
            Require(ReferenceEquals(opening, live.DialogueTemplates.Single()), "Native scalar UI samples replaced the opening identity and would restart text reveal.");
            Require(live.Dialogues.Single().NativeWindow == new NativeDialogueWindowState(1, (callback + 1) * 1024, 512), "Native window did not open through two512-unit UI samples after each VM callback.");
        }
        for (int callback = 0; callback < 12 && live.Dialogues.Single().NativeWindow!.Delta != -512; callback++)
        {
            live.Step();
        }
        Require(live.Dialogues.Single().NativeWindow == new NativeDialogueWindowState(1, 3072, -512), "WINCLOSE did not request its native closing delta before the two post-VM UI samples.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        foreach (int progress in new[] { 3072, 2048, 1024, 0 })
        {
            JsonElement held = Fiber(live);
            Require(held.GetProperty("Position").GetInt32() == closeOperation && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([5]) && !CapturedVariables(live.Capture()).TryGetProperty("5000", out _) && live.Dialogues.Single().NativeWindow == new NativeDialogueWindowState(1, progress, -512), "Closing WINCLOSE advanced its PC, popped its original channel or ran its successor before native window progress reached zero.");
            Require(live.DialogueTemplates.Count == (progress == 0 ? 0 : 1), "A zero-progress closing window remained visible while its channel awaited WINCLOSE completion.");
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native window close state or held VM diverged after independent restore.");
            live.Step();
            restored.Step();
        }
        Require(live.Dialogues.Count == 0 && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Inactive WINCLOSE did not pop its channel and continue its successor in the same entity budget.");
        EventEngine opened = Create();
        opened.Step();
        opened.Step();
        JsonObject missing = JsonNode.Parse(opened.Capture().GetRawText())!.AsObject();
        missing["Dialogues"]![0]!.AsObject().Remove("NativeWindow");
        bool rejected = false;
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(missing));
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, "Native window restore silently invented absent scalar progress instead of requiring explicit migration.");
        JsonObject invalid = JsonNode.Parse(opened.Capture().GetRawText())!.AsObject();
        invalid["Dialogues"]![0]!["NativeWindow"]!["Progress"] = 4097;
        rejected = false;
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(invalid));
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, "Native window restore accepted progress beyond the original4096 clamp.");
        Console.WriteLine("Native dialogue close: opening/closing scalar samples, return1 retained PC/operand, return2 same-budget completion, mid-close restore and absent-state refusal passed.");
    }
}
