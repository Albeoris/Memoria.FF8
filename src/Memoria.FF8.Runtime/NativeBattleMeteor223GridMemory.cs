using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeteor223GridPoint(NativeBattleWaterExecutionFrame Frame, uint Screen, int QuarterDepth, uint Flags);
public sealed record NativeBattleMeteor223GridServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> SelectTexture, Func<NativeBattleWaterExecutionFrame, NativeBattleMeteor223GridPoint> Project, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Publish, Func<uint, int, byte[]> ReadStatic);

internal sealed class NativeBattleMeteor223GridMemory
{
    internal NativeBattleWaterExecutionFrame Frame;
    internal readonly NativeBattleMeteor223GridServices Services;
    internal int Workspace => unchecked((int)Frame.TemporaryFrame.Address);
    internal int Current => unchecked((int)CoreDword(Frame, 0x27973E8));
    internal int Manager => unchecked((int)CoreDword(Frame, 0x27973EC));
    internal int Runtime => unchecked((int)CoreDword(Frame, 0x27973B8));
    private int packetStart;
    private int packetEnd;
    private readonly Dictionary<int, byte> packet = [];
    private readonly ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
    internal Int32Access Dwords { get; }
    internal UInt16Access Words { get; }
    internal Int16Access SignedWords { get; }
    internal ByteAccess Bytes { get; }

    internal NativeBattleMeteor223GridMemory(NativeBattleWaterExecutionFrame frame, NativeBattleMeteor223GridServices services)
    {
        Frame = frame;
        Services = services;
        Dwords = new(this);
        Words = new(this);
        SignedWords = new(this);
        Bytes = new(this);
    }

    internal int ResolveNode(int selector) => unchecked((int)Services.ResolveNode(Frame, unchecked((short)selector)));

    internal void BeginPackets(int start, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
        {
            throw new NotSupportedException("Native Meteor grid requires positive row and column counts; unwritten stack-local paths remain unsupported.");
        }
        packetStart = start;
        packetEnd = checked(start + checked((rows + 2) * columns + 2) * 0x44);
    }

    internal int Project(int source, int output)
    {
        if (source != Workspace + 0xC0)
        {
            throw new InvalidDataException("Native Meteor grid projection requires its actual workspace vector.");
        }
        NativeBattleMeteor223GridPoint result = Services.Project(Frame);
        Frame = result.Frame;
        Dwords[output, 0] = unchecked((int)result.Screen);
        Dwords[Workspace, 0xFC] = unchecked((int)result.Flags);
        return result.QuarterDepth;
    }

    internal void Link(int ordering, int address)
    {
        byte[] bytes = new byte[0x44];
        for (int index = 3; index < bytes.Length; index++)
        {
            bytes[index] = packet.GetValueOrDefault(address + index);
        }
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 * index))).ToImmutableArray();
        uint basis = Frame.ReadTemporaryDword(0x14C);
        uint delta = unchecked((uint)ordering - basis);
        if ((delta & 3) != 0 || delta / 4 >= 0xFFF)
        {
            throw new InvalidDataException("Native Meteor grid requires its actual alternate ordering-table address.");
        }
        NativeBattleGeometryPrimitive primitive = new(checked((int)(delta / 4)), words, []);
        attempts.Add(new(checked(address - packetStart), bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, bytes.Length).ToImmutableArray(), primitive));
    }

    internal NativeBattleWaterExecutionFrame Publish(int cursor)
    {
        NativeBattleGfGeometryBatch batch = new(attempts.ToImmutable(), checked(cursor - packetStart)) { NativeUnlinkedAllocation = true };
        return Services.Publish(Frame, unchecked((uint)packetStart), batch);
    }

    private byte[] Read(int address, int length)
    {
        if (packetEnd != 0 && address >= packetStart && address <= packetEnd - length)
        {
            return Enumerable.Range(address, length).Select(index => packet.TryGetValue(index, out byte value) ? value : throw new NotSupportedException("Native Meteor grid read requires a previously written packet byte.")).ToArray();
        }
        uint pointer = unchecked((uint)address);
        if (pointer >= Frame.TemporaryFrame.Address && (long)pointer - Frame.TemporaryFrame.Address <= 0x180 - length)
        {
            return Frame.ReadTemporary(checked((int)(pointer - Frame.TemporaryFrame.Address)), length);
        }
        if (pointer >= NativeBattleScriptedEffectMemory.Address && (long)pointer - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - length)
        {
            return Frame.Core.Read(pointer, length);
        }
        if (pointer >= NativeBattleEffectNodeMemory.ArenaAddress && (long)pointer - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
        {
            return Frame.Arena.Read(ArenaOffset(pointer, length), length);
        }
        return Services.ReadStatic(pointer, length);
    }

    private void Store(int address, ReadOnlySpan<byte> bytes)
    {
        if (packetEnd != 0 && address >= packetStart && address <= packetEnd - bytes.Length)
        {
            int aligned = address & ~3;
            int length = (address - aligned + bytes.Length + 3) & ~3;
            byte[] data = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            for (int index = 0; index < bytes.Length; index++)
            {
                packet[address + index] = bytes[index];
                data[address - aligned + index] = bytes[index];
                mask[address - aligned + index] = NativeBattleGfPacketByteState.Known;
            }
            attempts.Add(new(checked(aligned - packetStart), data.ToImmutableArray(), mask.ToImmutableArray(), null));
            return;
        }
        uint pointer = unchecked((uint)address);
        if (pointer >= Frame.TemporaryFrame.Address && (long)pointer - Frame.TemporaryFrame.Address <= 0x180 - bytes.Length)
        {
            Frame = Frame.WriteTemporary(checked((int)(pointer - Frame.TemporaryFrame.Address)), bytes);
        }
        else if (pointer >= NativeBattleScriptedEffectMemory.Address && (long)pointer - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - bytes.Length)
        {
            Frame = Frame with { Core = Frame.Core.Write(pointer, bytes) };
        }
        else
        {
            Frame = Frame with { Arena = Frame.Arena.Write(ArenaOffset(pointer, bytes.Length), bytes) };
        }
    }

    internal sealed class Int32Access(NativeBattleMeteor223GridMemory memory)
    {
        public int this[int address, int offset]
        {
            get => BinaryPrimitives.ReadInt32LittleEndian(memory.Read(unchecked(address + offset), 4));
            set => memory.Store(unchecked(address + offset), BitConverter.GetBytes(value));
        }
    }

    internal sealed class UInt16Access(NativeBattleMeteor223GridMemory memory)
    {
        public ushort this[int address, int offset]
        {
            get => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(unchecked(address + offset), 2));
            set => memory.Store(unchecked(address + offset), BitConverter.GetBytes(value));
        }
    }

    internal sealed class Int16Access(NativeBattleMeteor223GridMemory memory)
    {
        public short this[int address, int offset]
        {
            get => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(unchecked(address + offset), 2));
        }
    }

    internal sealed class ByteAccess(NativeBattleMeteor223GridMemory memory)
    {
        public byte this[int address, int offset]
        {
            get => memory.Read(unchecked(address + offset), 1)[0];
            set => memory.Store(unchecked(address + offset), new byte[] { value });
        }
    }
}
