using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketRestaurantProgram = "D007DFF714164FB01B4A1027FD3B272813888C2C30E6A870BE49B79BA6701B39";

    internal static bool RecognizesWallMarketRestaurant(Ff7Session session) => session.Field.Name == "mkt_s2" && session.Machine.ProgramIdentity == WallMarketRestaurantProgram;

    static partial void InspectWallMarketRestaurant(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketRestaurant(session) || story != 188)
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        int greeting = session.Machine.Variables.ReadByte(FieldBank.MapByte, 9);
        for (int entity = 10; entity <= 15; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{entry:X4}:greeting:{(entity is 11 or 15 ? greeting : 0)}", Ff7FieldEventCoverageKind.Interaction, "Speak with this currently visible native restaurant patron and observe its owned return."));
        }
        foreach (int entity in new[] { 4, 5 })
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
                bool empty = session.Field.Scripts.Data.Span[entry] == 0;
                events.Add(new($"line:{entity}:script:{script}", entity, script, $"{entry:X4}:188", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Interaction, empty ? "This native restaurant contact immediately returns." : entity == 4 ? "Cross the native service-area warning line and wait for its owned return." : "Order the original70-Gil meal, select its coupon-eligible rating and wait for the complete native meal return."));
            }
        }
        foreach (int entity in new[] { 0, 1, 2, 3, 6, 7, 8, 9, 17, 18, 19 })
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, "native-restaurant-owner", Ff7FieldEventCoverageKind.Automatic, "This native director, background, player, waiter, cook or meal model is initialized automatically or requested by the restaurant's owned scene."));
        }
        events.Add(new("actor:16:script:1", 16, 1, "story192-only", Ff7FieldEventCoverageKind.Inactive, "The native seventh patron is hidden and has talk/collision disabled until story192."));
        events.Add(new("alternative:5:decline", 5, 3, "exclusive-meal-answer", Ff7FieldEventCoverageKind.Inactive, "Declining is an exclusive answer to ordering this meal; it cannot substitute for the currently available one-time coupon reward."));
        events.Add(new("exit:0:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "Return through the original restaurant gateway after all local interactions."));
        bool lines = session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual(new[] { 4, 5 });
        bool exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).SequenceEqual([195]);
        plan = new(lines && exits, "The native restaurant patrons, warning contact, meal/coupon scene, automatic owners and return gateway are classified; completion still requires their native execution.", false, events.AsReadOnly());
    }
}
