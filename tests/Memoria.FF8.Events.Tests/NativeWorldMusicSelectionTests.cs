using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeWorldMusicSelectionContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        var vitals = assets.ReadNewGameCharacterVitals();
        var economy = assets.ReadNewGameEconomy();
        (Jsm.Opcode, int)[] commands = [(Jsm.Opcode.PSHN_L, 41), (Jsm.Opcode.POPM_B, 268), (Jsm.Opcode.CLEAR, 0), (Jsm.Opcode.PSHN_L, 50), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        var live = CreateWorldEntryMemoryFixture(vitals, economy, commands);
        var restored = CreateWorldEntryMemoryFixture(vitals, economy, commands);
        for (int callback = 0; callback < 8 && live.Engine.Scene.WorldMusicSelection?.Origin != NativeWorldMusicSelectionOrigin.ExecutedClear; callback++)
        {
            live.Engine.Step();
        }
        Require(live.Engine.LastFault is null && live.Engine.Scene.WorldMusicSelection is { Origin: NativeWorldMusicSelectionOrigin.ExecutedClear, Music: 0, RawOperand: null } && live.Engine.Variables[268] == 0, "Native CLEAR lost its executed known-zero BYTE268 owner.");
        JsonElement checkpoint = live.Engine.Capture();
        restored.Engine.Restore(checkpoint);
        Require(JsonElement.DeepEquals(checkpoint, restored.Engine.Capture()), "Executed CLEAR music owner diverged on independent restoration.");
        NativeWorldMusicSelection physical = NativeWorldMusicSelection.FromRetainedPhysicalByte(41, new string('A', 64));
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        JsonNode migrated = JsonNode.Parse(checkpoint.GetRawText())!;
        migrated["Scene"]!["WorldMusicSelection"] = JsonSerializer.SerializeToNode(physical, options);
        restored.Engine.Restore(JsonSerializer.SerializeToElement(migrated));
        Require(restored.Engine.Variables[268] == 41 && restored.Engine.Scene.WorldMusicSelection?.FieldId is null && !restored.Engine.EventMemory.Cells.ContainsKey(268), "Retained physical BYTE268 acquired an invented writer or a conflicting generic alias.");
        bool refused = false;
        try
        {
            (physical with { RawOperand = 41 }).Validate();
        }
        catch (InvalidDataException)
        {
            refused = true;
        }
        Require(refused, "Physical-byte provenance admitted invented raw operand history.");
        Console.WriteLine("World music BYTE268: executed CLEAR known-zero owner and independent physical-byte migration restoration pass.");
    }
}
