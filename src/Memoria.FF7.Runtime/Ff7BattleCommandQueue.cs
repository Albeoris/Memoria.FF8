namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleQueuedCommand(byte Priority, int ActorSlot, byte CommandId, ushort Argument, ushort TargetMask, ushort? AttackId = null, byte[]? Message = null, byte? Callback = null, bool GroupTarget = false)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool GuestAttack { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ushort GuestAbility { get; init; }

    public static Ff7BattleQueuedCommand FromAi(Ff7BattleAiCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ushort argument = command.CommandId == 0x20 ? checked((ushort)command.SceneAttackSlot) : command.AttackId;
        ushort? attackId = command.CommandId == 0x20 ? command.AttackId : null;
        return new(command.Priority, command.ActorSlot, command.CommandId, argument, command.TargetMask, attackId, command.Message, command.Callback);
    }
}

public sealed record Ff7BattleQueueEntry(int PhysicalSlot, byte Sequence, Ff7BattleQueuedCommand Command);
public sealed record Ff7BattleQueueSelection(Ff7BattleCommandQueueState State, Ff7BattleQueueEntry? Entry);

public sealed record Ff7BattleCommandQueueState(Ff7BattleQueueEntry?[] Slots, byte[] ReadSequences, byte[] WriteSequences, Ff7BattleQueuedCommand?[] Ready, int ReadyRead = 0, int ReadyWrite = 0, byte LastEnqueuedPriority = 0)
{
    public static Ff7BattleCommandQueueState Create()
    {
        return new(new Ff7BattleQueueEntry?[Ff7BattleCommandQueue.Capacity], new byte[Ff7BattleCommandQueue.PriorityCount], new byte[Ff7BattleCommandQueue.PriorityCount], new Ff7BattleQueuedCommand?[Ff7BattleCommandQueue.ReadyRingLength]);
    }

    public void Validate()
    {
        if (Slots is not { Length: Ff7BattleCommandQueue.Capacity } || ReadSequences is not { Length: Ff7BattleCommandQueue.PriorityCount } || WriteSequences is not { Length: Ff7BattleCommandQueue.PriorityCount } || Ready is not { Length: Ff7BattleCommandQueue.ReadyRingLength } || (uint)ReadyRead >= Ready.Length || (uint)ReadyWrite >= Ready.Length || LastEnqueuedPriority >= Ff7BattleCommandQueue.PriorityCount)
        {
            throw new InvalidDataException("FFVII battle command queue state has invalid bounds.");
        }

        HashSet<(byte Priority, byte Sequence)> sequences = new();
        for (int priority = 0; priority < ReadSequences.Length; priority++)
        {
            if (unchecked((byte)(WriteSequences[priority] - ReadSequences[priority])) > Slots.Length)
            {
                throw new InvalidDataException("FFVII battle command queue sequence window exceeds its physical capacity.");
            }
        }
        for (int slot = 0; slot < Slots.Length; slot++)
        {
            if (Slots[slot] is not Ff7BattleQueueEntry entry)
            {
                continue;
            }
            Ff7BattleCommandQueue.ValidateCommand(entry.Command);
            byte priority = entry.Command.Priority;
            int distance = unchecked((byte)(entry.Sequence - ReadSequences[priority]));
            int window = unchecked((byte)(WriteSequences[priority] - ReadSequences[priority]));
            if (entry.PhysicalSlot != slot || distance >= window || !sequences.Add((priority, entry.Sequence)))
            {
                throw new InvalidDataException("FFVII battle command queue entry is outside its native sequence window.");
            }
        }
        int readyCount = (ReadyWrite - ReadyRead + Ready.Length) % Ready.Length;
        for (int offset = 0; offset < Ready.Length; offset++)
        {
            Ff7BattleQueuedCommand? command = Ready[(ReadyRead + offset) % Ready.Length];
            if (offset < readyCount)
            {
                Ff7BattleCommandQueue.ValidateCommand(command);
                if (command!.Priority != Ff7BattleCommandQueue.ReadyPriority(command.CommandId))
                {
                    throw new InvalidDataException("FFVII battle readiness ring command has an invalid class.");
                }
            }
            else if (command is not null)
            {
                throw new InvalidDataException("FFVII battle readiness ring has an occupied slot outside its cursor range.");
            }
        }
    }
}

/// <summary>Native command insertion, readiness transfer and priority arbitration. The encounter consumes the selected command.</summary>
public static class Ff7BattleCommandQueue
{
    public const int Capacity = 64;
    public const int PriorityCount = 7;
    internal const int ReadyRingLength = 16;

    public static Ff7BattleCommandQueueState Enqueue(Ff7BattleCommandQueueState state, Ff7BattleQueuedCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateCommand(command);
        state.Validate();
        return Insert(state, command);
    }

