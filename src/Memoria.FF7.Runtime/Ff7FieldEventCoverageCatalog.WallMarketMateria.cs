using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketMateriaProgram = "6D87E25DC1D7742B4EEB71B921F7950A4A3EA4AD96CA1A207F9C1B09F412DAD4";

    internal static bool RecognizesWallMarketMateria(Ff7Session session) => session.Field.Name == "mkt_m" && session.Machine.ProgramIdentity == WallMarketMateriaProgram;

    static partial void InspectWallMarketMateria(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketMateria(session) || story != 188)
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = [];
        for (int entity = 1; entity <= 5; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.Entity == entity);
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            string predicates = entity == 5 ? $":{session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x40) & 0xC0}:{session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x41) & 1}:{session.Machine.Variables.ReadByte(FieldBank.MapByte, 5)}:{session.Machine.Variables.ReadByte(FieldBank.MapByte, 6)}" : "";
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{entry:X4}:{actor?.Model ?? -1}:{actor?.TalkRadius ?? 0}:{story}{predicates}", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Interaction, empty ? "This native party talk callback immediately returns." : "The current native keeper conversation requires physical acceptance and its owned return; its meaningful reward and per-visit branch facts are retained."));
        }
        events.Add(new("automatic:0:script:0", 0, 0, "01D4:188", Ff7FieldEventCoverageKind.Automatic, "The native director configures music, party control and per-visit conversation flags."));
        events.Add(new("exit:0:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "The original sole gateway returns to the market after the current conversation."));
        events.Add(new("alternative:5:shop:7", 5, 1, "native-story-at-least239", Ff7FieldEventCoverageKind.Inactive, "The native Shop7 branch requires the later story threshold and is unavailable at the current epoch; no shop is forced or claimed."));
        events.Add(new("alternative:5:quest:promise", 5, 1, "native-story191", Ff7FieldEventCoverageKind.Inactive, "The native request and its ASK answers belong to story191 and its quest prerequisites, which are inactive here."));
        events.Add(new("alternative:5:quest:reward", 5, 1, $"native-quest:{session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0xA2) & 0x78}", Ff7FieldEventCoverageKind.Inactive, "The native vending-item return and tiered quest reward are later conditional branches, not current available rewards."));
        foreach (int entity in new[] { 1, 2 })
        {
            for (int script = 3; script <= 5; script++)
            {
                events.Add(new($"party:{entity}:script:{script}", entity, script, $"{session.Field.Scripts.Entities[entity].ScriptOffsets[script]:X4}", Ff7FieldEventCoverageKind.Inactive, "This native party choreography is child-owned by the inactive later quest conversation."));
            }
        }
        bool actors = session.Host.Process.Actors.Select(actor => actor.Entity).Order().SequenceEqual([1, 2, 3, 4, 5]) && session.Host.Process.Actors.Single(actor => actor.Entity == 5) is { TalkEnabled: true, Visible: true };
        bool exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).SequenceEqual([195]);
        plan = new(actors && exits && session.Host.Process.Lines.Length == 0, "Current native keeper conversation, empty party callbacks, unavailable later shop/quest branches and original market return are classified.", false, events.AsReadOnly());
    }
}
