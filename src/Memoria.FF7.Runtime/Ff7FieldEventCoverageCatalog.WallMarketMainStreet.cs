using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7WallMarketTourCatalog
{
    internal static Ff7WallMarketTourPlan InspectMainStreet(Ff7Session session)
    {
        if (!Ff7FieldEventCoverageCatalog.RecognizesMainStreet(session, session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)))
        {
            return new("mrkt1", false, "This native main-street epoch requires its own reviewed tour frontier.", []);
        }
        Ff7WallMarketTourEdge[] edges =
        [
            new("main-street:206", 206, "colne_1", AutoPlayTourKind.StoryExit, "The native later-story route enters the mansion exterior through this doorway.", false, null),
            new("main-street:222", 222, "mrkt4", AutoPlayTourKind.OptionalGameplay, "The native adjoining district requires classification of current events and physical routes.", false, null),
            new("main-street:196", 196, "mkt_w", AutoPlayTourKind.OptionalGameplay, "The native weapon shop requires review of its local gameplay and return.", false, null),
            new("main-street:197", 197, "mkt_mens", AutoPlayTourKind.OptionalGameplay, "The native gym requires review of its local gameplay and return.", false, null),
            new("main-street:195", 195, "mrkt2", AutoPlayTourKind.Return, "The native doorway returns to the outer market after required child visits.", false, null),
            new("main-street:204", 204, "mktpb", AutoPlayTourKind.OptionalGameplay, "The native pub's current dialogue and return require a complete local catalog.", false, 205)
        ];
        int[] destinations = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).ToArray();
        if (!destinations.SequenceEqual(edges.Select(edge => edge.DestinationField)))
        {
            return new("mrkt1", false, "The native main-street gateway directory differs from the reviewed frontier.", []);
        }
        return new("mrkt1", false, "Native main-street child catalogs and covered returns remain pending.", edges);
    }
}

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketMainStreetProgram = "FD73131AC3CEDBD9811B0D59A4255E20656A38B20C1F69CF66DDF86CADF079F1";

    internal static bool RecognizesMainStreet(Ff7Session session, int story) => session.Field.Name == "mrkt1" && session.Machine.ProgramIdentity == WallMarketMainStreetProgram && story == 188;

    private static void InspectWallMarketMainStreet(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesMainStreet(session, story))
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        for (int entity = 10; entity <= 19; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            bool current = entity is 15 or 16;
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{entry:X4}:188", empty ? Ff7FieldEventCoverageKind.Empty : current ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, empty ? "This native actor talk slot immediately returns." : current ? "This available native resident owns a distinct talk request and return around the current shared conversation child." : "This actor belongs to native later-story placement and is hidden at the current epoch."));
        }
        foreach (int entity in new[] { 8, 9 })
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
                bool empty = session.Field.Scripts.Data.Span[entry] == 0;
                bool available = entity == 8 && script is 5 or 6;
                events.Add(new($"line:{entity}:script:{script}", entity, script, $"{entry:X4}:188", empty ? Ff7FieldEventCoverageKind.Empty : available ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, empty ? "This native contact slot immediately returns." : available ? "Physically enter and leave this enabled native door contact, requiring each owned return." : "This contact belongs to the disabled later-story native scene."));
            }
        }
        for (int entity = 0; entity <= 7; entity++)
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, "native-main-street-owner:188", Ff7FieldEventCoverageKind.Automatic, "Native director, background doors, ambience and requested dialogue children retain their own execution."));
        }
        events.Add(new("alternative:6:later-conversation", 6, 2, "native-story-at-least236", Ff7FieldEventCoverageKind.Inactive, "The native residents select the later conversation only from story236 onward."));
        events.Add(new("alternative:7:follow-scene", 7, 1, "native-later-scene", Ff7FieldEventCoverageKind.Inactive, "The follow-me scene belongs to the disabled later-story contact and placement."));
        foreach (int destination in session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField))
        {
            events.Add(new($"exit:map:{destination}", -1, -1, $"native-gateway:{destination}:188", Ff7FieldEventCoverageKind.Exit, destination == 195 ? "This original gateway returns to the outer market." : "This distinct native doorway is an unresolved regional frontier; local resident conversations do not complete its destination."));
        }
        bool complete = session.Host.Process.Lines.Select(line => (line.Entity, (int)line.Enabled)).OrderBy(line => line.Entity).SequenceEqual([(8, 1), (9, 0)]);
        plan = new(complete, complete ? "Native main-street conversations and door contact are classified locally; the regional directory owns child visits and their covered returns." : "The native main-street LINE allocation differs from the reviewed local state.", false, events.AsReadOnly());
    }
}
