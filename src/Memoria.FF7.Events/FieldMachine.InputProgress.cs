namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    /// <summary>Finds the current input that advances a finite local-counter sequence toward its native equality guard.</summary>
    public FieldScriptInputDemand? ReadCurrentCounterProgressInput(int entity, int priority, int instructionBudget = 256, int? window = null)
    {
        if (instructionBudget is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        }
        if (!Initialized || CycleCursor is not null || (uint)entity >= entities.Length || entities[entity].Priority != priority)
        {
            return null;
        }
        try
        {
            int remaining = instructionBudget;
            int pc = entities[entity].Pc;
            int counter = -1;
            int stages = 0;
            FieldScriptInputDemand? first = null;
            bool closesWindow = window is null;
            HashSet<int> visited = [];
            while (remaining-- > 0 && visited.Add(pc))
            {
                byte opcode = program.Read(pc);
                if (opcode == 0x24)
                {
                    pc += 3;
                    continue;
                }
                if (opcode is 0x2E or 0x54)
                {
                    if (stages == 1 && program.Read(pc + 1) == window)
                    {
                        closesWindow = true;
                    }
                    pc += 2;
                    continue;
                }
                if (opcode == 0x01)
                {
                    byte[] call = [opcode, program.Read(pc + 1), program.Read(pc + 2)];
                    if (!IsCounterIndependentPresentationChild(call, ref remaining))
                    {
                        return null;
                    }
                    pc += 3;
                    continue;
                }
                if (opcode is 0x14 or 0x15)
                {
                    int expected = program.Read(pc + 3);
                    return closesWindow && first is not null && program.Read(pc + 1) == ((int)FieldBank.MapByte << 4) && program.Read(pc + 2) == counter && program.Read(pc + 4) == 0 && expected == Variables.ReadByte(FieldBank.MapByte, (byte)counter) + stages && expected <= byte.MaxValue ? first : null;
                }
                if (opcode is not (>= 0x30 and <= 0x32))
                {
                    return null;
                }
                int start = pc;
                int join = -1;
                byte[]? sharedCall = null;
                FieldScriptInputDemand? progress = null;
                ushort masks = 0;
                int alternatives = 0;
                while (remaining-- > 0)
                {
                    opcode = program.Read(pc);
                    if (opcode is 0x12 or 0x13)
                    {
                        int distance = program.Read(pc + 1) | (opcode == 0x13 ? program.Read(pc + 2) << 8 : 0);
                        if (pc - distance != start || alternatives < 2 || progress is null || join <= pc)
                        {
                            return null;
                        }
                        break;
                    }
                    if (opcode is not (>= 0x30 and <= 0x32))
                    {
                        return null;
                    }
                    FieldScriptInputDemand demand = ReadInputDemand(entity, pc, opcode);
                    if (demand.Kind != FieldScriptInputKind.Pressed || demand.Mapped || demand.Mask == 0 || (demand.Mask & (demand.Mask - 1)) != 0 || (masks & demand.Mask) != 0)
                    {
                        return null;
                    }
                    masks |= demand.Mask;
                    int unmatched = pc + 3 + program.Read(pc + 3);
                    int arm = pc + 4;
                    bool increments = program.Read(arm) == 0x95;
                    if (increments)
                    {
                        if (progress is not null || program.Read(arm + 1) != (int)FieldBank.MapByte || counter >= 0 && counter != program.Read(arm + 2))
                        {
                            return null;
                        }
                        counter = program.Read(arm + 2);
                        progress = demand with { AwaitingInput = true };
                        arm += 3;
                    }
                    byte callOpcode = program.Read(arm);
                    if (callOpcode is not (0x03 or 0x06))
                    {
                        return null;
                    }
                    byte[] call = [callOpcode, program.Read(arm + 1), program.Read(arm + 2)];
                    if (sharedCall is not null && !sharedCall.AsSpan().SequenceEqual(call))
                    {
                        return null;
                    }
                    sharedCall = call;
                    arm += 3;
                    byte jump = program.Read(arm);
                    if (jump is not (0x10 or 0x11))
                    {
                        return null;
                    }
                    int destination = arm + 1 + program.Read(arm + 1) + (jump == 0x11 ? program.Read(arm + 2) << 8 : 0);
                    if (join >= 0 && join != destination || unmatched != arm + (jump == 0x11 ? 3 : 2))
                    {
                        return null;
                    }
                    join = destination;
                    alternatives++;
                    pc = unmatched;
                }
                if (remaining <= 0 || sharedCall is null || !IsCounterIndependentPresentationChild(sharedCall, ref remaining))
                {
                    return null;
                }
                first ??= progress;
                stages++;
                pc = join;
            }
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
        }
        return null;
    }

    private bool IsCounterIndependentPresentationChild(byte[] call, ref int remaining)
    {
        int target = call[1];
        if (call[0] == 0x06)
        {
            if (target >= 3)
            {
                return false;
            }
            int character = ReadParty()[target];
            target = character == byte.MaxValue ? -1 : Array.FindIndex(entities, actor => actor.PartyCharacter == character);
        }
        if ((uint)target >= entities.Length)
        {
            return false;
        }
        int pc = program.Entry(target, call[2] & 31);
        HashSet<int> visited = [];
        while (remaining-- > 0 && visited.Add(pc))
        {
            byte opcode = program.Read(pc);
            if (opcode == 0)
            {
                return true;
            }
            if (opcode == 0x24)
            {
                pc += 3;
            }
            else if (opcode is 0xA2 or 0xA3 or 0xAE or 0xAF or 0xBA && program.Read(pc + 2) != 0)
            {
                pc += 3;
            }
            else if (opcode == 0xF1 && program.Read(pc + 1) == 0)
            {
                pc += 5;
            }
            else if (opcode == 0xAC)
            {
                pc++;
            }
            else if (opcode == 0x40)
            {
                pc += 3;
            }
            else if (opcode is 0x2E or 0x54)
            {
                pc += 2;
            }
            else if (opcode == 0x50)
            {
                pc += 10;
            }
            else if (opcode == 0x52)
            {
                pc += 4;
            }
            else if (opcode is 0x10 or 0x11)
            {
                int next = pc + 1 + program.Read(pc + 1) + (opcode == 0x11 ? program.Read(pc + 2) << 8 : 0);
                if (next <= pc)
                {
                    return false;
                }
                pc = next;
            }
            else
            {
                return false;
            }
        }
        return false;
    }
}
