using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public enum Ff7FieldEventCoverageKind
{
    Interaction,
    Empty,
    Inactive,
    ResetHazard,
    Automatic,
    Exit,
    Conditional
}

public sealed record Ff7FieldEventCoverageTarget(string Id, int Entity, int Script, string Availability, Ff7FieldEventCoverageKind Kind, string Reason);
public sealed record Ff7FieldEventCoveragePlan(bool Complete, string Reason, bool WalkOnly, IReadOnlyList<Ff7FieldEventCoverageTarget> Events);

/// <summary>Source-reviewed native interaction classifications; unknown scenes retain an incomplete catalog.</summary>
public static partial class Ff7FieldEventCoverageCatalog
{
    private const string UpstairsProgram = "64B4BFE6C3BBAFC9AED64CA64879ED9FD217215DE28466E0DC921ACF1D08A611";
    private const string DownstairsProgram = "40A6A33601F78A04E6B6D6C1F6435844DFE0B9FD55512473305CF78A07028FE0";
    private const string GardenProgram = "FFE1DEC6B475EF7B61E2AF0EAF5CBF005AD4E5BF9F5E7A37CB999F8FA58ADF70";
    private const string SlumsProgram = "46398AA80E1F1E9D91B77E05E4C7504AD89F7303611E66D1BEC2BD35C4FF72E5";
    private const string ReunionProgram = "4181F8A3DDB2BE363CD6543D9E5F5B06E96B9A34E4B1D4F5ED4EA9877AA556D9";
    private const string RoadProgram = "2467803CD4D309398A0447509109D501A3E1D48B1ACAA2F5F40F32229EA865C7";
    private const string ParkProgram = "E5277315F645177347D095610B466C19383502CA803F26772A7EACAA6E0984F9";
    private const string ParkExitProgram = "3779008A52FABD2CC35BE9A98D65E1507943E22A1BF1AC0FC3177921DEEC5527";

    internal static bool RecognizesPark(Ff7Session session) => session.Field.Name == "mds6_2" && session.Machine.ProgramIdentity == ParkProgram;
    internal static bool RecognizesReunion(Ff7Session session) => session.Field.Name == "mds5_3" && session.Machine.ProgramIdentity == ReunionProgram;
    internal static bool RecognizesUpstairsScope(string scope) => scope == $"ealin_2:{UpstairsProgram}:170";

    public static bool CompletesOwnedReset(AutoPlayEventCoverageScope scope, AutoPlayEventCoverageEntry entry, Ff7NativeTickObservation observation)
    {
        if (!RecognizesUpstairsScope(scope.Id) || entry.Status != AutoPlayEventCoverageStatus.Active || observation.Field != "ealin_2" || observation.Transition?.Map != 190 || observation.TransitionOwner is not { Entity: 2, Priority: 3, Command: FieldTransitionCommand { Map: 190 } } owner)
        {
            return false;
        }
        int separator = entry.BeforeWitness?.IndexOf('/') ?? -1;
        if (separator < 0 || !long.TryParse(entry.BeforeWitness!.AsSpan(0, separator), out long generation) || generation != observation.Generation)
        {
            return false;
        }
        int expected = entry.Id switch
        {
            "line:11:script:2" => 0x09CF,
            "line:14:script:2" => 0x0944,
            _ => -1
        };
        return owner.ProgramCounter == expected;
    }

