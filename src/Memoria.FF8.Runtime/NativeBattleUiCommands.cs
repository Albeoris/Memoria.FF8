using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUiReadyNode([property: JsonRequired] int? Next, [property: JsonRequired] ushort Occupied);

/// <summary>4AD670's20 physical UI packets and three retained ready-character links.</summary>
public sealed record NativeBattleUiCommands([property: JsonRequired] ImmutableArray<NativeBattleDescriptorPacket> Packets, [property: JsonRequired] ImmutableArray<NativeBattleDescriptorNode> Nodes, [property: JsonRequired] int? Root, [property: JsonRequired] ImmutableArray<NativeBattleUiReadyNode> ReadyNodes, [property: JsonRequired] int? ReadyHead, [property: JsonRequired] sbyte? SelectedActor)
{
    public static NativeBattleUiCommands Clear()
    {
        return new(Enumerable.Range(0, 20).Select(_ => new NativeBattleDescriptorPacket(ImmutableArray.CreateRange(new byte[12])) { UnknownByteMask = 0xFFD }).ToImmutableArray(), Enumerable.Range(0, 20).Select(_ => new NativeBattleDescriptorNode(null, 0)).ToImmutableArray(), null, Enumerable.Range(0, 3).Select(_ => new NativeBattleUiReadyNode(null, 0)).ToImmutableArray(), null, null);
    }

    [JsonIgnore] public ImmutableArray<int> Queue => NativeBattlePacketQueues.Queue(Nodes, Root, 0x1D75258, 20);

    public NativeBattleUiCommands Enqueue(byte actor, ushort command)
    {
        if (actor >= 3 || command is not (0x11 or 0x12))
        {
            throw new InvalidDataException("Native UI ready commands require a player physical slot and command17/18.");
        }
        (ImmutableArray<NativeBattleDescriptorPacket> packets, ImmutableArray<NativeBattleDescriptorNode> nodes, int? root, int slot) = NativeBattlePacketQueues.Allocate(Packets, Nodes, Root, 0x1D75258, 0x80);
        byte[] bytes = packets[slot].Bytes.ToArray();
        bytes[0] = actor;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), command);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0);
        return this with { Packets = packets.SetItem(slot, new(bytes.ToImmutableArray()) { UnknownByteMask = 0xF00 }), Nodes = nodes, Root = root };
    }

    public (NativeBattleUiCommands Commands, uint ReadyMask, bool SelectMenu) Dispatch(uint readyMask)
    {
        NativeBattleUiCommands state = this;
        bool selectMenu = false;
        //4AD400 fixes the queue count before invoking any command callback.
        int count = Queue.Length;
        for (int index = 0; index < count; index++)
        {
            NativeBattleDescriptorPacket packet = state.Packets[state.Queue[0]];
            int actor = packet.Bytes[0];
            uint bit = 1u << actor;
            ImmutableArray<NativeBattleUiReadyNode> ready = state.ReadyNodes;
            int? head = state.ReadyHead;
            sbyte? selected = state.SelectedActor;
            if (packet.Command == 0x11)
            {
                if (ready[actor].Occupied == 0)
                {
                    ready = ready.SetItem(actor, new(null, 1));
                    if (head is int first)
                    {
                        int tail = first;
                        while (ready[tail].Next is int next)
                        {
                            tail = next;
                        }
                        ready = ready.SetItem(tail, ready[tail] with { Next = actor });
                    }
                    else
                    {
                        head = actor;
                    }
                }
                if (readyMask == 0)
                {
                    selected = checked((sbyte)actor);
                    selectMenu = true;
                }
                readyMask |= bit;
            }
            else if (packet.Command == 0x12)
            {
                int? following = null;
                int? previous = null;
                for (int? current = head; current is int slot; current = ready[slot].Next)
                {
                    if (slot == actor)
                    {
                        following = ready[slot].Next ?? head;
                        if (previous is int before)
                        {
                            ready = ready.SetItem(before, ready[before] with { Next = ready[slot].Next });
                        }
                        else
                        {
                            head = ready[slot].Next;
                        }
                        ready = ready.SetItem(slot, new(null, 0));
                        break;
                    }
                    previous = slot;
                }
                readyMask &= ~bit;
                if (readyMask == 0)
                {
                    selected = -1;
                    selectMenu = true;
                }
                else if (selected == actor)
                {
                    selected = checked((sbyte)(following ?? head ?? throw new InvalidDataException("Native ready mask lost its ordered player list.")));
                    selectMenu = true;
                }
            }
            else
            {
                throw new NotSupportedException($"Native UI command{packet.Command} is not implemented by the ready owner.");
            }
            (ImmutableArray<NativeBattleDescriptorPacket> packets, ImmutableArray<NativeBattleDescriptorNode> nodes, int? root) = NativeBattlePacketQueues.RemoveFirst(state.Packets, state.Nodes, state.Root, 0x1D75258);
            state = state with { Packets = packets, Nodes = nodes, Root = root, ReadyNodes = ready, ReadyHead = head, SelectedActor = selected };
        }
        return (state, readyMask, selectMenu);
    }

    public void Validate(uint readyMask)
    {
        if (Packets.IsDefault || Packets.Length != 20 || Packets.Any(packet => packet is null || packet.Bytes.IsDefault || packet.Bytes.Length != 12 || (packet.UnknownByteMask & ~0xFFDu) != 0 || packet.Message is not null) || Nodes.IsDefault || Nodes.Length != 20 || Nodes.Any(node => node is null || node.Next is < 0 or >= 20) || Root is < 0 or >= 20 || ReadyNodes.IsDefault || ReadyNodes.Length != 3 || ReadyNodes.Any(node => node is null || node.Next is < 0 or >= 3 || node.Occupied > 1) || ReadyHead is < 0 or >= 3 || SelectedActor is < -1 or >= 3 || (readyMask & ~7u) != 0)
        {
            throw new InvalidDataException("Native UI command owner lacks its physical20-packet/three-ready banks.");
        }
        HashSet<int> packets = [];
        foreach (int slot in Queue)
        {
            if (!packets.Add(slot) || Packets[slot].UnknownByteMask != 0xF00 || Packets[slot].PriorityState != 0x80 || Packets[slot].Bytes[0] >= 3 || Packets[slot].Command is not (0x11 or 0x12))
            {
                throw new InvalidDataException("Native UI command queue contains a duplicate, free or invalid ready packet.");
            }
        }
        HashSet<int> ready = [];
        uint mask = 0;
        for (int? next = ReadyHead; next is int slot; next = ReadyNodes[slot].Next)
        {
            if (!ready.Add(slot) || ReadyNodes[slot].Occupied != 1)
            {
                throw new InvalidDataException("Native UI ready list contains a cycle or free link.");
            }
            mask |= 1u << slot;
        }
        if (mask != readyMask || ReadyNodes.Where((node, slot) => node.Occupied != 0 && !ready.Contains(slot)).Any())
        {
            throw new InvalidDataException("Native UI ready mask contradicts its canonical ordered list.");
        }
    }
}
