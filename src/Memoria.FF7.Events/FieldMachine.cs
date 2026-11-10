namespace Memoria.FF7.Events;

/// <summary>A bounded FFVII field VM. The semantic profile is explicit and independent of FFVIII.</summary>
public sealed partial class FieldMachine
{
    public const string SemanticProfile = "ff7-ps1-field-2.5-subset-v1";
    private readonly FieldProgram program;
    private readonly Entity[] entities;

    /// <summary>Initial entity PCs are explicit prior-state facts, needed only for preemption before an entity begins initialization.</summary>
    public FieldMachine(FieldProgram program, FieldVariables? variables = null, IReadOnlyList<ushort>? initialEntityProgramCounters = null, IFieldHost? host = null)
    {
        this.program = program ?? throw new ArgumentNullException(nameof(program));
        Variables = variables ?? new FieldVariables();
        this.host = host;
        if (initialEntityProgramCounters is not null && (initialEntityProgramCounters.Count != program.EntityCount || initialEntityProgramCounters.Any(pc => pc >= program.Length)))
        {
            throw new ArgumentException("Initial FFVII entity PCs must match the entity count and refer inside the section.", nameof(initialEntityProgramCounters));
        }
        entities = Enumerable.Range(0, program.EntityCount).Select(entity => new Entity { Pc = initialEntityProgramCounters is null ? -1 : initialEntityProgramCounters[entity] }).ToArray();
    }

    public FieldVariables Variables { get; }
    public string ProgramIdentity => program.Identity;
    public long Tick { get; private set; }
    public bool Initialized { get; private set; }
    public FieldInputState Input { get; private set; } = new();
    public FieldCycleCursor? CycleCursor { get; private set; }
    public Action<FieldScriptReturn>? ScriptReturned { get; set; }

    public void SetInput(ushort held, ushort pressed, ushort? rawHeld = null, ushort? rawPressed = null)
    {
        ushort raw = rawHeld ?? held;
        Input = new FieldInputState(held, pressed, (ushort)(Input.Held & ~held), raw, rawPressed ?? pressed, (ushort)(Input.RawHeld & ~raw), PreviousHeld: Input.Held, PreviousRawHeld: Input.RawHeld);
    }

    public void SetMovementInput(int horizontal, int vertical, bool run)
    {
        if (horizontal != 0 || vertical != 0)
        {
            const ushort runButton = 0x40;
            static ushort Project(ushort value, bool enabled) => enabled ? (ushort)(value | runButton) : (ushort)(value & ~runButton);
            bool previous = Input.PreviousHeld is ushort previousHeld ? (previousHeld & runButton) != 0 : (Input.Released & runButton) != 0 || (Input.Held & runButton) != 0 && (Input.Pressed & runButton) == 0;
            bool previousRaw = Input.PreviousRawHeld is ushort previousRawHeld ? (previousRawHeld & runButton) != 0 : (Input.RawReleased & runButton) != 0 || (Input.RawHeld & runButton) != 0 && (Input.RawPressed & runButton) == 0;
            Input = Input with { Held = Project(Input.Held, run), Pressed = Project(Input.Pressed, run && !previous), Released = Project(Input.Released, !run && previous), RawHeld = Project(Input.RawHeld, run), RawPressed = Project(Input.RawPressed, run && !previousRaw), RawReleased = Project(Input.RawReleased, !run && previousRaw) };
        }
        Input = Input with { Horizontal = Math.Sign(horizontal), Vertical = Math.Sign(vertical), Run = run };
    }

    /// <summary>Admits an external field trigger. Talk/push/line scripts use priority 1 and IDs 1/2/3–6.</summary>
    public bool TryRequestScript(int entityId, int scriptId, int priority = 1)
    {
        if (!Initialized)
        {
            throw new InvalidOperationException("Field triggers require completed initialization.");
        }
        if ((uint)entityId >= entities.Length || scriptId is < 0 or > 31 || priority is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Invalid FFVII field trigger target, script or priority.");
        }
        Entity target = entities[entityId];
        if (target.Priority <= priority)
        {
            return false;
        }
        if (target.Sync[priority] != FieldSyncState.None)
        {
            return true;
        }
        int entry = program.Entry(entityId, scriptId);
        if (program.Read(entry) == 0)
        {
            return true;
        }
        CancelPreemptedMove(entityId);
        target.Saved[target.Priority] = target.Pc;
        target.Pc = entry;
        target.Priority = priority;
        target.Wait = 0;
        return true;
    }

