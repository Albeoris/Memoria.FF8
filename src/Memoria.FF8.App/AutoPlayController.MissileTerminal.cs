using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private const int MissileTerminalField = 773;
    private const string MissileTerminalProgram = "17171D657A5C3BFB93AE38EF28C08DFC9174183288724C9D88E584D692420EC5";

    private bool TryDriveMissileBaseInput(GameWorld world)
    {
        EventEngine events = world.Events;
        int field = events.FieldId;
        string? program = field switch
        {
            777 when events.Variables.GetValueOrDefault(256) == 467 => AutoPlayTraversalPolicy.MissileLauncherProgram,
            MissileTerminalField => MissileTerminalProgram,
            _ => null
        };
        if (program is null || !HasScriptedInputOwner(world, field, allowControlled: false) || !world.HasCurrentFieldAllocation || events.NativeRuntimeProfile is not { IsVerified: true } || world.ReadAutoPlayFieldProgramHash(field) != program)
        {
            return false;
        }
        ScriptedInputQuery? launcherQuery = field == 777 ? new(10, 108, 1422, "KEYON", 128) : null;
        if (launcherQuery is not null && events.DiscoverScriptedInput(launcherQuery.ObjectId)?.Queries.Contains(launcherQuery) != true)
        {
            return false;
        }

        // NG-028: The native terminal requires the error ratio and data upload before exit.
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        Apply(new(PlayerActionKind.FieldButtons, 0));
        if (!HasNeutralScriptedInput(session.State.FieldButtonInput, field, events.Frame))
        {
            AdvanceInputFrame(world);
            return true;
        }

        List<ScriptedInputQuery> queries = [];
        void ObserveQuery(ScriptCommand command)
        {
            if (command.FieldId == field && command.Name is "KEYON" or "KEYSCAN" && command.Arguments.Count == 1)
            {
                queries.Add(new(command.ObjectId, command.ScriptId, command.Operation, command.Name, unchecked((ushort)command.Arguments[0])));
            }
        }
        events.CommandExecuted += ObserveQuery;
        try
        {
            AdvanceInputFrame(world);
        }
        finally
        {
            events.CommandExecuted -= ObserveQuery;
        }
        if (!HasScriptedInputOwner(world, field, allowControlled: false) || events.LastFault is not null || world.CurrentDialogue is { CanConfirm: true } || queries.Count == 0)
        {
            return true;
        }

        ScriptedInputQuery? selected = launcherQuery ?? SelectMissileTerminalInput(events, queries);
        if (selected is null || !queries.Contains(selected))
        {
            return true;
        }
        ScriptedInputMetadata input = events.DiscoverObservedScriptedInput([selected], selected.ObjectId) ?? throw new NotSupportedException("The missile-base input lost its observed native key query.");
        if (++run.ScriptedInputSteps > 10000)
        {
            throw new TimeoutException("The missile-base sequence exceeded its bounded scripted input steps.");
        }
        if (PulseScriptedInput(input.Masks.Single(), input, allowControlled: false, prepared: true))
        {
            RecordRecentAction($"Missile-base input {selected.Mask} at {selected.ObjectId}/{selected.ScriptId}/{selected.Operation}");
            ResetIdleDeadline();
        }
        return true;
    }

    private static ScriptedInputQuery? SelectMissileTerminalInput(EventEngine events, List<ScriptedInputQuery> queries)
    {
        if (queries.Any(query => query.ObjectId != queries[0].ObjectId || query.ScriptId != queries[0].ScriptId))
        {
            return null;
        }
        ScriptedInputQuery owner = queries[0];
        int progress = events.EventMemory.Read(484, 1).RequireExact();
        bool ratioSet = (progress & 1) != 0;
        bool uploaded = (progress & 2) != 0;
        (int Operation, int Mask, string Opcode) request = (owner.ObjectId, owner.ScriptId) switch
        {
            (0, 1) when queries.All(query => query.Operation == 251) && events.Dialogues.Any(dialogue => dialogue is { ObjectId: 0, ScriptId: 1, Operation: 245, MessageId: 9 }) => (251, 64, "KEYON"),
            (0, 1) => MissileTerminalMenuInput(events.EventMemory.Read(1024, 1).RequireExact(), ratioSet && uploaded ? 3 : 0, 105, 121, 137),
            (1, 8) => MissileTerminalMenuInput(events.EventMemory.Read(1025, 1).RequireExact(), !ratioSet ? 1 : !uploaded ? 2 : 3, 425, 441, 457),
            (2, 13) => events.EventMemory.Read(482, 2, signed: true).RequireExact() <= -150 ? (658, 64, "KEYON") : (693, 8192, "KEYSCAN"),
            (2, 14) => events.EventMemory.Read(1028, 1).RequireExact() == 1 ? (753, 64, "KEYON") : (745, 32768, "KEYON"),
            _ => throw new NotSupportedException("The missile terminal reached an unsupported native input prompt.")
        };
        return new(owner.ObjectId, owner.ScriptId, request.Operation, request.Opcode, request.Mask);
    }

    private static (int Operation, int Mask, string Opcode) MissileTerminalMenuInput(int current, int desired, int down, int up, int confirm)
    {
        if (current is < 0 or > 3)
        {
            throw new InvalidDataException("The missile terminal menu cursor is outside its original four entries.");
        }
        int difference = (desired - current + 4) % 4;
        return difference switch
        {
            0 => (confirm, 64, "KEYON"),
            1 or 2 => (down, 16384, "KEYON"),
            _ => (up, 4096, "KEYON")
        };
    }
}
