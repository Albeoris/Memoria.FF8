using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicCapture96Services(Func<NativeBattleWaterExecutionFrame, byte> ReadBuffer80, Func<NativeBattleWaterExecutionFrame, byte, ImmutableArray<byte>> ReadDrawingEnvironment, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>The cinematic five-strip screen capture packet producer.</summary>
public static class NativeBattleCinematicCapture96
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicCapture96Services services)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x96)
        {
            throw new NotSupportedException("Native cinematic capture requires its selected command.");
        }
        short destinationX = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(int)destinationX));
        short destinationY = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)(int)destinationY));
        frame = Capture(frame, services);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }

    public static NativeBattleWaterExecutionFrame Capture(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicCapture96Services services)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint manager = CoreDword(frame, 0x27973EC);
        byte buffer = services.ReadBuffer80(frame);
        uint cursor = CoreDword(frame, unchecked(manager + 0x7C));
        if (buffer > 1)
        {
            throw new NotSupportedException("Native cinematic capture requires its selected DRAWENV bank.");
        }
        Append([0x01000000, 0xE6000000], 12);
        ImmutableArray<byte> environment = services.ReadDrawingEnvironment(frame, buffer);
        if (environment.IsDefault || environment.Length < 4)
        {
            throw new InvalidDataException("Native cinematic capture requires its current DRAWENV source coordinates.");
        }
        frame = frame.WriteTemporaryWord(0xF8, BinaryPrimitives.ReadUInt16LittleEndian(environment.AsSpan()))
            .WriteTemporaryWord(0xFA, BinaryPrimitives.ReadUInt16LittleEndian(environment.AsSpan().Slice(2)))
            .WriteTemporaryDword(0xFC, 0x00E00040);
        int destinationX = unchecked((int)frame.ReadTemporaryDword(0xF0));
        int destinationY = unchecked((int)frame.ReadTemporaryDword(0xF4));
        for (int strip = 0; strip < 5; strip++)
        {
            uint extent = frame.ReadTemporaryDword(0xFC);
            if (unchecked((short)extent) > 0 && unchecked((short)(extent >> 16)) > 0)
            {
                uint source = frame.ReadTemporaryDword(0xF8);
                uint destination = unchecked((uint)((destinationX & 0xFFFF) | (destinationY << 16)));
                Append([0x05000000, 0x01000000, 0x80000000, source, destination, extent], 24);
            }
            else
            {
                Append([0], 24);
            }
            destinationX = unchecked(destinationX + 64);
            frame = frame.WriteTemporaryDword(0xF8, unchecked(frame.ReadTemporaryDword(0xF8) + 64));
        }
        Append([0x01000000, 0xE6000001], 12);
        manager = CoreDword(frame, 0x27973EC);
        return WriteCoreDword(frame, unchecked(manager + 0x7C), cursor);

        void Append(ImmutableArray<uint> words, int allocation)
        {
            byte[] data = new byte[allocation];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, data.Length).Select(index => index >= 3 && index < words.Length * 4 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            ImmutableArray<uint> packetWords = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(1, packetWords, []) { PhysicalWriteMask = mask };
            NativeBattleGfGeometryBatch batch = new([new(0, data.ToImmutableArray(), mask, primitive)], allocation);
            uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + 4);
            frame = services.Append(frame, cursor, ordering, batch);
            cursor = unchecked(cursor + (uint)allocation);
        }
    }
}
