using System.Numerics;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeDoorLinePoint(short X, short Y, short Z)
{
    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);
}

public sealed record NativeSetLineReceipt(int Version, int FieldId, string ProgramHash, int ObjectId, byte NativeDoorOrdinal, int ScriptId, int Operation, long Frame, NativeDoorLinePoint Start, NativeDoorLinePoint End);

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSetLine(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SETLINE)
        {
            return null;
        }
        if (fiber.Stack.Count < 6)
        {
            return "SETLINE requires six original operands; PC and operands preserved";
        }
        return null;
    }

    private int[] ExecuteNativeSetLine(Fiber fiber, JsmOperation operation)
    {
        int[] args = Pop(fiber, 6);
        ActorState actor = actors[fiber.ObjectId];
        NativeDoorLinePoint start = new(unchecked((short)args[0]), unchecked((short)args[1]), unchecked((short)args[2]));
        NativeDoorLinePoint end = new(unchecked((short)args[3]), unchecked((short)args[4]), unchecked((short)args[5]));
        // Original51DC30 writes six WORDs and enables byte404. Existing contact/event bytes survive.
        actor.LineStart = start.Position;
        actor.LineEnd = end.Position;
        actor.LineEnabled = true;
        actor.LineOperation = operation.Index;
        actor.NativeSetLineReceipt = program!.Objects[actor.ObjectId].IsDoor
            ? new NativeSetLineReceipt(1, FieldId, programHash, actor.ObjectId, NativeDoorOrdinal(actor.ObjectId), fiber.Script.Id, operation.Index, Frame, start, end)
            : null;
        return args;
    }

    private byte NativeDoorOrdinal(int objectId)
    {
        return checked((byte)program!.NativeObjects.Where(id => program.Objects[id].IsDoor).ToList().IndexOf(objectId));
    }

    private bool ValidNativeSetLine(ActorState line, long frame, bool allowLiteralLegacy)
    {
        if (!ValidNativeLineOwner(line) || line.LineStart is not Vector3 start || line.LineEnd is not Vector3 end || NativeInfPoint(start) != start || NativeInfPoint(end) != end)
        {
            return false;
        }
        if (line.NativeSetLineReceipt is NativeSetLineReceipt receipt)
        {
            EventScript? script = program!.Objects[line.ObjectId].Scripts.SingleOrDefault(script => script.Id == receipt.ScriptId);
            return receipt.Version == 1 && receipt.FieldId == FieldId && receipt.ProgramHash == programHash && receipt.ObjectId == line.ObjectId && receipt.NativeDoorOrdinal == NativeDoorOrdinal(line.ObjectId) && receipt.Operation == line.LineOperation && receipt.Frame > 0 && receipt.Frame <= frame && receipt.Start is not null && receipt.End is not null && receipt.Start.Position == start && receipt.End.Position == end && script is not null && receipt.Operation >= script.Start && receipt.Operation < script.End;
        }
        if (!allowLiteralLegacy)
        {
            return false;
        }
        EventScript owner = program!.Objects[line.ObjectId].Scripts.Single(script => line.LineOperation >= script.Start && line.LineOperation < script.End);
        int cursor = line.LineOperation - 1;
        int[] values = new int[6];
        for (int index = values.Length - 1; index >= 0; index--)
        {
            if (!TryReadNativeLineLiteral(owner, ref cursor, out values[index]))
            {
                return false;
            }
        }
        return start == new Vector3(unchecked((short)values[0]), unchecked((short)values[1]), unchecked((short)values[2])) && end == new Vector3(unchecked((short)values[3]), unchecked((short)values[4]), unchecked((short)values[5]));
    }

    private bool TryReadNativeLineLiteral(EventScript script, ref int cursor, out int value)
    {
        value = 0;
        if (cursor <= script.Start)
        {
            return false;
        }
        JsmOperation operation = program!.Operations[cursor--];
        if (operation.Opcode == Jsm.Opcode.PSHN_L)
        {
            value = operation.Parameter;
            return true;
        }
        if (operation.Opcode == Jsm.Opcode.CAL && operation.Parameter == 5 && TryReadNativeLineLiteral(script, ref cursor, out int operand))
        {
            value = unchecked(-operand);
            return true;
        }
        return false;
    }
}
