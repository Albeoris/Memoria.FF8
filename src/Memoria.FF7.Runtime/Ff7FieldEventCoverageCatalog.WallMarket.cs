using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WallMarketTourEdge(string Id, int DestinationField, string DestinationName, AutoPlayTourKind Kind, string Reason, bool CatalogReady, int? ReturnField, string? Availability = null);
public sealed record Ff7WallMarketTourPlan(string RootField, bool Complete, string Reason, IReadOnlyList<Ff7WallMarketTourEdge> Edges);

public static partial class Ff7WallMarketTourCatalog
{
    public static Ff7WallMarketTourPlan Inspect(Ff7Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        if (!Ff7FieldEventCoverageCatalog.RecognizesWallMarket(session) || story is not (185 or 188))
        {
            return new("mrkt2", false, "This native field epoch requires its own reviewed tour frontier.", []);
        }
        int materiaReward40 = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x40) & 0xC0;
        int materiaReward41 = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 1;
        string? materiaAvailability = materiaReward40 == 0 && materiaReward41 == 0 ? null : $"reward:{materiaReward40}:{materiaReward41}";
        bool pharmacyCoupon = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 0x40) != 0;
        Ff7WallMarketTourEdge[] edges =
        [
            new("gateway:0:205", 205, "mrkt1", AutoPlayTourKind.OptionalGameplay, "The south market has available native NPC and door events plus additional gameplay rooms; required nested frontier catalogs remain pending.", false, 195),
            new("gateway:1:200", 200, "mkt_m", AutoPlayTourKind.OptionalGameplay, "Native story188 keeper conversation from reachable floor and return195 are physically proved; meaningful reward predicates retain their availability epoch.", story == 188, 195, materiaAvailability),
            new("gateway:2:201", 201, "mkt_s1", AutoPlayTourKind.OptionalGameplay, "Native boutique entry and return195 are present; its current interactions are not yet reviewed.", false, 195),
            new("gateway:3:202", 202, "mkt_s2", AutoPlayTourKind.OptionalGameplay, "Native story188 patrons, warning, paid meal, rating, coupon and return195 are physically proved before holding a coupon.", story == 188 && !pharmacyCoupon, 195),
            new("gateway:4:203", 203, "mkt_s3", AutoPlayTourKind.OptionalGameplay, "Native story188 ordinary shop and coupon/Digestive counter paths plus their returns are physically proved; predicate history retains their distinct receipts.", story == 188, 195, pharmacyCoupon ? "coupon40" : null),
            new("gateway:5:198", 198, "mkt_ia", AutoPlayTourKind.OptionalGameplay, "Native story188 abandoned item terminal, owned child/parent returns and return195 are physically proved; later Premium Heart reward remains unavailable.", story == 188, 195),
            new("gateway:6:199", 199, "mktinn", AutoPlayTourKind.OptionalGameplay, "The native story188 inn interactions, decline choice and return195 are physically proved; other epochs require their own review.", story == 188, 195),
            new("gateway:7:214", 214, "mrkt3", AutoPlayTourKind.StoryExit, "The existing story route reaches this native district only after local events and required side-room frontiers.", false, 195),
            new("gateway:8:194", 194, "mds6_3", AutoPlayTourKind.Return, "The reviewed park departure corridor returns to195 and contains no new current-story interaction.", true, 195)
        ];
        int[] destinations = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).ToArray();
        if (!destinations.SequenceEqual(edges.Select(edge => edge.DestinationField)))
        {
            return new("mrkt2", false, "The native market gateway directory differs from the reviewed frontier.", []);
        }
        return new("mrkt2", false, "The native market side-room interaction catalogs are explicit unvisited frontiers.", edges);
    }
}

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketProgram = "B1070D7852D99DA97CC4F9164EEE3586381900F3CD4553ED2E4D316D29D318A3";

    internal static bool RecognizesWallMarket(Ff7Session session) => session.Field.Name == "mrkt2" && session.Machine.ProgramIdentity == WallMarketProgram;

    static partial void InspectWallMarket(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarket(session) || story is not (185 or 188))
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        bool questInformation = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 0x47) != 0 || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 7) != 0;
        for (int entity = 12; entity <= 23; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.Entity == entity);
            string predicates = entity == 16
                ? questInformation ? ":quest-info" : ":0:0"
                : entity == 21 ? $":{session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x41) & 16}" : "";
            string availability = $"{entry:X4}:{actor?.Model ?? -1}:{actor?.TalkRadius ?? 0}:{actor?.TalkEnabled ?? false}:{actor?.Visible ?? false}:{story}{predicates}";
            bool available = story == 188 && actor is { TalkEnabled: true, Visible: true };
            events.Add(new($"actor:{entity}:script:1", entity, 1, availability, empty ? Ff7FieldEventCoverageKind.Empty : available ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, empty ? "This native party/save-model talk callback immediately returns." : available ? "This native conversation requires physical acceptance and its owned return." : "The native arrival scene or actor state currently prevents this conversation."));
        }
        events.Add(new("actor:17:script:2", 17, 2, $"0BBF:{story}", story == 188 ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, "This native touch conversation is distinct from the turning-only talk callback and requires physical contact and its owned return."));
        foreach (int entity in new[] { 10, 11, 24 })
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
                bool empty = session.Field.Scripts.Data.Span[entry] == 0;
                Ff7Line? line = session.Host.Process.Lines.SingleOrDefault(line => line.Entity == entity);
                string reason = empty ? "This native contact callback immediately returns." : entity == 10
                    ? "The native cross-line owner enables only at story192; its dressed-party scene is inactive at this epoch."
                    : entity == 11 ? "The native boutique-door Enter/Leave callbacks require separate physical contact and owned return witnesses."
                    : "The native save-point Enter/Leave callbacks require separate physical witnesses; no gameplay slot write is implied.";
                bool available = story == 188 && entity != 10 && line?.Enabled == 1;
                events.Add(new($"line:{entity}:script:{script}", entity, script, $"{entry:X4}:{line?.Enabled ?? -1}:{story}", empty ? Ff7FieldEventCoverageKind.Empty : available ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, reason));
            }
        }
        foreach ((var gateway, int index) in session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767))
        {
            events.Add(new($"exit:{index}:map:{gateway.DestinationField}", -1, -1, $"native-gateway:{gateway.DestinationField}", Ff7FieldEventCoverageKind.Exit, "This native exit is tracked by the market tour frontier; an unreviewed side-room is not a completed alternative."));
        }
        foreach (int entity in new[] { 0, 1, 2, 3, 8, 9 })
        {
            if (entity == 2)
            {
                events.Add(new("conditional:2:script:1", 2, 1, "079E:arrival185", story == 185 ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive, "The native arrival scene must finish its synchronous child, publish story188 and restore player/menu controls before its owned return."));
                continue;
            }
            events.Add(new($"automatic:{entity}:script:0", entity, 0, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[0]:X4}:{story}", Ff7FieldEventCoverageKind.Automatic, entity is 0 or 2 ? "The native arrival director owns the conditional scene, including the early story188 write; the scene's completion remains required." : "This native owner supplies setup, camera or repeating background presentation rather than an independent player interaction."));
        }
        for (int entity = 4; entity <= 7; entity++)
        {
            for (int script = 1; script <= 2; script++)
            {
                events.Add(new($"background:{entity}:script:{script}", entity, script, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[script]:X4}", Ff7FieldEventCoverageKind.Automatic, "This native door-background callback is child-owned presentation; the triggering LINE or field handoff must supply the player-event witness."));
            }
        }
        for (int entity = 12; entity <= 15; entity++)
        {
            events.Add(new($"party:{entity}:script:3", entity, 3, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[3]:X4}:{story}", Ff7FieldEventCoverageKind.Inactive, entity == 13 && story == 185 ? "The native arrival director owns Aerith's scene; require story188, return0A13, init_ev return07B6 and restored player/menu control." : "This native party movement/wardrobe scene is owned by a conditional request; it is not an independently available conversation."));
        }
        bool complete = story == 185 || session.Host.Process.Lines.Select(line => (line.Entity, Enabled: (int)line.Enabled)).OrderBy(line => line.Entity).SequenceEqual([(10, 0), (11, 1), (24, 1)]);
        plan = new(complete, complete ? "Native arrival, party/talk/contact interfaces and independent room frontiers are classified; available interactions still require their real completion witnesses." : "The native market contact allocation differs from its reviewed state.", false, events.AsReadOnly());
    }
}
