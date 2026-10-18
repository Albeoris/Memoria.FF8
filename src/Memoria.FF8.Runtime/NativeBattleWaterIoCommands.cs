using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterFileReadRequest(short FileId, uint DestinationAddress, int Argument, uint CompletionCallback);
public sealed record NativeBattleWaterIoCommandResult(NativeBattleScriptedEffectMemory Core, uint NextInstructionPointer, ushort? YieldWait, NativeBattleWaterFileReadRequest? Request, NativeBattleEffectTextureState? Textures);

/// <summary>The selected asynchronous read, wait and literal rectangle commands; external IO owns delivery and completion timing.</summary>
public static class NativeBattleWaterIoCommands
{
    public static NativeBattleWaterIoCommandResult Execute(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, NativeBattleWaterScriptProgram program, uint instructionPointer, uint managerAddress, NativeBattleEffectTextureState? textures = null)
    {
        core.Validate();
        arena.Validate();
        ushort instruction = BinaryPrimitives.ReadUInt16LittleEndian(Program(0, 2));
        if (instruction is not (0xC006 or 0xC206 or 0xC406 or 0x2029) && (instruction & 0x1FF) != 8)
        {
            throw new NotSupportedException("Native Water IO admits only its selected external reads, wait and literal rectangle transfer.");
        }
        byte busy = core.Read(0x2798219, 1)[0];
        if (busy != 0)
        {
            return Wait();
        }
        if ((instruction & 0x1FF) == 8)
        {
            return core.Read(0x2798218, 1)[0] != 0 ? new(core, unchecked(instructionPointer + 2), null, null, textures) : Wait();
        }
        if (instruction == 0x2029)
        {
            ReadOnlySpan<byte> literal = Program(2, 8);
            NativeBattleEffectTextureRectangle rectangle = new(BinaryPrimitives.ReadInt16LittleEndian(literal), BinaryPrimitives.ReadInt16LittleEndian(literal[2..]), BinaryPrimitives.ReadInt16LittleEndian(literal[4..]), BinaryPrimitives.ReadInt16LittleEndian(literal[6..]));
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(checked(managerAddress + 0xB4), 4));
            long offset = (long)source - NativeBattleEffectNodeMemory.ArenaAddress;
            if (offset < 0 || offset > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Native rectangle source requires its actual current arena pointer.");
            }
            NativeBattleEffectTextureState queued = (textures ?? throw new NotSupportedException("Native literal rectangle transfer requires its current shared texture queue.")).EnqueueArenaRectangle505DF0(rectangle, arena, (int)offset);
            return new(core, unchecked(instructionPointer + 10), null, null, queued);
        }
        core = core.Write(0x2798219, new byte[] { 0xFF });
        uint destination = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(checked(managerAddress + 0xB8), 4));
        Span<byte> address = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(address, destination);
        core = core.Write(checked(managerAddress + 0xB4), address);
        short file = (short)(0x16B + (((instruction >> 9) & 0x3F) & 0x1F));
        return new(core, unchecked(instructionPointer + 2), null, new(file, destination, 0, 0xAA0ED0), textures);

        NativeBattleWaterIoCommandResult Wait()
        {
            ushort ticks = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(bank.SlotOffset(arena, index) + 0xC8, 2));
            return new(core, instructionPointer, ticks, null, textures);
        }

        byte[] Program(int delta, int length)
        {
            uint relative = unchecked(instructionPointer + (uint)delta - program.Address);
            if (program.ArenaOffset < 0 || program.Length < length || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - length))
            {
                throw new InvalidDataException("Native Water IO operand leaves its actual current arena program owner.");
            }
            return arena.Read(checked(program.ArenaOffset + (int)relative), length);
        }
    }

    public static NativeBattleScriptedEffectMemory CompleteAA0ED0(NativeBattleScriptedEffectMemory core) => core.Write(0x2798219, new byte[] { 0 });
}
