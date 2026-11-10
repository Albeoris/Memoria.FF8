using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketPubProgram = "183300C8ADC56D0A9BE1DB6E87E5B03EDE28CA9B528FC16A1DFC3A842A4803E3";

    private static void InspectWallMarketPub(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (session.Field.Name != "mktpb" || session.Machine.ProgramIdentity != WallMarketPubProgram || story != 188)
        {
            return;
        }
        int reward = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0) & 7;
        int medicines = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2) & 7;
        List<Ff7FieldEventCoverageTarget> events = [];
        for (int entity = 5; entity <= 16; entity++)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(value => value.Entity == entity);
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            bool active = actor is { Visible: true, TalkEnabled: true };
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{session.Machine.ProgramIdentity}:{entry:X4}:{reward}:{medicines}:{active}", empty ? Ff7FieldEventCoverageKind.Empty : active ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, empty ? "This native party talk callback immediately returns." : active ? "This native pub conversation remains a required physical owner; medicine and wardrobe choices retain their original prerequisites." : "The native current pub placement hides or disables this actor; no interaction is fabricated."));
        }
        for (int script = 1; script <= 6; script++)
        {
            int entry = session.Field.Scripts.Entities[4].ScriptOffsets[script];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            events.Add(new($"line:4:script:{script}", 4, script, $"{entry:X4}:{reward}", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Conditional, empty ? "This native toilet-door contact callback immediately returns." : "The native knock and leave owners open or close the original floor triangles; their current physical acceptance and ordering remain under review."));
        }
        for (int entity = 0; entity <= 3; entity++)
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, "native-pub-background", Ff7FieldEventCoverageKind.Automatic, "The original director, television, door and background retain native execution ownership."));
        }
        events.Add(new("alternative:11:other-medicines", 11, 1, $"native-medicine:{medicines}:reward:{reward}", Ff7FieldEventCoverageKind.Inactive, "The native mutually exclusive lower medicine rewards are not forced when the legitimate best medicine is available."));
        events.Add(new("alternative:14:wardrobe", 14, 1, "native-wardrobe-prerequisites", Ff7FieldEventCoverageKind.Inactive, "The elaborate native wardrobe scene requires its original quest predicates; it is not seeded during the current medicine visit."));
        events.Add(new("exit:map:205", -1, -1, "native-pub-return", Ff7FieldEventCoverageKind.Exit, "The original sole gateway returns to the main street; a nested regional return receipt remains required."));
        plan = new(false, "The pub medicine, toilet door and resident interfaces are under native review; no complete local or nested regional visit is admitted.", false, events.AsReadOnly());
    }
}
