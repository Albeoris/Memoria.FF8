using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketAbandonedItemStoreProgram = "8970C4355B0BE108BC5721D7F472F1A8D6D2E80E442ED9E390F9704E758B1A83";

    internal static bool RecognizesWallMarketAbandonedItemStore(Ff7Session session) => session.Field.Name == "mkt_ia" && session.Machine.ProgramIdentity == WallMarketAbandonedItemStoreProgram;

    static partial void InspectWallMarketAbandonedItemStore(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketAbandonedItemStore(session) || story != 188)
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        for (int entity = 6; entity <= 16; entity++)
        {
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[1]:X4}", Ff7FieldEventCoverageKind.Empty, "This native party/presentation model's direct talk callback immediately returns; the terminal contact owns its player interaction."));
        }
        for (int script = 1; script <= 6; script++)
        {
            int entry = session.Field.Scripts.Entities[5].ScriptOffsets[script];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            events.Add(new($"line:5:script:{script}", 5, script, $"{entry:X4}:188", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Automatic, empty ? "This native terminal contact immediately returns." : "The native terminal Touch callback requires a real Confirm edge before requesting its child scene; an unconfirmed touch return does not complete the terminal event."));
        }
        events.Add(new("conditional:4:script:1", 4, 1, "05CA:broken-terminal:188", Ff7FieldEventCoverageKind.Conditional, "Physically use the original terminal with Confirm, run its broken-machine child scene and require the final parent contact return with controls released."));
        for (int entity = 0; entity <= 4; entity++)
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, "native-item-store-owner", Ff7FieldEventCoverageKind.Automatic, "This native director, gun/fire background or child-scene owner runs automatically or is requested by the terminal."));
        }
        events.Add(new("alternative:4:premium-heart", 4, 3, "native-story-at-least999", Ff7FieldEventCoverageKind.Inactive, "The original unique weapon reward requires story999 or later and its unclaimed reward predicate; it is unavailable at188 and no item or party change is forced."));
        events.Add(new("alternative:4:alternate-player", 4, 2, "native-story192", Ff7FieldEventCoverageKind.Inactive, "The alternate player-model terminal choreography is selected only at story192, not this visit."));
        events.Add(new("exit:0:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "The original sole gateway returns to the market; this field has no nested room gateway."));
        bool lines = session.Host.Process.Lines is [{ Entity: 5, Enabled: 1 }];
        bool exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).SequenceEqual([195]);
        plan = new(lines && exits, "All abandoned item-store contacts, empty party conversations, gated terminal scene, unavailable later reward and sole market return are classified; no Honey Bee or nested-room completion is claimed.", false, events.AsReadOnly());
    }
}
