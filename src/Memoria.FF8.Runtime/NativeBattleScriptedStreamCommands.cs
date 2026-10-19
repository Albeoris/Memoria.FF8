using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedStreamStep(NativeBattleWaterExecutionFrame Frame, NativeBattleSummonStreamState Stream);

public static class NativeBattleScriptedStreamCommands
{
    public static NativeBattleScriptedStreamStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleSummonStreamState stream, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readExternal, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> clearExternal)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        if (opcode is not (0x31 or 0x34))
        {
            throw new NotSupportedException("Native stream commands require their selected stream or ready-wait instruction.");
        }
        uint manager = CoreDword(frame, 0x27973EC);
        if (opcode == 0x34)
        {
            return frame.Core.Read(manager + 0xA3, 1)[0] == 0 ? Wait() : new(NativeBattleScriptedCommandReads.Advance(frame, 2), stream);
        }
        if (frame.Core.Read(0x2798219, 1)[0] != 0)
        {
            return Wait();
        }
        int index = (instruction >> 9) & 15;
        uint map = CoreDword(frame, manager + 0xB0);
        byte file = Read(unchecked(map + (uint)index), 1)[0];
        uint source;
        if (file == 0xFF)
        {
            source = CoreDword(frame, manager + 0xB8);
        }
        else
        {
            uint resource = CoreDword(frame, unchecked(0x2798A68u + (uint)(4 * file)));
            uint directory = unchecked(resource + Dword(unchecked(resource + 0x18)));
            source = unchecked(directory + Dword(unchecked(directory + (uint)(4 * index))));
        }
        if ((instruction & 0x8000) != 0)
        {
            frame = clearExternal(frame);
        }
        stream = stream.Publish501860(source, unchecked((int)Dword(source)));
        frame = frame with { Core = frame.Core.Write(manager + 0xA3, [1]) };
        return new(NativeBattleScriptedCommandReads.Advance(frame, 2), stream);

        NativeBattleScriptedStreamStep Wait() => new(frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8)), stream);
        byte[] Read(uint address, int length) => NativeBattleScriptedCommandReads.Address(frame, address, length, readExternal);
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    }
}
