using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeWorldEntryMemoryContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        GameSession cold = GameSession.CreateColdProcess();
        Require(cold.WorldEntryMemory is null, "Cold process inferred world-entry bytes before NewGame.");
        cold.StartNewGame();
        Require(cold.WorldEntryMemory is { Origin: NativeWorldEntryMemoryOrigin.NativeNewGame } fresh && fresh.Bytes.Length == 12 && fresh.Bytes.All(value => value == 0), "Actual NewGame did not establish its complete zeroed selected event bank.");

        (Jsm.Opcode, int)[] writes = [(Jsm.Opcode.PSHN_L, 0x11223344), (Jsm.Opcode.POPM_L, 256), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0xA1B2), (Jsm.Opcode.POPM_W, 257), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_B, 259), (Jsm.Opcode.PSHN_L, unchecked((int)0x80FF7E01)), (Jsm.Opcode.POPM_L, 264), (Jsm.Opcode.PSHM_L, 256), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHSM_B, 267), (Jsm.Opcode.POPM_L, 5004), (Jsm.Opcode.PSHM_W, 266), (Jsm.Opcode.POPM_L, 5008), (Jsm.Opcode.PSHSM_W, 266), (Jsm.Opcode.POPM_L, 5012), (Jsm.Opcode.PSHM_L, 264), (Jsm.Opcode.POPM_L, 5016), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5020), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        var live = CreateWorldEntryMemoryFixture(vitals, economy, writes);
        var restored = CreateWorldEntryMemoryFixture(vitals, economy, writes);
        for (int callback = 0; callback < 24 && !live.Session.State.Variables.ContainsKey(5020); callback++)
        {
            restored.Engine.Restore(live.Engine.Capture());
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Overlapping world-entry byte stores or signed reads diverged after independent restoration.");
        }
        NativeWorldEntryMemory memory = live.Session.WorldEntryMemory!;
        Require(memory.Bytes.SequenceEqual(new byte[] { 0x44, 0xB2, 0xA1, 0xFF, 0, 0, 0, 0, 1, 0x7E, 0xFF, 0x80 }) && live.Session.State.Variables[5000] == unchecked((int)0xFFA1B244) && live.Session.State.Variables[5004] == -128 && live.Session.State.Variables[5008] == 0x80FF && live.Session.State.Variables[5012] == unchecked((short)0x80FF) && live.Session.State.Variables[5016] == unchecked((int)0x80FF7E01), $"Byte/WORD/DWORD stores lost little-endian overlap, narrowing, sign extension or byte267: bytes{Convert.ToHexString(memory.Bytes.AsSpan())}, outputs[{string.Join(',', new[] { 5000, 5004, 5008, 5012, 5016 }.Select(address => live.Session.State.Variables.GetValueOrDefault(address)))}].");
        Require(memory.StoryWord == 0xB244 && memory.ReplacementFlags264 == 1 && memory.StoryByte266 == 255 && live.Session.State.Variables[256] == 0xB244 && live.Session.State.Variables[264] == 1 && live.Session.State.Variables[265] == 0x7E && live.Session.State.Variables[266] == 255 && !live.Session.State.Variables.Keys.Any(address => address is >= 256 and <= 267 && address is not (256 or 264 or 265 or 266)), "Selected owner retained conflicting dictionary aliases or changed canonical story/loader projections.");

        (Jsm.Opcode, int)[] unaligned = [(Jsm.Opcode.PSHN_L, 0x44332211), (Jsm.Opcode.POPM_L, 261), (Jsm.Opcode.PSHN_L, 0xBBAA), (Jsm.Opcode.POPM_W, 263), (Jsm.Opcode.PSHN_L, unchecked((int)0x88776655)), (Jsm.Opcode.POPM_L, 264), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        var overlapping = CreateWorldEntryMemoryFixture(vitals, economy, unaligned);
        bool dwordClobberObserved = false;
        overlapping.Engine.CommandExecuted += command =>
        {
            if (command.Name == "POPM_L" && command.Arguments.SequenceEqual(new[] { 0x44332211 }))
            {
                dwordClobberObserved = true;
                Require(overlapping.Session.WorldEntryMemory!.ReadBytes(264, 1) == 0x44 && overlapping.Session.State.Variables.GetValueOrDefault(264) == 0x44, "DWORD261 failed to publish its actual high byte into loader BYTE264 before the next opcode.");
            }
        };
        for (int callback = 0; callback < 4; callback++)
        {
            overlapping.Engine.Step();
        }
        Require(dwordClobberObserved && overlapping.Session.WorldEntryMemory!.Bytes.SequenceEqual(new byte[] { 0, 0, 0, 0, 0, 0x11, 0x22, 0xAA, 0x55, 0x66, 0x77, 0x88 }) && overlapping.Session.WorldEntryMemory.ReadBytes(261, 4) == 0x55AA2211 && overlapping.Session.WorldEntryMemory.ReadBytes(264, 4) == unchecked((int)0x88776655), "Wholly owned unaligned DWORD261/WORD263/DWORD264 stores lost their shared byte264 overlap.");
        foreach (int address in new[] { 255, 266 })
        {
            NativeWorldEntryMemoryRefusal(vitals, economy, [(Jsm.Opcode.PSHN_L, 0x44332211), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.POPM_L, address)], "POPM_L");
        }
        NativeWorldEntryMemoryRefusal(vitals, economy, [(Jsm.Opcode.NOP, 0), (Jsm.Opcode.PSHM_L, 266)], "PSHM_L");
        foreach ((Jsm.Opcode opcode, int address) in new[] { (Jsm.Opcode.POPM_B, 264), (Jsm.Opcode.POPM_W, 256), (Jsm.Opcode.POPM_L, 264) })
        {
            NativeWorldEntryMemoryRefusal(vitals, economy, [(Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.NOP, 0), (opcode, address)], opcode.ToString(), unknownTimer: true);
        }
        NativeWorldEntryMemoryClear(vitals, economy, knownOwner: true);
        NativeWorldEntryMemoryClear(vitals, economy, knownOwner: false);
        NativeWorldEntryMemoryRefusal(vitals, economy, [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.POPM_B, 264)], "POPM_B", knownOwner: false);
        NativeWorldEntryMemoryRefusal(vitals, economy, [(Jsm.Opcode.NOP, 0), (Jsm.Opcode.PSHM_W, 256)], "PSHM_W", knownOwner: false);

        JsonElement valid = live.Engine.Capture();
        foreach (string fault in new[] { "missing", "short", "mirror" })
        {
            JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
            if (fault == "missing")
            {
                malformed.AsObject().Remove("WorldEntryMemory");
            }
            else if (fault == "short")
            {
                malformed["WorldEntryMemory"]!["Bytes"]!.AsArray().RemoveAt(11);
            }
            else
            {
                SetCapturedCell(malformed, 264, 4, 99);
            }
            try
            {
                live.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed selected world-entry memory was inferred or accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(valid, live.Engine.Capture()), "Rejected selected-memory restore partially changed the live VM or canonical projections.");
        }
        Console.WriteLine("Native world-entry bytes256..267: NewGame/CLEAR producers, overlapping B/W/L and signed reads, wholly owned unaligned stores, canonical projections, uncertain/unknown/cross-boundary-access atomic refusals and independent restoration pass.");
    }

    private static void NativeWorldEntryMemoryClear(NativeCharacterVitals vitals, NativeEconomy economy, bool knownOwner)
    {
        List<(Jsm.Opcode, int)> commands = [];
        if (knownOwner)
        {
            commands.AddRange([(Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_L, 256), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_L, 260), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_L, 264)]);
        }
        commands.AddRange([(Jsm.Opcode.CLEAR, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        var live = CreateWorldEntryMemoryFixture(vitals, economy, commands.ToArray(), knownOwner);
        for (int callback = 0; callback < 8 && !live.Session.State.Variables.ContainsKey(5000); callback++)
        {
            live.Engine.Step();
        }
        Require(live.Session.State.Variables.GetValueOrDefault(5000) == 77 && live.Session.WorldEntryMemory is { } cleared && cleared.Bytes.All(value => value == 0) && !live.Session.State.Variables.Keys.Any(address => address is >= 256 and <= 267), "Executed CLEAR failed to establish or zero the complete selected bank and remove zero projections.");
        if (!knownOwner)
        {
            Require(live.Session.WorldEntryMemory!.Origin == NativeWorldEntryMemoryOrigin.ExecutedFieldClear, "Missing selected memory acquired an inferred NewGame owner instead of its actual CLEAR producer.");
        }
    }

    private static void NativeWorldEntryMemoryRefusal(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] commands, string opcode, bool knownOwner = true, bool unknownTimer = false)
    {
        var live = CreateWorldEntryMemoryFixture(vitals, economy, commands.Concat(new[] { (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8) }).ToArray(), knownOwner, unknownTimer);
        JsonElement? before = null;
        int operation = -1;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == "NOP")
            {
                before = live.Engine.Capture();
                operation = command.Operation + 1;
            }
        };
        bool refused = false;
        for (int callback = 0; callback < 12 && !refused; callback++)
        {
            try
            {
                live.Engine.Step();
            }
            catch (EventScriptException)
            {
                refused = true;
            }
        }
        JsonElement after = live.Engine.Capture();
        JsonElement fiber = after.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Require(refused && before is not null && operation >= 0 && live.Engine.LastFault?.Operation == operation && live.Engine.LastFault.Opcode == opcode && fiber.GetProperty("Position").GetInt32() == operation, "Unsupported selected-memory access advanced its original instruction pointer.");
        foreach (string property in new[] { "WorldEntryMemory", "EventMemory", "Fibers" })
        {
            bool hadValue = before!.Value.TryGetProperty(property, out JsonElement oldValue);
            bool hasValue = after.TryGetProperty(property, out JsonElement newValue);
            Require(hadValue == hasValue && (!hadValue || JsonElement.DeepEquals(oldValue, newValue)), $"Unsupported selected-memory access mutated {property} before refusal.");
        }
        if (unknownTimer)
        {
            Require(fiber.GetProperty("Stack")[0].GetProperty("Values").GetArrayLength() > 1, "Uncertain store fixture did not retain a genuinely typed timer alternative set.");
        }
    }

    private static (GameSession Session, EventEngine Engine) CreateWorldEntryMemoryFixture(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] commands, bool knownOwner = true, bool unknownTimer = false)
    {
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        if (unknownTimer)
        {
            // Existing supported phase-unknown timer component; actual SET/WAIT/GET creates the alternatives.
            session.SetExamTimer(NativeExamTimerState.FromReachedSet(0));
        }
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
        foreach ((Jsm.Opcode opcode, int parameter) in commands)
        {
            if (opcode == Jsm.Opcode.PSHN_L && parameter is < -0x800000 or > 0x7FFFFF)
            {
                // JSM immediate operands are signed24. Build full DWORDs through real CAL operations.
                main.AddRange([(Jsm.Opcode.PSHN_L, parameter >> 16), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.CAL, 17), (Jsm.Opcode.PSHN_L, parameter & 0xFFFF), (Jsm.Opcode.CAL, 13)]);
            }
            else
            {
                main.Add((opcode, parameter));
            }
        }
        engine.LoadField(600, SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        if (!knownOwner)
        {
            // Explicit live missing-owner fault injection; never restored, saved or used as player history.
            // Only the subsequent actual CLEAR instruction may acquire a new executed owner.
            typeof(GameSession).GetProperty(nameof(GameSession.State))!.SetValue(session, session.State with { WorldEntryMemory = null });
        }
        return (session, engine);
    }
}
