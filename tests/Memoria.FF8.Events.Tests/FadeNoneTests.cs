using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFadeNoneContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADEOUT, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_W, 216), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte[]? bytes = null, bool known = true)
        {
            GameSession session = GameSession.CreateColdProcess();
            // Declared VM component: genuine cold process plus executed control initializer,
            // without claiming the unrelated full character/economy New Game producers.
            session.SetWorldControls(NativeWorldControlState.CreateNewGame());
            if (!known)
            {
                session = new GameSession(session.State with { FieldFade = null });
            }
            EventEngine engine = new(session);
            engine.LoadField(100, bytes ?? content, []);
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        JsonElement begun = first.Capture();
        Require(first.FieldFade is { Physical.Mode: 0, Physical.Rate: 8, Saved.Mode: 0, SyncMode: 3, Draw.Mode: NativeFadeDrawMode.None } && begun.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234 && begun.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("4").GetInt32() == 0, "FADENONE consumed an operand, reset retained physical words or lost exact native WORD216.");
        Require(!CapturedVariables(begun).TryGetProperty("216", out _), "Fade bank has a second mutable memory owner.");
        EventEngine resumed = Create();
        resumed.Restore(begun);
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(first.FieldFade!.Draw.Mode == NativeFadeDrawMode.None && JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Mode0 resumed a retained transition or changed after independent restore.");
        }
        byte[] direct = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.FADEBLACK, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine changed = Create(direct);
        changed.Step();
        Require(changed.FieldFade is { Physical.Mode: 4, Saved.Mode: 4, Draw.Mode: NativeFadeDrawMode.Black }, "FADEBLACK did not replace the sole physical mode and drawing sample.");
        byte[] overlap = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 215), (Jsm.Opcode.RET, 0));
        EventEngine refused = Create(overlap);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Cross-bank write bypassed native fade ownership.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "POPM_L")
        {
        }
        Require(refused.FieldFade!.Physical.Mode == 0 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77, "Cross-bank refusal consumed the write or changed physical fade.");
        EventEngine unknown = Create(direct, known: false);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Unknown fade owner acquired invented zero words.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "FADENONE")
        {
        }
        Require(unknown.FieldFade is null, "Unknown fade refusal fabricated an owner.");
        JsonNode malformed = JsonNode.Parse(begun.GetRawText())!;
        malformed["FieldFade"]!["Physical"]!.AsObject().Remove("Rate");
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Missing physical fade WORD passed restore.");
        }
        catch (JsonException)
        {
        }
        Console.WriteLine("Native fade: zero-operand commands, retained physical/saved words, single bank reads, independent restore, cross-bank and unknown atomic refusals verified.");
    }
}