    public static Ff7BattleCommandQueueState StageReady(Ff7BattleCommandQueueState state, int actorSlot, byte commandId, ushort argument = 0, ushort targetMask = 0, bool groupTarget = false, bool guestAttack = false, ushort guestAbility = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        Ff7BattleQueuedCommand command = new(ReadyPriority(commandId), actorSlot, commandId, argument, targetMask, GroupTarget: groupTarget) { GuestAttack = guestAttack, GuestAbility = guestAbility };
        ValidateCommand(command);
        int nextWrite = (state.ReadyWrite + 1) % ReadyRingLength;
        if (nextWrite == state.ReadyRead)
        {
            throw new InvalidOperationException("FFVII battle readiness ring has no free slot.");
        }
        Ff7BattleQueuedCommand?[] ready = (Ff7BattleQueuedCommand?[])state.Ready.Clone();
        ready[state.ReadyWrite] = command;
        return state with { Ready = ready, ReadyWrite = nextWrite };
    }

    public static Ff7BattleCommandQueueState PromoteReady(Ff7BattleCommandQueueState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        if (state.ReadyRead == state.ReadyWrite)
        {
            return state;
        }
        Ff7BattleCommandQueueState next = state;
        for (int read = state.ReadyRead; read != state.ReadyWrite; read = (read + 1) % ReadyRingLength)
        {
            next = Insert(next, state.Ready[read]!);
        }
        return next with { Ready = new Ff7BattleQueuedCommand?[ReadyRingLength], ReadyRead = state.ReadyWrite };
    }

    public static Ff7BattleQueueSelection TakeNext(Ff7BattleCommandQueueState state, int priorityLimitExclusive = PriorityCount)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfLessThan(priorityLimitExclusive, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(priorityLimitExclusive, PriorityCount);
        state.Validate();
        byte[]? read = null;
        for (int priority = 0; priority < priorityLimitExclusive; priority++)
        {
            byte sequence = state.ReadSequences[priority];
            if (sequence == state.WriteSequences[priority])
            {
                continue;
            }
            read ??= (byte[])state.ReadSequences.Clone();
            read[priority] = unchecked((byte)(sequence + 1));
            int slot = Array.FindIndex(state.Slots, entry => entry?.Command.Priority == priority && entry.Sequence == sequence);
            if (slot >= 0)
            {
                Ff7BattleQueueEntry?[] slots = (Ff7BattleQueueEntry?[])state.Slots.Clone();
                Ff7BattleQueueEntry entry = slots[slot]!;
                slots[slot] = null;
                return new(state with { ReadSequences = read, Slots = slots }, entry);
            }
        }
        return new(read is null ? state : state with { ReadSequences = read }, null);
    }

    public static uint ActorConditionsAfterEnqueue(Ff7BattleQueuedCommand command, uint actorConditions)
    {
        ValidateCommand(command);
        if (command.Priority < 2)
        {
            return actorConditions;
        }
        return (actorConditions & ~0x20U) | ((command.CommandId & 0x3F) == 0x13 ? 0x20U : 0);
    }

    internal static byte ReadyPriority(byte commandId) => commandId == 0x14 ? (byte)5 : (byte)6;

    internal static void ValidateCommand(Ff7BattleQueuedCommand? command)
    {
        if (command is null || command.Priority >= PriorityCount || command.ActorSlot is < -1 or > 9 || command.Callback is >= 16 || command.Message is not null && (command.CommandId != 0x21 || command.Message.Length == 0 || command.Message[^1] != 0xFF))
        {
            throw new InvalidDataException("FFVII battle command has invalid native fields.");
        }
        if (command.GuestAbility != 0 && !command.GuestAttack || command.GuestAttack && (command is not { Priority: 6, ActorSlot: >= 0 and < 3, AttackId: null, Message: null, Callback: null, GroupTarget: false } || command is not ({ GuestAbility: 0, CommandId: 1, Argument: 0 } or { GuestAbility: 1, CommandId: 2, Argument: 1 }) || (command.TargetMask & 0x3F0) != command.TargetMask || !System.Numerics.BitOperations.IsPow2(command.TargetMask)))
        {
            throw new InvalidDataException("A guest command requires one selected enemy occurrence and the ordinary player queue class.");
        }
    }

    private static Ff7BattleCommandQueueState Insert(Ff7BattleCommandQueueState state, Ff7BattleQueuedCommand command)
    {
        int slot = Array.FindIndex(state.Slots, entry => entry is null);
        byte priority = command.Priority;
        byte sequence = state.WriteSequences[priority];
        if (slot < 0 || unchecked((byte)(sequence - state.ReadSequences[priority])) >= Capacity)
        {
            throw new InvalidOperationException("FFVII battle command queue has no free slot in its native sequence window.");
        }
        Ff7BattleQueueEntry?[] slots = (Ff7BattleQueueEntry?[])state.Slots.Clone();
        byte[] write = (byte[])state.WriteSequences.Clone();
        slots[slot] = new(slot, sequence, command with { Message = command.Message?.ToArray() });
        write[priority] = unchecked((byte)(sequence + 1));
        return state with { Slots = slots, WriteSequences = write, LastEnqueuedPriority = priority };
    }
}
