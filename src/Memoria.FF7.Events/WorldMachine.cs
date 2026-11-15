using System.Buffers.Binary;

namespace Memoria.FF7.Events;

public sealed partial class WorldMachine
{
    private sealed class Entity(int model)
    {
        public int Model = model;
        public int Pc;
        public byte MoveCounter;
        public byte Priority;
        public bool Disabled;
        public bool Detached;
        public Entity? Next;
        public int CalledModel;
        public List<WorldCallSnapshot> Calls = [];
    }

    private readonly WorldProgram program;
    private readonly byte[] persistent;
    private readonly IWorldHost host;
    private readonly byte[] local = new byte[64];
    private readonly List<WorldStackValue> stack = [];
    private readonly List<Entity> models = [];
    private readonly Stack<Entity> freeModels = [];
    private Entity system = new(-1);
    private Entity active;
    private Entity executing;
    private long nextRequestId = 1;
    private bool unloading;
    private bool moved;
    private bool faulted;

    public WorldMachine(WorldProgram program, byte[] sharedPersistent, IWorldHost host)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(sharedPersistent);
        ArgumentNullException.ThrowIfNull(host);
        if (sharedPersistent.Length != 1280)
        {
            throw new ArgumentException("FFVII world events require the shared 1280-byte persistent variable storage.", nameof(sharedPersistent));
        }
        this.program = program;
        persistent = sharedPersistent;
        this.host = host;
        active = executing = system;
    }

    public long TickCount { get; private set; }
    public bool IsUnloading => unloading;
    public byte[] CopyPersistent() => (byte[])persistent.Clone();

    public bool OwnsHostOperation(int address, WorldOperation operation) => address > 0 && address < program.WordCount && program.Read(address) == (ushort)operation;

    public void CompleteUnload()
    {
        EnsureHealthy();
        StartSystem(1);
        for (int pass = 0; system.Pc != 0 || models.Any(entity => !entity.Detached && entity.Pc != 0); pass++)
        {
            if (pass == 4096)
            {
                throw new InvalidDataException("FFVII world unload did not finish within its execution budget.");
            }
            ExecuteScripts();
        }
    }

    public void StartSystem(int eventId)
    {
        if ((uint)eventId > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId));
        }
        active = system;
        unloading |= eventId == 1;
        Start(system, eventId, Math.Clamp(eventId - 2, 0, 3));
    }

    public void StartModel(int model, int eventId)
    {
        if ((uint)model > 255 || (uint)eventId > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }
        Entity? entity = models.Find(value => value.Model == model);
        if (entity is { Detached: false })
        {
            active = entity;
            if (!entity.Disabled)
            {
                Start(entity, 0x4000 | (model << 8 & 0x3F00) | eventId, Math.Clamp(eventId - 2, 0, 3));
            }
        }
    }

    public bool IsModelScriptEnabled(int model) => models.Any(entity => entity.Model == model && !entity.Detached && !entity.Disabled);

    public bool IsModelAttached(int model) => models.Any(entity => entity.Model == model && !entity.Detached);

    public void SetModelScriptEnabled(int model, bool enabled)
    {
        models.Single(entity => entity.Model == model).Disabled = !enabled;
    }

    public void DetachModel(int model)
    {
        Entity entity = models.Single(entity => entity.Model == model);
        entity.Detached = true;
        entity.Next = null;
        LinkResidentModels();
    }

    public void ReattachModel(int model)
    {
        Entity entity = models.Single(entity => entity.Model == model);
        models.Remove(entity);
        models.Insert(0, entity);
        entity.Detached = false;
        LinkResidentModels();
    }

    public void RestoreTransportRider(int model)
    {
        Entity entity = models.Single(entity => entity.Model == model && entity.Detached);
        entity.Pc = 0;
        entity.MoveCounter = 0;
        entity.Priority = 0;
        entity.Disabled = false;
        entity.CalledModel = 0;
        entity.Calls.Clear();
        ReattachModel(model);
    }

    public void RemoveModel(int model)
    {
        Entity entity = models.Single(entity => entity.Model == model);
        models.Remove(entity);
        entity.Detached = false;
        entity.Next = freeModels.TryPeek(out Entity? nextFree) ? nextFree : null;
        freeModels.Push(entity);
        LinkResidentModels();
        if (active == entity)
        {
            active = system;
        }
    }

    public void StartMesh(int chunkX, int chunkZ, int eventId)
    {
        int header = WorldProgram.MeshDispatchHeader(chunkX, chunkZ, eventId);
        active = system;
        Start(system, header, 3);
    }

    private void Start(Entity entity, int header, int priority)
    {
        inspection?.Requests.Add(new(entity.Model, header, priority));
        if (entity.Pc != 0 && entity.Priority >= priority && !(entity.Priority == priority && priority == 3))
        {
            return;
        }
        int address = program.Find(header);
        if (address == 0)
        {
            return;
        }
        if (entity.Pc != 0)
        {
            if (entity.Calls.Count == 4)
            {
                throw new InvalidDataException("FFVII world script call stack overflow.");
            }
            entity.Calls.Add(new(entity.Pc, entity.MoveCounter, entity.Priority));
        }
        entity.Pc = address;
        entity.Priority = (byte)priority;
    }

    public void Tick()
    {
        EnsureHealthy();
        try
        {
            ExecuteScripts();
            TickCount++;
        }
        catch
        {
            faulted = true;
            throw;
        }
    }

    private void ExecuteScripts()
    {
        Run(system);
        // Allocation prepends to the native list; models created by the system participate in this tick.
        Entity? entity = models.FirstOrDefault(entity => !entity.Detached);
        while (entity is not null)
        {
            if (!entity.Detached)
            {
                Run(entity);
            }
            entity = entity.Next;
        }
    }

    private void EnsureHealthy()
    {
        if (faulted)
        {
            throw new InvalidOperationException("FFVII world execution failed during a tick; restore the last completed snapshot before continuing or saving.");
        }
    }

    private void Run(Entity entity)
    {
        executing = active = entity;
        moved = false;
        for (int budget = 0; entity.Pc != 0; budget++)
        {
            if (budget == 100000)
            {
                throw new InvalidDataException("FFVII world script exceeded its instruction budget without yielding.");
            }
            if (ExecuteInstruction(entity))
            {
                return;
            }
        }
    }

    private bool ExecuteInstruction(Entity entity)
    {
        int address = entity.Pc;
        WorldInstruction instruction = program.ReadInstruction(address);
        entity.Pc = instruction.NextProgramCounter;
        ushort opcode = instruction.Opcode;
        if (opcode < 0x100)
        {
            Arithmetic(opcode);
        }
        else if (opcode < 0x200)
        {
            if (opcode == 0x100)
            {
                stack.Clear();
            }
            else
            {
                if (opcode is < 0x110 or > 0x11F)
                {
                    throw Unsupported(opcode, address);
                }
                Push(new(opcode, instruction.Operand));
            }
        }
        else if (opcode == 0x200 || opcode == 0x201)
        {
            int target = instruction.Operand;
            if (opcode == 0x200 || Pop() == 0)
            {
                CheckPc(target);
                entity.Pc = target;
            }
        }
        else if (opcode == 0x203)
        {
            if (entity.Calls.Count > 0)
            {
                WorldCallSnapshot call = entity.Calls[^1];
                entity.Calls.RemoveAt(entity.Calls.Count - 1);
                entity.Pc = call.ProgramCounter;
                entity.Priority = call.Priority;
                entity.MoveCounter = call.MoveCounter;
            }
            else
            {
                entity.Pc = 0;
                entity.Priority = 0;
                if (unloading)
                {
                    if (entity == system)
                    {
                        foreach (Entity model in models.ToArray())
                        {
                            StartModel(model.Model, 1);
                        }
                    }
                }
                else if (entity == system)
                {
                    StartSystem(2);
                }
                else
                {
                    StartModel(entity.Model, 2);
                }
                return true;
            }
        }
        else if (opcode >= 0x204 && opcode < 0x300)
        {
            entity.CalledModel = unchecked((byte)Pop());
            if (entity.CalledModel < 64)
            {
                StartModel(entity.CalledModel, opcode - 0x204);
            }
            else
            {
                entity.CalledModel = active.Model;
                if (!active.Disabled)
                {
                    Start(active, (opcode - 0x204) & 255, Math.Clamp(opcode - 0x206, 0, 3));
                }
            }
            return true;
        }
        else if (HostOpcode(opcode, address))
        {
            return true;
        }
        return false;
    }

    private void Arithmetic(ushort opcode)
    {
        if (opcode == 0xE0)
        {
            int value = Pop();
            WorldStackValue reference = Take();
            Write(reference, value);
            Push(new(0x110, value));
            return;
        }
        int right = Pop();
        if (opcode is 0x18 or 0x19 or 0x1A or 0x1B)
        {
            int value = opcode switch
            {
                0x18 => host.ReadZoneDistance(active.Model, right, ActiveFreeModelIndex()),
                0x19 => host.ReadModelDistance(active.Model, right, ActiveFreeModelIndex()),
                0x1A => host.ReadStoredPositionDistance(active.Model, right, ActiveFreeModelIndex()),
                _ => host.ReadZoneDirection(active.Model, right, ActiveFreeModelIndex()) >> 4
            };
            Push(new(0x110, value));
            return;
        }
        if (opcode is 0x15 or 0x17)
        {
            Push(new(0x110, opcode == 0x15 ? unchecked(-right) : right == 0 ? 1 : 0));
            return;
        }
        int left = Pop();
        int result = opcode switch
        {
            0x30 => unchecked(left * right),
            0x40 => unchecked(left + right),
            0x41 => unchecked(left - right),
            0x50 => left << right,
            0x51 => left >> right,
            0x60 => left < right ? 1 : 0,
            0x61 => left > right ? 1 : 0,
            0x62 => left <= right ? 1 : 0,
            0x63 => left >= right ? 1 : 0,
            0x70 => left == right ? 1 : 0,
            0x71 => left != right ? 1 : 0,
            0x80 => left & right,
            0xA0 => left | right,
            0xB0 => left != 0 && right != 0 ? 1 : 0,
            0xC0 => left != 0 || right != 0 ? 1 : 0,
            _ => throw Unsupported(opcode, executing.Pc - 1)
        };
        Push(new(0x110, result));
    }

    private void Push(WorldStackValue value)
    {
        if (stack.Count >= 4)
        {
            throw new InvalidDataException("FFVII world operand stack overflow.");
        }
        stack.Add(value);
    }

    private WorldStackValue Take()
    {
        if (stack.Count == 0)
        {
            throw new InvalidDataException("FFVII world operand stack underflow.");
        }
        WorldStackValue value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private int Pop()
    {
        return ReadValue(Take());
    }

    private int ReadValue(WorldStackValue operand)
    {
        if ((operand.Opcode & 3) == 3)
        {
            return host.ReadSpecial(operand.Value, active.Model, ActiveFreeModelIndex());
        }
        if ((operand.Opcode & ~3) == 0x110)
        {
            return operand.Value;
        }
        inspection?.Reads.Add(new(operand.Opcode, operand.Value));
        return ReadBankValue(operand, persistent, local);
    }

    private static int ReadBankValue(WorldStackValue operand, byte[] persistent, byte[] local)
    {
        (byte[] bank, int index) = Locate(operand, persistent, local);
        return (operand.Opcode & ~3) switch
        {
            0x114 => bank[index] >> (operand.Value & 7) & 1,
            0x118 => bank[index],
            0x11C => BinaryPrimitives.ReadInt16LittleEndian(bank.AsSpan(index, 2)),
            _ => throw new InvalidDataException("Invalid FFVII world operand selector.")
        };
    }

    private (byte[] Bank, int Index) Locate(WorldStackValue operand)
    {
        return Locate(operand, persistent, local);
    }

    private static (byte[] Bank, int Index) Locate(WorldStackValue operand, byte[] persistent, byte[] local)
    {
        byte[] bank = (operand.Opcode & 3) == 0 ? persistent : local;
        int address = (operand.Opcode & ~3) == 0x114 ? operand.Value >> 3 : operand.Value;
        int width = (operand.Opcode & ~3) == 0x11C ? 2 : 1;
        int length = bank == persistent ? 1024 : 64;
        if (address < 0 || address > length - width || width == 2 && (address & 1) != 0)
        {
            throw new InvalidDataException("FFVII world bank access is outside storage or unaligned.");
        }
        return (bank, address);
    }

    private void Write(WorldStackValue operand, int value)
    {
        if ((operand.Opcode & ~3) is not (0x114 or 0x118 or 0x11C))
        {
            throw new InvalidDataException("FFVII world assignment requires a bank reference.");
        }
        (byte[] bank, int index) = Locate(operand);
        switch (operand.Opcode & ~3)
        {
            case 0x114:
                int bit = 1 << (operand.Value & 7);
                bank[index] = (byte)((bank[index] & ~bit) | (value != 0 ? bit : 0));
                break;
            case 0x118:
                bank[index] = unchecked((byte)value);
                break;
            case 0x11C:
                BinaryPrimitives.WriteInt16LittleEndian(bank.AsSpan(index, 2), unchecked((short)value));
                break;
        }
    }

    private void CheckPc(int pc)
    {
        if (pc <= 0 || pc >= program.WordCount)
        {
            throw new InvalidDataException("FFVII world script PC is outside the program.");
        }
    }

    private static NotSupportedException Unsupported(ushort opcode, int address) => new($"Unsupported FFVII world opcode {opcode:X4} at word {address:X4}.");
}
