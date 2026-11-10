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
            bool active = actor is { Visible: true, TalkEnabled: true };
            string availability = $"{entry:X4}:188";
            Ff7FieldEventCoverageKind kind = Ff7FieldEventCoverageKind.Interaction;
            string reason = "Speak with this native resident and observe its owned return.";
            if (session.Field.Scripts.Data.Span[entry] == 0)
            {
                kind = Ff7FieldEventCoverageKind.Empty;
                reason = "This native party talk callback immediately returns.";
            }
            else if (entity == 11)
            {
                availability += $":reward:{reward}:medicine:{medicines}";
                kind = active && reward == 0 ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive;
                reason = reward != 0 ? "The native medicine reward is already claimed." : "The medicine offer requires the open native door; its question and outcome must be observed separately from a guard return.";
            }
            else if (entity == 12 && reward != 0)
            {
                availability += ":rewarded";
                kind = Ff7FieldEventCoverageKind.Empty;
                reason = "The rewarded man's current talk branches directly to its native return.";
            }
            else if (!active)
            {
                kind = Ff7FieldEventCoverageKind.Inactive;
                reason = "The native current pub placement hides or disables this actor.";
            }
            events.Add(new($"actor:{entity}:script:1", entity, 1, availability, kind, reason));
        }
        bool doorEnabled = session.Host.Process.Lines.Any(line => line is { Entity: 4, Enabled: 1 });
        for (int script = 1; script <= 6; script++)
        {
            int entry = session.Field.Scripts.Entities[4].ScriptOffsets[script];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            string availability = script == 1 ? $"{entry:X4}:188:rewarded:{reward != 0}" : $"{entry:X4}:188";
            Ff7FieldEventCoverageKind kind = empty ? Ff7FieldEventCoverageKind.Empty : doorEnabled ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive;
            string reason = empty ? "This native toilet-door contact callback immediately returns." : script == 1 ? "Physically knock at the enabled toilet door and distinguish the native opening sequence from an already-open guard return." : "Physically leave the door contact and distinguish its native closing sequence from its already-closed return.";
            events.Add(new($"line:4:script:{script}", 4, script, availability, kind, reason));
        }
        for (int entity = 0; entity <= 3; entity++)
        {
            events.Add(new($"automatic:{entity}:script:0", entity, 0, "native-pub-background", Ff7FieldEventCoverageKind.Automatic, "The original director, television, door and background retain native execution ownership."));
        }
        bool lowerMedicineAvailable = reward == 0 && (medicines & 4) == 0 && (medicines & 3) != 0;
        events.Add(new("alternative:11:other-medicines", 11, 1, $"native-medicine:{medicines}:reward:{reward}", lowerMedicineAvailable ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive, "The mutually exclusive lower medicine outcomes require an unclaimed reward, no Digestive and an owned lower medicine."));
        events.Add(new("alternative:14:wardrobe", 14, 1, "native-wardrobe-prerequisites", Ff7FieldEventCoverageKind.Inactive, "The elaborate native wardrobe scene requires its original quest predicates."));
        events.Add(new("exit:map:205", -1, -1, "native-pub-return", Ff7FieldEventCoverageKind.Exit, "The original sole gateway returns to the main street."));
        plan = new(false, "The current pub contacts and reward availability are classified; the coverage observer still needs to distinguish door and medicine outcomes before admitting a nested visit.", false, events.AsReadOnly());
    }
}
