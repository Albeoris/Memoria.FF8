using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketInnProgram = "FE9D6E69F8B2CF6B30BBFAC051445CFC2D02E5DDD719849DB1F9A9B1B178AEDB";

    internal static bool RecognizesWallMarketInn(Ff7Session session) => session.Field.Name == "mktinn" && session.Machine.ProgramIdentity == WallMarketInnProgram;

    static partial void InspectWallMarketRoom(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketInn(session) || story != 188)
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        foreach (int entity in new[] { 7, 8, 9, 10 })
        {
            Ff7Line? line = session.Host.Process.Lines.SingleOrDefault(line => line.Entity == entity);
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
                bool empty = session.Field.Scripts.Data.Span[entry] == 0;
                Ff7FieldEventCoverageKind kind = empty ? Ff7FieldEventCoverageKind.Empty : entity == 7 ? Ff7FieldEventCoverageKind.Automatic : Ff7FieldEventCoverageKind.Interaction;
                events.Add(new($"line:{entity}:script:{script}", entity, script, $"{entry:X4}:{line?.Enabled ?? -1}", kind, empty ? "This native inn contact callback immediately returns." : entity == 7 ? "The counter contact checks physical Confirm before requesting its child conversation; an empty touch return alone does not complete the inn event." : "This native guest-door Enter/Leave callback requires physical contact and its owned return."));
            }
        }
        events.Add(new("conditional:11:script:3", 11, 3, "0666:decline:188", Ff7FieldEventCoverageKind.Conditional, "Physically use the inn counter and decline its native question; require its child-owned return and released controls, without purchasing a rest."));
        for (int entity = 13; entity <= 17; entity++)
        {
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[1]:X4}", Ff7FieldEventCoverageKind.Empty, "This native party/innkeeper talk callback immediately returns; the counter LINE owns the actual question."));
        }
        for (int entity = 1; entity <= 5; entity++)
        {
            for (int script = 1; script <= 2; script++)
            {
                events.Add(new($"background:{entity}:script:{script}", entity, script, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[script]:X4}", Ff7FieldEventCoverageKind.Automatic, "This native door-background callback is child-owned presentation rather than a separate player event."));
            }
        }
        foreach (int entity in new[] { 0, 6 })
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[0]:X4}", Ff7FieldEventCoverageKind.Automatic, "This native owner initializes map music, locked walkmesh and door backgrounds."));
        }
        events.Add(new("alternative:11:stay", 11, 3, "native-stay-answer", Ff7FieldEventCoverageKind.Inactive, "Paying and resting is an exclusive alternative to declining the same question. This visit does not claim that purchase, healing or its dependent scene."));
        events.Add(new("alternative:12:rest", 12, 1, "paid-rest-child", Ff7FieldEventCoverageKind.Inactive, "The native rest child is requested only after the paid answer, which this visit does not select."));
        events.Add(new("alternative:13:script:4", 13, 4, $"quest:{session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 8}", Ff7FieldEventCoverageKind.Inactive, "This native vending scene is nested in the paid-rest branch and its separate quest predicate; no purchase or later quest completion is claimed."));
        foreach (int script in new[] { 3, 4 })
        {
            events.Add(new($"background:17:script:{script}", 17, script, $"{session.Field.Scripts.Entities[17].ScriptOffsets[script]:X4}", Ff7FieldEventCoverageKind.Automatic, "This native innkeeper visibility callback is requested by the unselected paid-rest scene."));
        }
        events.Add(new("exit:0:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "The original sole gateway returns to the market after local events."));
        bool contacts = session.Host.Process.Lines.Select(line => (line.Entity, Enabled: (int)line.Enabled)).OrderBy(line => line.Entity).SequenceEqual([(7, 1), (8, 1), (9, 1), (10, 1)]);
        bool exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).SequenceEqual([195]);
        plan = new(contacts && exits, "The native inn counter, guest-door contacts, empty conversations, exclusive paid-rest branches and original return are classified; required events still need their real owned returns.", false, events.AsReadOnly());
    }
}
