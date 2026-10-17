using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeReboundEmptyAnimation(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        foreach (string unknown in new[] { "Terminal", "Frame", "Flags" })
        {
            EventEngine Create() => CreateNativeAnimationBackupFixture(vitals, economy, [], ushort.MaxValue);
            EventEngine live = Create();
            live.Step();
            JsonObject snapshot = JsonNode.Parse(live.Capture().GetRawText())!.AsObject();
            JsonNode header = snapshot["Actors"]![0]!["NativeAnimationHeader"]!;
            header["EmptyModelTerminal"] = 257;
            header["Terminal"] = 257;
            header["Frame"] = 0;
            header["Flags"] = 0x2000;
            header[unknown] = null;
            live.Restore(JsonSerializer.SerializeToElement(snapshot));
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            live.Step();
            restored.Step();
            Require(live.Actors[0].NativeAnimationHeader is { BoundModelId: -1, EmptyModelTerminal: 257, Frame: null, Flags: null }, "Fresh empty renderer WORD12 inferred the opaque retained body phase.");
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Opaque rebound empty body changed after cold restore.");
        }
    }
}
