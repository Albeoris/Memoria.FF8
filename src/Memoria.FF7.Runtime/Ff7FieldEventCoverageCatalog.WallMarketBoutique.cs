using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketBoutiqueProgram = "E1F3AB9A23A214DDC2D133C9610A76D2E5C4076B511B48C9B379481A144312F2";

    internal static bool RecognizesWallMarketBoutique(Ff7Session session) => session.Field.Name == "mkt_s1" && session.Machine.ProgramIdentity == WallMarketBoutiqueProgram;

    static partial void InspectWallMarketBoutique(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketBoutique(session) || story != 188)
        {
            return;
        }
        int state = session.Machine.Variables.ReadByte(FieldBank.MapByte, 4);
        List<Ff7FieldEventCoverageTarget> events = [];
        for (int entity = 5; entity <= 11; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.Entity == entity);
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            Ff7FieldEventCoverageKind kind = empty ? Ff7FieldEventCoverageKind.Empty : entity == 11 ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive;
            string reason = empty ? "This native party talk callback immediately returns." : entity == 9 ? "Direct talk at the current closed-counter placement remains an unproved physical frontier; matching counter text alone does not establish equivalent animation, control or wait behavior." : entity == 10 ? "The native layout keeps the older tailor hidden at this current epoch." : "The available native customer conversation requires physical acceptance and its owned return.";
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{entry:X4}:{actor?.Model ?? -1}:{actor?.TalkEnabled ?? false}:{actor?.Visible ?? false}:{state}", kind, reason));
        }
        foreach (int entity in new[] { 2, 3 })
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
                bool empty = session.Field.Scripts.Data.Span[entry] == 0;
                events.Add(new($"line:{entity}:script:{script}", entity, script, $"{entry:X4}:{state}", empty ? Ff7FieldEventCoverageKind.Empty : entity == 2 ? Ff7FieldEventCoverageKind.Automatic : Ff7FieldEventCoverageKind.Inactive, empty ? "This native boutique contact callback immediately returns." : entity == 2 ? "The counter touch checks actual Confirm before requesting its owned conversation; an empty touch return does not complete that conversation." : "The fitting-room crossing is disabled by the current native wardrobe prerequisites."));
            }
        }
        events.Add(new("conditional:9:script:15", 9, 15, "0DAF:counter:188", Ff7FieldEventCoverageKind.Conditional, "Physically use the current boutique counter and require its actual child-owned informational return."));
        events.Add(new("automatic:0:script:0", 0, 0, "0384:188", Ff7FieldEventCoverageKind.Automatic, "The native director selects the party and requests its layout state and actor placements."));
        foreach (int script in new[] { 1, 2 })
        {
            events.Add(new($"background:1:script:{script}", 1, script, $"{session.Field.Scripts.Entities[1].ScriptOffsets[script]:X4}", Ff7FieldEventCoverageKind.Automatic, "This native background callback is owned by later dressing-room choreography."));
        }
        for (int script = 1; script <= 8; script++)
        {
            events.Add(new($"event:4:script:{script}", 4, script, $"{session.Field.Scripts.Entities[4].ScriptOffsets[script]:X4}:{state}", script == 6 ? Ff7FieldEventCoverageKind.Automatic : Ff7FieldEventCoverageKind.Inactive, script == 6 ? "The native layout decoder derives current state from story and original wardrobe flags." : "This native wardrobe request, reward, fitting-room or scoring branch belongs to later prerequisites, not this current visit."));
        }
        for (int entity = 5; entity <= 8; entity++)
        {
            events.Add(new($"alternative:party:{entity}", entity, -1, "later-wardrobe-children", Ff7FieldEventCoverageKind.Inactive, "The remaining native party scripts are child-owned by the inactive later wardrobe scenes."));
        }
        foreach (int entity in new[] { 9, 10 })
        {
            events.Add(new($"automatic:placement:{entity}", entity, -1, $"native-layout:{state}", Ff7FieldEventCoverageKind.Automatic, "The native layout owns actor placement, visibility and subsequent choreography; it is not a separate player interaction."));
        }
        events.Add(new("exit:0:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "Return through the original sole market gateway after current conversations."));
        bool contacts = session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual([2, 3]);
        bool exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).SequenceEqual([195]);
        string catalogReason = state == 0 && contacts && exits ? "The counter and customer interfaces are reviewed, but direct talk behind the current counter remains an unproved physical frontier; departure is not admitted." : "The native boutique layout differs from its reviewed current placement.";
        plan = new(false, catalogReason, false, events.AsReadOnly());
    }
}
