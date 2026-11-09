namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    private readonly IFieldHost? host;
    private readonly Dictionary<(int Entity, int Priority, int Pc), FieldHostRequest> pendingRequests = [];
    private long nextRequestId = 1;

    private byte B(int id, int offset) => program.Read(entities[id].Pc + offset);
    private ushort W(int id, int offset) => (ushort)(B(id, offset) | B(id, offset + 1) << 8);
    private short S(int id, int offset) => unchecked((short)W(id, offset));
    private FieldBank Bank(int id, int half)
    {
        byte packed = B(id, 1 + (half - 1) / 2);
        return (FieldBank)((half & 1) == 1 ? packed >> 4 : packed & 15);
    }
    private byte R8(int id, int half, int offset) => Variables.ReadByte(Bank(id, half), B(id, offset));
    private short R16(int id, int half, int offset) => Variables.ReadWord(Bank(id, half), W(id, offset));
    private void Advance(int id, int length) => entities[id].Pc = unchecked((ushort)(entities[id].Pc + length));

    private bool Host(int id, FieldCommand command, int length, bool yieldAfter = false, bool firstCallYields = false, Action<int>? result = null, bool applyPendingResult = false, Action<FieldHostReply>? replyResult = null)
    {
        if (host is null)
        {
            throw new NotSupportedException($"Required FFVII host operation {command.GetType().Name} has no binding.");
        }
        var key = (id, entities[id].Priority, entities[id].Pc);
        bool first = !pendingRequests.TryGetValue(key, out FieldHostRequest? request);
        if (first)
        {
            request = new FieldHostRequest(checked(nextRequestId++), id, key.Priority, key.Pc, command);
            pendingRequests.Add(key, request);
        }
        FieldHostReply reply = host.Execute(request!);
        if (!reply.Completed || (first && firstCallYields))
        {
            if (applyPendingResult)
            {
                result?.Invoke(reply.Value);
            }
            return true;
        }
        result?.Invoke(reply.Value);
        replyResult?.Invoke(reply);
        pendingRequests.Remove(key);
        Advance(id, length);
        return yieldAfter;
    }

    private void RestoreHostRequests(FieldVmSnapshot snapshot)
    {
        if (snapshot.NextRequestId < 1)
        {
            throw new InvalidDataException("Invalid FFVII next host request ID.");
        }
        nextRequestId = snapshot.NextRequestId;
        HashSet<long> ids = [];
        foreach (FieldHostRequest request in snapshot.PendingRequests ?? [])
        {
            if (request is null || request.Command is null || request.Id < 1 || request.Id >= nextRequestId || !ids.Add(request.Id) || request.Entity < 0 || request.Entity >= entities.Length || request.Priority is < 0 or > 7 || request.ProgramCounter < 0 || request.ProgramCounter >= program.Length || !pendingRequests.TryAdd((request.Entity, request.Priority, request.ProgramCounter), request))
            {
                throw new InvalidDataException("Invalid FFVII pending host request.");
            }
            Entity entity = entities[request.Entity];
            int ownerPc = entity.Priority == request.Priority ? entity.Pc : entity.Saved[request.Priority];
            if (ownerPc != request.ProgramCounter)
            {
                throw new InvalidDataException("FFVII pending host request has no active or suspended script owner.");
            }
        }
    }

    private void CancelPreemptedMove(int id)
    {
        var key = (id, entities[id].Priority, entities[id].Pc);
        if (pendingRequests.TryGetValue(key, out FieldHostRequest? request) && request.Command is FieldActorMove)
        {
            if (host is null)
            {
                throw new NotSupportedException("Restored pending motion requires its FFVII host before preemption.");
            }
            host.Cancel(request);
            pendingRequests.Remove(key);
        }
    }

    private bool Compare(int left, int right, int operation)
    {
        const int mask = 255;
        return operation switch
        {
            0 => left == right,
            1 => left != right,
            2 => left > right,
            3 => left < right,
            4 => left >= right,
            5 => left <= right,
            6 => ((left & right) & mask) != 0,
            7 => ((left ^ right) & mask) != 0,
            8 => ((left | right) & mask) != 0,
            9 when right is >= 0 and < 32 => ((left & (1 << right)) & mask) != 0,
            10 when right is >= 0 and < 32 => ((left & (1 << right)) & mask) == 0,
            _ => throw new NotSupportedException($"Unsupported FFVII comparison operation {operation} or bit index {right}.")
        };
    }

    private bool ExecuteExtended(int id, byte opcode)
    {
        switch (opcode)
        {
            case 0x30:
            case 0x31:
            case 0x32:
            {
                bool mapped = (B(id, 2) & 2) != 0;
                ushort keys = opcode switch
                {
                    0x30 => mapped ? Input.Held : Input.RawHeld,
                    0x31 => mapped ? Input.Pressed : Input.RawPressed,
                    _ => mapped ? Input.Released : Input.RawReleased
                };
                Advance(id, (keys & W(id, 1)) != 0 ? 4 : B(id, 3) + 3);
                return false;
            }
            case >= 0x82 and <= 0x84:
            {
                int bit = R8(id, 2, 3);
                if (bit >= 32)
                {
                    throw new NotSupportedException("Bit index exceeds the proved 32-bit shift contract.");
                }
                int value = R8(id, 1, 2);
                int mask = 1 << bit;
                value = opcode == 0x82 ? value | mask : opcode == 0x83 ? value & ~mask : value ^ mask;
                Variables.WriteByte(Bank(id, 1), B(id, 2), unchecked((byte)value));
                Advance(id, 4);
                return false;
            }
            case 0x85:
            case 0x86:
            case 0x87:
            case 0x88:
            case 0x8D:
            case 0x8E:
            case 0x8F:
            case 0x90:
            case 0x91:
            case 0x92:
            case 0x93:
            case 0x94:
            {
                bool word = (opcode & 1) == 0;
                int left = word ? R16(id, 1, 2) : R8(id, 1, 2);
                int right = word ? R16(id, 2, 3) : R8(id, 2, 3);
                int value = opcode switch
                {
                    0x85 or 0x86 => left + right,
                    0x87 or 0x88 => left - right,
                    0x8D or 0x8E => right == 0 ? throw new InvalidDataException("FFVII remainder divisor is zero.") : left % right,
                    0x8F or 0x90 => left & right,
                    0x91 or 0x92 => left | right,
                    _ => left ^ right
                };
                if (word)
                {
                    Variables.WriteWord(Bank(id, 1), B(id, 2), unchecked((short)value));
                }
                else
                {
                    Variables.WriteByte(Bank(id, 1), B(id, 2), unchecked((byte)value));
                }
                Advance(id, word ? 5 : 4);
                return false;
            }
            case >= 0x95 and <= 0x98:
            {
                bool word = (opcode & 1) == 0;
                int delta = opcode < 0x97 ? 1 : -1;
                if (word)
                {
                    Variables.WriteWord(Bank(id, 2), B(id, 2), unchecked((short)(R16(id, 2, 2) + delta)));
                }
                else
                {
                    Variables.WriteByte(Bank(id, 2), B(id, 2), unchecked((byte)(R8(id, 2, 2) + delta)));
                }
                Advance(id, 3);
                return false;
            }
            case >= 0x14 and <= 0x19:
            {
                bool word = opcode >= 0x16;
                bool unsigned = opcode >= 0x18;
                bool longBranch = (opcode & 1) != 0;
                int left = word ? R16(id, 1, 2) : R8(id, 1, 2);
                int right = word ? R16(id, 2, 4) : R8(id, 2, 3);
                if (unsigned)
                {
                    left = (ushort)left;
                    right = (ushort)right;
                }
                int branchOffset = word ? 7 : 5;
                int length = branchOffset + (longBranch ? 2 : 1);
                bool matched = Compare(left, right, B(id, word ? 6 : 4));
                int distance = longBranch ? S(id, branchOffset) : B(id, branchOffset);
                Advance(id, matched ? length : branchOffset + distance);
                return false;
            }
            default:
                return ExecuteHostOpcode(id, opcode);
        }
    }
}
