using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDescriptorPacket([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    [JsonRequired]
    public ushort UnknownByteMask { get; init; }
    [JsonRequired]
    public BattleEvent? Message { get; init; }
    [JsonIgnore] public byte Serial => Bytes[0];
    [JsonIgnore] public byte PriorityState => Bytes[1];
    [JsonIgnore] public ushort Command => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(2));
    [JsonIgnore] public uint Argument => (UnknownByteMask & 0xF0) == 0 ? BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(4)) : throw new NotSupportedException("Native descriptor argument is a logical text binding, not an invented native heap address.");
    [JsonIgnore] public short Index => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(8));
    [JsonIgnore] public ushort Value => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(10));
    [JsonIgnore] public uint Data => BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(8));
}

public sealed record NativeBattleDescriptorNode([property: JsonRequired] int? Next, [property: JsonRequired] uint Data);

public enum NativeBattleDescriptorTaskKind
{
    None,
    Pump500CC0,
    Load502670,
    Remove502ED0,
    Barrier5085F0,
    Callback502F30,
    Stage506CF0,
    Texture506DE0,
    Map500AC0,
    Appearance500B60
}

public sealed record NativeBattleDescriptorTask([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] NativeBattleDescriptorTaskKind Kind, [property: JsonRequired] ImmutableArray<byte> Payload)
{
    [JsonIgnore] public byte Phase => Payload[1];
    [JsonIgnore] public int PacketSlot => checked((int)((BinaryPrimitives.ReadUInt32LittleEndian(Payload.AsSpan().Slice(4)) - 0x1D96AB8u) / 12));
}

/// <summary>500C00's30 retained12-byte packets,30 physical queue nodes,16-byte priority stack and four20-byte callbacks.</summary>
public sealed partial record NativeBattleDescriptorState([property: JsonRequired] ImmutableArray<NativeBattleDescriptorPacket> Packets, [property: JsonRequired] ImmutableArray<NativeBattleDescriptorNode> Nodes, [property: JsonRequired] int? Root, [property: JsonRequired] ImmutableArray<byte> Priorities, [property: JsonRequired] byte Depth, [property: JsonRequired] byte Serial, [property: JsonRequired] byte CurrentCommand, [property: JsonRequired] byte CurrentSerial, [property: JsonRequired] ImmutableArray<NativeBattleDescriptorTask> Tasks, [property: JsonRequired] int? TaskHead, [property: JsonRequired] int? TaskTail)
{
    public static NativeBattleDescriptorState Clear()
    {
        NativeBattleDescriptorState state = new(Enumerable.Range(0, 30).Select(_ => new NativeBattleDescriptorPacket(ImmutableArray.CreateRange(new byte[12]))).ToImmutableArray(), Enumerable.Range(0, 30).Select(_ => new NativeBattleDescriptorNode(null, 0)).ToImmutableArray(), null, ImmutableArray.CreateRange(new byte[16]), 0, 0, 0, 0, Enumerable.Range(0, 4).Select(_ => new NativeBattleDescriptorTask(0, 0, null, NativeBattleDescriptorTaskKind.None, ImmutableArray.CreateRange(new byte[8]))).ToImmutableArray(), null, null);
        return state.AppendTask(NativeBattleDescriptorTaskKind.Pump500CC0).State;
    }

    [JsonIgnore]
    public ImmutableArray<int> Queue
    {
        get
        {
            ImmutableArray<int>.Builder result = ImmutableArray.CreateBuilder<int>();
            for (int? next = Root is int root ? Nodes[root].Next : null; next is int index; next = Nodes[index].Next)
            {
                result.Add(PacketIndex(Nodes[index].Data));
            }
            return result.ToImmutable();
        }
    }

