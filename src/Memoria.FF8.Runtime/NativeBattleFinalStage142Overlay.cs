using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Only bytes selected by WriteMask are stores; linking each packet belongs to the caller.</summary>
public sealed record NativeBattleFinalStage142OverlayPacket(uint Address, ImmutableArray<byte> Bytes, ImmutableArray<NativeBattleGfPacketByteState> WriteMask, int AllocatedBytes, int Bucket);
public sealed record NativeBattleFinalStage142OverlayStep(NativeBattleFinalStage142State State, ImmutableArray<NativeBattleFinalStage142OverlayPacket> Packets);

public static class NativeBattleFinalStage142Overlay
{
    public const int OrderingBucket = 4385;

    public static NativeBattleFinalStage142OverlayStep Build(NativeBattleFinalStage142State state, NativeBattleFinalStage142Command command, ImmutableArray<byte> oppositeDrawEnvironment92, uint dpcsColor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        state.Validate();
        if (command.Kind != NativeBattleFinalStage142CommandKind.Overlay || oppositeDrawEnvironment92.IsDefault || oppositeDrawEnvironment92.Length != 92)
        {
            throw new InvalidDataException("Native final stage overlay requires its overlay command and actual opposite draw environment.");
        }
        state.WriteOverlay(command.PacketAddress, new byte[NativeBattleFinalStage142.OverlayBytes].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, NativeBattleFinalStage142.OverlayBytes).ToImmutableArray());
        ImmutableArray<NativeBattleFinalStage142OverlayPacket>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleFinalStage142OverlayPacket>(6);
        uint cursor = command.PacketAddress;
        Area(Word(0), Word(2), Word(4), Word(6));
        Offset(Word(0), Word(2));
        byte[] rectangle = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(rectangle, 0x03000000);
        BinaryPrimitives.WriteUInt32LittleEndian(rectangle.AsSpan(4), (dpcsColor & 0xFFFFFF) | 0x62000000);
        BinaryPrimitives.WriteInt16LittleEndian(rectangle.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(rectangle.AsSpan(10), 0);
        BinaryPrimitives.WriteInt16LittleEndian(rectangle.AsSpan(12), 255);
        BinaryPrimitives.WriteInt16LittleEndian(rectangle.AsSpan(14), unchecked((short)command.Z));
        Add(rectangle.ToImmutableArray(), 0, 16);
        Area(0, 240, 256, 16);
        Offset(0, 240);
        byte[] mode = new byte[8];
        mode[3] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000600u | (unchecked((uint)command.Y) & 0x9FF));
        Add(mode.ToImmutableArray(), 3, 8);
        return new(state, packets.MoveToImmutable());

        short Word(int offset)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(oppositeDrawEnvironment92.AsSpan().Slice(offset));
        }

        void Area(short x, short y, short width, short height)
        {
            Add(NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), x, y, width, height), 3, 12);
        }

        void Offset(short x, short y)
        {
            Add(NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[12].ToImmutableArray(), x, y), 3, 8);
        }

        void Add(ImmutableArray<byte> bytes, int firstWritten, int endWritten)
        {
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, bytes.Length).ToArray();
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, firstWritten, endWritten - firstWritten);
            ImmutableArray<NativeBattleGfPacketByteState> writeMask = mask.ToImmutableArray();
            state = state.WriteOverlay(cursor, bytes, writeMask);
            packets.Add(new(cursor, bytes, writeMask, bytes.Length, OrderingBucket));
            cursor = unchecked(cursor + (uint)bytes.Length);
        }
    }
}