    public static Ff7FieldEventCoveragePlan Inspect(Ff7Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        Ff7FieldEventCoveragePlan? extension = null;
        InspectWallMarket(session, story, ref extension);
        InspectWallMarketRoom(session, story, ref extension);
        InspectWallMarketMateria(session, story, ref extension);
        InspectWallMarketItemShop(session, story, ref extension);
        InspectWallMarketBoutique(session, story, ref extension);
        InspectWallMarketRestaurant(session, story, ref extension);
        InspectWallMarketAbandonedItemStore(session, story, ref extension);
        InspectWallMarketMainStreet(session, story, ref extension);
        InspectWallMarketPub(session, story, ref extension);
        if (extension is not null)
        {
            return extension;
        }
        if (session.Field.Name == "mds6_3" && session.Machine.ProgramIdentity == ParkExitProgram && story == 185)
        {
            return InspectParkExit(session);
        }
        if (RecognizesPark(session) && story is 176 or 179 or 183 or 185)
        {
            return InspectPark(session, story);
        }
        if (session.Field.Name == "mds6_1" && session.Machine.ProgramIdentity == RoadProgram && story == 176)
        {
            return InspectRoad(session);
        }
        if (RecognizesReunion(session) && story is 173 or 176)
        {
            return InspectReunion(session, story == 173);
        }
        if (session.Field.Name == "mds5_1" && session.Machine.ProgramIdentity == SlumsProgram && story == 173)
        {
            return InspectSlums(session);
        }
        if (session.Field.Name == "eals_1" && session.Machine.ProgramIdentity == GardenProgram && story == 173)
        {
            return InspectGarden(session);
        }
        if (session.Field.Name == "ealin_1" && session.Machine.ProgramIdentity == DownstairsProgram && story == 170)
        {
            return InspectDownstairs(session);
        }
        if (session.Field.Name != "ealin_2" || session.Machine.ProgramIdentity != UpstairsProgram || story != 170)
        {
            return new(false, "Conditional interactions, LINE callbacks and exits in this native field state require their reviewed coverage catalog.", false, Array.Empty<Ff7FieldEventCoverageTarget>());
        }
        int[] expectedLines = [9, 10, 11, 13, 14, 16];
        if (!session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual(expectedLines))
        {
            return new(false, "The current native LINE allocation differs from the reviewed field state.", true, Array.Empty<Ff7FieldEventCoverageTarget>());
        }
        List<Ff7FieldEventCoverageTarget> events = new();
        bool drawerClaimed = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x30) & 2) != 0;
        Ff7Actor? drawer = session.Host.Process.Actors.SingleOrDefault(actor => actor.Entity == 15);
        string drawerAvailability = $"{session.Field.Scripts.Entities[15].ScriptOffsets[1]:X4}:{drawer?.Model ?? 8}:{drawer?.TalkRadius ?? 80}";
        bool drawerAvailable = !drawerClaimed && drawer is { EntityBound: true, TalkEnabled: true };
        events.Add(new("actor:15:script:1", 15, 1, drawerAvailability, drawerAvailable ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, drawerClaimed ? "The native drawer reward flag is already set; this one-time reward is unavailable." : "Inspect the drawer through its native talk handler and collect its authored items."));
        foreach (Ff7Actor actor in session.Host.Process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled))
        {
            int entry = session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1];
            if (actor.Entity != 15 && session.Field.Scripts.Data.Span[entry] != 0)
            {
                return new(false, "An additional native actor interaction requires classification before leaving this field.", true, events.AsReadOnly());
            }
        }
        foreach (Ff7Line line in session.Host.Process.Lines)
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[line.Entity].ScriptOffsets[script];
                string availability = $"{entry:X4}:{line.Enabled}";
                Ff7FieldEventCoverageKind kind;
                string reason;
                if (session.Field.Scripts.Data.Span[entry] == 0)
                {
                    kind = Ff7FieldEventCoverageKind.Empty;
                    reason = "This native contact callback immediately returns without an interaction.";
                }
                else if (line.Enabled != 1)
                {
                    kind = Ff7FieldEventCoverageKind.Inactive;
                    reason = "The native LINE owner is disabled in the current field state.";
                }
                else if (line.Entity is 11 or 14)
                {
                    kind = Ff7FieldEventCoverageKind.ResetHazard;
                    reason = "Execute this finite native room-reset interaction once; retain its exact child-owned field handoff before a successful downstairs departure.";
                    availability += ":owned-reset";
                }
                else if (line.Entity == 9 && script == 2)
                {
                    kind = Ff7FieldEventCoverageKind.Interaction;
                    reason = "Approach the bed to complete its native repeatable dialogue before leaving.";
                    availability += $":{session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x41) & 2}";
                }
                else
                {
                    return new(false, "An enabled native LINE callback requires a physical interaction classification.", true, events.AsReadOnly());
                }
                events.Add(new($"line:{line.Entity}:script:{script}", line.Entity, script, availability, kind, reason));
            }
        }
        events.Add(new("automatic:11:script:0", 11, 0, "walk-alarm", Ff7FieldEventCoverageKind.Automatic, "The native held-run alarm executes automatically; all coverage movement must walk."));
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (exits.Length != 1 || exits[0].gateway.DestinationField != 188)
        {
            return new(false, "The current native exit directory differs from its reviewed coverage state.", true, events.AsReadOnly());
        }
        events.Add(new("exit:0:map:188", -1, -1, "native-gateway:188", Ff7FieldEventCoverageKind.Exit, "The native downstairs gateway is a navigation goal after the currently available interactions finish."));
        return new(true, "The native drawer, bed, inactive callbacks, reset hazards, automatic alarm and downstairs exit are classified.", true, events.AsReadOnly());
    }

    private static Ff7FieldEventCoveragePlan InspectDownstairs(Ff7Session session)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (!session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual(new[] { 13, 14 }))
        {
            return new(false, "The native downstairs LINE allocation differs from its reviewed state.", false, events.AsReadOnly());
        }
        foreach (Ff7Actor actor in session.Host.Process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled))
        {
            if (session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0)
            {
                return new(false, "An additional native downstairs actor interaction requires classification.", false, events.AsReadOnly());
            }
        }
        foreach (Ff7Line line in session.Host.Process.Lines)
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[line.Entity].ScriptOffsets[script];
                Ff7FieldEventCoverageKind kind;
                string reason;
                if (session.Field.Scripts.Data.Span[entry] == 0)
                {
                    kind = Ff7FieldEventCoverageKind.Empty;
                    reason = "The native downstairs contact callback immediately returns.";
                }
                else if (line.Enabled != 1)
                {
                    kind = Ff7FieldEventCoverageKind.Inactive;
                    reason = "The native downstairs contact owner is disabled by its story predicate.";
                }
                else if (line.Entity == 14 && script == 2)
                {
                    kind = Ff7FieldEventCoverageKind.Interaction;
                    reason = "Approach the stairs for Cloud's native warning and return movement before departing.";
                }
                else
                {
                    return new(false, "An enabled downstairs contact requires a reviewed native classification.", false, events.AsReadOnly());
                }
                events.Add(new($"line:{line.Entity}:script:{script}", line.Entity, script, $"{entry:X4}:{line.Enabled}", kind, reason));
            }
        }
        var exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).Select(gateway => (int)gateway.DestinationField).Order().ToArray();
        if (!exits.SequenceEqual(new[] { 187, 190 }))
        {
            return new(false, "The downstairs native exit directory differs from its reviewed state.", false, events.AsReadOnly());
        }
        events.Add(new("exit:0:map:187", -1, -1, "native-gateway:187", Ff7FieldEventCoverageKind.Exit, "Leave by the native outside gateway after current interactions complete."));
        events.Add(new("exit:1:map:190", -1, -1, "native-gateway:190", Ff7FieldEventCoverageKind.Exit, "Returning upstairs is an alternative visit handoff, mutually exclusive with outside departure in this visit."));
        events.Add(new("automatic:0:script:0", 0, 0, "story:170", Ff7FieldEventCoverageKind.Automatic, "The native director's current story branch returns without starting another scene."));
        events.Add(new("automatic:15:script:0", 15, 0, "story:170", Ff7FieldEventCoverageKind.Automatic, "The native background owner executes automatically and exposes no contact interaction."));
        return new(true, "The native stair warning, disabled and empty contacts, autonomous owners and both alternative exits are classified.", false, events.AsReadOnly());
    }

    private static Ff7FieldEventCoveragePlan InspectGarden(Ff7Session session)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (!session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual(new[] { 8, 9, 10, 11 }))
        {
            return new(false, "The garden's native LINE allocation differs from its reviewed state.", false, events.AsReadOnly());
        }
        byte rewards = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x30);
        foreach (int entity in new[] { 5, 6 })
        {
            Ff7Actor actor = session.Host.GetActor(entity);
            bool claimed = (rewards & (1 << (entity - 3))) != 0;
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[1];
            events.Add(new($"actor:{entity}:script:1", entity, 1, $"{entry:X4}:{actor.Model}:{actor.TalkRadius}", !claimed && actor.TalkEnabled ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, claimed ? "The native one-time garden reward has already been claimed." : entity == 5 ? "Collect the garden's native Ether item through its actor interaction." : "Collect the garden's native Cover materia through its actor interaction."));
        }
        foreach (Ff7Actor actor in session.Host.Process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && actor.Entity is not (5 or 6)))
        {
            if (session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0)
            {
                return new(false, "An additional garden actor interaction requires native classification.", false, events.AsReadOnly());
            }
        }
        foreach (Ff7Line line in session.Host.Process.Lines)
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[line.Entity].ScriptOffsets[script];
                Ff7FieldEventCoverageKind kind;
                string reason;
                if (session.Field.Scripts.Data.Span[entry] == 0)
                {
                    kind = Ff7FieldEventCoverageKind.Empty;
                    reason = "The native garden contact callback immediately returns.";
                }
                else if (line.Enabled != 1)
                {
                    kind = Ff7FieldEventCoverageKind.Inactive;
                    reason = "The native garden contact owner is disabled in the current story state.";
                }
                else if (line.Entity == 9 && script == 3)
                {
                    kind = Ff7FieldEventCoverageKind.Exit;
                    reason = "This native crossing enters the house, an alternative visit handoff to departure toward the slums.";
                }
                else if (line.Entity == 8 && script is 5 or 6)
                {
                    kind = Ff7FieldEventCoverageKind.Interaction;
                    reason = script == 5 ? "Enter the native save-point area and complete any original instruction message." : "Leave the native save-point area so its exit handler restores the native permission bits.";
                }
                else if (line.Entity == 10 && script == 5)
                {
                    kind = Ff7FieldEventCoverageKind.Interaction;
                    reason = "Complete the native garden departure message triggered on entering this area.";
                }
                else
                {
                    return new(false, "An enabled garden LINE callback requires a reviewed physical policy.", false, events.AsReadOnly());
                }
                string availability = line.Entity == 10 ? $"{entry:X4}:story173" : $"{entry:X4}:{line.Enabled}";
                events.Add(new($"line:{line.Entity}:script:{script}", line.Entity, script, availability, kind, reason));
            }
        }
        var exits = session.Field.Triggers.Gateways.Where(gateway => gateway.DestinationField != 32767).ToArray();
        if (exits.Length != 1 || exits[0].DestinationField != 177)
        {
            return new(false, "The garden's native exit directory differs from its reviewed state.", false, events.AsReadOnly());
        }
        events.Add(new("exit:0:map:177", -1, -1, "native-gateway:177", Ff7FieldEventCoverageKind.Exit, "The slums gateway is a navigation goal after the native garden interactions finish."));
        events.Add(new("automatic:0:script:0", 0, 0, "story:173", Ff7FieldEventCoverageKind.Automatic, "The native director owns the story transition and automatic party scene; it is not a physical interaction request."));
        return new(true, "Both garden rewards, save-point enter/leave, departure message, alternative house crossing and outside exit are classified.", false, events.AsReadOnly());
    }

    private static Ff7FieldEventCoveragePlan InspectSlums(Ff7Session session)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (session.Host.Process.Lines.Length != 0)
        {
            return new(false, "The native slums LINE allocation differs from its reviewed story state.", false, events.AsReadOnly());
        }
        foreach (Ff7Actor actor in session.Host.Process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled))
        {
            int entry = session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1];
            if (session.Field.Scripts.Data.Span[entry] == 0)
            {
                continue;
            }
            if (actor.Entity != 15)
            {
                return new(false, "An additional slums actor interaction requires a reviewed native classification.", false, events.AsReadOnly());
            }
            events.Add(new("actor:15:script:1", 15, 1, $"{entry:X4}:{actor.Model}:{actor.TalkRadius}", Ff7FieldEventCoverageKind.Interaction, "Interact with the dog through its native party-facing, bark, animation and movement handler."));
        }
        int[] expectedExits = [172, 174, 176, 178, 179, 180, 187];
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (!exits.Select(value => (int)value.gateway.DestinationField).Order().SequenceEqual(expectedExits))
        {
            return new(false, "The native slums exit directory differs from its reviewed state.", false, events.AsReadOnly());
        }
        foreach (var (gateway, index) in exits)
        {
            events.Add(new($"exit:{index}:map:{gateway.DestinationField}", -1, -1, $"native-gateway:{gateway.DestinationField}", Ff7FieldEventCoverageKind.Exit, gateway.DestinationField == 172 ? "Continue through the native road exit after available interactions complete." : "This gateway is an alternative native visit handoff, mutually exclusive with the road departure during this visit."));
        }
        events.Add(new("automatic:0:script:0", 0, 0, "native-background", Ff7FieldEventCoverageKind.Automatic, "The native television background owner runs automatically without a player interaction."));
        return new(true, "The native dog interaction, inactive story-conditioned residents, background owner and seven alternative exits are classified.", false, events.AsReadOnly());
    }

    static partial void InspectWallMarket(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketRoom(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketMateria(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketItemShop(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketBoutique(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketRestaurant(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);
    static partial void InspectWallMarketAbandonedItemStore(Ff7Session session, int story, ref Ff7FieldEventCoveragePlan? plan);

    private static Ff7FieldEventCoveragePlan InspectParkExit(Ff7Session session)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (session.Host.Process.Lines is not [{ Entity: 4, Enabled: 1 }] || session.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0))
        {
            return new(false, "The native park departure contacts differ from the reviewed state.", false, events.AsReadOnly());
        }
        for (int script = 1; script <= 6; script++)
        {
            int entry = session.Field.Scripts.Entities[4].ScriptOffsets[script];
            bool empty = session.Field.Scripts.Data.Span[entry] == 0;
            if (!empty && script != 5)
            {
                return new(false, "An additional park departure contact requires classification.", false, events.AsReadOnly());
            }
            events.Add(new($"line:4:script:{script}", 4, script, $"{entry:X4}:1", empty ? Ff7FieldEventCoverageKind.Empty : Ff7FieldEventCoverageKind.Exit, empty ? "This native corridor contact callback immediately returns." : "The native Enter callback returns to park map192 at this story state, an alternative to the forward gateway."));
        }
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (exits.Length != 1 || exits[0].gateway.DestinationField != 195)
        {
            return new(false, "The park departure gateway directory differs from the reviewed state.", false, events.AsReadOnly());
        }
        events.Add(new($"exit:{exits[0].index}:map:195", -1, -1, "native-gateway:195", Ff7FieldEventCoverageKind.Exit, "Continue through the original forward gateway after the empty corridor contact catalog is checked."));
        events.Add(new("automatic:0:script:0", 0, 0, "native-background", Ff7FieldEventCoverageKind.Automatic, "The native director configures map music and global audio channel values without a player interaction."));
        return new(true, "All corridor contact slots, empty party conversations, native audio setup and both alternative handoffs are classified.", false, events.AsReadOnly());
    }

    internal static Ff7FieldEventCoveragePlan InspectPark(Ff7Session session, int story)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (session.Host.Process.Lines.Length != 0 || session.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && actor.Entity != 2 && session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0))
        {
            return new(false, "An additional native park contact requires classification.", false, events.AsReadOnly());
        }
        int talkPhase = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0D);
        events.Add(new("conditional:0:phase:entry", 0, 3, "033B:entry176", story == 176 ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive, "The native arrival scene must reach its unlocked talk intermission; later progress does not replay it."));
        events.Add(new("actor:2:script:1", 2, 1, $"0818:intermission:{talkPhase >= 1}", story == 179 && talkPhase == 1 ? Ff7FieldEventCoverageKind.Interaction : Ff7FieldEventCoverageKind.Inactive, "Aerith's original talk callback advances the native intermission predicate only when it is available."));
        events.Add(new("conditional:0:phase:departure", 0, 3, $"0401:talk-complete:{talkPhase >= 2}", story is 179 or 183 && talkPhase >= 2 ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive, "The native post-talk scene must reach its final return and release controls before departure."));
        int[] expectedExits = [191, 194];
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (!exits.Select(value => (int)value.gateway.DestinationField).Order().SequenceEqual(expectedExits))
        {
            return new(false, "The native park gateway directory differs from its reviewed state.", false, events.AsReadOnly());
        }
        foreach (var (gateway, index) in exits)
        {
            events.Add(new($"exit:{index}:map:{gateway.DestinationField}", -1, -1, $"native-gateway:{gateway.DestinationField}", Ff7FieldEventCoverageKind.Exit, gateway.DestinationField == 194 ? "Continue through the native departure gateway after the park scene finishes." : "This native gateway is an alternative return toward the road."));
        }
        return new(true, "The park entry scene, available intermission conversation, post-talk scene and alternative exits are classified.", false, events.AsReadOnly());
    }

    private static Ff7FieldEventCoveragePlan InspectRoad(Ff7Session session)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (!session.Host.Process.Lines.Select(line => line.Entity).Order().SequenceEqual(new[] { 6, 7 }) || session.Host.Process.Lines.Single(line => line.Entity == 6).Enabled != 0 || session.Host.Process.Lines.Single(line => line.Entity == 7).Enabled != 1)
        {
            return new(false, "The native road contact allocation differs from the reviewed story state.", false, events.AsReadOnly());
        }
        if (session.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0))
        {
            return new(false, "An additional native road conversation requires classification.", false, events.AsReadOnly());
        }
        foreach (Ff7Line line in session.Host.Process.Lines)
        {
            for (int script = 1; script <= 6; script++)
            {
                int entry = session.Field.Scripts.Entities[line.Entity].ScriptOffsets[script];
                Ff7FieldEventCoverageKind kind;
                string reason;
                if (session.Field.Scripts.Data.Span[entry] == 0)
                {
                    kind = Ff7FieldEventCoverageKind.Empty;
                    reason = "This native road contact callback immediately returns.";
                }
                else if (line.Entity == 6)
                {
                    kind = Ff7FieldEventCoverageKind.Inactive;
                    reason = "The native road scene contact is disabled before story239.";
                }
                else if (script == 5)
                {
                    kind = Ff7FieldEventCoverageKind.Exit;
                    reason = "Entering this native contact transfers to map192 at story176; its later-story branch instead selects map193.";
                }
                else
                {
                    return new(false, "An enabled road contact callback requires a reviewed policy.", false, events.AsReadOnly());
                }
                events.Add(new($"line:{line.Entity}:script:{script}", line.Entity, script, $"{entry:X4}:{line.Enabled}", kind, reason));
            }
        }
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (exits.Length != 1 || exits[0].gateway.DestinationField != 172)
        {
            return new(false, "The native road gateway directory differs from the reviewed state.", false, events.AsReadOnly());
        }
        events.Add(new($"exit:{exits[0].index}:map:172", -1, -1, "native-gateway:172", Ff7FieldEventCoverageKind.Exit, "This native gateway returns toward the slums and is an alternative to the forward contact handoff."));
        events.Add(new("automatic:0:script:0", 0, 0, "story:176", Ff7FieldEventCoverageKind.Automatic, "The director configures native music and map state; its story239 and245 scenes are inactive."));
        return new(true, "Both road contact owners, empty conversations, inactive later scenes and alternative native handoffs are classified.", false, events.AsReadOnly());
    }

    internal static Ff7FieldEventCoveragePlan InspectReunion(Ff7Session session, bool available)
    {
        List<Ff7FieldEventCoverageTarget> events = new();
        if (session.Host.Process.Lines.Length != 0 || session.Host.Process.Actors.Any(actor => actor.EntityBound && !actor.PlayerControlled && actor.TalkEnabled && session.Field.Scripts.Data.Span[session.Field.Scripts.Entities[actor.Entity].ScriptOffsets[1]] != 0))
        {
            return new(false, "An additional native reunion-field contact requires classification.", false, events.AsReadOnly());
        }
        events.Add(new("conditional:2:script:0", 2, 0, "030D:triangles57/47", available ? Ff7FieldEventCoverageKind.Conditional : Ff7FieldEventCoverageKind.Inactive, available ? "Approach the native reunion triangle and observe Aerith's original scene through its final return." : "The native story predicate no longer exposes the reunion event; completed progress must not be replayed."));
        int[] expectedExits = [173, 177, 191];
        var exits = session.Field.Triggers.Gateways.Select((gateway, index) => (gateway, index)).Where(value => value.gateway.DestinationField != 32767).ToArray();
        if (!exits.Select(value => (int)value.gateway.DestinationField).Order().SequenceEqual(expectedExits))
        {
            return new(false, "The native reunion-field exit directory differs from its reviewed state.", false, events.AsReadOnly());
        }
        foreach (var (gateway, index) in exits)
        {
            events.Add(new($"exit:{index}:map:{gateway.DestinationField}", -1, -1, $"native-gateway:{gateway.DestinationField}", Ff7FieldEventCoverageKind.Exit, gateway.DestinationField == 191 ? "Continue toward the park after the original reunion finishes." : "This native gateway is an alternative visit handoff to park departure during this visit."));
        }
        events.Add(new("automatic:0:script:0", 0, 0, "native-director", Ff7FieldEventCoverageKind.Automatic, "The native director configures map, music and battle permission automatically."));
        return new(true, "The native triangle-conditioned reunion, inactive resident conversations, director and three exits are classified.", false, events.AsReadOnly());
    }
}