    public void Validate()
    {
        if (Packets.IsDefault || Packets.Length != 30 || Packets.Any(packet => packet is null || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & ~0xF0) != 0 || (packet.UnknownByteMask == 0) != (packet.Message is null) || packet.Message is not null && packet.Message.Kind != "NativeText") || Nodes.IsDefault || Nodes.Length != 30 || Nodes.Any(node => node is null || node.Next is < 0 or >= 30) || Root is < 0 or >= 30 || Priorities.IsDefault || Priorities.Length != 16 || Depth >= 16 || Tasks.IsDefault || Tasks.Length != 4 || Tasks.Any(task => task is null || task.Next is < 0 or >= 4 || !Enum.IsDefined(task.Kind) || task.Payload.IsDefault || task.Payload.Length != 8 || (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.None) || TaskHead is < 0 or >= 4 || TaskTail is < 0 or >= 4)
        {
            throw new InvalidDataException("Native descriptors require their physical packet/node/task banks and priority stack.");
        }
        HashSet<int> nodes = [];
        HashSet<int> packets = [];
        if (Root is int root)
        {
            if (Nodes[root].Data != 1)
            {
                throw new InvalidDataException("Native descriptor list lacks its allocated sentinel.");
            }
            nodes.Add(root);
            for (int? next = Nodes[root].Next; next is int index; next = Nodes[index].Next)
            {
                if (!nodes.Add(index) || !packets.Add(PacketIndex(Nodes[index].Data)) || Packets[PacketIndex(Nodes[index].Data)].PriorityState == 0)
                {
                    throw new InvalidDataException("Native descriptor links contain a cycle, duplicate or free packet.");
                }
            }
        }
        HashSet<int> tasks = [];
        for (int? next = TaskHead; next is int index; next = Tasks[index].Next)
        {
            if (!tasks.Add(index) || (Tasks[index].Flags & 1) == 0)
            {
                throw new InvalidDataException("Native descriptor callback list contains a cycle or a free task.");
            }
            if (Tasks[index].Kind != NativeBattleDescriptorTaskKind.Pump500CC0)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(Tasks[index].Payload.AsSpan().Slice(4));
                int packet = PacketIndex(pointer);
                if (!packets.Contains(packet) && !(Tasks[index].Kind == NativeBattleDescriptorTaskKind.Appearance500B60 && Tasks[index].Phase != 0))
                {
                    throw new InvalidDataException("Native asynchronous callback lost its retained descriptor packet.");
                }
            }
        }
    }

    public (NativeBattleDescriptorState State, int Slot) Enqueue(ushort command, byte priority, uint argument, ushort? index = null, ushort? value = null)
    {
        Validate();
        int packet = -1;
        for (int slot = 0; slot < 30; slot++)
        {
            if (Packets[slot].PriorityState == 0)
            {
                packet = slot;
                break;
            }
        }
        if (packet < 0)
        {
            throw new InvalidOperationException("Native descriptor bank exhausted its30 physical packets.");
        }
        ImmutableArray<NativeBattleDescriptorNode> nodes = Nodes;
        int? root = Root;
        if (root is null)
        {
            root = FreeNode(nodes);
            nodes = nodes.SetItem(root.Value, nodes[root.Value] with { Next = null, Data = 1 });
        }
        int node = FreeNode(nodes);
        nodes = nodes.SetItem(node, nodes[node] with { Next = null, Data = PacketAddress(packet) });
        int tail = root.Value;
        while (nodes[tail].Next is int next)
        {
            tail = next;
        }
        nodes = nodes.SetItem(tail, nodes[tail] with { Next = node });
        byte[] bytes = Packets[packet].Bytes.ToArray();
        bytes[0] = Serial;
        bytes[1] = (byte)(priority & 0xF0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), command);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), argument);
        if (index is ushort low)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), low);
        }
        if (value is ushort high)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), high);
        }
        return (this with { Nodes = nodes, Root = root, Packets = Packets.SetItem(packet, new(bytes.ToImmutableArray())), Serial = unchecked((byte)(Serial + 1)) }, packet);
    }

    public (NativeBattleDescriptorState State, int Slot) EnqueueMessage(BattleEvent message, byte priority, byte tag)
    {
        if (message.Kind != "NativeText" || message.NativeMessageMode is not (>= 0 and <= 255))
        {
            throw new InvalidDataException("Native text descriptor requires its message and original mode BYTE.");
        }
        (NativeBattleDescriptorState state, int slot) = Enqueue(8, priority, 0, unchecked((ushort)message.Amount), (ushort)(message.NativeMessageMode.Value | tag << 8));
        return (state with { Packets = state.Packets.SetItem(slot, state.Packets[slot] with { UnknownByteMask = 0xF0, Message = message }) }, slot);
    }

    public NativeBattleDescriptorState Rotate()
    {
        if (Root is not int root || Nodes[root].Next is not int first || Nodes[first].Next is null)
        {
            return this;
        }
        ImmutableArray<NativeBattleDescriptorNode> nodes = Nodes.SetItem(root, Nodes[root] with { Next = Nodes[first].Next }).SetItem(first, Nodes[first] with { Next = null });
        int tail = root;
        while (nodes[tail].Next is int next)
        {
            tail = next;
        }
        return this with { Nodes = nodes.SetItem(tail, nodes[tail] with { Next = first }) };
    }

    public NativeBattleDescriptorState RemoveFirst()
    {
        if (Root is not int root || Nodes[root].Next is not int first)
        {
            throw new InvalidDataException("Native descriptor removal has no queued node.");
        }
        int packet = PacketIndex(Nodes[first].Data);
        byte[] bytes = Packets[packet].Bytes.ToArray();
        bytes[1] = 0;
        ImmutableArray<NativeBattleDescriptorNode> nodes = Nodes.SetItem(root, Nodes[root] with { Next = Nodes[first].Next }).SetItem(first, Nodes[first] with { Data = 0 });
        int? newRoot = root;
        if (nodes[root].Next is null)
        {
            nodes = nodes.SetItem(root, nodes[root] with { Data = 0 });
            newRoot = null;
        }
        return this with { Nodes = nodes, Root = newRoot, Packets = Packets.SetItem(packet, Packets[packet] with { Bytes = bytes.ToImmutableArray() }) };
    }

    public NativeBattleDescriptorState SetPacketState(int slot, byte value)
    {
        byte[] bytes = Packets[slot].Bytes.ToArray();
        bytes[1] = value;
        return this with { Packets = Packets.SetItem(slot, Packets[slot] with { Bytes = bytes.ToImmutableArray() }) };
    }

    public (NativeBattleDescriptorState State, int Slot) AppendTask(NativeBattleDescriptorTaskKind kind, int? packet = null)
    {
        int slot = -1;
        for (int index = 0; index < 4; index++)
        {
            if ((Tasks[index].Flags & 1) == 0)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            throw new InvalidOperationException("Native descriptor callback bank exhausted itsfour physical tasks.");
        }
        NativeBattleDescriptorTask task = Tasks[slot];
        byte[] payload = task.Payload.ToArray();
        if (packet is int owner)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), PacketAddress(owner));
        }
        //502F30 does not initialize its retained phase byte;102/105 explicitly clear BYTE13.
        if (kind is NativeBattleDescriptorTaskKind.Load502670 or NativeBattleDescriptorTaskKind.Remove502ED0 or NativeBattleDescriptorTaskKind.Stage506CF0 or NativeBattleDescriptorTaskKind.Texture506DE0 or NativeBattleDescriptorTaskKind.Map500AC0 or NativeBattleDescriptorTaskKind.Appearance500B60)
        {
            payload[1] = 0;
        }
        if (kind == NativeBattleDescriptorTaskKind.Appearance500B60)
        {
            payload[3] = 20;
        }
        ImmutableArray<NativeBattleDescriptorTask> tasks = Tasks.SetItem(slot, task with { Flags = (ushort)(task.Flags | 1), State = 0, Next = null, Kind = kind, Payload = payload.ToImmutableArray() });
        if (TaskTail is int tail)
        {
            tasks = tasks.SetItem(tail, tasks[tail] with { Next = slot });
        }
        return (this with { Tasks = tasks, TaskHead = TaskTail is null ? slot : TaskHead, TaskTail = slot }, slot);
    }

    private static int FreeNode(ImmutableArray<NativeBattleDescriptorNode> nodes)
    {
        for (int index = 0; index < nodes.Length; index++)
        {
            if (nodes[index].Data == 0)
            {
                return index;
            }
        }
        throw new InvalidOperationException("Native descriptor node bank exhausted its30 physical nodes, including the sentinel.");
    }

    private static uint PacketAddress(int slot)
    {
        if ((uint)slot >= 30)
        {
            throw new InvalidDataException("Native descriptor packet slot is invalid.");
        }
        return 0x1D96AB8u + 12u * (uint)slot;
    }

    private static int PacketIndex(uint address)
    {
        uint offset = unchecked(address - 0x1D96AB8u);
        if (offset % 12 != 0 || offset / 12 >= 30)
        {
            throw new InvalidDataException("Native descriptor node or task has an invalid packet pointer.");
        }
        return (int)(offset / 12);
    }
}
