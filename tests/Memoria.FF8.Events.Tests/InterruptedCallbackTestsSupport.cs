using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void ExpectInterruptedRestoreRefusal(EventEngine engine, JsonElement interrupted)
    {
        Require(interrupted.GetProperty("FieldCallbackInterrupted").GetBoolean(), "The refusal fixture did not record an interrupted native callback.");
        JsonElement before = engine.Capture();
        EventFault? fault = engine.LastFault;
        try
        {
            engine.Restore(interrupted);
            throw new InvalidOperationException("An interrupted native callback was accepted as a resumable checkpoint.");
        }
        catch (NotSupportedException exception) when (exception.Message.Contains("interrupted native callback", StringComparison.Ordinal))
        {
        }
        Require(JsonElement.DeepEquals(before, engine.Capture()) && engine.LastFault == fault, "Refusing an interrupted callback changed the current VM or cleared its fault guard.");
    }
}
