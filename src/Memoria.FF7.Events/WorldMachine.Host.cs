namespace Memoria.FF7.Events;

public sealed partial class WorldMachine
{
    private bool HostOpcode(ushort opcode, int address)
    {
        switch (opcode)
        {
            case 0x300:
                int modelId = Pop();
                if ((uint)modelId > 255)
                {
                    throw new InvalidDataException("FFVII world model ID is outside the supported model table.");
                }
                Entity? found = models.Find(value => value.Model == modelId);
                if (found is null)
                {
                    if (models.Count == 16)
                    {
                        throw new InvalidDataException("FFVII world model pool is exhausted.");
                    }
                    found = new(modelId);
                    models.Insert(0, found);
                    active = found;
                    Send(WorldOperation.CreateModel, address, [modelId]);
                    StartModel(modelId, 0);
                }
                else
                {
                    active = found;
                }
                return false;
            case 0x330:
                int selectedId = Pop();
                Entity? selected = models.Find(value => value.Model == selectedId);
                if (selected is not null)
                {
                    active = selected;
                }
                return false;
            case 0x305:
                executing.MoveCounter = unchecked((byte)Pop());
                return false;
            case 0x306:
                if (moved)
                {
                    executing.Pc--;
                    return true;
                }
                executing.MoveCounter = unchecked((byte)(executing.MoveCounter - 1));
                if (executing.MoveCounter != 0)
                {
                    executing.Pc--;
                }
                else
                {
                    moved = true;
                }
                Send(WorldOperation.Move, address, []);
                return executing.MoveCounter != 0;
            case 0x354:
                active.Disabled = Pop() == 0;
                return false;
            case 0x334:
                if (!unloading && models.Find(value => value.Model == executing.CalledModel)?.Priority > 0)
                {
                    executing.Pc--;
                    return true;
                }
                return false;
            case 0x327:
                if (!unloading && host.ReadSpecial(12, active.Model) < 0)
                {
                    executing.Pc--;
                    return true;
                }
                return false;
            case 0x32D:
            case 0x32E:
                if (!unloading && !host.Execute(new(nextRequestId++, executing.Model, active.Model, address, new((WorldOperation)opcode, []))).Completed)
                {
                    executing.Pc--;
                    return true;
                }
                return false;
            case 0x341:
            case 0x342:
                throw Unsupported(opcode, address);
        }
        int count = opcode switch
        {
            0x302 or 0x320 or 0x338 or 0x339 => 0,
            0x303 or 0x304 or 0x307 or 0x30A or 0x30B or 0x317 or 0x319 or 0x31C or 0x31D or 0x31E or 0x31F or 0x325 or 0x328 or 0x329 or 0x32A or 0x32B or 0x32F or 0x336 or 0x33A or 0x33D or 0x33E or 0x349 or 0x34A or 0x34B or 0x34C or 0x34E or 0x34F or 0x350 or 0x351 or 0x352 or 0x355 => 1,
            0x308 or 0x309 or 0x30E or 0x310 or 0x311 or 0x312 or 0x314 or 0x318 or 0x32C or 0x33B or 0x348 => 2,
            0x313 or 0x315 or 0x316 or 0x326 or 0x34D => 3,
            0x324 => 4,
            _ => throw Unsupported(opcode, address)
        };
        int[] arguments = new int[count];
        for (int i = count - 1; i >= 0; i--)
        {
            arguments[i] = Pop();
        }
        Send((WorldOperation)opcode, address, arguments);
        return false;
    }

    private void Send(WorldOperation operation, int address, int[] arguments)
    {
        WorldHostReply reply = host.Execute(new(nextRequestId++, executing.Model, active.Model, address, new(operation, arguments)));
        if (!reply.Completed)
        {
            throw new NotSupportedException($"FFVII world immediate operation {operation} cannot suspend in the host.");
        }
    }

    public WorldSnapshot Capture()
    {
        EnsureHealthy();
        return new(1, program.Identity, TickCount, nextRequestId, unloading, (byte[])local.Clone(), stack.ToArray(), CaptureEntity(system), models.Select(CaptureEntity).ToArray());
    }

    private static WorldEntitySnapshot CaptureEntity(Entity entity)
    {
        return new(entity.Model, entity.Pc, entity.MoveCounter, entity.Priority, entity.Disabled, entity.CalledModel, entity.Calls.ToArray());
    }

    public void Restore(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != 1 || snapshot.ProgramIdentity != program.Identity || snapshot.Tick < 0 || snapshot.NextRequestId < 1 || snapshot.Local is null || snapshot.Local.Length != 64 || snapshot.Stack is null || snapshot.Stack.Length > 4 || snapshot.System is null || snapshot.System.Model != -1 || snapshot.Models is null || snapshot.Models.Length > 16)
        {
            throw new InvalidDataException("FFVII world snapshot has an invalid version or storage shape.");
        }
        Entity newSystem = RestoreEntity(snapshot.System);
        List<Entity> newModels = snapshot.Models.Select(RestoreEntity).ToList();
        if (newModels.Any(value => (uint)value.Model > 255) || newModels.Select(value => value.Model).Distinct().Count() != newModels.Count)
        {
            throw new InvalidDataException("FFVII world snapshot has invalid or duplicate models.");
        }
        foreach (WorldStackValue value in snapshot.Stack)
        {
            if (value is null || value.Opcode is < 0x110 or > 0x11F)
            {
                throw new InvalidDataException("FFVII world snapshot has an invalid operand stack.");
            }
            if ((value.Opcode & 3) != 3 && (value.Opcode & ~3) != 0x110)
            {
                Locate(value);
            }
        }
        snapshot.Local.CopyTo(local, 0);
        stack.Clear();
        stack.AddRange(snapshot.Stack);
        models.Clear();
        models.AddRange(newModels);
        system = newSystem;
        active = executing = system;
        TickCount = snapshot.Tick;
        nextRequestId = snapshot.NextRequestId;
        unloading = snapshot.Unloading;
        faulted = false;
    }

    private Entity RestoreEntity(WorldEntitySnapshot source)
    {
        if (source is null || source.ProgramCounter < 0 || source.ProgramCounter >= program.WordCount || source.Priority > 3 || source.Calls is null || source.Calls.Length > 4 || source.CalledModel is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII world snapshot has an invalid script context.");
        }
        foreach (WorldCallSnapshot call in source.Calls)
        {
            if (call is null || call.ProgramCounter <= 0 || call.ProgramCounter >= program.WordCount || call.Priority > 3)
            {
                throw new InvalidDataException("FFVII world snapshot has an invalid suspended script.");
            }
        }
        return new(source.Model)
        {
            Pc = source.ProgramCounter,
            MoveCounter = source.MoveCounter,
            Priority = source.Priority,
            Disabled = source.ScriptDisabled,
            CalledModel = source.CalledModel,
            Calls = source.Calls.ToList()
        };
    }
}
