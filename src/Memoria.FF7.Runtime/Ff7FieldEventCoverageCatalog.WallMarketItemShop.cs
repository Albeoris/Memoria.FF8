using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public static partial class Ff7FieldEventCoverageCatalog
{
    private const string WallMarketItemShopProgram = "B50291B2F4636F3A3C0283E40928EEC6F069BA49F14A2F5AC45EBE4BBC3CD26B";

    internal static bool RecognizesWallMarketItemShop(Ff7Session session) => session.Field.Name == "mkt_s3" && session.Machine.ProgramIdentity == WallMarketItemShopProgram;

    static partial void InspectWallMarketItemShop(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan)
    {
        if (!RecognizesWallMarketItemShop(session) || story != 188)
        {
            return;
        }
        List<Ff7FieldEventCoverageTarget> events = new();
        byte couponFlags = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA0);
        byte medicineFlags = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA2);
        bool hasCoupon = (couponFlags & 64) != 0;
        bool couponVisit = (couponFlags & 0x47) != 0 || (medicineFlags & 7) != 0;
        if (session.Host.Process.Lines is not [{ Entity: 2, Enabled: 1 }])
        {
            plan = new(false, "The native item-shop contact differs from the reviewed visit.", false, events.AsReadOnly());
            return;
        }
        if (session.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0))
        {
            plan = new(false, "An additional native item-shop conversation requires classification.", false, events.AsReadOnly());
            return;
        }
        for (int script = 1; script <= 6; script++)
        {
            int entry = session.Field.Scripts.Entities[2].ScriptOffsets[script];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            if (!empty && script != 1)
            {
                plan = new(false, "An additional native item-shop contact requires classification.", false, events.AsReadOnly());
                return;
            }
            events.Add(new($"line:2:script:{script}", 2, script, $"{entry:X4}:story188:coupon{(couponVisit ? 1 : 0)}", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Interaction, empty ? "This original item-shop contact immediately returns." : hasCoupon ? "Use the native counter's coupon question, select Digestive and wait for the complete owned counter return; the acquired medicine/cologne flags retain this visit's availability after coupon consumption." : "Talk at the original counter, enter its native shop module and observe the complete counter return after leaving the shop."));
        }
        if (hasCoupon)
        {
            events.Add(new("alternative:7:disinfectant", 7, 3, "coupon-choice0", Ff7FieldEventCoverageKind.Inactive, "Disinfectant is an exclusive alternative to Digestive for this single native coupon; it is not claimed as completed."));
            events.Add(new("alternative:7:deodorant", 7, 3, "coupon-choice1", Ff7FieldEventCoverageKind.Inactive, "Deodorant is an exclusive alternative to Digestive for this single native coupon; it is not claimed as completed."));
        }
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (exits.Length != 1 || exits[0].gateway.DestinationField != 195)
        {
            plan = new(false, "The native item-shop return gateway differs from the reviewed directory.", false, events.AsReadOnly());
            return;
        }
        events.Add(new($"exit:{exits[0].index}:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "Return through the original market gateway after the counter interaction completes."));
        events.Add(new("automatic:0:script:0", 0, 0, "native-director", Ff7FieldEventCoverageKind.Automatic, "The original director configures music, map and locked walkmesh triangles automatically."));
        events.Add(new("automatic:1:script:0", 1, 0, "native-background", Ff7FieldEventCoverageKind.Automatic, "The background layer owner is automatic or requested by the counter's native scripts, not a separate player interaction."));
        events.Add(new("automatic:7:script:0", 7, 0, "native-shopkeeper-motion", Ff7FieldEventCoverageKind.Automatic, "The shopkeeper's original default movement/animation loop runs automatically; its direct talk callback is empty."));
        plan = new(true, "All item-shop contact slots, native shop counter, automatic owners and return gateway are classified for this visit.", false, events.AsReadOnly());
    }
}
