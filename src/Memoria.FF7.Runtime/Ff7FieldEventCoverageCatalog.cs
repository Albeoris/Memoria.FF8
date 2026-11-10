using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public enum Ff7FieldEventCoverageKind
{
    Interaction,
    Empty,
    Inactive,
    ResetHazard,
    Automatic,
    Exit
}

public sealed record Ff7FieldEventCoverageTarget(string Id, int Entity, int Script, string Availability, Ff7FieldEventCoverageKind Kind, string Reason);
public sealed record Ff7FieldEventCoveragePlan(bool Complete, string Reason, bool WalkOnly, IReadOnlyList<Ff7FieldEventCoverageTarget> Events);

/// <summary>Source-reviewed native interaction classifications; unknown scenes retain an incomplete catalog.</summary>
public static class Ff7FieldEventCoverageCatalog
{
    private const string UpstairsProgram = "64B4BFE6C3BBAFC9AED64CA64879ED9FD217215DE28466E0DC921ACF1D08A611";
    private const string DownstairsProgram = "40A6A33601F78A04E6B6D6C1F6435844DFE0B9FD55512473305CF78A07028FE0";
    private const string GardenProgram = "FFE1DEC6B475EF7B61E2AF0EAF5CBF005AD4E5BF9F5E7A37CB999F8FA58ADF70";

    public static Ff7FieldEventCoveragePlan Inspect(Ff7Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        int story = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
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
                    reason = "This native contact exits into a reset of the same room, mutually exclusive with a successful downstairs exit during this visit. It remains deferred rather than completed.";
                }
                else if (line.Entity == 9 && script == 2)
                {
                    kind = Ff7FieldEventCoverageKind.Interaction;
                    reason = "Approach the bed to complete its native repeatable dialogue before leaving.";
                    availability += $":{session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x41) & 4}";
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
}
