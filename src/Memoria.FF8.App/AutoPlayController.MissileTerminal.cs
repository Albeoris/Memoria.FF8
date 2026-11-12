using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private const int MissileTerminalField = 773;
    private const string MissileTerminalProgram = "17171D657A5C3BFB93AE38EF28C08DFC9174183288724C9D88E584D692420EC5";

    private bool TryDriveMissileTerminal(GameWorld world)
    {
        EventEngine events = world.Events;
        if (!HasScriptedInputOwner(world, MissileTerminalField, allowControlled: false) || !world.HasCurrentFieldAllocation || events.FieldId != MissileTerminalField || events.NativeRuntimeProfile is not { IsVerified: true } || world.ReadAutoPlayFieldProgramHash(MissileTerminalField) != MissileTerminalProgram)
        {
            return false;
        }

        // NG-028: The native terminal requires the error ratio and data upload before exit.
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        Apply(new(PlayerActionKind.FieldButtons, 0));
        if (!HasNeutralScriptedInput(session.State.FieldButtonInput, MissileTerminalField, events.Frame))
        {
            AdvanceInputFrame(world);
            return true;
        }

        List<ScriptedInputQuery> queries = [];
        void ObserveQuery(ScriptCommand command)
        {
            if (command.FieldId == MissileTerminalField && command.Name is "KEYON" or "KEYSCAN" && command.Arguments.Count == 1)
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
        if (!HasScriptedInputOwner(world, MissileTerminalField, allowControlled: false) || events.LastFault is not null || world.CurrentDialogue is { CanConfirm: true } || queries.Count == 0 || queries.Any(query => query.ObjectId != queries[0].ObjectId || query.ScriptId != queries[0].ScriptId))
        {
            return true;
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
        ScriptedInputQuery selected = new(owner.ObjectId, owner.ScriptId, request.Operation, request.Opcode, request.Mask);
        if (!queries.Contains(selected))
        {
            return true;
        }

        ScriptedInputMetadata input = events.DiscoverObservedScriptedInput([selected], owner.ObjectId) ?? throw new NotSupportedException("The missile terminal lost its observed native key query.");
        ushort raw = (session.State.InputConfiguration ?? throw new NotSupportedException("The missile terminal requires its saved input mapping.")).RawForMappedPulse(checked((ushort)request.Mask));
        if (++run.ScriptedInputSteps > 10000)
        {
            throw new TimeoutException("The missile terminal exceeded its bounded scripted input steps.");
        }
        if (PulseScriptedInput(raw, input, allowControlled: false, prepared: true))
        {
            RecordRecentAction($"Missile terminal input {request.Mask} at {owner.ObjectId}/{owner.ScriptId}/{request.Operation}");
            ResetIdleDeadline();
        }
        return true;
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
