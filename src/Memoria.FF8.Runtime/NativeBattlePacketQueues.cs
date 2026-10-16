using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5031E0/503240/5032E0's shared physical12-byte packet and sentinel-node queue.</summary>
internal static class NativeBattlePacketQueues
{
    internal static int PacketIndex(uint address, uint packetBase, int capacity)
    {
        uint offset = unchecked(address - packetBase);
        if (offset % 12 != 0 || offset / 12 >= capacity)
        {
            throw new InvalidDataException("Native packet queue contains an invalid physical packet pointer.");
        }
        return (int)(offset / 12);
    }

    internal static ImmutableArray<int> Queue(ImmutableArray<NativeBattleDescriptorNode> nodes, int? root, uint packetBase, int capacity)
    {
        ImmutableArray<int>.Builder result = ImmutableArray.CreateBuilder<int>();
        HashSet<int> seen = [];
        for (int? next = root is int head ? nodes[head].Next : null; next is int index; next = nodes[index].Next)
        {
            if ((uint)index >= capacity || !seen.Add(index))
            {
                throw new InvalidDataException("Native packet queue has an invalid or cyclic physical link.");
            }
            result.Add(PacketIndex(nodes[index].Data, packetBase, capacity));
        }
        return result.ToImmutable();
    }

    internal static (ImmutableArray<NativeBattleDescriptorPacket> Packets, ImmutableArray<NativeBattleDescriptorNode> Nodes, int? Root, int Slot) Allocate(ImmutableArray<NativeBattleDescriptorPacket> packets, ImmutableArray<NativeBattleDescriptorNode> nodes, int? root, uint packetBase, byte priority)
    {
        int slot = -1;
        for (int index = 0; index < packets.Length; index++)
        {
            if (packets[index].PriorityState == 0)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            throw new InvalidOperationException("Native packet queue exhausted its physical packet bank.");
        }
        if (root is null)
        {
            root = FreeNode(nodes);
            nodes = nodes.SetItem(root.Value, nodes[root.Value] with { Next = null, Data = 1 });
        }
        int node = FreeNode(nodes);
        nodes = nodes.SetItem(node, nodes[node] with { Next = null, Data = packetBase + 12u * (uint)slot });
        int tail = root.Value;
        while (nodes[tail].Next is int next)
        {
            tail = next;
        }
        nodes = nodes.SetItem(tail, nodes[tail] with { Next = node });
        byte[] bytes = packets[slot].Bytes.ToArray();
        bytes[1] = (byte)(priority & 0xF0);
        packets = packets.SetItem(slot, new(bytes.ToImmutableArray()));
        return (packets, nodes, root, slot);
    }

    internal static ImmutableArray<NativeBattleDescriptorNode> Rotate(ImmutableArray<NativeBattleDescriptorNode> nodes, int? root)
    {
        if (root is not int head || nodes[head].Next is not int first || nodes[first].Next is null)
        {
            return nodes;
        }
        nodes = nodes.SetItem(head, nodes[head] with { Next = nodes[first].Next }).SetItem(first, nodes[first] with { Next = null });
        int tail = head;
        while (nodes[tail].Next is int next)
        {
            tail = next;
        }
        return nodes.SetItem(tail, nodes[tail] with { Next = first });
    }

    internal static (ImmutableArray<NativeBattleDescriptorPacket> Packets, ImmutableArray<NativeBattleDescriptorNode> Nodes, int? Root) RemoveFirst(ImmutableArray<NativeBattleDescriptorPacket> packets, ImmutableArray<NativeBattleDescriptorNode> nodes, int? root, uint packetBase)
    {
        if (root is not int head || nodes[head].Next is not int first)
        {
            throw new InvalidDataException("Native packet removal has no queued node.");
        }
        int packet = PacketIndex(nodes[first].Data, packetBase, packets.Length);
        byte[] bytes = packets[packet].Bytes.ToArray();
        bytes[1] = 0;
        nodes = nodes.SetItem(head, nodes[head] with { Next = nodes[first].Next }).SetItem(first, nodes[first] with { Data = 0 });
        if (nodes[head].Next is null)
        {
            nodes = nodes.SetItem(head, nodes[head] with { Data = 0 });
            root = null;
        }
        return (packets.SetItem(packet, packets[packet] with { Bytes = bytes.ToImmutableArray() }), nodes, root);
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
        throw new InvalidOperationException("Native packet queue exhausted its physical nodes, including its sentinel.");
    }
}