    /// <summary>Runs initialization to the first RET per entity. Yields do not advance logical time here.</summary>
    public void Initialize(int instructionLimit = 100000)
    {
        if (Initialized)
        {
            throw new InvalidOperationException("Field initialization has already completed.");
        }
        if (instructionLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionLimit));
        }
        Variables.ClearMap();
        Variables.WriteByte(FieldBank.Persistent0Byte, 31, (byte)(Variables.ReadByte(FieldBank.Persistent0Byte, 31) | 3));
        for (int id = 0; id < entities.Length; id++)
        {
            entities[id].Pc = program.Entry(id, 0);
            int count = 0;
            while (ReadOpcode(id) != 0)
            {
                if (++count > instructionLimit)
                {
                    throw Fault(id, "Initialization instruction limit reached.");
                }
                Execute(id);
            }
            int continuation = unchecked((ushort)(entities[id].Pc + 1));
            if (continuation >= program.Length)
            {
                throw Fault(id, "Initialization RET has no in-section continuation.");
            }
            entities[id].Pc = continuation;
        }
        Initialized = true;
    }

    /// <summary>One logical field cycle, entity order ascending, at most eight handlers per entity.</summary>
    public void AdvanceTick()
    {
        if (!Initialized)
        {
            throw new InvalidOperationException("Initialize the field before advancing ticks.");
        }
        CycleCursor ??= new FieldCycleCursor(0, 0, entities[0].Pc);
        while (CycleCursor is FieldCycleCursor cursor)
        {
            int id = cursor.Entity;
            if (cursor.ProgramCounter != entities[id].Pc)
            {
                throw new InvalidOperationException("The active FFVII cycle cursor lost its entity program counter owner.");
            }
            bool yielded = Execute(id);
            ReadOpcode(id);
            int completed = cursor.HandlersCompleted + 1;
            if (yielded || completed == 8)
            {
                CycleCursor = id + 1 < entities.Length ? new FieldCycleCursor(id + 1, 0, entities[id + 1].Pc) : null;
            }
            else
            {
                CycleCursor = new FieldCycleCursor(id, completed, entities[id].Pc);
            }
        }
        Tick++;
    }

    public FieldVmSnapshot Capture()
    {
        if (!Initialized)
        {
            throw new InvalidOperationException("Snapshots require completed field initialization.");
        }
        for (int id = 0; id < entities.Length; id++)
        {
            ReadOpcode(id);
        }
        if (CycleCursor is FieldCycleCursor cursor && cursor.ProgramCounter != entities[cursor.Entity].Pc)
        {
            throw new InvalidOperationException("The active FFVII cycle cursor lost its entity program counter owner.");
        }
        return new FieldVmSnapshot(1, SemanticProfile, program.Identity, Tick, Initialized, Variables.CopyPersistent(), Variables.CopyMap(), entities.Select(entity => new FieldEntitySnapshot(entity.Pc, entity.Priority, entity.Wait, (int[])entity.Saved.Clone(), (FieldSyncState[])entity.Sync.Clone(), (int[])entity.Owners.Clone(), entity.ModelBound)).ToArray(), nextRequestId, pendingRequests.Values.OrderBy(request => request.Id).ToArray(), Input, CycleCursor);
    }

    public static FieldMachine Restore(FieldProgram program, FieldVmSnapshot snapshot, IFieldHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != 1 || snapshot.Profile != SemanticProfile || snapshot.ProgramIdentity != program.Identity)
        {
            throw new InvalidDataException("Unsupported FFVII execution contract or mismatched program identity.");
        }
        if (!snapshot.Initialized || snapshot.Tick < 0 || snapshot.Entities is null || snapshot.Entities.Length != program.EntityCount || snapshot.PersistentVariables is not { Length: 1280 } || snapshot.MapVariables is not { Length: 256 })
        {
            throw new InvalidDataException("Invalid FFVII snapshot state.");
        }
        var machine = new FieldMachine(program, new FieldVariables(snapshot.PersistentVariables, snapshot.MapVariables), host: host);
        for (int id = 0; id < machine.entities.Length; id++)
        {
            var source = snapshot.Entities[id];
            if (source is null || source.ProgramCounter < 0 || source.ProgramCounter >= program.Length || source.Priority is < 0 or > 7 || source.SavedProgramCounters is not { Length: 8 } || source.SyncStates is not { Length: 8 } || source.SyncOwners is not { Length: 8 })
            {
                throw new InvalidDataException($"Invalid FFVII entity snapshot {id}.");
            }
            for (int priority = 0; priority < 8; priority++)
            {
                int pc = source.SavedProgramCounters[priority];
                int owner = source.SyncOwners[priority];
                var sync = source.SyncStates[priority];
                if (pc < 0 || pc >= program.Length || !Enum.IsDefined(sync) || owner < -1 || owner >= program.EntityCount || (sync == FieldSyncState.None ? owner != -1 : owner == -1))
                {
                    throw new InvalidDataException($"Invalid FFVII priority slot {id}/{priority}.");
                }
            }
            var target = machine.entities[id];
            target.ModelBound = source.ModelBound;
            target.Pc = source.ProgramCounter;
            target.Priority = source.Priority;
            target.Wait = source.WaitCounter;
            source.SavedProgramCounters.CopyTo(target.Saved, 0);
            source.SyncStates.CopyTo(target.Sync, 0);
            source.SyncOwners.CopyTo(target.Owners, 0);
        }
        machine.RestoreHostRequests(snapshot);
        machine.Tick = snapshot.Tick;
        machine.Initialized = snapshot.Initialized;
        machine.Input = snapshot.Input ?? new FieldInputState();
        if (snapshot.CycleCursor is FieldCycleCursor cursor)
        {
            if (cursor.Entity < 0 || cursor.Entity >= machine.entities.Length || cursor.HandlersCompleted is < 0 or > 7 || cursor.ProgramCounter != machine.entities[cursor.Entity].Pc)
            {
                throw new InvalidDataException("Invalid FFVII active cycle cursor or entity program counter owner.");
            }
            machine.CycleCursor = cursor;
        }
        if (machine.Input.Horizontal is < -1 or > 1 || machine.Input.Vertical is < -1 or > 1)
        {
            throw new InvalidDataException("FFVII saved movement input is invalid.");
        }
        return machine;
    }

    private bool Execute(int id)
    {
        Entity entity = entities[id];
        int pc = entity.Pc;
        byte opcode = 255;
        try
        {
            opcode = program.Read(pc);
            byte Byte(int offset) => program.Read(pc + offset);
            ushort Word(int offset) => (ushort)(Byte(offset) | Byte(offset + 1) << 8);
            switch (opcode)
            {
                case 0x00:
                    if (entity.Priority < 7)
                    {
                        ScriptReturned?.Invoke(new(id, entity.Priority, pc, Tick));
                    }
                    return Return(entity);
                case 0x01:
                case 0x02:
                case 0x03:
                    return Request(id, opcode, Byte(1), Byte(2));
                case 0x10:
                    entity.Pc = unchecked((ushort)(pc + 1 + Byte(1)));
                    return false;
                case 0x11:
                    entity.Pc = unchecked((ushort)(pc + 1 + Word(1)));
                    return false;
                case 0x12:
                    entity.Pc = unchecked((ushort)(pc - Byte(1)));
                    return true;
                case 0x13:
                    entity.Pc = unchecked((ushort)(pc - Word(1)));
                    return true;
                case 0x24:
                    ushort duration = Word(1);
                    if (entity.Wait == 0)
                    {
                        entity.Wait = duration;
                        if (duration == 0)
                        {
                            entity.Pc = unchecked((ushort)(entity.Pc + 3));
                        }
                        return true;
                    }
                    if (entity.Wait == 1)
                    {
                        entity.Wait = 0;
                        entity.Pc = unchecked((ushort)(entity.Pc + 3));
                        return false;
                    }
                    entity.Wait--;
                    return true;
                case 0x5F:
                    entity.Pc = unchecked((ushort)(entity.Pc + 1));
                    return true;
                case 0x80:
                    Variables.WriteByte((FieldBank)(Byte(1) >> 4), Byte(2), Variables.ReadByte((FieldBank)(Byte(1) & 15), Byte(3)));
                    entity.Pc = unchecked((ushort)(entity.Pc + 4));
                    return false;
                case 0x81:
                    Variables.WriteWord((FieldBank)(Byte(1) >> 4), Byte(2), Variables.ReadWord((FieldBank)(Byte(1) & 15), Word(3)));
                    entity.Pc = unchecked((ushort)(entity.Pc + 5));
                    return false;
                default:
                    return ExecuteExtended(id, opcode);
            }
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            throw new FieldExecutionException(new FieldFaultContext(program.Identity, Tick, id, entity.Priority, pc, opcode), error.Message, error);
        }
    }

    private bool Request(int callerId, byte type, byte targetId, byte packed)
    {
        Entity caller = entities[callerId];
        if (targetId == 255)
        {
            caller.Pc = unchecked((ushort)(caller.Pc + 3));
            return false;
        }
        if (targetId >= entities.Length)
        {
            throw new InvalidDataException($"Request targets absent entity {targetId}.");
        }
        Entity target = entities[targetId];
        int priority = packed >> 5;
        int script = packed & 31;
        if (type == 3 && target.Owners[priority] == callerId)
        {
            if (target.Sync[priority] == FieldSyncState.Waiting)
            {
                return true;
            }
            if (target.Sync[priority] == FieldSyncState.Done)
            {
                target.Sync[priority] = FieldSyncState.None;
                target.Owners[priority] = -1;
                caller.Pc = unchecked((ushort)(caller.Pc + 3));
                return false;
            }
        }
        if (target.Priority == priority || (target.Priority < priority && target.Saved[priority] != 0))
        {
            if (type == 1)
            {
                caller.Pc = unchecked((ushort)(caller.Pc + 3));
                return false;
            }
            return true;
        }
        if (target.Priority < priority)
        {
            target.Saved[priority] = program.Entry(targetId, script);
        }
        else
        {
            if (target.Sync[priority] != FieldSyncState.None)
            {
                return true;
            }
            if (target.Pc < 0)
            {
                throw new NotSupportedException("Initialization request preempts an unvisited entity whose initial PC has not been supplied.");
            }
            CancelPreemptedMove(targetId);
            target.Saved[target.Priority] = target.Pc;
            target.Pc = program.Entry(targetId, script);
            target.Priority = priority;
            target.Wait = 0;
        }
        if (type == 3)
        {
            target.Owners[priority] = callerId;
            target.Sync[priority] = FieldSyncState.Waiting;
            return true;
        }
        caller.Pc = unchecked((ushort)(caller.Pc + 3));
        return false;
    }

    private static bool Return(Entity entity)
    {
        if (entity.Priority == 7)
        {
            return true;
        }
        if (entity.Sync[entity.Priority] == FieldSyncState.Waiting)
        {
            entity.Sync[entity.Priority] = FieldSyncState.Done;
        }
        do
        {
            entity.Priority++;
            entity.Pc = entity.Saved[entity.Priority];
        }
        while (entity.Pc == 0 && entity.Priority < 7);
        entity.Saved[entity.Priority] = 0;
        return false;
    }

    private byte ReadOpcode(int id)
    {
        Entity entity = entities[id];
        try
        {
            return program.Read(entity.Pc);
        }
        catch (InvalidDataException error)
        {
            throw new FieldExecutionException(new FieldFaultContext(program.Identity, Tick, id, entity.Priority, entity.Pc, 255), error.Message, error);
        }
    }

    private FieldExecutionException Fault(int id, string message)
    {
        Entity entity = entities[id];
        return new FieldExecutionException(new FieldFaultContext(program.Identity, Tick, id, entity.Priority, entity.Pc, program.Read(entity.Pc)), message);
    }

    private sealed class Entity
    {
        public bool ModelBound;
        public int Pc;
        public int Priority = 7;
        public ushort Wait;
        public int[] Saved { get; } = new int[8];
        public FieldSyncState[] Sync { get; } = new FieldSyncState[8];
        public int[] Owners { get; } = Enumerable.Repeat(-1, 8).ToArray();
    }
}
