using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeKeyScan2Contract(string gameDirectory, bool rawEdge = false)
    {
        using GameAssets assets = new(gameDirectory);
        NativeInputConfiguration configuration = assets.ReadNewGameInputConfiguration();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        Require(configuration.Options == 0x20001 && configuration.ButtonMap.SequenceEqual(Enumerable.Range(1, 12).Select(value => (byte)value)), "Authentic native options do not match the extracted56DA10 template.");
        EventEngine Create(Jsm.Opcode query, int mask, bool mapped = true)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            if (mapped)
            {
                session.SetInputConfiguration(configuration);
            }
            EventEngine engine = new(session);
            (Jsm.Opcode, int)[] init = rawEdge ? [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)];
            engine.LoadField(600, SyntheticObjects([1], [init, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, mask), (query, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.JMP, -4), (Jsm.Opcode.RET, 8)]]), [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            engine.Step();
            return engine;
        }
        foreach (int mask in new[] { 0, 4, 0x10, 0x20, 0x40, 0x80, 0xF000, 0x10000, int.MinValue, -1 })
        {
            EventEngine live = Create(rawEdge ? Jsm.Opcode.KEYON2 : Jsm.Opcode.KEYSCAN2, mask, mapped: false);
            List<ScriptedInputQuery> observed = [];
            if (rawEdge)
            {
                live.CommandExecuted += command =>
                {
                    if (command.Name == nameof(Jsm.Opcode.KEYON2))
                    {
                        observed.Add(new(command.ObjectId, command.ScriptId, command.Operation, command.Name, unchecked((ushort)command.Arguments.Single())));
                    }
                };
            }
            foreach (int raw in new[] { 0, 4, 0x10, 0x20, 0x40, 0x80, 0x100, 0xF000, ushort.MaxValue })
            {
                observed.Clear();
                int previous = live.ButtonInput!.RawHeld;
                Require(live.Apply(new PlayerAction(PlayerActionKind.FieldButtons, raw)).Accepted, "Raw WORD request was refused.");
                live.Step();
                JsonElement saved = live.Capture();
                int expected = (unchecked((uint)mask) & (uint)(rawEdge ? raw & ~previous : raw)) != 0 ? 1 : 0;
                JsonElement fiber = saved.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
                Require(CapturedVariables(saved).GetProperty("5000").GetInt32() == expected && live.ButtonInput!.RawHeld == raw && live.ButtonInput.Held is null && fiber.GetProperty("Results").GetProperty("3").GetInt32() == 333 && fiber.GetProperty("Results").GetProperty("7").GetInt32() == 777, "KEYSCAN2 failed its zero-extended raw word/fullDWORD mask or return2 register preservation.");
                if (rawEdge)
                {
                    int[] physical = Enumerable.Range(0, 16).Select(bit => 1 << bit).Where(bit => (mask & bit) != 0).ToArray();
                    ScriptedInputMetadata discovered = live.DiscoverScriptedInput() ?? throw new InvalidOperationException("KEYON2 was absent from scripted input discovery.");
                    ScriptedInputMetadata sampled = live.DiscoverObservedScriptedInput(observed) ?? throw new InvalidOperationException("Executed KEYON2 was absent from sampled input discovery.");
                    Require(live.IsScriptedInterface && live.ButtonInput.Held is null && discovered.Masks.SequenceEqual(physical) && sampled.Masks.SequenceEqual(physical) && sampled.ObservedQueriesProven && sampled.Queries.All(query => query.Opcode == nameof(Jsm.Opcode.KEYON2)), "KEYON2 discovery lost its raw physical masks or required unavailable mapped input configuration.");
                }
                EventEngine restored = Create(rawEdge ? Jsm.Opcode.KEYON2 : Jsm.Opcode.KEYSCAN2, mask, mapped: false);
                restored.Restore(saved);
                live.Step();
                restored.Step();
                Require(live.ButtonInput!.RawPressed == 0 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Raw held input became an edge or diverged after independent restore.");
                if (rawEdge)
                {
                    Require(CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 0, "KEYON2 repeated an already consumed raw edge.");
                }
            }
        }
        if (rawEdge)
        {
            Console.WriteLine("KEYON2 raw WORD rising edges/full-DWORD masks, unmapped input, R0/other-register ownership, held-edge expiry and independent restoration PASS.");
            return;
        }
        EventEngine unknownMapped = Create(Jsm.Opcode.KEYSCAN, 4, mapped: false);
        unknownMapped.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 4));
        try
        {
            unknownMapped.Step();
            throw new InvalidOperationException("Mapped query silently substituted raw input for unknown saved options.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement failed = unknownMapped.Capture();
        JsonElement blocked = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(unknownMapped.LastFault?.Opcode == "KEYSCAN" && blocked.GetProperty("Position").GetInt32() == unknownMapped.LastFault.Operation && blocked.GetProperty("Stack")[0].GetInt32() == 4 && !CapturedVariables(failed).TryGetProperty("5000", out _), "Unknown mapped query consumed its mask, PC or result memory.");

        byte[] custom = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        custom[2] = 8;
        custom[6] = 0;
        custom[7] = 3;
        Require(NativeInputConfiguration.Project(4 | 0x40 | 0x80 | 0xF000, 0x20, custom) == (0x80 | 4 | 0xF000), "Native remapping did not drop0, redirectlow12 or preserve highF000.");
        Require(NativeInputConfiguration.InvertPulse(0x80, 0x20, custom) == 4, "Logical pulse failed unique physical inverse.");
        custom[1] = 8;
        try
        {
            NativeInputConfiguration.InvertPulse(0x80, 0x20, custom);
            throw new InvalidOperationException("Logical pulse guessed among multiple native physical sources.");
        }
        catch (NotSupportedException)
        {
        }
        custom[0] = 32;
        Require(NativeInputConfiguration.Project(1, 0x20, custom) == 0, "Native mapped result did not narrow the x86 shift to unsignedWORD.");
        try
        {
            (configuration with { Options = configuration.Options | 0x20 }).Validate();
            throw new InvalidOperationException("A custom options bank was invented without its native writer.");
        }
        catch (InvalidDataException)
        {
        }

        EventEngine carry = Create(Jsm.Opcode.KEYON, 0x40);
        carry.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x40));
        carry.Step();
        NativeFieldButtonInput beforeLoad = carry.ButtonInput!;
        byte[] later = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0x40), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        carry.LoadField(601, later, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
        Require(carry.Frame == 0 && carry.ButtonInput == beforeLoad && carry.ButtonInput.SampledFieldId == 600, "Normal allocation reset or rebased the global raw sampler.");
        JsonElement allocated = carry.Capture();
        EventEngine independent = Create(Jsm.Opcode.KEYON, 0x40);
        independent.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x40));
        independent.Step();
        independent.LoadField(601, later, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
        independent.Restore(allocated);
        carry.Step();
        independent.Step();
        carry.Step();
        independent.Step();
        Require(carry.ButtonInput!.RawHeld == 0x40 && carry.ButtonInput.RawPressed == 0 && JsonElement.DeepEquals(carry.Capture(), independent.Capture()), "First new-field sample fabricated another edge or diverged after exact allocation-boundary restore.");

        EventEngine pulse = Create(Jsm.Opcode.KEYON, 0x10);
        pulse.Apply(new PlayerAction(PlayerActionKind.Cancel));
        Require(pulse.ButtonInput!.RawPressed == 0x10, "First logical pulse did not generate its real sampled rise.");
        pulse.Apply(new PlayerAction(PlayerActionKind.Cancel));
        Require(pulse.ButtonInput.RawHeld == 0x10 && pulse.ButtonInput.RawPressed == 0, "Consecutive logical pulses fabricated an unsampled release/second edge.");
        pulse.Step();
        pulse.Apply(new PlayerAction(PlayerActionKind.Cancel));
        Require(pulse.ButtonInput.RawPressed == 0x10, "A real sampled release failed to permit the next rise.");
        JsonNode malformed = JsonNode.Parse(allocated.GetRawText())!;
        malformed.AsObject().Remove("FieldButtonInput");
        JsonElement unchanged = independent.Capture();
        try
        {
            independent.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Current snapshot accepted an omitted input-process shape.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(unchanged, independent.Capture()), "Input-process restore refusal partially mutated the VM.");
        foreach ((int entity, string original) in new[] { (0, "Squall"), (1, "Rinoa"), (2, "Angelo") })
        {
            GameSession namingSession = CreateDirectedLinearTurnSession(vitals, economy);
            namingSession.SetInputConfiguration(configuration);
            EventEngine naming = new(namingSession);
            naming.LoadField(600, SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, entity), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, entity), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]), [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            naming.Step();
            naming.Step();
            Require(naming.CurrentDialogue is { IsNamePrompt: true } name && name.Text == "Name: " + original, "Native naming prompt invented a placeholder or selected another default subject.");
            naming.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: " "));
            Require(naming.Scene.Names[entity] == original, "Blank native name confirmation used another subject's default.");
            naming.Step();
            Require(naming.CurrentDialogue?.Text == "Name: " + original, "Repeated naming did not preserve the current saved name.");
            naming.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: "Actual"));
            Require(naming.Scene.Names[entity] == "Actual", "Native naming default overwrote explicit input.");
        }
    }
}
