using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static JsonElement CapturedVariables(JsonElement checkpoint)
    {
        NativeEventMemory memory = checkpoint.GetProperty("EventMemory").Deserialize<NativeEventMemory>()!;
        Dictionary<int, int> values = new(memory.KnownValues);
        JsonElement scene = checkpoint.GetProperty("Scene");
        void Add(JsonElement source, string property, int address)
        {
            if (source.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                values[address] = value.GetInt32();
            }
        }
        Add(scene, "PreviousFieldWord84", 84);
        Add(scene, "PreviousWorldEntityByte86", 86);
        Add(scene, "SoundMusicVolume197", 197);
        Add(scene, "SoundMusicVolume198", 198);
        if (scene.TryGetProperty("PartyBindingMemory", out JsonElement party) && party.ValueKind == JsonValueKind.Object)
        {
            Add(party, "First", 194);
            Add(party, "Second", 195);
            Add(party, "Third", 196);
        }
        foreach ((string property, int channel, int music) in new[] { ("MusicChannel108", 108, 199), ("MusicChannel112", 112, 200) })
        {
            if (scene.TryGetProperty(property, out JsonElement owner) && owner.ValueKind == JsonValueKind.Object)
            {
                Add(owner, "ChannelId", channel);
                Add(owner, "MusicId", music);
            }
        }
        if (scene.TryGetProperty("MusicProcess", out JsonElement process) && process.ValueKind == JsonValueKind.Object)
        {
            Add(process, "Bank201", 201);
            Add(process, "Bank202", 202);
            Add(process, "Requested213", 213);
            Add(process, "Complete214", 214);
        }
        if (checkpoint.TryGetProperty("WorldEntryMemory", out JsonElement selected) && selected.ValueKind == JsonValueKind.Object)
        {
            values = new Dictionary<int, int>(selected.Deserialize<NativeWorldEntryMemory>()!.ProjectVariables(values));
        }
        return JsonSerializer.SerializeToElement(values);
    }

    private static void ExpectEventMemoryPublicationFault(GameSession session, NativeEventMemory memory)
    {
        GameState before = session.State;
        try
        {
            session.SetEventMemory(memory);
            throw new InvalidOperationException("Overlapping generic event memory was published.");
        }
        catch (InvalidDataException)
        {
        }
        Require(ReferenceEquals(before, session.State), "Refused event-memory publication changed a canonical owner or the prior memory.");
    }

    private static void SetCapturedCell(JsonNode checkpoint, int address, int width, int value)
    {
        checkpoint["EventMemory"]!["Cells"]![address.ToString(System.Globalization.CultureInfo.InvariantCulture)] = JsonSerializer.SerializeToNode(new NativeMemoryValue(width, NativeScalarValue.Exact(value)));
    }
}
